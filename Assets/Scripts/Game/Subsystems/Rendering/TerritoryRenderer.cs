using Core.Services;
using Game.Data;
using UnityEngine;

namespace Game.Subsystems.Rendering
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

        private int _totalCellsUpdated;

        public MeshFilter MeshFilter => _meshFilter;
        public Mesh SharedMesh => _mesh;
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

            CreateMeshFromVisualData();

            Debug.Log($"[TerritoryRenderer] Initialized: {_gameWorldData.GridWidth}x{_gameWorldData.GridHeight} grid, " +
                      $"{_mesh.vertexCount} vertices, {_mesh.triangles.Length / 3} triangles");
        }

        public void FlushToMesh(int changeCount = 0)
        {
            if (!IsInitialized)
            {
                return;
            }

            var visualData = _gameWorldData.Territory.VisualData;
            if (visualData == null || !visualData.IsInitialized) return;

            _mesh.colors32 = visualData.Colors;

            _totalCellsUpdated += changeCount;

            if (debugLogUpdates && changeCount > 0)
            {
                Debug.Log($"[TerritoryRenderer] Flushed {changeCount} cell changes to mesh");
            }
        }

        private void CreateMeshFromVisualData()
        {
            var visualData = _gameWorldData.Territory.VisualData;
            if (visualData == null || !visualData.IsInitialized)
            {
                Debug.LogError("[TerritoryRenderer] TerritoryVisualData not initialized!");
                return;
            }

            _mesh = new Mesh
            {
                name = "TerritoryMesh"
            };

            if (visualData.VertexCount > 65535)
            {
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            _mesh.vertices = visualData.Vertices;
            _mesh.triangles = visualData.Triangles;
            _mesh.colors32 = visualData.Colors;
            _mesh.uv = visualData.UVs;

            _mesh.RecalculateNormals();
            _mesh.MarkDynamic();
            _mesh.RecalculateBounds();

            _meshFilter.mesh = _mesh;
        }

        public void Tick() { }

        public void Dispose(bool _) { }

        private void OnDrawGizmosSelected()
        {
            if (!IsInitialized) return;

            Gizmos.color = Color.green;
            Vector3 size = new Vector3(
                _gameWorldData.GridWidth * _gameWorldData.Config.CellSize,
                0.1f,
                _gameWorldData.GridHeight * _gameWorldData.Config.CellSize);
            Vector3 center = size / 2f;
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Vector3.zero, _gameWorldData.Config.CellSize * 0.5f);
        }
    }
}