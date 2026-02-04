using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Rendering;
using Helpers;
using UnityEngine;

namespace Game
{
    public class GameWorld : MonoBehaviour, IService, IGameWorldDataProvider
    {
        [SerializeField] private GameWorldConfig config;
        [SerializeField] private TerritoryRenderer territoryRenderer;
        
        [Header("Debug")]
        [SerializeField] private bool logTerritoryUpdates = false;
        [SerializeField] private bool logPlayerUpdates = false;
        
        private TerritoryData _territoryData;
        private uint _localPlayerId;
        private uint _gridWidth;
        private uint _gridHeight;
        private uint _tickRateMs;
        private bool _isGameActive;

        private float _lastTickTime;
        private uint _lastTick;
        
        public event Action OnGameStarted;
        public event Action OnGameEnded;
        public event Action<uint> OnLocalPlayerSpawned;
        public event Action<List<TerritoryChange>> OnTerritoryChanged;

        public GameWorldConfig Config => config;
        public TerritoryData Territory => _territoryData;
        public uint LocalPlayerId => _localPlayerId;
        public bool IsGameActive => _isGameActive;
        public uint GridWidth => _gridWidth;
        public uint GridHeight => _gridHeight;
        public uint TickRateMs => _tickRateMs;
        
        private float TickProgress
        {
            get
            {
                if (_tickRateMs == 0)
                {
                    return 1f;
                }
                float tickDuration = _tickRateMs / 1000f;
                float elapsed = Time.time - _lastTickTime;
                return Mathf.Clamp01(elapsed / tickDuration);
            }
        }
        
        public PlayerVisual LocalPlayerVisual => _playerVisualsManager?.LocalPlayerVisual;
        
        private CameraController _cameraController;
        private PlayerVisualsManager _playerVisualsManager;
        public void Initialize(ServiceContainer services)
        {
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            
            if (territoryRenderer == null)
            {
                Debug.LogWarning("[GameWorld] TerritoryRenderer not assigned - territory won't render");
            }
        }

        public void Tick()
        {
            if (!_isGameActive)
            {
                return;
            }
            
            _playerVisualsManager.UpdateInterpolation(TickProgress);
        }

        public void TickLate()
        {
            if(_cameraController != null)
            {
                _cameraController.TickLate();
            }
        }

        public void Dispose()
        {
            _playerVisualsManager.ClearAll();
            
            _territoryData?.Clear();
            _isGameActive = false;
            
            _cameraController.Dispose();
            Debug.Log("[GameWorld] Disposed");
        }

        public void OnJoinedGame(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            
            if (response.InitialState != null)
            {
                _gridWidth = response.InitialState.GridWidth;
                _gridHeight = response.InitialState.GridHeight;
                
                _playerVisualsManager.UpdateFromState(response.InitialState, _localPlayerId);
                _playerVisualsManager.SpawnPlayers();
                
                _cameraController = FindFirstObjectByType<CameraController>();
                _cameraController.Initialize(this as IGameWorldDataProvider);
                
                Debug.Log($"[GameWorld] PlayerVisualsManager initialized with {response.InitialState.Players.Count} players");
                
                InitializeFromState(response.InitialState);
            }
            
            _isGameActive = true;
            _lastTickTime = Time.time;
            
            
            Debug.Log($"[GameWorld] Game started! " +
                      $"LocalPlayer={_localPlayerId}, " +
                      $"Grid={GridWidth}x{GridHeight}, " +
                      $"TickRate={_tickRateMs}ms");
            
            OnGameStarted?.Invoke();
            OnLocalPlayerSpawned?.Invoke(_localPlayerId);
        }

        public void OnServerStateUpdated(PaperioState state)
        {
            if (!_isGameActive)
            {
                return;
            }
            
            _lastTick = state.Tick;
            _lastTickTime = Time.time;
            
            if (_territoryData != null && state.Territory != null)
            {
                var changes = _territoryData.ApplyServerState(state.Territory);
                
                if (changes.Count > 0)
                {
                    if (territoryRenderer != null && territoryRenderer.IsInitialized)
                    {
                        territoryRenderer.UpdateTerritory(changes);
                    }
                    
                    OnTerritoryChanged?.Invoke(changes);
                    
                    if (logTerritoryUpdates && state.Tick % 20 == 0)
                    {
                        Debug.Log($"[GameWorld] Tick {state.Tick}: " +
                                  $"{changes.Count} territory changes, " +
                                  $"{_territoryData.ClaimedCells}/{_territoryData.TotalCells} claimed");
                    }
                }
            }
            
            if (_playerVisualsManager != null)
            {
                _playerVisualsManager.UpdateFromState(state, _localPlayerId);
                
                if (logPlayerUpdates && state.Tick % 20 == 0)
                {
                    Debug.Log($"[GameWorld] Tick {state.Tick}: " +
                              $"{state.Players.Count} players, " +
                              $"{_playerVisualsManager.ActiveCount} visuals active");
                }
            }
        }

