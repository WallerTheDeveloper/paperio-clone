using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Rendering;
using Network;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class GameWorldConfig
    {
        [Tooltip("Size of each grid cell in world units")]
        public float CellSize = 1.0f;
        
        [Tooltip("Height offset for players above the territory")]
        public float PlayerHeight = 0.5f;
        
        [Tooltip("Height offset for trails above the territory")]
        public float TrailHeight = 0.1f;
        
        [Tooltip("Neutral territory color (unclaimed cells)")]
        public Color NeutralColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    }

    public class GameWorld : MonoBehaviour, IService
    {
        [SerializeField] private GameWorldConfig config = new GameWorldConfig();
        
        [SerializeField] private TerritoryRenderer territoryRenderer;
        [SerializeField] private PlayerVisualsManager playerVisualsManager;
        
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
        public int GridWidth => (int)_gridWidth;
        public int GridHeight => (int)_gridHeight;
        public uint TickRateMs => _tickRateMs;
        public PlayerVisualsManager PlayerVisuals => playerVisualsManager;
        
        public float TickProgress
        {
            get
            {
                if (_tickRateMs == 0) return 1f;
                float tickDuration = _tickRateMs / 1000f;
                float elapsed = Time.time - _lastTickTime;
                return Mathf.Clamp01(elapsed / tickDuration);
            }
        }
        
        public PlayerVisual LocalPlayerVisual => playerVisualsManager?.LocalPlayerVisual;

        private ServerStateHandler _serverStateHandler;
        private PlayersContainer _playersContainer;
        public void Initialize(ServiceContainer services)
        {
            _serverStateHandler = services.Get<ServerStateHandler>();
            _playersContainer = services.Get<PlayersContainer>();
            
            _serverStateHandler.OnJoinedGame += HandleJoinedGame;
            _serverStateHandler.OnStateUpdated += HandleStateUpdated;
            _serverStateHandler.OnPlayerEliminated += HandlePlayerEliminated;
            _serverStateHandler.OnPlayerRespawned += HandlePlayerRespawned;
            
            if (territoryRenderer == null)
            {
                Debug.LogWarning("[GameWorld] TerritoryRenderer not assigned - territory won't render");
            }
            
            if (playerVisualsManager == null)
            {
                Debug.LogWarning("[GameWorld] PlayerVisualsManager not assigned - players won't render");
            }
            
            Debug.Log("[GameWorld] Initialized - subscribed to server events");
        }

        public void Tick()
        {
            if (!_isGameActive) return;
            
            playerVisualsManager?.UpdateInterpolation(TickProgress);
        }

        public void Dispose()
        {
            if (_serverStateHandler != null)
            {
                _serverStateHandler.OnJoinedGame -= HandleJoinedGame;
                _serverStateHandler.OnStateUpdated -= HandleStateUpdated;
                _serverStateHandler.OnPlayerEliminated -= HandlePlayerEliminated;
                _serverStateHandler.OnPlayerRespawned -= HandlePlayerRespawned;
            }
            
            playerVisualsManager?.ClearAll();
            
            _territoryData?.Clear();
            _isGameActive = false;
            
            Debug.Log("[GameWorld] Disposed");
        }

        private void HandleJoinedGame(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            
            if (response.InitialState != null)
            {
                _gridWidth = response.InitialState.GridWidth;
                _gridHeight = response.InitialState.GridHeight;
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

        private void HandleStateUpdated(PaperioState state)
        {
            if (!_isGameActive) return;
            
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
            
            if (playerVisualsManager != null)
            {
                playerVisualsManager.UpdateFromState(state);
                
                if (logPlayerUpdates && state.Tick % 20 == 0)
                {
                    Debug.Log($"[GameWorld] Tick {state.Tick}: " +
                              $"{state.Players.Count} players, " +
                              $"{playerVisualsManager.ActiveCount} visuals active");
                }
            }
        }

        private void HandlePlayerEliminated(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} eliminated" + 
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
        }

        private void HandlePlayerRespawned(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} respawned" +
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
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
            
            if (playerVisualsManager != null)
            {
                playerVisualsManager.Initialize(this, _playersContainer, _localPlayerId);
                playerVisualsManager.UpdateFromState(initialState);
                Debug.Log($"[GameWorld] PlayerVisualsManager initialized with {initialState.Players.Count} players");
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

        public Vector3 GridToWorld(Vector2Int gridPos, float height = 0f)
        {
            return GridToWorld(gridPos.x, gridPos.y, height);
        }

        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / config.CellSize),
                Mathf.FloorToInt(worldPos.z / config.CellSize)
            );
        }

        public Vector3 GetGridCenter()
        {
            return new Vector3(
                (GridWidth * config.CellSize) / 2f,
                0f,
                (GridHeight * config.CellSize) / 2f
            );
        }

        public Bounds GetGridBounds()
        {
            var center = GetGridCenter();
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

        private static readonly Color32[] PlayerColors = new Color32[]
        {
            new Color32(255, 77, 77, 255),   // Red
            new Color32(77, 153, 255, 255),  // Blue  
            new Color32(77, 255, 77, 255),   // Green
            new Color32(255, 255, 77, 255),  // Yellow
            new Color32(255, 77, 255, 255),  // Magenta
            new Color32(77, 255, 255, 255),  // Cyan
            new Color32(255, 153, 77, 255),  // Orange
            new Color32(153, 77, 255, 255),  // Purple
        };

        public Color GetPlayerColor(uint playerId)
        {
            if (playerId == 0)
            {
                return config.NeutralColor;
            }
            
            var playerData = _playersContainer?.TryGetPlayerById(playerId);
            if (playerData != null && playerData.Color != default)
            {
                return playerData.Color;
            }
            
            int index = (int)((playerId - 1) % PlayerColors.Length);
            return PlayerColors[index];
        }

        public Color GetTerritoryColor(uint ownerId)
        {
            if (ownerId == 0)
            {
                return config.NeutralColor;
            }
            
            Color playerColor = GetPlayerColor(ownerId);
            return new Color(
                playerColor.r * 0.7f,
                playerColor.g * 0.7f,
                playerColor.b * 0.7f,
                1f
            );
        }

        public Color GetTrailColor(uint playerId)
        {
            Color playerColor = GetPlayerColor(playerId);
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
            int activeVisuals = playerVisualsManager?.ActiveCount ?? 0;
            
            return $"Local Player: {_localPlayerId}\n" +
                   $"Grid: {GridWidth}x{GridHeight}\n" +
                   $"Tick: {_lastTick} (progress: {TickProgress:F2})\n" +
                   $"Tick Rate: {_tickRateMs}ms\n" +
                   $"Claimed Cells: {_territoryData?.ClaimedCells ?? 0}\n" +
                   $"My Territory: {myPercentage:F2}%\n" +
                   $"Active Players: {activeVisuals}\n" +
                   $"Renderer Updates: {rendererUpdates}";
        }

        private void OnDrawGizmosSelected()
        {
            if (!_isGameActive || _territoryData == null) return;
            
            var bounds = GetGridBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(GetGridCenter(), 1f);
        }
    }
}