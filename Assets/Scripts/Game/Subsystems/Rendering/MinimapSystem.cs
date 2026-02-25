using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.UI;
using Helpers;
using UnityEngine;

namespace Game.Subsystems.Rendering
{
    public class MinimapSystem : MonoBehaviour, IService
    {
        [SerializeField] private MinimapUI minimapUI;
        
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private float gridPadding = 2f;
        [SerializeField] private float cameraHeight = 200f;
        [SerializeField] private float indicatorSize = 2.5f;
        [SerializeField] private float localIndicatorSize = 3.5f;
        [SerializeField] private float indicatorHeight = 5f;
        [SerializeField] private bool pulseLocalIndicator = true;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmplitude = 0.3f;
        [SerializeField] private bool showViewBounds = true;
        [SerializeField] private Color viewBoundsColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField] private float viewBoundsLineWidth = 0.5f;
        [SerializeField] private int indicatorLayer = 31;
        [SerializeField] private bool debugLog = false;

        private readonly Dictionary<uint, GameObject> _indicators = new();
        private readonly List<uint> _staleIndicators = new();

        private float _pulseTimer;

        private GameObject _viewBoundsObj;
        private LineRenderer _viewBoundsLine;
        private Camera _mainCamera;

        private bool _isInitialized;

        public RenderTexture MinimapTexture => minimapCamera != null ? minimapCamera.targetTexture : null;
        public bool IsReady => _isInitialized && MinimapTexture != null;

        private IGameWorldDataProvider _gameData;
        private PlayerVisualsManager _playerVisualsManager;
        private IColorDataProvider _colorDataProvider;
        public void Initialize(ServiceContainer services)
        {
            _gameData = services.Get<GameWorld>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            
            if (minimapCamera == null)
            {
                Debug.LogError("[MinimapSystem] No camera assigned! Drag your minimap camera into the Inspector.");
                return;
            }

            if (minimapCamera.targetTexture == null)
            {
                Debug.LogError("[MinimapSystem] Minimap camera has no Target Texture. " +
                               "Create a RenderTexture and assign it on the camera.");
                return;
            }

            if (_gameData is GameWorld gw)
            {
                gw.OnGameStarted += OnGameStarted;
            }
        }

        public void Tick()
        {
            if (!_isInitialized)
            {
                return;
            }

            UpdateIndicators();

            if (showViewBounds)
            {
                UpdateViewBounds();
            }

            if (pulseLocalIndicator)
            {
                _pulseTimer += Time.deltaTime * pulseSpeed;
            }
        }

        public void TickLate() { }

        public void Dispose()
        {
            Cleanup();

            if (_gameData is GameWorld gw)
            {
                gw.OnGameStarted -= OnGameStarted;
            }
        }

        public void CreateUI()
        {
            minimapUI.Initialize(_gameData);
        }
        
        private void OnGameStarted()
        {
            FitCameraToGrid();

            if (showViewBounds)
            {
                CreateViewBoundsIndicator();
            }

            // Ensure camera sees the indicator layer
            minimapCamera.cullingMask |= (1 << indicatorLayer);

            _isInitialized = true;

            if (debugLog)
            {
                Debug.Log($"[MinimapSystem] Initialized — Grid: {_gameData.GameSessionData.GridWidth}x{_gameData.GameSessionData.GridHeight}, " +
                          $"OrthoSize: {minimapCamera.orthographicSize:F1}");
            }
        }

        private void Cleanup()
        {
            foreach (var kvp in _indicators)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _indicators.Clear();

            if (_viewBoundsObj != null)
            {
                Destroy(_viewBoundsObj);
            }

            Destroy(minimapUI.gameObject);
            
            _isInitialized = false;
        }

        private void FitCameraToGrid()
        {
            float gridWorldWidth = _gameData.GameSessionData.GridWidth * _gameData.Config.CellSize;
            float gridWorldHeight = _gameData.GameSessionData.GridHeight * _gameData.Config.CellSize;

            Vector3 gridCenter = GridHelper.GetGridCenter(
                _gameData.GameSessionData.GridWidth, _gameData.GameSessionData.GridHeight, _gameData.Config.CellSize);

            minimapCamera.transform.position = new Vector3(gridCenter.x, cameraHeight, gridCenter.z);

            float verticalHalf = (gridWorldHeight + gridPadding * 2f) / 2f;
            float horizontalHalf = (gridWorldWidth + gridPadding * 2f) / 2f;

            minimapCamera.orthographicSize = Mathf.Max(verticalHalf, horizontalHalf);
        }