        public void OnPlayerEliminated(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} eliminated" + 
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
            
            if (isLocal && _cameraController != null)
            {
                _cameraController.Shake(1f, 0.5f);
            }
            
        }

        public void OnPlayerRespawned(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} respawned" +
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
            
            if (isLocal && _cameraController != null && LocalPlayerVisual != null)
            {
                _cameraController.SetTarget(LocalPlayerVisual.transform);
            }
        }



        private void InitializeFromState(PaperioState initialState)
        {
            int width = (int)initialState.GridWidth;
            int height = (int)initialState.GridHeight;
            
            _territoryData = new TerritoryData(width, height);
            
            if (initialState.Territory != null)
            {
                var changes = _territoryData.ApplyFullState(initialState.Territory);
                Debug.Log($"[GameWorld] Initial territory: {_territoryData.ClaimedCells} cells claimed");
            }
            
            if (territoryRenderer != null)
            {
                territoryRenderer.Initialize(width, height, config.CellSize, this);
                territoryRenderer.ApplyFullState(_territoryData);
                Debug.Log($"[GameWorld] TerritoryRenderer initialized: {width}x{height}");
            }
            
            if (logTerritoryUpdates)
            {
                int cx = width / 2, cy = height / 2;
                Debug.Log($"[GameWorld] Territory around center ({cx},{cy}):\n" + 
                          _territoryData.DebugRegion(cx - 5, cy - 5, 11, 11));
            }
        }
        public Vector3 GridToWorld(int gridX, int gridY, float height = 0f)
        {
            return new Vector3(
                gridX * config.CellSize + config.CellSize * 0.5f,
                height,
                gridY * config.CellSize + config.CellSize * 0.5f
            );
        }
        
        public Bounds GetGridBounds()
        {
            var center = GridHelper.GetGridCenter(GridWidth, GridHeight, config.CellSize);
            var size = new Vector3(
                GridWidth * config.CellSize,
                10f,
                GridHeight * config.CellSize
            );
            return new Bounds(center, size);
        }

        public (Vector3 min, Vector3 max) GetGridCorners()
        {
            return (
                new Vector3(0, 0, 0),
                new Vector3(GridWidth * config.CellSize, 0, GridHeight * config.CellSize)
            );
        }

        public Color GetTerritoryColor(uint ownerId)
        {
            if (ownerId == 0)
            {
                return config.NeutralColor;
            }
            
            Color playerColor = _playerVisualsManager.GetPlayerColor(ownerId);
            return new Color(
                playerColor.r * 0.7f,
                playerColor.g * 0.7f,
                playerColor.b * 0.7f,
                1f
            );
        }

        public Color GetTrailColor(uint playerId)
        {
            Color playerColor = _playerVisualsManager.GetPlayerColor(playerId);
            return new Color(
                Mathf.Min(1f, playerColor.r * 1.2f),
                Mathf.Min(1f, playerColor.g * 1.2f),
                Mathf.Min(1f, playerColor.b * 1.2f),
                0.9f
            );
        }
        public string GetDebugInfo()
        {
            if (!_isGameActive)
            {
                return "Game not active";
            }

            float myPercentage = _territoryData?.GetOwnershipPercentage(_localPlayerId) ?? 0f;
            int rendererUpdates = territoryRenderer?.TotalCellsUpdated ?? 0;
            int activeVisuals = _playerVisualsManager?.ActiveCount ?? 0;
            bool cameraFollowing = _cameraController?.IsFollowing ?? false;
            
            return $"Local Player: {_localPlayerId}\n" +
                   $"Grid: {GridWidth}x{GridHeight}\n" +
                   $"Tick: {_lastTick} (progress: {TickProgress:F2})\n" +
                   $"Tick Rate: {_tickRateMs}ms\n" +
                   $"Claimed Cells: {_territoryData?.ClaimedCells ?? 0}\n" +
                   $"My Territory: {myPercentage:F2}%\n" +
                   $"Active Players: {activeVisuals}\n" +
                   $"Camera Following: {cameraFollowing}\n" +
                   $"Renderer Updates: {rendererUpdates}";
        }

        private void OnDrawGizmosSelected()
        {
            if (!_isGameActive || _territoryData == null) return;
            
            var bounds = GetGridBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(GridHelper.GetGridCenter(_gridWidth, _gridHeight, config.CellSize), 1f);
        }
    }
}