using System;
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
        public int ClaimedCells { get; private set; }
        public int TotalCells => Width * Height;

        public TerritoryVisualData VisualData { get; private set; }

        private readonly uint[] _cells;

        public TerritoryData(int width, int height)
        {
            Width = width;
            Height = height;
            ClaimedCells = 0;
            _cells = new uint[width * height];
        }

        public void InitializeVisuals(float cellSize, Color32 neutralColor, Func<uint, Color32> colorResolver)
        {
            VisualData = new TerritoryVisualData();
            VisualData.Initialize(Width, Height, cellSize, neutralColor, colorResolver);
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

        public uint[] GetRawCells()
        {
            return _cells;
        }

        public List<TerritoryChange> ApplyFullState(IEnumerable<TerritoryRow> territoryRows)
        {
            var changes = new List<TerritoryChange>();
            var processedRows = new HashSet<int>();

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

            for (int y = 0; y < Height; y++)
            {
                if (!processedRows.Contains(y))
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int idx = y * Width + x;
                        if (_cells[idx] != 0)
                        {
                            changes.Add(new TerritoryChange(x, y, _cells[idx], 0));
                            _cells[idx] = 0;
                        }
                    }
                }
            }

            RecalculateClaimedCells();

            VisualData?.ApplyChanges(changes);

            return changes;
        }

        public List<TerritoryChange> ApplyDeltaChanges(IEnumerable<TerritoryCell> cellChanges)
        {
            var changes = new List<TerritoryChange>();

            foreach (var cell in cellChanges)
            {
                int x = cell.X;
                int y = cell.Y;

                if (!IsInBounds(x, y))
                {
                    Debug.LogWarning($"[TerritoryData] Delta cell ({x},{y}) out of bounds");
                    continue;
                }

                int idx = y * Width + x;
                uint previousOwner = _cells[idx];
                uint newOwner = cell.OwnerId;

                if (previousOwner != newOwner)
                {
                    _cells[idx] = newOwner;

                    if (previousOwner == 0 && newOwner != 0)
                    {
                        ClaimedCells++;
                    }
                    else if (previousOwner != 0 && newOwner == 0)
                    {
                        ClaimedCells--;
                    }

                    changes.Add(new TerritoryChange(x, y, previousOwner, newOwner));
                }
            }

            VisualData?.ApplyChanges(changes);

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
                    int idx = y * Width + x;
                    uint previousOwner = _cells[idx];

                    if (previousOwner != ownerId)
                    {
                        _cells[idx] = ownerId;
                        changes.Add(new TerritoryChange(x, y, previousOwner, ownerId));
                    }
                }
            }
        }

        public float GetOwnershipPercentage(uint playerId)
        {
            if (TotalCells == 0 || playerId == 0)
            {
                return 0f;
            }

            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == playerId)
                {
                    count++;
                }
            }

            return (count * 100f) / TotalCells;
        }

        private void RecalculateClaimedCells()
        {
            ClaimedCells = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != 0)
                {
                    ClaimedCells++;
                }
            }
        }

        public void Clear()
        {
            System.Array.Clear(_cells, 0, _cells.Length);
            ClaimedCells = 0;

            VisualData.RebuildAllColors(_cells);
        }
    }
}