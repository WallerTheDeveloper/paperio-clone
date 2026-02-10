using System.Collections.Generic;
using Core.Services;
using Game.Data;
using UnityEngine;

namespace Game.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerritoryRenderer : MonoBehaviour, IService
    {
        [Header("Configuration")]
        [SerializeField] private Material territoryMaterial;
        [SerializeField] private bool debugLogUpdates = false;
        
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        
        private Vector3[] _vertices;
        private int[] _triangles;
        private Color32[] _colors;
        private Vector2[] _uvs;

        private uint[] _cellOwners;
        
        private int _totalCellsUpdated;

        public MeshFilter MeshFilter => _meshFilter;
        public int TotalCellsUpdated => _totalCellsUpdated;
        public bool IsInitialized => _mesh != null;

        private IGameWorldDataProvider _gameWorldData;
        public void Initialize(ServiceContainer services)
        {
            _gameWorldData = services.Get<GameWorld>();
            
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Dispose()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }
        
        public void CreateTerritory()
        {
            if (territoryMaterial != null)
            {
                _meshRenderer.material = territoryMaterial;
            }
            
            CreateMesh();
            
            Debug.Log($"[TerritoryRenderer] Initialized: {_gameWorldData.GridWidth}x{_gameWorldData.GridHeight} grid, " +
                      $"{_vertices.Length} vertices, {_triangles.Length / 3} triangles");
        }
        public void ApplyFullState(TerritoryData territoryData)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[TerritoryRenderer] Not initialized, call Initialize() first");
                return;
            }
            
            if (territoryData.Width != _gameWorldData.GridWidth || territoryData.Height != _gameWorldData.GridHeight)
            {
                Debug.LogError($"[TerritoryRenderer] Size mismatch: " +
                               $"renderer={_gameWorldData.GridWidth}x{_gameWorldData.GridHeight}, data={territoryData.Width}x{territoryData.Height}");
                return;
            }
            
            int updated = 0;
            var rawCells = territoryData.GetRawCells();
            
            for (int i = 0; i < rawCells.Length; i++)
            {
                uint owner = rawCells[i];
                if (_cellOwners[i] != owner)
                {
                    _cellOwners[i] = owner;
                    SetCellColor(i, owner);
                    updated++;
                }
            }
            
            _mesh.colors32 = _colors;
            
            _totalCellsUpdated += updated;
            
            if (debugLogUpdates)
            {
                Debug.Log($"[TerritoryRenderer] Full state applied: {updated} cells updated");
            }
        }

        public void UpdateTerritory(List<TerritoryChange> changes)
        {
            if (!IsInitialized || changes == null || changes.Count == 0)
            {
                return;
            }
            
            foreach (var change in changes)
            {
                if (!IsValidCell(change.X, change.Y))
                {
                    continue;
                }
                
                long cellIndex = change.Y * _gameWorldData.GridWidth + change.X;
                _cellOwners[cellIndex] = change.NewOwner;
                SetCellColor(cellIndex, change.NewOwner);
            }
            
            _mesh.colors32 = _colors;
            
            _totalCellsUpdated += changes.Count;
            
            if (debugLogUpdates && changes.Count > 0)
            {
                Debug.Log($"[TerritoryRenderer] Updated {changes.Count} cells");
            }
        }
        
        private void CreateMesh()
        {
            uint cellCount = _gameWorldData.GridWidth * _gameWorldData.GridHeight;
            uint vertexCount = cellCount * 4;  // 4 vertices per cell
            uint triangleCount = cellCount * 6; // 6 indices per cell (2 triangles)
            
            _vertices = new Vector3[vertexCount];
            _triangles = new int[triangleCount];
            _colors = new Color32[vertexCount];
            _uvs = new Vector2[vertexCount];
            _cellOwners = new uint[cellCount];

            Color32 neutralColor = _gameWorldData.Config.NeutralColor;
            
            for (int y = 0; y < _gameWorldData.GridHeight; y++)
            {
                for (int x = 0; x < _gameWorldData.GridWidth; x++)
                {
                    long cellIndex = y * _gameWorldData.GridWidth + x;
                    long vertexBase = cellIndex * 4;
                    long triangleBase = cellIndex * 6;
                    
                    float x0 = x * _gameWorldData.Config.CellSize;
                    float x1 = (x + 1) * _gameWorldData.Config.CellSize;
                    int flippedY = (int)(_gameWorldData.GridHeight - 1 - y);
                    float z0 = flippedY * _gameWorldData.Config.CellSize;
                    float z1 = (flippedY + 1) * _gameWorldData.Config.CellSize;
                    
                    _vertices[vertexBase + 0] = new Vector3(x0, 0, z1); // Top-left
                    _vertices[vertexBase + 1] = new Vector3(x1, 0, z1); // Top-right
                    _vertices[vertexBase + 2] = new Vector3(x0, 0, z0); // Bottom-left
                    _vertices[vertexBase + 3] = new Vector3(x1, 0, z0); // Bottom-right

                    _uvs[vertexBase + 0] = new Vector2(0, 1);
                    _uvs[vertexBase + 1] = new Vector2(1, 1);
                    _uvs[vertexBase + 2] = new Vector2(0, 0);
                    _uvs[vertexBase + 3] = new Vector2(1, 0);
                    
                    _triangles[triangleBase + 0] = (int)vertexBase + 0;
                    _triangles[triangleBase + 1] = (int)vertexBase + 2;
                    _triangles[triangleBase + 2] = (int)vertexBase + 1;
                    
                    _triangles[triangleBase + 3] = (int)vertexBase + 1;
                    _triangles[triangleBase + 4] = (int)vertexBase + 2;
                    _triangles[triangleBase + 5] = (int)vertexBase + 3;
                    
                    _colors[vertexBase + 0] = neutralColor;
                    _colors[vertexBase + 1] = neutralColor;
                    _colors[vertexBase + 2] = neutralColor;
                    _colors[vertexBase + 3] = neutralColor;

                    _cellOwners[cellIndex] = 0;
                }
            }
            
            _mesh = new Mesh
            {
                name = "TerritoryMesh"
            };

            if (vertexCount > 65535)
            {
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            
            _mesh.vertices = _vertices;
            _mesh.triangles = _triangles;
            _mesh.colors32 = _colors;
            _mesh.uv = _uvs;
            
            _mesh.RecalculateNormals();
            
            _mesh.MarkDynamic();
            
            _mesh.RecalculateBounds();
            
            _meshFilter.mesh = _mesh;
        }
        
        private void SetCellColor(long cellIndex, uint ownerId)
        {
            Color32 color = GetColorForOwner(ownerId);
            
            long vertexBase = cellIndex * 4;
            _colors[vertexBase + 0] = color;
            _colors[vertexBase + 1] = color;
            _colors[vertexBase + 2] = color;
            _colors[vertexBase + 3] = color;
        }
        
        private Color32 GetColorForOwner(uint ownerId)
        {
            if (ownerId == 0)
            {
                return _gameWorldData.Config.NeutralColor;
            }

            return GetTerritoryColor(ownerId);
        }

        private Color GetTerritoryColor(uint ownerId)
        {
            if (ownerId == 0)
            {
                return _gameWorldData.Config.NeutralColor;
            }
            _gameWorldData.PlayerColors.TryGetValue(ownerId, out Color playerColor);
            return new Color(
                playerColor.r * 0.7f,
                playerColor.g * 0.7f,
                playerColor.b * 0.7f,
                1f
            );
        }
        
        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < _gameWorldData.GridWidth && y >= 0 && y < _gameWorldData.GridHeight;
        }
        
        private void OnDrawGizmosSelected()
        {
            if (!IsInitialized) return;
            
            Gizmos.color = Color.green;
            Vector3 size = new Vector3(_gameWorldData.GridWidth * _gameWorldData.Config.CellSize, 0.1f,  _gameWorldData.GridHeight * _gameWorldData.Config.CellSize);
            Vector3 center = size / 2f;
            Gizmos.DrawWireCube(center, size);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Vector3.zero, _gameWorldData.Config.CellSize * 0.5f);
        }
    }
}