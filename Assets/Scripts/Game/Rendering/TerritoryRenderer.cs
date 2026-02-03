using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TerritoryRenderer : MonoBehaviour
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

        private int _width;
        private int _height;
        private float _cellSize;
        
        private GameWorld _gameWorld;
        
        private uint[] _cellOwners;
        
        private int _totalCellsUpdated;
        
        public int Width => _width;
        public int Height => _height;
        public float CellSize => _cellSize;
        public int TotalCellsUpdated => _totalCellsUpdated;
        public bool IsInitialized => _mesh != null;

        public void Initialize(int width, int height, float cellSize, GameWorld gameWorld)
        {
            _width = width;
            _height = height;
            _cellSize = cellSize;
            _gameWorld = gameWorld;
            
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            
            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            
            if (territoryMaterial != null)
            {
                _meshRenderer.material = territoryMaterial;
            }
            else
            {
                Debug.LogWarning("[TerritoryRenderer] No material assigned, using default");
                _meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            CreateMesh();
            
            Debug.Log($"[TerritoryRenderer] Initialized: {width}x{height} grid, " +
                      $"{_vertices.Length} vertices, {_triangles.Length / 3} triangles");
        }
        private void CreateMesh()
        {
            int cellCount = _width * _height;
            int vertexCount = cellCount * 4;  // 4 vertices per cell
            int triangleCount = cellCount * 6; // 6 indices per cell (2 triangles)
            
            _vertices = new Vector3[vertexCount];
            _triangles = new int[triangleCount];
            _colors = new Color32[vertexCount];
            _uvs = new Vector2[vertexCount];
            _cellOwners = new uint[cellCount];
            
            Color32 neutralColor = _gameWorld != null 
                ? (Color32)_gameWorld.Config.NeutralColor 
                : new Color32(40, 40, 40, 255);
            
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int cellIndex = y * _width + x;
                    int vertexBase = cellIndex * 4;
                    int triangleBase = cellIndex * 6;
                    
                    float x0 = x * _cellSize;
                    float x1 = (x + 1) * _cellSize;
                    float z0 = y * _cellSize;
                    float z1 = (y + 1) * _cellSize;
                    
                    _vertices[vertexBase + 0] = new Vector3(x0, 0, z1); // Top-left
                    _vertices[vertexBase + 1] = new Vector3(x1, 0, z1); // Top-right
                    _vertices[vertexBase + 2] = new Vector3(x0, 0, z0); // Bottom-left
                    _vertices[vertexBase + 3] = new Vector3(x1, 0, z0); // Bottom-right

                    _uvs[vertexBase + 0] = new Vector2(0, 1);
                    _uvs[vertexBase + 1] = new Vector2(1, 1);
                    _uvs[vertexBase + 2] = new Vector2(0, 0);
                    _uvs[vertexBase + 3] = new Vector2(1, 0);
                    
                    _triangles[triangleBase + 0] = vertexBase + 0;
                    _triangles[triangleBase + 1] = vertexBase + 2;
                    _triangles[triangleBase + 2] = vertexBase + 1;
                    
                    _triangles[triangleBase + 3] = vertexBase + 1;
                    _triangles[triangleBase + 4] = vertexBase + 2;
                    _triangles[triangleBase + 5] = vertexBase + 3;
                    
                    _colors[vertexBase + 0] = neutralColor;
                    _colors[vertexBase + 1] = neutralColor;
                    _colors[vertexBase + 2] = neutralColor;
                    _colors[vertexBase + 3] = neutralColor;

                    _cellOwners[cellIndex] = 0;
                }
            }
            
            _mesh = new Mesh();
            _mesh.name = "TerritoryMesh";
            
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

        public void ApplyFullState(TerritoryData territoryData)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[TerritoryRenderer] Not initialized, call Initialize() first");
                return;
            }
            
            if (territoryData.Width != _width || territoryData.Height != _height)
            {
                Debug.LogError($"[TerritoryRenderer] Size mismatch: " +
                               $"renderer={_width}x{_height}, data={territoryData.Width}x{territoryData.Height}");
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
                
                int cellIndex = change.Y * _width + change.X;
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

        private void SetCellColor(int cellIndex, uint ownerId)
        {
            Color32 color = GetColorForOwner(ownerId);
            
            int vertexBase = cellIndex * 4;
            _colors[vertexBase + 0] = color;
            _colors[vertexBase + 1] = color;
            _colors[vertexBase + 2] = color;
            _colors[vertexBase + 3] = color;
        }

        private Color32 GetColorForOwner(uint ownerId)
        {
            if (_gameWorld != null)
            {
                return _gameWorld.GetTerritoryColor(ownerId);
            }
            
            if (ownerId == 0)
            {
                return new Color32(40, 40, 40, 255); // Neutral gray
            }
            
            return GetFallbackColor(ownerId);
        }

        private Color32 GetFallbackColor(uint playerId)
        {
            float hue = (playerId * 0.618033988749895f) % 1.0f;
            Color color = Color.HSVToRGB(hue, 0.7f, 0.6f);
            return color;
        }

        private bool IsValidCell(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }

        public void RefreshAllColors(TerritoryData territoryData)
        {
            ApplyFullState(territoryData);
        }

        public Vector3 GetCellCenter(int x, int y)
        {
            return new Vector3(
                (x + 0.5f) * _cellSize,
                0,
                (y + 0.5f) * _cellSize
            );
        }

        public Vector2Int GetCellAt(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / _cellSize),
                Mathf.FloorToInt(worldPos.z / _cellSize)
            );
        }

        public void DebugHighlightCell(int x, int y, Color32 color)
        {
            if (!IsValidCell(x, y)) return;
            
            int cellIndex = y * _width + x;
            int vertexBase = cellIndex * 4;
            
            _colors[vertexBase + 0] = color;
            _colors[vertexBase + 1] = color;
            _colors[vertexBase + 2] = color;
            _colors[vertexBase + 3] = color;
            
            _mesh.colors32 = _colors;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsInitialized) return;
            
            // Draw grid bounds
            Gizmos.color = Color.green;
            Vector3 size = new Vector3(_width * _cellSize, 0.1f, _height * _cellSize);
            Vector3 center = size / 2f;
            Gizmos.DrawWireCube(center, size);
            
            // Draw origin marker
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Vector3.zero, _cellSize * 0.5f);
        }
    }
}