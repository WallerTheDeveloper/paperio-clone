using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Effects.Implementations
{
    public class TerritoryClaim : MonoBehaviour, IEffect
    {
        [SerializeField] private Effect type;
        
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
            public uint PlayerId;
            public Color PlayerColor;
            public List<Vector2Int> AffectedCells;
            public float MaxDistance;
        }

        private readonly List<ClaimWave> _activeWaves = new();
        private readonly Dictionary<long, CellAnimState> _animatingCells = new();

        private struct CellAnimState
        {
            public float Progress;
            public Color TargetColor;
            public Color HighlightColor;
        }

        private Mesh _mesh;
        private Vector3[] _originalVertices;
        private Vector3[] _animatedVertices;
        private Color32[] _colors;
        private uint _width;
        private uint _height;
        private float _cellSize;
        private bool _isInitialized;

        public Effect Type => type;
        public GameObject GameObject => this.gameObject;
        public bool IsPlaying { get; private set; }

        private IGameWorldDataProvider _gameData;
        
        public void Prepare(IGameWorldDataProvider gameData)
        {
            _width = gameData.GridWidth;
            _height = gameData.GridHeight;
            _cellSize = gameData.Config.CellSize;
            _gameData = gameData;
            RebuildMeshData(_gameData);
        }

        private void RebuildMeshData(IGameWorldDataProvider gameData)
        {
            var meshFilter = gameData.Territory.MeshFilter;
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                _mesh = meshFilter.sharedMesh;
                _originalVertices = _mesh.vertices;
                _animatedVertices = new Vector3[_originalVertices.Length];
                _originalVertices.CopyTo(_animatedVertices, 0);
                _colors = _mesh.colors32;
                _isInitialized = true;
            }
        }
        
        public void Play(EffectData data)
        {
            RebuildMeshData(_gameData);
            
            var changes = data.TerritoryChange;
            var playerId = data.PlayerId;
            var playerColor = data.Color;

            IsPlaying = true;
            if (changes == null || changes.Count == 0)
            {
                return;
            }

            Vector2Int origin = CalculateOrigin(changes);
            float maxDist = CalculateMaxDistance(changes, origin);

            var wave = new ClaimWave
            {
                Origin = origin,
                StartTime = Time.time,
                PlayerId = playerId,
                PlayerColor = playerColor,
                AffectedCells = new List<Vector2Int>(changes.Count),
                MaxDistance = maxDist
            };

            Color highlight = new Color(
                Mathf.Min(1f, playerColor.r * brightnessBoost),
                Mathf.Min(1f, playerColor.g * brightnessBoost),
                Mathf.Min(1f, playerColor.b * brightnessBoost),
                1f
            );

            foreach (var change in changes)
            {
                if (change.NewOwner != playerId) continue;
                
                wave.AffectedCells.Add(new Vector2Int(change.X, change.Y));
                
                long cellIndex = change.Y * _width + change.X;
                if (!_animatingCells.ContainsKey(cellIndex))
                {
                    _animatingCells[cellIndex] = new CellAnimState
                    {
                        Progress = 0f,
                        TargetColor = playerColor,
                        HighlightColor = highlight
                    };
                }
            }

            _activeWaves.Add(wave);
        }

        public void Stop()
        {
            IsPlaying = false;
            ClearAllAnimations();
        }

        public void Reset()
        {
            _activeWaves.Clear();
            _animatingCells.Clear();
            IsPlaying = false;
            
            // Restore mesh to unmodified state
            if (_isInitialized && _originalVertices != null && _animatedVertices != null)
            {
                _originalVertices.CopyTo(_animatedVertices, 0);
                ApplyMeshChanges();
            }
        }

        public void Update()
        {
            if (_activeWaves.Count == 0)
            {
                return;
            }

            bool meshDirty = false;
            float currentTime = Time.time;

            for (int w = _activeWaves.Count - 1; w >= 0; w--)
            {
                var wave = _activeWaves[w];
                float elapsed = currentTime - wave.StartTime;

                bool waveComplete = true;

                foreach (var cell in wave.AffectedCells)
                {
                    float dist = Vector2.Distance(cell, wave.Origin);
                    float cellDelay = dist / waveSpeed;
                    float cellElapsed = elapsed - cellDelay;

                    if (cellElapsed < 0f)
                    {
                        waveComplete = false;
                        continue;
                    }

                    float cellProgress = Mathf.Clamp01(cellElapsed / waveDuration);
                    long cellIndex = cell.y * _width + cell.x;

                    if (cellProgress < 1f)
                    {
                        waveComplete = false;
                        ApplyCellAnimation(cellIndex, cellProgress, wave.PlayerColor);
                        meshDirty = true;
                    }
                    else
                    {
                        ResetCellAnimation(cellIndex, wave.PlayerColor);
                        _animatingCells.Remove(cellIndex);
                        meshDirty = true;
                    }
                }

                if (waveComplete)
                {
                    _activeWaves.RemoveAt(w);
                }
            }

            if (meshDirty)
            {
                ApplyMeshChanges();
            }

            // Mark as done when all waves finished
            if (_activeWaves.Count == 0)
            {
                IsPlaying = false;
            }
        }

        private void ApplyCellAnimation(long cellIndex, float progress, Color targetColor)
        {
            float brightnessFactor = brightnessCurve.Evaluate(Easing.PingPong(progress));
            float heightOffset = heightPulse * Easing.Spike(progress, 0.3f);

            int vertexBase = (int)(cellIndex * 4);
            
            if (_animatedVertices == null || vertexBase + 3 >= _animatedVertices.Length) return;

            for (int i = 0; i < 4; i++)
            {
                Vector3 original = _originalVertices[vertexBase + i];
                _animatedVertices[vertexBase + i] = new Vector3(original.x, heightOffset, original.z);
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

            Color32 color32 = animColor;
            _colors[vertexBase + 0] = color32;
            _colors[vertexBase + 1] = color32;
            _colors[vertexBase + 2] = color32;
            _colors[vertexBase + 3] = color32;
        }

        private void ResetCellAnimation(long cellIndex, Color finalColor)
        {
            int vertexBase = (int)(cellIndex * 4);
            
            if (_animatedVertices == null || vertexBase + 3 >= _animatedVertices.Length) return;

            for (int i = 0; i < 4; i++)
            {
                _animatedVertices[vertexBase + i] = _originalVertices[vertexBase + i];
            }

            Color32 color32 = finalColor;
            _colors[vertexBase + 0] = color32;
            _colors[vertexBase + 1] = color32;
            _colors[vertexBase + 2] = color32;
            _colors[vertexBase + 3] = color32;
        }

        private void ApplyMeshChanges()
        {
            if (_mesh == null || _animatedVertices == null) return;
            
            _mesh.vertices = _animatedVertices;
            _mesh.colors32 = _colors;
        }

        private void ClearAllAnimations()
        {
            _activeWaves.Clear();
            _animatingCells.Clear();
            
            if (_isInitialized && _originalVertices != null && _animatedVertices != null)
            {
                _originalVertices.CopyTo(_animatedVertices, 0);
                ApplyMeshChanges();
            }
        }

        private Vector2Int CalculateOrigin(List<TerritoryChange> changes)
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

        private float CalculateMaxDistance(List<TerritoryChange> changes, Vector2Int origin)
        {
            float maxDist = 0f;
            foreach (var change in changes)
            {
                float dist = Vector2.Distance(new Vector2(change.X, change.Y), origin);
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }
    }
}