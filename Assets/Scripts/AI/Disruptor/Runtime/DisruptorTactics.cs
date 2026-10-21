using System.Collections.Generic;
using Skybreak.Disruptor.Core;
using Skybreak.Movement;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    public sealed class TacticalChoice
    {
        public bool Found;
        public Vector3 Point;
        public PathResult Path;
        public double Score = double.NegativeInfinity;
        public double Exposure;
        public string Source = "";
        public void Clear() { Found = false; Path = null; Score = double.NegativeInfinity; Exposure = 0; Source = ""; }
    }

    /// <summary>
    /// Where to stand. Candidates are hand-placed <see cref="DisruptorAnchor"/>s plus positions sampled in
    /// rings around the player (strike) and around the Disruptor and player (escape). Search is two-stage
    /// to bound cost: every candidate is scored with a straight-line travel estimate, then only the best
    /// <see cref="Shortlist"/> get a real threat-aware A* route and are re-scored with its length.
    /// </summary>
    public sealed class DisruptorTactics
    {
        public const int Shortlist = 4, RingSamples = 12;
        public const float EscapeRadiusSelf = 7, EscapeRadiusPlayer = 11, EscapeMinimum = 8, ClaimRadius = 2.2f, BlacklistSeconds = 5;

        public readonly TacticalChoice Strike = new TacticalChoice(), Escape = new TacticalChoice();
        public PathResult InvestigatePath, PatrolPath;
        public Vector3 PatrolPoint;
        public double CloseInDistance = double.PositiveInfinity;
        public int CandidatesScored { get; private set; }
        public int RoutesSearched { get; private set; }

        struct Candidate { public Vector3 Point; public AnchorFeatures F; public double Score; public string Source; }
        readonly List<Candidate> candidates = new List<Candidate>(64);
        readonly List<(Vector3 point, float until)> blacklist = new List<(Vector3, float)>();
        readonly Dictionary<Vector3, float> lastUsed = new Dictionary<Vector3, float>();
        readonly List<Vector3> patrolPoints = new List<Vector3>();
        int patrolIndex;

        public void Blacklist(Vector3 point, float now) => blacklist.Add((point, now + BlacklistSeconds));
        public void MarkUsed(Vector3 point, float now) => lastUsed[Quantise(point)] = now;
        public void AdvancePatrol() { if (patrolPoints.Count > 0) patrolIndex = (patrolIndex + 1) % patrolPoints.Count; }
        public void ResetMemory() { blacklist.Clear(); lastUsed.Clear(); patrolIndex = 0; }

        static Vector3 Quantise(Vector3 p) => new Vector3(Mathf.Round(p.x), 0, Mathf.Round(p.z));

        public void BuildPatrol(DisruptorNavGrid grid, Vector3 home)
        {
            patrolPoints.Clear();
            foreach (var a in DisruptorAnchor.All) if (a != null && grid.IsWalkable(a.transform.position)) patrolPoints.Add(a.transform.position);
            if (patrolPoints.Count >= 2) return;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = home + Quaternion.Euler(0, 90 * i + 45, 0) * Vector3.forward * 4;
                if (grid.NearestWalkable(p, 2, out var c)) patrolPoints.Add(grid.CellToWorld(c));
            }
            if (patrolPoints.Count == 0) patrolPoints.Add(home);
        }

        public void Analyse(DisruptorBrain brain, DisruptorNavGrid grid, DisruptorPerception senses, float strikeDistance, bool playerKnown, float now)
        {
            CandidatesScored = 0; RoutesSearched = 0;
            blacklist.RemoveAll(b => b.until < now);
            Strike.Clear(); Escape.Clear(); InvestigatePath = null; PatrolPath = null; CloseInDistance = double.PositiveInfinity;
            Vector3 self = brain.transform.position;
            Vector3 target = senses.Visible ? senses.Player.position : senses.LastKnown;
            Vector3 chest = senses.Visible ? senses.PlayerChest : senses.LastKnown + Vector3.up * 1.2f;

            if (playerKnown)
            {
                // The player's aim cone is expensive to cross: routes curve around the crosshair.
                grid.SetThreat(senses.PlayerEye, senses.PlayerAim, brain.aimDangerCone, brain.sightRange, brain.aimDangerWeight);

                Gather(brain, grid, senses, chest, self, true, target, strikeDistance, now);
                Choose(brain, grid, self, true, strikeDistance, Strike);

                Gather(brain, grid, senses, chest, self, false, target, strikeDistance, now);
                Choose(brain, grid, self, false, strikeDistance, Escape);

                grid.ClearThreat();
                if (senses.Visible) CloseInDistance = Mathf.Max(0, Flat(target - self).magnitude - strikeDistance);
                else { InvestigatePath = grid.FindPath(self, senses.LastKnown); RoutesSearched++; }
            }

            if (patrolPoints.Count == 0) BuildPatrol(grid, brain.Home);
            // Skip points we are standing on or another Disruptor is already heading to.
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                PatrolPoint = patrolPoints[patrolIndex % patrolPoints.Count];
                if (Flat(PatrolPoint - self).magnitude >= 0.6f && !brain.ClaimedByOther(PatrolPoint, ClaimRadius)) break;
                AdvancePatrol();
            }
            PatrolPath = grid.FindPath(self, PatrolPoint); RoutesSearched++;
        }

        void Gather(DisruptorBrain brain, DisruptorNavGrid grid, DisruptorPerception senses, Vector3 chest, Vector3 self, bool strike, Vector3 player, float strikeDistance, float now)
        {
            candidates.Clear();
            foreach (var a in DisruptorAnchor.All) if (a != null) Consider(brain, grid, senses, chest, self, a.transform.position, strike, strikeDistance, now, "anchor " + a.name);
            if (strike)
            {
                for (int i = 0; i < RingSamples; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, 360f / RingSamples * i, 0) * Vector3.forward;
                    Consider(brain, grid, senses, chest, self, player + dir * strikeDistance, true, strikeDistance, now, "ring");
                    Consider(brain, grid, senses, chest, self, player + dir * strikeDistance * 0.75f, true, strikeDistance, now, "inner ring");
                }
            }
            else
            {
                for (int i = 0; i < RingSamples; i++)
                {
                    Vector3 dir = Quaternion.Euler(0, 360f / RingSamples * i + 15, 0) * Vector3.forward;
                    Consider(brain, grid, senses, chest, self, self + dir * EscapeRadiusSelf, false, strikeDistance, now, "escape ring");
                    Consider(brain, grid, senses, chest, self, player + dir * EscapeRadiusPlayer, false, strikeDistance, now, "outer ring");
                }
            }
        }

        void Consider(DisruptorBrain brain, DisruptorNavGrid grid, DisruptorPerception senses, Vector3 chest, Vector3 self, Vector3 point, bool strike, float strikeDistance, float now, string source)
        {
            if (!grid.Contains(point) || !grid.NearestWalkable(point, 1, out var cell)) return;
            point = grid.CellToWorld(cell);
            foreach (var b in blacklist) if (Flat(b.point - point).magnitude < 1) return;

            Vector3 eye = point + Vector3.up * brain.eyeHeight;
            Vector3 toChest = chest - eye;
            bool line = !Physics.Raycast(eye, toChest.normalized, toChest.magnitude - 0.2f, brain.sightBlockers, QueryTriggerInteraction.Ignore);
            float aimAngle = Vector3.Angle(senses.PlayerAim, eye - senses.PlayerEye);
            float seen = lastUsed.TryGetValue(Quantise(point), out var t) ? now - t : float.PositiveInfinity;
            var f = new AnchorFeatures
            {
                DistanceToPlayer = Flat(chest - point).magnitude,
                AimAngle = aimAngle,
                LineToPlayer = line,
                PathLength = Flat(point - self).magnitude,   // straight-line estimate for the first stage
                SecondsSinceUsed = seen,
                ClaimedByOther = brain.ClaimedByOther(point, ClaimRadius),
            };
            double score = strike ? AnchorScoring.Strike(f, strikeDistance) : AnchorScoring.Escape(f, EscapeMinimum);
            CandidatesScored++;
            if (!double.IsNegativeInfinity(score)) candidates.Add(new Candidate { Point = point, F = f, Score = score, Source = source });
        }

        void Choose(DisruptorBrain brain, DisruptorNavGrid grid, Vector3 self, bool strike, float strikeDistance, TacticalChoice choice)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < candidates.Count && i < Shortlist; i++)
            {
                var c = candidates[i];
                var path = grid.FindPath(self, c.Point); RoutesSearched++;
                if (path.Status != PathStatus.Complete) continue;           // a partial route does not reach the anchor
                c.F.PathLength = path.Length;
                double score = strike ? AnchorScoring.Strike(c.F, strikeDistance) : AnchorScoring.Escape(c.F, EscapeMinimum);
                if (score <= choice.Score) continue;
                choice.Found = true; choice.Point = c.Point; choice.Path = path; choice.Score = score; choice.Source = c.Source;
                choice.Exposure = AnchorScoring.Exposure(c.F.AimAngle, c.F.LineToPlayer);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    }
}
