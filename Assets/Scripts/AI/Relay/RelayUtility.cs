using Skybreak.AI.Algorithms.Fuzzy;
using UnityEngine;

namespace Skybreak.AI.Relay
{
    public struct RelayScores { public float follow, heal, defend, retreat; }

    // Layer 3 (utility-based, Lecture 03): expected utility EU(a) = P(success | a, state) x U(a).
    // U(a) comes from the fuzzy urgencies (layer 2). P(success) captures what can go wrong in a stochastic,
    // partially observable world: on cooldown the action cannot succeed (P = 0); having to move first
    // makes success less certain. The highest EU wins.
    public static class RelayUtility
    {
        public const float NeedsToMove = .7f;          // P(heal succeeds) when Relay must first fly into range
        public const float NeedsToReposition = .6f;    // P(shot lands) when the target is beyond attack range
        public const float Hysteresis = .05f;          // bonus for the action chosen last cycle (avoids flicker)

        public static RelayScores Score(in RelayPercept p, FuzzyResult urgency, bool playerAlive, bool healReady, bool attackReady, RelayAction previous)
        {
            float heal = (float)urgency[RelayFuzzyModel.HealUrgency];
            float defend = (float)urgency[RelayFuzzyModel.DefendUrgency];
            float self = (float)urgency[RelayFuzzyModel.SelfPreservation];

            float pHeal = !playerAlive || !healReady ? 0 : p.canHealNow ? 1 : NeedsToMove;
            float pDefend = p.threat == null || !attackReady ? 0 : p.threatInAttackRange ? 1 : NeedsToReposition;
            float pRetreat = p.remembersThreat ? 1 : 0;   // nothing to retreat from without a recent threat

            RelayScores s;
            s.heal = pHeal * heal;
            s.defend = pDefend * defend;                  // threat confidence is 1: only a currently visible threat is targeted
            s.retreat = pRetreat * self;
            s.follow = .2f + .4f * Mathf.Clamp01(p.distanceToPlayer / 9);
            switch (previous)
            {
                case RelayAction.Heal: s.heal += Hysteresis; break;
                case RelayAction.Defend: s.defend += Hysteresis; break;
                case RelayAction.Retreat: s.retreat += Hysteresis; break;
                case RelayAction.Follow: s.follow += Hysteresis; break;
            }
            return s;
        }

        // Highest expected utility wins; ties go to the earlier action in the order Retreat, Heal, Defend, Follow.
        // An action whose P(success) is 0 is never chosen over Follow, which always succeeds.
        public static RelayAction Select(in RelayScores s, bool hasThreat)
        {
            if (s.retreat > 0 && s.retreat >= Mathf.Max(s.heal, s.defend, s.follow)) return RelayAction.Retreat;
            if (s.heal > 0 && s.heal >= Mathf.Max(s.defend, s.follow)) return RelayAction.Heal;
            if (s.defend > 0 && s.defend >= s.follow && hasThreat) return RelayAction.Defend;
            return RelayAction.Follow;
        }
    }
}
