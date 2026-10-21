using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Skybreak.AI.SawanIS.VaultHound
{
    /// <summary>
    /// A discrete world-space navigation grid for the Vault Hound agent pair.
    /// Cells are passable or blocked based on Physics overlap tests at initialisation
    /// and updated dynamically when doors or barriers change.
    ///
    /// Grid version increments on every world change so VaultHoundBrain can detect
    /// stale paths without polling geometry every frame.
    ///
    /// IS justification:
    ///   - Provides the discrete state space over which A* (informed search) and
    ///     BFS (uninformed search) operate.
    ///   - Keeping pathfinding separate from brain logic preserves the single-
    ///     responsibility principle and allows both Hound instances to share one grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VaultHoundGrid : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Grid dimensions")]
        [Tooltip("Number of cells along the X axis.")]
        [SerializeField, Min(4)] int width  = 40;

        [Tooltip("Number of cells along the Z axis.")]
        [SerializeField, Min(4)] int height = 40;

        [Tooltip("World-space size of one cell (meters).")]
        [SerializeField, Min(0.2f)] float cellSize = 1f;

        [Tooltip("World-space origin of cell (0,0) — bottom-left corner.")]
        [SerializeField] Vector3 origin = Vector3.zero;

        [Header("Obstacle detection")]
        [Tooltip("Layers considered solid/blocking (walls, doors, static geometry).")]
        [SerializeField] LayerMask obstacleMask = ~0;

        [Tooltip("Half-extent of the overlap box used to test each cell for geometry.")]
        [SerializeField, Min(0.05f)] float obstacleCheckRadius = 0.3f;

        [Tooltip("Height at which the overlap box centre is placed (above cell origin).")]
        [SerializeField, Min(0f)] float checkHeight = 0.6f;

        [Header("Debug")]
        [SerializeField] bool drawGizmos;
        [SerializeField] bool requireNavMesh;
        [SerializeField] int agentTypeID;

        // ── Data ──────────────────────────────────────────────────────────────

        bool[] passable;  // flat row-major array: passable[z * width + x]

        /// <summary>Increments whenever <see cref="SetCellPassable"/> or
        /// <see cref="NotifyWorldChanged"/> is called. Brains watch this to
        /// invalidate cached paths cheaply.</summary>
        public int Version { get; private set; }

        public int  Width    => width;
        public int  Height   => height;
        public float CellSize => cellSize;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        void Awake() => Bake();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Re-scan the whole grid (call after large environment changes).</summary>
        public void Bake()
        {
            passable = new bool[width * height];
            var half = new Vector3(cellSize * 0.5f, obstacleCheckRadius, cellSize * 0.5f);
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width;  x++)
            {
                Vector3 centre = CellCentre(x, z) + Vector3.up * checkHeight;
                passable[z * width + x] = !Physics.CheckBox(centre, half,
                    Quaternion.identity, obstacleMask, QueryTriggerInteraction.Ignore);
                if (requireNavMesh && passable[z * width + x])
                    passable[z * width + x] = NavMesh.SamplePosition(CellCentre(x, z), out _, cellSize * .6f,
                        new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas });
            }
            Version++;
        }

        /// <summary>Mark a single cell passable or blocked (e.g. when a door opens/closes).
        /// Increments Version so brains know to replan.</summary>
        public void SetCellPassable(int x, int z, bool value)
        {
            if (!InBounds(x, z)) return;
            passable[z * width + x] = value;
            Version++;
        }

        /// <summary>Force a version bump without full rebake (use when many cells change).</summary>
        public void NotifyWorldChanged() => Version++;

        public bool IsPassable(int x, int z) => InBounds(x, z) && passable[z * width + x];

        public bool InBounds(int x, int z) => x >= 0 && x < width && z >= 0 && z < height;

        /// <summary>World position → grid cell (clamped, no out-of-bounds guarantee).</summary>
        public Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.FloorToInt((world.x - origin.x) / cellSize);
            int z = Mathf.FloorToInt((world.z - origin.z) / cellSize);
            return new Vector2Int(Mathf.Clamp(x, 0, width - 1), Mathf.Clamp(z, 0, height - 1));
        }

        /// <summary>Grid cell → world-space centre of that cell (at origin.y).</summary>
        public Vector3 CellCentre(int x, int z) =>
            origin + new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);

        public Vector3 CellCentre(Vector2Int cell) => CellCentre(cell.x, cell.y);

        // Four cardinal neighbours (N E S W) — diagonal movement not used.
        static readonly Vector2Int[] Neighbours =
        {
            new Vector2Int( 0,  1),
            new Vector2Int( 1,  0),
            new Vector2Int( 0, -1),
            new Vector2Int(-1,  0)
        };

        /// <summary>Returns passable cardinal neighbours of <paramref name="cell"/>.</summary>
        public IEnumerable<Vector2Int> PassableNeighbours(Vector2Int cell)
        {
            foreach (var d in Neighbours)
            {
                var n = cell + d;
                if (IsPassable(n.x, n.y)) yield return n;
            }
        }

        // ── Debug ─────────────────────────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || passable == null) return;
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width;  x++)
            {
                Gizmos.color = passable[z * width + x]
                    ? new Color(0f, 1f, 0.4f, 0.12f)
                    : new Color(1f, 0.2f, 0.2f, 0.18f);
                Vector3 c = CellCentre(x, z) + Vector3.up * checkHeight;
                Gizmos.DrawCube(c, new Vector3(cellSize * 0.9f, 0.1f, cellSize * 0.9f));
            }
        }
    }
}
