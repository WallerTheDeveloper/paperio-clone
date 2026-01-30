using UnityEngine;

namespace Helpers
{
    public class GridHelper
    {
        /// <summary>
        /// Convert grid coordinates to world position (3D).
        /// Grid (x, y) → World (x * cellSize, height, y * cellSize)
        /// </summary>
        public static Vector3 GridToWorld(Vector2Int gridPos, float cellSize, float height = 0f)
        {
            return new Vector3(
                gridPos.x * cellSize,
                height,
                gridPos.y * cellSize
            );
        }

        /// <summary>
        /// Convert grid coordinates to world position (3D).
        /// </summary>
        public static Vector3 GridToWorld(int gridX, int gridY, float cellSize, float height = 0f)
        {
            return new Vector3(
                gridX * cellSize,
                height,
                gridY * cellSize
            );
        }

        /// <summary>
        /// Convert world position to grid coordinates.
        /// World (x, y, z) → Grid (x / cellSize, z / cellSize)
        /// </summary>
        public static Vector2Int WorldToGrid(Vector3 worldPos, float cellSize)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPos.x / cellSize),
                Mathf.RoundToInt(worldPos.z / cellSize)
            );
        }

        /// <summary>
        /// Get the center of the grid in world coordinates.
        /// </summary>
        public static Vector3 GetGridCenter(int gridWidth, int gridHeight, float cellSize)
        {
            return new Vector3(
                (gridWidth * cellSize) / 2f,
                0f,
                (gridHeight * cellSize) / 2f
            );
        }

        /// <summary>
        /// Get the world bounds of the grid.
        /// </summary>
        public static Bounds GetGridBounds(int gridWidth, int gridHeight, float cellSize)
        {
            var center = GetGridCenter(gridWidth, gridHeight, cellSize);
            var size = new Vector3(gridWidth * cellSize, 0f, gridHeight * cellSize);
            return new Bounds(center, size);
        }

        /// <summary>
        /// Check if a grid position is within bounds.
        /// </summary>
        public static bool IsInBounds(Vector2Int gridPos, int gridWidth, int gridHeight)
        {
            return gridPos.x >= 0 && gridPos.x < gridWidth &&
                   gridPos.y >= 0 && gridPos.y < gridHeight;
        }

    }
}