using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Effects;
using Game.Paperio;
using Game.Rendering;
using Game.UI;
using Helpers;
using Network;
using UnityEngine;

namespace Game
{
    public class GameWorld : MonoBehaviour, IService, IGameWorldDataProvider
    {
        [SerializeField] private GameWorldConfig config;
        
        [Header("Debug")]
        [SerializeField] private bool logPlayerUpdates = false;
        
        private readonly Dictionary<uint, Color> _playerColors = new();
        
        private uint _moveIntervalTicks;
        private uint _estimatedServerTick;
        private float _tickAccumulator;
        private uint _localPlayerId;
        private uint _gridWidth;
        private uint _gridHeight;
        private uint _tickRateMs;
        private bool _isGameActive;

        private float _lastTickTime;
        private uint _lastTick;
        private bool _inputSubscribed;
        
        public event Action OnGameStarted;
        public event Action<PaperioState> OnStateRefreshed;
        public event Action OnGameEnded;
        public event Action<uint> OnLocalPlayerSpawned;
        public event Action<List<TerritoryChange>> OnTerritoryChanged;
        public PlayerVisual LocalPlayerVisual => _playerVisualsManager?.LocalPlayerVisual;
        public GameWorldConfig Config => config;
        public TerritoryData Territory => _territoryData;
        public Dictionary<uint, Color> PlayerColors => _playerColors;
        public uint LocalPlayerId => _localPlayerId;
        public bool IsGameActive => _isGameActive;
        public uint GridWidth => _gridWidth;
        public uint GridHeight => _gridHeight;
        public uint TickRateMs => _tickRateMs;

        public Camera LocalPlayerCamera
        {
            get
            {
                // TODO: rewrite this to not rely on FindWithTag every time, maybe cache reference in CameraController?
                var camObj = GameObject.FindWithTag("LocalPlayerCamera");
                if (camObj != null)
                {
                    var cam = camObj.GetComponent<Camera>();
                    return cam;
                }

                return null;
            }
        }

        private float TickProgress
        {
            get
            {
                if (_tickRateMs == 0)
                {
                    return 1f;
                }

                float moveDuration = config.moveIntervalTicks * (_tickRateMs / 1000f);
                float elapsed = Time.time - _lastTickTime;
                return Mathf.Clamp01(elapsed / moveDuration);
            }
        }
        
        private TerritoryData _territoryData;
        private ClientPrediction _prediction;
        
        private CameraController _cameraController;
        private PlayerVisualsManager _playerVisualsManager;
        private EffectsManager _effectsManager;
        private TerritoryRenderer _territoryRenderer;
        private TrailVisualsManager _trailVisualsManager;
        private TerritoryClaim _territoryClaim;
        private MinimapSystem _minimapSystem;
        private TerritoryClaimPopupManager _claimPopupManager;
        public void Initialize(ServiceContainer services)
        {
            _prediction = new ClientPrediction(_gridWidth, _gridHeight);
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _effectsManager = services.Get<EffectsManager>();
            _territoryRenderer = services.Get<TerritoryRenderer>();
            _trailVisualsManager = services.Get<TrailVisualsManager>();
            _territoryClaim = services.Get<TerritoryClaim>();
            _minimapSystem = services.Get<MinimapSystem>();
            _claimPopupManager = services.Get<TerritoryClaimPopupManager>();
        }

