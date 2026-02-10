using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Effects;
using UnityEngine;

namespace Game.Rendering
{
    public class TerritoryClaim : MonoBehaviour, IService
    {
        [Header("Animation Settings")]
        [SerializeField] private float waveDuration = 0.4f;
        [SerializeField] private float waveSpeed = 30f;
        [SerializeField] private float brightnessBoost = 1.5f;
        [SerializeField] private float heightPulse = 0.3f;
        [SerializeField] private AnimationCurve brightnessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private struct ClaimWave
        {
            public Vector2Int Origin;
            public float StartTime;
            public Color PlayerColor;
            public List<Vector2Int> AffectedCells;
        }

        // Tracks which wave currently "owns" each cell's animation.
        // Key: cellIndex, Value: index into _activeWaves.
        // When a new wave claims a cell already animating, the old wave loses it.
        private readonly Dictionary<long, int> _cellToWaveIndex = new();
        private readonly List<ClaimWave> _activeWaves = new();

        private Mesh _mesh;
        private Vector3[] _originalVertices;
        private Vector3[] _animatedVertices;
        private Color32[] _originalColors;
        private Color32[] _colors;
        private uint _width;
        private uint _height;
        private bool _isInitialized;

        public bool IsAnimating => _activeWaves.Count > 0;

        private IGameWorldDataProvider _gameData;
        public void Initialize(ServiceContainer services)
        {
            _gameData = services.Get<GameWorld>();
        }

        public void Prepare()
        {
            _width = _gameData.GridWidth;
            _height = _gameData.GridHeight;

            CaptureOriginalMesh();
        }
        
        public void Tick()
        {
            if (_activeWaves.Count == 0) return;

            bool meshDirty = false;
            float currentTime = Time.time;

            for (int w = _activeWaves.Count - 1; w >= 0; w--)
            {
                var wave = _activeWaves[w];
                float elapsed = currentTime - wave.StartTime;
                bool waveComplete = true;

                foreach (var cell in wave.AffectedCells)
                {
                    long cellIndex = (long)cell.y * _width + cell.x;

                    if (_cellToWaveIndex.TryGetValue(cellIndex, out int ownerWave) && ownerWave != w)
                    {
                        continue;
                    }

                    float dist = Vector2.Distance(cell, wave.Origin);
                    float cellDelay = dist / waveSpeed;
                    float cellElapsed = elapsed - cellDelay;

                    if (cellElapsed < 0f)
                    {
                        waveComplete = false;
                        continue;
                    }

                    float cellProgress = Mathf.Clamp01(cellElapsed / waveDuration);

                    if (cellProgress < 1f)
                    {
                        waveComplete = false;
                        AnimateCell(cellIndex, cellProgress, wave.PlayerColor);
                        meshDirty = true;
                    }
                    else
                    {
                        ResetCellToFinal(cellIndex);
                        _cellToWaveIndex.Remove(cellIndex);
                        meshDirty = true;
                    }
                }

                if (waveComplete)
                {
                    RemoveWaveAndRemapIndices(w);
                }
            }

            if (meshDirty)
            {
                _mesh.vertices = _animatedVertices;
                _mesh.colors32 = _colors;
            }

            if (_activeWaves.Count == 0)
            {
                _cellToWaveIndex.Clear();
            }
        }

        public void Dispose()
        {
            FinishAllImmediately();
        }

        private void CaptureOriginalMesh()
        {
            var meshFilter = _gameData.Territory.MeshFilter;
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            _mesh = meshFilter.sharedMesh;

            _originalVertices = _mesh.vertices;
            _animatedVertices = _mesh.vertices;
            _originalColors = _mesh.colors32;
            _colors = _mesh.colors32;
            _isInitialized = true;
        }