        private void UpdateIndicators()
        {
            if (_playerVisualsManager == null)
            {
                return;
            }

            var allVisuals = _playerVisualsManager.ActiveVisuals;
            if (allVisuals == null)
            {
                return;
            }

            var activePlayers = new HashSet<uint>();

            foreach (var kvp in allVisuals)
            {
                uint playerId = kvp.Key;
                PlayerVisual visual = kvp.Value;
                activePlayers.Add(playerId);

                if (visual == null || !visual.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool isLocal = playerId == _gameData.GameSessionData.LocalPlayerId;

                if (!_indicators.TryGetValue(playerId, out var indicator) || indicator == null)
                {
                    indicator = CreateIndicator(playerId, isLocal);
                    _indicators[playerId] = indicator;
                }

                Vector3 playerPos = visual.transform.position;
                indicator.transform.position = new Vector3(playerPos.x, indicatorHeight, playerPos.z);

                if (isLocal && pulseLocalIndicator)
                {
                    float pulse = 1f + Mathf.Sin(_pulseTimer) * pulseAmplitude;
                    float size = localIndicatorSize * pulse;
                    indicator.transform.localScale = new Vector3(size, 0.1f, size);
                }
            }

            _staleIndicators.Clear();
            foreach (var kvp in _indicators)
            {
                if (!activePlayers.Contains(kvp.Key))
                {
                    _staleIndicators.Add(kvp.Key);
                }
            }

            foreach (uint staleId in _staleIndicators)
            {
                if (_indicators.TryGetValue(staleId, out var staleObj) && staleObj != null)
                {
                    Destroy(staleObj);
                }
                _indicators.Remove(staleId);
            }
        }

        private GameObject CreateIndicator(uint playerId, bool isLocal)
        {
            float size = isLocal ? localIndicatorSize : indicatorSize;
            Color color = _colorDataProvider.GetColorOf(playerId);

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicator.name = $"MinimapIndicator_{playerId}";
            indicator.layer = indicatorLayer;
            indicator.transform.SetParent(transform);
            indicator.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            indicator.transform.localScale = new Vector3(size, size, 1f);

            var collider = indicator.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = indicator.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", color);
            mpb.SetColor("_Color", color);
            renderer.SetPropertyBlock(mpb);

            if (debugLog)
            {
                Debug.Log($"[MinimapSystem] Created indicator for player {playerId} (local={isLocal})");
            }

            return indicator;
        }

        private void CreateViewBoundsIndicator()
        {
            _viewBoundsObj = new GameObject("MinimapViewBounds");
            _viewBoundsObj.layer = indicatorLayer;
            _viewBoundsObj.transform.SetParent(transform);

            _viewBoundsLine = _viewBoundsObj.AddComponent<LineRenderer>();
            _viewBoundsLine.positionCount = 5;
            _viewBoundsLine.loop = false;
            _viewBoundsLine.useWorldSpace = true;
            _viewBoundsLine.startWidth = viewBoundsLineWidth;
            _viewBoundsLine.endWidth = viewBoundsLineWidth;
            _viewBoundsLine.startColor = viewBoundsColor;
            _viewBoundsLine.endColor = viewBoundsColor;
            _viewBoundsLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _viewBoundsLine.receiveShadows = false;

            _viewBoundsLine.material = new Material(Shader.Find("Sprites/Default"));
            _viewBoundsLine.material.color = viewBoundsColor;
        }

        private void UpdateViewBounds()
        {
            if (_viewBoundsLine == null) return;

            _mainCamera ??= Camera.main;
            if (_mainCamera == null) return;

            float y = indicatorHeight - 0.5f;

            if (TryGetGroundRect(_mainCamera, out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl))
            {
                _viewBoundsLine.SetPosition(0, new Vector3(bl.x, y, bl.z));
                _viewBoundsLine.SetPosition(1, new Vector3(br.x, y, br.z));
                _viewBoundsLine.SetPosition(2, new Vector3(tr.x, y, tr.z));
                _viewBoundsLine.SetPosition(3, new Vector3(tl.x, y, tl.z));
                _viewBoundsLine.SetPosition(4, new Vector3(bl.x, y, bl.z));
                _viewBoundsLine.enabled = true;
            }
            else
            {
                _viewBoundsLine.enabled = false;
            }
        }

        private bool TryGetGroundRect(Camera cam, out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl)
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero);

            bool gotBL = RaycastGround(cam, new Vector3(0, 0, 0), ground, out bl);
            bool gotBR = RaycastGround(cam, new Vector3(1, 0, 0), ground, out br);
            bool gotTR = RaycastGround(cam, new Vector3(1, 1, 0), ground, out tr);
            bool gotTL = RaycastGround(cam, new Vector3(0, 1, 0), ground, out tl);

            return gotBL && gotBR && gotTR && gotTL;
        }

        private bool RaycastGround(Camera cam, Vector3 viewportPoint, Plane plane, out Vector3 hit)
        {
            Ray ray = cam.ViewportPointToRay(viewportPoint);
            if (plane.Raycast(ray, out float distance))
            {
                hit = ray.GetPoint(distance);
                return true;
            }
            hit = Vector3.zero;
            return false;
        }

        public Vector2 WorldToMinimapUV(Vector3 worldPos)
        {
            if (_gameData == null) return Vector2.zero;

            float gridWorldWidth = _gameData.GameSessionData.GridWidth * _gameData.Config.CellSize;
            float gridWorldHeight = _gameData.GameSessionData.GridHeight * _gameData.Config.CellSize;

            return new Vector2(
                Mathf.Clamp01(worldPos.x / gridWorldWidth),
                Mathf.Clamp01(worldPos.z / gridWorldHeight));
        }

        public Vector3 MinimapUVToWorld(Vector2 uv)
        {
            if (_gameData == null) return Vector3.zero;

            float gridWorldWidth = _gameData.GameSessionData.GridWidth * _gameData.Config.CellSize;
            float gridWorldHeight = _gameData.GameSessionData.GridHeight * _gameData.Config.CellSize;

            return new Vector3(uv.x * gridWorldWidth, 0f, uv.y * gridWorldHeight);
        }
    }
}