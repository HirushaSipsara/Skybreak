using System;
using System.Collections.Generic;
using UnityEngine;
using Skybreak.PhysicsInteraction;

namespace Skybreak.AI.Striker
{
    /// <summary>
    /// Represents a discrete 2D spatial grid (in the X-Z horizontal plane) for Striker navigation.
    /// Maintains cell traversability and an incremental version counter so agents automatically
    /// detect world topology changes (such as security doors opening or closing).
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public sealed class StrikerGrid : MonoBehaviour
    {
        [Header("Grid Geometry")]
        [Tooltip("World coordinate of cell (0, 0) bottom-left origin.")]
        [SerializeField] Vector3 gridOrigin = Vector3.zero;

        [Tooltip("Number of columns (X) and rows (Z).")]
        [SerializeField] Vector2Int gridSize = new Vector2Int(20, 20);

        [Tooltip("Size of each grid cell in world units (meters).")]
        [SerializeField, Min(0.2f)] float cellSize = 1.0f;

        [Header("Obstacle Scanning")]
        [Tooltip("Layers treated as impassable barriers (e.g. walls, security doors, obstacles).")]
        [SerializeField] LayerMask obstacleMask = ~0;

        [Tooltip("Check height relative to grid elevation for obstacle detection.")]
        [SerializeField] float checkElevationOffset = 0.5f;

        [Tooltip("Radius of the sphere used to sample obstacles in each cell.")]
        [SerializeField, Range(0.1f, 1f)] float obstacleCheckFraction = 0.42f;

        [Tooltip("Automatically scan the scene on Awake to populate obstacle flags.")]
        [SerializeField] bool autoScanOnAwake = true;

        [Tooltip("Automatically listen to SlidingSecurityDoor state changes in the scene.")]
        [SerializeField] bool listenToSecurityDoors = true;

        [Header("Visualization")]
        [SerializeField] bool showGizmos = true;
        [SerializeField] bool showUnwalkableOnly = false;

        bool[,] passable;
        int version = 1;

        /// <summary>Number of columns along the X axis.</summary>
        public int Columns => gridSize.x;

        /// <summary>Number of rows along the Z axis.</summary>
        public int Rows => gridSize.y;

        /// <summary>Cell width/length in world units.</summary>
        public float CellSize => cellSize;

        /// <summary>World position of cell (0, 0).</summary>
        public Vector3 GridOrigin => gridOrigin;

        /// <summary>
        /// Monotonically increasing version counter. Increments whenever cell traversability changes.
        /// Agents monitor this version to invalidate cached paths when a door opens or closes.
        /// </summary>
        public int Version => version;

        public event Action<StrikerGrid, int> GridChanged;

        void Awake()
        {
            InitializeGrid();
            if (autoScanOnAwake)
            {
                ScanObstacles();
            }
        }

        void Start()
        {
            if (listenToSecurityDoors)
            {
                SubscribeToDoors();
            }
        }

        void OnDestroy()
        {
            UnsubscribeFromDoors();
        }

        void SubscribeToDoors()
        {
            SlidingSecurityDoor[] doors = FindObjectsByType<SlidingSecurityDoor>(FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].OnStateChanged.AddListener(OnDoorStateChanged);
            }
        }