        public void AddWave(List<TerritoryChange> changes, uint playerId, Color playerColor)
        {
            if (!_isInitialized || changes == null || changes.Count == 0) return;

            _originalColors = _mesh.colors32;

            int waveIndex = _activeWaves.Count;

            var wave = new ClaimWave
            {
                Origin = CalculateOrigin(changes),
                StartTime = Time.time,
                PlayerColor = playerColor,
                AffectedCells = new List<Vector2Int>(changes.Count),
            };

            foreach (var change in changes)
            {
                if (change.NewOwner != playerId) continue;

                var cell = new Vector2Int(change.X, change.Y);
                wave.AffectedCells.Add(cell);

                long cellIndex = (long)change.Y * _width + change.X;

                if (_cellToWaveIndex.TryGetValue(cellIndex, out int oldWaveIdx))
                {
                    if (oldWaveIdx < _activeWaves.Count)
                    {
                        ResetCellToFinal(cellIndex);
                    }
                }

                _cellToWaveIndex[cellIndex] = waveIndex;
            }

            _activeWaves.Add(wave);
        }

        private void AnimateCell(long cellIndex, float progress, Color targetColor)
        {
            int vBase = (int)(cellIndex * 4);
            if (_originalVertices == null || vBase + 3 >= _originalVertices.Length) return;

            float brightnessFactor = brightnessCurve.Evaluate(Easing.PingPong(progress));
            float heightOffset = heightPulse * Easing.Spike(progress, 0.3f);

            for (int i = 0; i < 4; i++)
            {
                Vector3 orig = _originalVertices[vBase + i];
                _animatedVertices[vBase + i] = new Vector3(orig.x, heightOffset, orig.z);
            }

            Color animColor = Color.Lerp(
                targetColor,
                new Color(
                    Mathf.Min(1f, targetColor.r * brightnessBoost),
                    Mathf.Min(1f, targetColor.g * brightnessBoost),
                    Mathf.Min(1f, targetColor.b * brightnessBoost),
                    1f
                ),
                brightnessFactor
            );

            Color32 c = animColor;
            _colors[vBase + 0] = c;
            _colors[vBase + 1] = c;
            _colors[vBase + 2] = c;
            _colors[vBase + 3] = c;
        }

        private void ResetCellToFinal(long cellIndex)
        {
            int vBase = (int)(cellIndex * 4);
            if (_originalVertices == null || vBase + 3 >= _originalVertices.Length) return;

            for (int i = 0; i < 4; i++)
            {
                _animatedVertices[vBase + i] = _originalVertices[vBase + i];
                _colors[vBase + i] = _originalColors[vBase + i];
            }
        }

        public void FinishAllImmediately()
        {
            // Re-sync from mesh to get the latest TerritoryRenderer colors
            if (_mesh != null)
            {
                _originalColors = _mesh.colors32;
            }

            foreach (var wave in _activeWaves)
            {
                foreach (var cell in wave.AffectedCells)
                {
                    long cellIndex = (long)cell.y * _width + cell.x;
                    ResetCellToFinal(cellIndex);
                }
            }

            _activeWaves.Clear();
            _cellToWaveIndex.Clear();

            if (_isInitialized && _mesh != null)
            {
                _mesh.vertices = _animatedVertices;
                _mesh.colors32 = _colors;
            }
        }

        private void RemoveWaveAndRemapIndices(int removedIndex)
        {
            _activeWaves.RemoveAt(removedIndex);

            var keysToUpdate = new List<long>();
            var keysToRemove = new List<long>();

            foreach (var kvp in _cellToWaveIndex)
            {
                if (kvp.Value == removedIndex)
                    keysToRemove.Add(kvp.Key);
                else if (kvp.Value > removedIndex)
                    keysToUpdate.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
                _cellToWaveIndex.Remove(key);

            foreach (var key in keysToUpdate)
                _cellToWaveIndex[key] -= 1;
        }

        private static Vector2Int CalculateOrigin(List<TerritoryChange> changes)
        {
            if (changes.Count == 0) return Vector2Int.zero;

            int sumX = 0, sumY = 0;
            foreach (var change in changes)
            {
                sumX += change.X;
                sumY += change.Y;
            }
            return new Vector2Int(sumX / changes.Count, sumY / changes.Count);
        }
    }
}