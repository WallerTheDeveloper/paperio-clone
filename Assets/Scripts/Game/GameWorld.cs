using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Effects;
using Game.Paperio;
using Game.Rendering;
using Helpers;
using UnityEngine;

namespace Game
{
    public class GameWorld : MonoBehaviour, IService, IGameWorldDataProvider
    {
        [SerializeField] private GameWorldConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool logTerritoryUpdates = false;
        [SerializeField] private bool logPlayerUpdates = false;
        
        private TerritoryData _territoryData;
        private readonly Dictionary<uint, Color> _playerColors = new();
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
        public Dictionary<uint, Color> PlayerColors => _playerColors;
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
        private EffectsManager _effectsManager;
        private TerritoryRenderer _territoryRenderer;
        public void Initialize(ServiceContainer services)
        {
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _effectsManager = services.Get<EffectsManager>();
            _territoryRenderer = services.Get<TerritoryRenderer>();
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
            
            _territoryData.Clear();
            _isGameActive = false;
            
            _cameraController.Dispose();
        }

        public void OnJoinedGame(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            
            if (response.InitialState != null)
            {
                _gridWidth = response.InitialState.GridWidth;
                _gridHeight = response.InitialState.GridHeight;
                
                _territoryRenderer.CreateTerritory();
                
                _playerVisualsManager.UpdateFromState(response.InitialState, _localPlayerId);
                _playerVisualsManager.SpawnPlayers();
                
                _cameraController = FindFirstObjectByType<CameraController>();
                _cameraController.Initialize(this as IGameWorldDataProvider);
                
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

            foreach (var player in state.Players)
            {
                var playerColor = _playerVisualsManager.GetPlayerColor(player.PlayerId);
                if (!_playerColors.ContainsKey(player.PlayerId))
                {
                    _playerColors.Add(player.PlayerId, playerColor);
                }
            }

            if (_territoryData != null && state.Territory != null)
            {
                var changes = _territoryData.ApplyServerState(state.Territory);
                
                if (changes.Count > 0)
                {
                    _territoryRenderer.UpdateTerritory(changes);
                    
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
            
            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
            var playerCurrentPosition =
                GridHelper.GridToWorld(playerData.GridPosition.x, playerData.GridPosition.y, Config.CellSize);

            var effectData = new EffectData(position: playerCurrentPosition, color: playerData.Color);
            
            _effectsManager.PlayEffect(Effect.Death, effectData);
            
            if (isLocal)
            {
                _effectsManager.PlayEffect(Effect.CameraShake, effectData);
            }
            
        }

        public void OnPlayerRespawned(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} respawned" +
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
            var playerCurrentPosition = GridHelper.GridToWorld(playerData.GridPosition.x, playerData.GridPosition.y, Config.CellSize);
            
            var effectData = new EffectData(position: playerCurrentPosition, color: playerData.Color);
            _effectsManager.PlayEffect(Effect.Respawn, effectData);
            if (isLocal && _cameraController != null && LocalPlayerVisual != null)
            {
                _cameraController.SetTarget(LocalPlayerVisual.transform);
            }
        }
        

        private void InitializeFromState(PaperioState initialState)
        {
            int width = (int)initialState.GridWidth;
            int height = (int)initialState.GridHeight;
            
            _territoryData = new TerritoryData(width, height, _territoryRenderer.MeshFilter);
            
            if (initialState.Territory != null)
            {
                var changes = _territoryData.ApplyFullState(initialState.Territory);
                Debug.Log($"[GameWorld] Initial territory: {_territoryData.ClaimedCells} cells claimed");
            }
            
            _territoryRenderer.ApplyFullState(_territoryData);
            
            if (logTerritoryUpdates)
            {
                int cx = width / 2, cy = height / 2;
                Debug.Log($"[GameWorld] Territory around center ({cx},{cy}):\n" + 
                          _territoryData.DebugRegion(cx - 5, cy - 5, 11, 11));
            }
        }
        
        private Bounds GetGridBounds()
        {
            var center = GridHelper.GetGridCenter(GridWidth, GridHeight, config.CellSize);
            var size = new Vector3(
                GridWidth * config.CellSize,
                10f,
                GridHeight * config.CellSize
            );
            return new Bounds(center, size);
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