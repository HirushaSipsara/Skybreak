using System.Collections.Generic;
using Skybreak.Disruptor.Core;
using Skybreak.Movement;
using UnityEngine;

namespace Skybreak.AI.Disruptor
{
    /// <summary>
    /// Scene service: the navigation grid the Disruptor plans on, plus the world/grid conversion.
    ///
    /// Walkable cells are found by a capsule overlap the size of the agent, so obstacles are already
    /// inflated by the agent radius and a route for the cell centre is a route for the whole body.
    /// Doors, gates and barricades (Student 2's components) are watched: when one of their colliders moves
    /// or toggles, only the cells under its old and new bounds are rescanned and <see cref="Version"/>
    /// increases, which makes every agent that holds an older route replan.
    ///
    /// Danger (the player's aim cone) is a per-query overlay, not stored state: the planner sets the
    /// threat, runs A*, and the next query may use a different threat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DisruptorNavGrid : MonoBehaviour, IGridMap
    {
        [Header("Geometry")]
        [Tooltip("World position of the lower-left corner of cell (0,0); Y is the floor height.")]
        public Vector3 origin = new Vector3(-24, 0, -24);
        public Vector2Int size = new Vector2Int(96, 96);
        [Min(0.25f)] public float cellSize = 0.5f;

        [Header("Agent clearance")]
        public float agentRadius = 0.55f;
        public float agentHeight = 1.7f;
        [Tooltip("Solid geometry. Excludes layer 8, where actors live, so agents never block the map for each other.")]
        public LayerMask obstacleMask = ~(1 << 8);
        [Tooltip("Cells need floor within this distance below them.")]
        public float floorProbe = 2.5f;

        [Header("Dynamic obstacles")]
        public bool watchDoorsAndBarricades = true;
        public List<Collider> watched = new List<Collider>();
        public float watchInterval = 0.25f;

        [Header("Debug")]
        public bool drawGizmos = true;

        bool[] walkable;
        readonly Dictionary<Collider, (bool on, Bounds bounds)> watchState = new Dictionary<Collider, (bool, Bounds)>();
        float nextWatch;
        // Threat overlay for the current query
        bool threatActive;
        Vector3 threatOrigin, threatAim;
        float threatCosLimit, threatRange, threatWeight, threatCone;

        public int Version { get; private set; } = 1;
        public int Width => size.x;
        public int Height => size.y;
        public bool Ready => walkable != null;
        public int LastExpanded { get; private set; }

        void Awake() { Scan(); }

        void Start()
        {
            if (watchDoorsAndBarricades) CollectDynamicObstacles();
            foreach (var c in watched) if (c != null) watchState[c] = Snapshot(c);
        }

        void CollectDynamicObstacles()
        {
            void Add(Component owner)
            {
                foreach (var c in owner.GetComponentsInChildren<Collider>(true))
                    if (c != null && !c.isTrigger && !watched.Contains(c)) watched.Add(c);
            }
            foreach (var d in FindObjectsByType<PhysicsInteraction.SlidingSecurityDoor>(FindObjectsSortMode.None)) Add(d);
            foreach (var d in FindObjectsByType<PhysicsInteraction.MovableBarricade>(FindObjectsSortMode.None)) Add(d);
            foreach (var d in FindObjectsByType<PhysicsInteraction.DungeonHoldingDoor>(FindObjectsSortMode.None)) Add(d);
            foreach (var d in FindObjectsByType<Skybreak.Descent.DescentGate>(FindObjectsSortMode.None)) Add(d);
        }

        static (bool, Bounds) Snapshot(Collider c) => (c.enabled && c.gameObject.activeInHierarchy, c.bounds);

        void Update()
        {
            if (Time.time < nextWatch || walkable == null) return;
            nextWatch = Time.time + watchInterval;
            for (int i = 0; i < watched.Count; i++)
            {
                var c = watched[i];
                if (c == null) continue;
                var now = Snapshot(c);
                if (!watchState.TryGetValue(c, out var before)) { watchState[c] = now; continue; }
                if (!now.Item1 && before.bounds.size != Vector3.zero) now.Item2 = before.bounds;   // a disabled collider reports empty bounds
                bool moved = now.Item1 != before.on || (now.Item2.center - before.bounds.center).sqrMagnitude > 0.0025f || (now.Item2.size - before.bounds.size).sqrMagnitude > 0.0025f;
                if (!moved) continue;
                watchState[c] = now;
                var region = before.bounds; region.Encapsulate(now.Item2);
                Refresh(region);
            }
        }

        // ------------------------------------------------------------------ scanning
        public void Scan()
        {
            walkable = new bool[Mathf.Max(1, size.x * size.y)];
            for (int z = 0; z < size.y; z++)
                for (int x = 0; x < size.x; x++)
                    walkable[z * size.x + x] = Probe(x, z);
        }

        /// <summary>Rescans every cell (call after a large scripted change). Bumps the version.</summary>
        public void Refresh() { Physics.SyncTransforms(); Scan(); Version++; }

        /// <summary>Rescans cells under a world region; bumps the version only if a cell changed.</summary>
        public bool Refresh(Bounds region)
        {
            if (walkable == null) { Scan(); Version++; return true; }
            Physics.SyncTransforms();
            region.Expand(new Vector3(agentRadius * 2 + cellSize, 0, agentRadius * 2 + cellSize));
            var a = WorldToCell(region.min); var b = WorldToCell(region.max);
            bool changed = false;
            for (int z = Mathf.Max(0, a.Z); z <= Mathf.Min(size.y - 1, b.Z); z++)
                for (int x = Mathf.Max(0, a.X); x <= Mathf.Min(size.x - 1, b.X); x++)
                {
                    bool now = Probe(x, z);
                    int i = z * size.x + x;
                    if (walkable[i] != now) { walkable[i] = now; changed = true; }
                }
            if (changed) Version++;
            return changed;
        }

        bool Probe(int x, int z)
        {
            Vector3 c = CellToWorld(new Cell(x, z));
            Vector3 bottom = c + Vector3.up * (agentRadius + 0.2f);
            Vector3 top = c + Vector3.up * Mathf.Max(agentRadius + 0.21f, agentHeight - agentRadius);
            if (Physics.CheckCapsule(bottom, top, agentRadius, obstacleMask, QueryTriggerInteraction.Ignore)) return false;
            return Physics.Raycast(c + Vector3.up * 0.5f, Vector3.down, floorProbe, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ IGridMap
        public bool Walkable(int x, int z) => walkable != null && x >= 0 && z >= 0 && x < size.x && z < size.y && walkable[z * size.x + x];

        public double Danger(int x, int z)
        {
            if (!threatActive) return 0;
            Vector3 to = CellToWorld(new Cell(x, z)) - threatOrigin; to.y = 0;
            float d = to.magnitude;
            if (d > threatRange || d < 0.01f) return d < 0.01f ? threatWeight : 0;
            float cos = Vector3.Dot(to / d, threatAim);
            if (cos < threatCosLimit) return 0;
            float angle = Mathf.Acos(Mathf.Clamp(cos, -1, 1)) * Mathf.Rad2Deg;
            return threatWeight * (1 - angle / threatCone);   // strongest down the barrel
        }

        // ------------------------------------------------------------------ conversion
        public Cell WorldToCell(Vector3 p) =>
            new Cell(Mathf.FloorToInt((p.x - origin.x) / cellSize), Mathf.FloorToInt((p.z - origin.z) / cellSize));

        public Vector3 CellToWorld(Cell c) =>
            new Vector3(origin.x + (c.X + 0.5f) * cellSize, origin.y, origin.z + (c.Z + 0.5f) * cellSize);

        public bool Contains(Vector3 p) { var c = WorldToCell(p); return c.X >= 0 && c.Z >= 0 && c.X < size.x && c.Z < size.y; }

        public bool IsWalkable(Vector3 p) { var c = WorldToCell(p); return Walkable(c.X, c.Z); }

        /// <summary>Nearest walkable cell within <paramref name="radiusCells"/> rings, searched outwards.</summary>
        public bool NearestWalkable(Vector3 p, int radiusCells, out Cell cell)
        {
            cell = WorldToCell(p);
            if (Walkable(cell.X, cell.Z)) return true;
            Cell centre = cell; float best = float.MaxValue; bool found = false;
            for (int r = 1; r <= radiusCells && !found; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        int x = centre.X + dx, z = centre.Z + dz;
                        if (!Walkable(x, z)) continue;
                        float d = dx * dx + dz * dz;
                        if (d < best) { best = d; cell = new Cell(x, z); found = true; }
                    }
            return found;
        }

        // ------------------------------------------------------------------ queries
        /// <summary>Sets the aim cone that costs extra to cross, for the next searches.</summary>
        public void SetThreat(Vector3 eye, Vector3 aim, float coneDegrees, float range, float weight)
        {
            aim.y = 0;
            threatActive = aim.sqrMagnitude > 1e-4f && coneDegrees > 0 && weight > 0;
            if (!threatActive) return;
            threatOrigin = eye; threatAim = aim.normalized; threatCone = coneDegrees;
            threatCosLimit = Mathf.Cos(coneDegrees * Mathf.Deg2Rad); threatRange = range; threatWeight = weight;
        }

        public void ClearThreat() { threatActive = false; }

        /// <summary>Plans a route and converts it to world waypoints (smoothed, floor height).</summary>
        public PathResult FindPath(Vector3 from, Vector3 to, int budget = 6000)
        {
            if (walkable == null || !NearestWalkable(from, 3, out var start)) return PathResult.None(Version);
            bool snapped = !NearestWalkable(to, 3, out var goal);
            if (snapped) goal = WorldToCell(to);
            var route = GridSearch.FindPath(this, start, goal, budget);
            LastExpanded = route.Expanded;
            if (route.Status == GridPathStatus.NoPath || route.Status == GridPathStatus.Invalid || route.Cells.Count == 0)
                return PathResult.None(Version);
            var cells = GridSearch.Smooth(this, route.Cells);
            var points = new Vector3[cells.Count];
            for (int i = 0; i < cells.Count; i++) points[i] = CellToWorld(cells[i]);
            // Finish exactly on the requested point when it is inside the goal cell's free space.
            if (route.Status == GridPathStatus.Complete && !snapped && Vector3.Distance(Flat(to), Flat(points[points.Length - 1])) < cellSize)
                points[points.Length - 1] = new Vector3(to.x, origin.y, to.z);
            return new PathResult
            {
                Status = route.Status == GridPathStatus.Complete ? PathStatus.Complete : PathStatus.Partial,
                Waypoints = points, NavigationVersion = Version, Expanded = route.Expanded,
                Length = (float)route.Length * cellSize,
            };
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0, 1, 1, .4f);
            Gizmos.DrawWireCube(origin + new Vector3(size.x * cellSize, 0, size.y * cellSize) * .5f, new Vector3(size.x * cellSize, .05f, size.y * cellSize));
            if (walkable == null) return;
            Gizmos.color = new Color(1, .2f, .2f, .35f);
            for (int z = 0; z < size.y; z++)
                for (int x = 0; x < size.x; x++)
                    if (!walkable[z * size.x + x]) Gizmos.DrawCube(CellToWorld(new Cell(x, z)), new Vector3(cellSize * .9f, .02f, cellSize * .9f));
        }
    }
}
