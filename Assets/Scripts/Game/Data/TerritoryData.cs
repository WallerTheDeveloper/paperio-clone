using System.Collections.Generic;
using Game.Paperio;
using UnityEngine;

namespace Game.Data
{
    public readonly struct TerritoryChange
    {
        public readonly int X;
        public readonly int Y;
        public readonly uint PreviousOwner;
        public readonly uint NewOwner;

        public TerritoryChange(int x, int y, uint previousOwner, uint newOwner)
        {
            X = x;
            Y = y;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
        }

        public Vector2Int Position => new Vector2Int(X, Y);
    }

    public class TerritoryData
    {
        public int Width { get; }
        public int Height { get; }
        public MeshFilter MeshFilter { get; private set; }
        public int ClaimedCells { get; private set; }
        public int TotalCells => Width * Height;
        private readonly uint[] _cells;
        public TerritoryData(int width, int height, MeshFilter meshFilter)
        {
            Width = width;
            Height = height;
            MeshFilter = meshFilter;
            ClaimedCells = 0;
            _cells = new uint[width * height];
        }
        public uint GetOwner(int x, int y)
        {
            if (!IsInBounds(x, y))
            {
                return 0;
            }
            return _cells[y * Width + x];
        }
        public uint GetOwner(Vector2Int position)
        {
            return GetOwner(position.x, position.y);
        }
        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
        public bool IsInBounds(Vector2Int position)
        {
            return IsInBounds(position.x, position.y);
        }
        public bool IsOwnedBy(int x, int y, uint playerId)
        {
            return GetOwner(x, y) == playerId;
        }
        public bool IsOwnedBy(Vector2Int position, uint playerId)
        {
            return IsOwnedBy(position.x, position.y, playerId);
        }
        public List<TerritoryChange> ApplyServerState(IEnumerable<TerritoryRow> territoryRows)
        {
            var changes = new List<TerritoryChange>();

            foreach (var row in territoryRows)
            {
                int y = row.Y;
                
                if (y < 0 || y >= Height)
                {
                    Debug.LogWarning($"[TerritoryData] Row y={y} out of bounds (height={Height})");
                    continue;
                }

                DecodeRow(row, changes);
            }

            // Update claimed cells count
            RecalculateClaimedCells();

            return changes;
        }
        public List<TerritoryChange> ApplyFullState(IEnumerable<TerritoryRow> territoryRows)
        {
            var changes = new List<TerritoryChange>();
            var processedRows = new HashSet<int>();

            // First, apply the rows we received
            foreach (var row in territoryRows)
            {
                int y = row.Y;
                
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                processedRows.Add(y);
                DecodeRow(row, changes);
            }

            // Clear rows not in the update (they should be entirely unclaimed)
            for (int y = 0; y < Height; y++)
            {
                if (!processedRows.Contains(y))
                {
                    ClearRow(y, changes);
                }
            }

            RecalculateClaimedCells();
            return changes;
        }
        private void DecodeRow(TerritoryRow row, List<TerritoryChange> changes)
        {
            int y = row.Y;
            int x = 0;

            foreach (var run in row.Runs)
            {
                uint ownerId = run.OwnerId;
                int count = (int)run.Count;

                for (int i = 0; i < count && x < Width; i++, x++)
                {
                    SetCell(x, y, ownerId, changes);
                }
            }

            while (x < Width)
            {
                SetCell(x, y, 0, changes);
                x++;
            }
        }
        private void ClearRow(int y, List<TerritoryChange> changes)
        {
            for (int x = 0; x < Width; x++)
            {
                SetCell(x, y, 0, changes);
            }
        }
        private void SetCell(int x, int y, uint newOwner, List<TerritoryChange> changes)
        {
            int index = y * Width + x;
            uint previousOwner = _cells[index];

            if (previousOwner != newOwner)
            {
                _cells[index] = newOwner;
                changes.Add(new TerritoryChange(x, y, previousOwner, newOwner));
            }
        }
        private void RecalculateClaimedCells()
        {
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != 0)
                {
                    count++;
                }
            }
            ClaimedCells = count;
        }
        public int CountOwnedBy(uint playerId)
        {
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == playerId)
                {
                    count++;
                }
            }
            return count;
        }
        public float GetOwnershipPercentage(uint playerId)
        {
            if (TotalCells == 0) return 0f;
            return (float)CountOwnedBy(playerId) / TotalCells * 100f;
        }
        public List<Vector2Int> GetCellsOwnedBy(uint playerId)
        {
            var cells = new List<Vector2Int>();
            
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (_cells[y * Width + x] == playerId)
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }
            }
            
            return cells;
        }
        public void Clear()
        {
            System.Array.Clear(_cells, 0, _cells.Length);
            ClaimedCells = 0;
        }
        public uint[] GetRawCells() => _cells;
        public string DebugRegion(int startX, int startY, int regionWidth, int regionHeight)
        {
            var sb = new System.Text.StringBuilder();
            
            for (int y = startY + regionHeight - 1; y >= startY && y >= 0; y--) // Top to bottom
            {
                for (int x = startX; x < startX + regionWidth && x < Width; x++)
                {
                    if (x < 0) continue;
                    
                    uint owner = GetOwner(x, y);
                    if (owner == 0)
                    {
                        sb.Append('.');
                    }
                    else if (owner <= 9)
                    {
                        sb.Append(owner);
                    }
                    else if (owner <= 35)
                    {
                        sb.Append((char)('A' + owner - 10));
                    }
                    else
                    {
                        sb.Append('#');
                    }
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        public void DebugLogAround(int centerX, int centerY, int radius = 5)
        {
            Debug.Log($"[TerritoryData] Region around ({centerX},{centerY}):\n" +
                      DebugRegion(centerX - radius, centerY - radius, radius * 2 + 1, radius * 2 + 1));
        }
    }
}