        void UnsubscribeFromDoors()
        {
            SlidingSecurityDoor[] doors = FindObjectsByType<SlidingSecurityDoor>(FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].OnStateChanged.RemoveListener(OnDoorStateChanged);
            }
        }

        void OnDoorStateChanged(DoorState state)
        {
            NotifyWorldChanged();
        }

        /// <summary>
        /// Allocates the internal 2D array and marks all cells passable.
        /// </summary>
        public void InitializeGrid()
        {
            int cols = Mathf.Max(1, gridSize.x);
            int rows = Mathf.Max(1, gridSize.y);
            if (passable == null || passable.GetLength(0) != cols || passable.GetLength(1) != rows)
            {
                passable = new bool[cols, rows];
                for (int x = 0; x < cols; x++)
                {
                    for (int z = 0; z < rows; z++)
                    {
                        passable[x, z] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates physics colliders in each cell to determine passability.
        /// </summary>
        public void ScanObstacles()
        {
            InitializeGrid();
            float checkRadius = cellSize * obstacleCheckFraction;

            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    Vector3 worldCenter = CellToWorld(x, z, checkElevationOffset);
                    bool hitObstacle = Physics.CheckSphere(worldCenter, checkRadius, obstacleMask, QueryTriggerInteraction.Ignore);
                    passable[x, z] = !hitObstacle;
                }
            }

            IncrementVersion();
        }

        /// <summary>
        /// Returns true if the coordinate is within grid bounds.
        /// </summary>
        public bool InBounds(int col, int row)
        {
            return col >= 0 && col < gridSize.x && row >= 0 && row < gridSize.y;
        }

        /// <summary>
        /// Returns true if the coordinate is within grid bounds.
        /// </summary>
        public bool InBounds(Vector2Int cell) => InBounds(cell.x, cell.y);

        /// <summary>
        /// Returns true if the cell is within bounds and passable.
        /// </summary>
        public bool IsPassable(int col, int row)
        {
            if (!InBounds(col, row)) return false;
            if (passable == null) InitializeGrid();
            return passable[col, row];
        }

        /// <summary>
        /// Returns true if the cell is within bounds and passable.
        /// </summary>
        public bool IsPassable(Vector2Int cell) => IsPassable(cell.x, cell.y);

        /// <summary>
        /// Sets passability of an individual cell and increments grid version.
        /// </summary>
        public void SetPassable(int col, int row, bool isPassable)
        {
            if (!InBounds(col, row)) return;
            if (passable == null) InitializeGrid();

            if (passable[col, row] != isPassable)
            {
                passable[col, row] = isPassable;
                IncrementVersion();
            }
        }

        /// <summary>
        /// Sets passability of an individual cell and increments grid version.
        /// </summary>
        public void SetPassable(Vector2Int cell, bool isPassable) => SetPassable(cell.x, cell.y, isPassable);

        /// <summary>
        /// Sets passability for all cells overlapping a world-space bounding box.
        /// Useful when a security door or obstacle changes state.
        /// </summary>
        public void SetAreaPassable(Bounds worldBounds, bool isPassable)
        {
            WorldToCell(worldBounds.min, out Vector2Int minCell);
            WorldToCell(worldBounds.max, out Vector2Int maxCell);

            int minX = Mathf.Clamp(minCell.x, 0, gridSize.x - 1);
            int maxX = Mathf.Clamp(maxCell.x, 0, gridSize.x - 1);
            int minZ = Mathf.Clamp(minCell.y, 0, gridSize.y - 1);
            int maxZ = Mathf.Clamp(maxCell.y, 0, gridSize.y - 1);

            bool anyChanged = false;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (passable[x, z] != isPassable)
                    {
                        passable[x, z] = isPassable;
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged)
            {
                IncrementVersion();
            }
        }

        /// <summary>
        /// Manually triggers a world change event and version bump.
        /// Compatible with door events and external level modifiers.
        /// </summary>
        public void NotifyWorldChanged()
        {
            if (autoScanOnAwake)
            {
                ScanObstacles();
            }
            else
            {
                IncrementVersion();
            }
        }

        /// <summary>
        /// Converts a discrete (col, row) coordinate to world space center position.
        /// </summary>
        public Vector3 CellToWorld(int col, int row, float elevationOffset = 0f)
        {
            float x = gridOrigin.x + (col + 0.5f) * cellSize;
            float y = gridOrigin.y + elevationOffset;
            float z = gridOrigin.z + (row + 0.5f) * cellSize;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Converts a discrete cell coordinate to world space center position.
        /// </summary>
        public Vector3 CellToWorld(Vector2Int cell, float elevationOffset = 0f) => CellToWorld(cell.x, cell.y, elevationOffset);

        /// <summary>
        /// Converts a continuous 3D world position to grid cell (col, row).
        /// </summary>
        public bool WorldToCell(Vector3 worldPos, out Vector2Int cell)
        {
            int col = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
            int row = Mathf.FloorToInt((worldPos.z - gridOrigin.z) / cellSize);
            cell = new Vector2Int(col, row);
            return InBounds(col, row);
        }

        /// <summary>
        /// Finds the closest valid and passable cell to a world position.
        /// Falls back to the nearest in-bounds cell if none is passable.
        /// </summary>
        public Vector2Int GetClosestPassableCell(Vector3 worldPos)
        {
            WorldToCell(worldPos, out Vector2Int rawCell);
            int clampedX = Mathf.Clamp(rawCell.x, 0, gridSize.x - 1);
            int clampedZ = Mathf.Clamp(rawCell.y, 0, gridSize.y - 1);
            Vector2Int clamped = new Vector2Int(clampedX, clampedZ);

            if (IsPassable(clamped)) return clamped;

            // Spiral / BFS search for nearest passable cell
            for (int r = 1; r < Mathf.Max(gridSize.x, gridSize.y); r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        Vector2Int candidate = new Vector2Int(clamped.x + dx, clamped.y + dz);
                        if (IsPassable(candidate)) return candidate;
                    }
                }
            }

            return clamped;
        }

        /// <summary>
        /// Appends the 4 cardinal neighbors (North, South, East, West) of a cell to the results list.
        /// Using 4 cardinal directions ensures Manhattan distance heuristic admissibility for A*.
        /// </summary>
        public void GetCardinalNeighbors(Vector2Int cell, List<Vector2Int> results)
        {
            results.Clear();
            Vector2Int up = new Vector2Int(cell.x, cell.y + 1);
            Vector2Int down = new Vector2Int(cell.x, cell.y - 1);
            Vector2Int right = new Vector2Int(cell.x + 1, cell.y);
            Vector2Int left = new Vector2Int(cell.x - 1, cell.y);

            if (IsPassable(up)) results.Add(up);
            if (IsPassable(down)) results.Add(down);
            if (IsPassable(right)) results.Add(right);
            if (IsPassable(left)) results.Add(left);
        }

        void IncrementVersion()
        {
            version++;
            GridChanged?.Invoke(this, version);
        }

        void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            // Draw bounding grid
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.6f);
            Vector3 size = new Vector3(gridSize.x * cellSize, 0.05f, gridSize.y * cellSize);
            Vector3 center = gridOrigin + new Vector3(size.x * 0.5f, 0.025f, size.z * 0.5f);
            Gizmos.DrawWireCube(center, size);

            if (passable == null) return;

            Vector3 cellBox = new Vector3(cellSize * 0.85f, 0.1f, cellSize * 0.85f);
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int z = 0; z < gridSize.y; z++)
                {
                    bool isWalkable = passable[x, z];
                    if (isWalkable && showUnwalkableOnly) continue;

                    Vector3 pos = CellToWorld(x, z, 0.05f);
                    Gizmos.color = isWalkable
                        ? new Color(0.1f, 0.9f, 0.2f, 0.25f)
                        : new Color(0.95f, 0.15f, 0.15f, 0.6f);
                    Gizmos.DrawCube(pos, cellBox);
                }
            }
        }
    }
}