        public void Tick()
        {
            if (!_isGameActive)
            {
                Debug.Log("[GameWorld] Game is not active!");
                return;
            }
    
            if (!_inputSubscribed && _prediction != null)
            {
                var localPlayerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(_localPlayerId);
                if (localPlayerData?.InputService != null)
                {
                    localPlayerData.InputService.OnDirectionChanged += OnLocalDirectionChanged;
                    _inputSubscribed = true;
                    Debug.Log("[GameWorld] Subscribed to local player input for prediction");
                }
            }
            if (_prediction != null && _tickRateMs > 0)
            {
                _tickAccumulator += Time.deltaTime;
                float tickDuration = _tickRateMs / 1000f;
        
                while (_tickAccumulator >= tickDuration)
                {
                    _tickAccumulator -= tickDuration;
                    _estimatedServerTick++;
                    _prediction.AdvancePrediction(_estimatedServerTick);
                }
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
            _territoryClaim.FinishAllImmediately();
            _playerVisualsManager.ClearAll();
            
            if (_inputSubscribed)
            {
                var localPlayerData = _playerVisualsManager?.PlayersContainer?.TryGetPlayerById(_localPlayerId);
                if (localPlayerData?.InputService != null)
                {
                    localPlayerData.InputService.OnDirectionChanged -= OnLocalDirectionChanged;
                }
                _inputSubscribed = false;
            }
            _territoryData.Clear();
            _isGameActive = false;
            
            _cameraController.Dispose();
            _prediction = null;
        }

        public void OnJoinedGame(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            _moveIntervalTicks = response.MoveIntervalTicks;
            
            if (response.InitialState != null)
            {
                _gridWidth = response.InitialState.GridWidth;
                _gridHeight = response.InitialState.GridHeight;
                
                _playerVisualsManager.UpdateFromState(response.InitialState, _localPlayerId);
                _playerVisualsManager.SpawnPlayers();
                
                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    float moveDuration = _moveIntervalTicks * (_tickRateMs / 1000f);
                    _playerVisualsManager.LocalPlayerVisual.SetMoveDuration(moveDuration);
                }
                
                _cameraController = FindFirstObjectByType<CameraController>();
                _cameraController.Initialize(this as IGameWorldDataProvider);
                
                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    _cameraController.SetLocalTarget(_playerVisualsManager.LocalPlayerVisual.transform);
                }
                
                foreach (var player in response.InitialState.Players)
                {
                    var playerColor = _playerVisualsManager.GetPlayerColor(player.PlayerId);
                    if (!_playerColors.ContainsKey(player.PlayerId))
                    {
                        _playerColors.Add(player.PlayerId, playerColor);
                    }
                }
                
                InitializeFromState(response.InitialState);
                
                foreach (var player in response.InitialState.Players)
                {
                    if (player.PlayerId == _localPlayerId && player.Position != null)
                    {
                        _prediction.Initialize(
                            new Vector2Int(player.Position.X, player.Position.Y),
                            player.Direction
                        );
                        _prediction.SetMoveInterval(_moveIntervalTicks);
                        break;
                    }
                }
    
                _estimatedServerTick = response.InitialState.Tick;
                _tickAccumulator = 0f;
            }
            
            _isGameActive = true;
            _lastTickTime = Time.time;
            
            
            Debug.Log($"[GameWorld] Game started! " +
                      $"LocalPlayer={_localPlayerId}, " +
                      $"Grid={GridWidth}x{GridHeight}, " +
                      $"TickRate={_tickRateMs}ms");
            
            OnGameStarted?.Invoke();
            OnLocalPlayerSpawned?.Invoke(_localPlayerId);
            
            _minimapSystem.CreateUI();
        }

        public void OnLocalDirectionChanged(Direction direction)
        {
            Debug.Log($"[GameWorld] PREDICTION INPUT: {direction}, estimated tick: {_estimatedServerTick}");

            _prediction?.RecordInput(direction, _estimatedServerTick + 1);
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
            
            if (_territoryData != null)
            {
                List<TerritoryChange> changes;

                bool isDeltaChange = state.StateType == Game.Paperio.StateType.StateDelta;

                if (isDeltaChange && state.TerritoryChanges.Count > 0)
                {
                    changes = _territoryData.ApplyDeltaChanges(state.TerritoryChanges);
                }
                else if (!isDeltaChange && state.Territory != null && state.Territory.Count > 0)
                {
                    changes = _territoryData.ApplyFullState(state.Territory);
                }
                else
                {
                    changes = new List<TerritoryChange>();
                }

                if (changes.Count > 0)
                {
                    _territoryRenderer.FlushToMesh(changes.Count);
                    _territoryClaim.SyncNonAnimatedColors();
                    
                    var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(changes[0].NewOwner);
                    if (playerData != null)
                    {
                        _territoryClaim.AddWave(changes, playerData.PlayerId, playerData.Color);

                        if (_claimPopupManager != null)
                        {
                            int localClaimCount = 0;
                            Color localColor = Color.white;
                            foreach (var change in changes)
                            {
                                if (change.NewOwner == _localPlayerId)
                                {
                                    localClaimCount++;
                                    if (localColor == Color.white && _playerColors.TryGetValue(_localPlayerId, out var c))
                                    {
                                        localColor = c;
                                    }
                                }
                            }

                            if (localClaimCount > 0)
                            {
                                _claimPopupManager.ShowClaimPopup(localClaimCount, localColor);
                            }
                        }
                    }

                    OnTerritoryChanged?.Invoke(changes);
                }
            }
            
            if (_playerVisualsManager != null)
            {
                if (_prediction != null)
                {
                    foreach (var player in state.Players)
                    {
                        if (player.PlayerId == _localPlayerId && player.Position != null)
                        {
                            var serverPos = new Vector2Int(player.Position.X, player.Position.Y);
                            bool corrected = _prediction.Reconcile(state.Tick, serverPos, player.Direction);
            
                            _estimatedServerTick = state.Tick;
                            
                            _tickAccumulator = 0f;
            
                            if (corrected && logPlayerUpdates && (serverPos != _prediction.PredictedPosition))
                            {
                                Debug.Log($"[GameWorld] Prediction corrected at tick {state.Tick}: " +
                                          $"server=({serverPos.x},{serverPos.y}), " +
                                          $"predicted=({_prediction.PredictedPosition.x},{_prediction.PredictedPosition.y}), " +
                                          $"pending={_prediction.PendingInputCount}");
                            }
                            break;
                        }
                    }
                }
                _playerVisualsManager.UpdateFromState(state, _localPlayerId);
                if (_prediction != null && _playerVisualsManager.LocalPlayerVisual != null)
                {
                    var predictedWorldPos = GridHelper.GridToWorld(
                        _prediction.PredictedPosition.x,
                        _prediction.PredictedPosition.y,
                        config.CellSize,
                        _playerVisualsManager.LocalPlayerVisual.transform.position.y
                    );
    
                    _playerVisualsManager.LocalPlayerVisual.SetPredictedTarget(predictedWorldPos);
                }
            }
            if (_trailVisualsManager != null)
            {
                foreach (var player in state.Players)
                {
                    var gridPoints = new List<Vector2Int>();
                    foreach (var trailPos in player.Trail)
                    {
                        gridPoints.Add(new Vector2Int(trailPos.X, trailPos.Y));
                    }
            
                    Color playerColor = _playerVisualsManager.GetPlayerColor(player.PlayerId);
                    _trailVisualsManager.UpdatePlayerTrail(player.PlayerId, gridPoints, playerColor);
                }
            }
            
            OnStateRefreshed?.Invoke(state);
        }

        public void OnPlayerEliminated(uint playerId)
        {
            bool isLocal = playerId == _localPlayerId;
            Debug.Log($"[GameWorld] Player {playerId} eliminated" + 
                      (isLocal ? " (LOCAL PLAYER!)" : ""));
            
            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
            var playerCurrentPosition =
                GridHelper.GridToWorld(playerData.GridPosition.x, playerData.GridPosition.y, Config.CellSize);

            _trailVisualsManager.RemoveTrail(playerId);
            
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
                _cameraController.SetLocalTarget(LocalPlayerVisual.transform);
                if (_prediction != null)
                {
                    _prediction.Initialize(
                        playerData.GridPosition,
                        Direction.None
                    );
                    _prediction.SetMoveInterval(_moveIntervalTicks);
                    float moveDuration = _moveIntervalTicks * (_tickRateMs / 1000f);
                    LocalPlayerVisual.SetMoveDuration(moveDuration);
                    
                }
            }
        }
        

        private void InitializeFromState(PaperioState initialState)
        {
            int width = (int)initialState.GridWidth;
            int height = (int)initialState.GridHeight;

            _territoryData = new TerritoryData(width, height);

            _territoryData.InitializeVisuals(config.CellSize, config.NeutralColor, ResolveTerritoryColor);

            if (initialState.Territory != null)
            {
                var changes = _territoryData.ApplyFullState(initialState.Territory);
                Debug.Log($"[GameWorld] Initial territory: {_territoryData.ClaimedCells} cells claimed");
            }

            _territoryRenderer.CreateTerritory();

            _effectsManager.PreparePools();
            _territoryClaim.Prepare();
        }
        
        // TODO: should this method be here?
        private Color32 ResolveTerritoryColor(uint ownerId)
        {
            if (ownerId == 0)
            {
                return config.NeutralColor;
            }

            if (_playerColors.TryGetValue(ownerId, out Color playerColor))
            {
                return new Color(
                    playerColor.r * 0.7f,
                    playerColor.g * 0.7f,
                    playerColor.b * 0.7f,
                    1f
                );
            }

            Color resolved = _playerVisualsManager.GetPlayerColor(ownerId);
            return new Color(
                resolved.r * 0.7f,
                resolved.g * 0.7f,
                resolved.b * 0.7f,
                1f
            );
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