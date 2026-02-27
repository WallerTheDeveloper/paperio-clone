using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems;
using Game.Subsystems.Input;
using Game.Subsystems.Rendering;
using Game.Subsystems.UI;
using UnityEngine;
using Utils;

namespace Game
{
    public interface IGameWorldDataProvider
    {
        public GameWorldConfig Config { get; }
        // public Camera LocalPlayerCamera { get; }
    }
    
    public class GameWorld : MonoBehaviour, ITickableService, IGameWorldDataProvider
    {
        [SerializeField] private GameWorldConfig config;
        
        private float _lastTickTime;

        public GameWorldConfig Config => config;
        public Dictionary<uint, Color> PlayerColors => new(_colorRegistry?.Colors ?? new Dictionary<uint, Color>());
        public PlayerVisual LocalPlayerVisual => _playerVisualsManager?.LocalPlayerVisual;
        public event Action<PaperioState> OnStateRefreshed;
        public event Action<uint> OnLocalPlayerSpawned;
        public event Action<List<TerritoryChange>> OnTerritoryChanged;
        
        private Camera LocalPlayerCamera
        {
            get
            {
                var camObj = GameObject.FindWithTag(Constants.Tags.LocalPlayerCamera);
                return camObj != null ? camObj.GetComponent<Camera>() : null;
            }
        }
        
        private float TickProgress
        {
            get
            {
                if (_sessionData.TickRateMs == 0)
                {
                    return 1f;
                }
                float moveDuration = _sessionData.MoveDuration;
                float elapsed = Time.time - _lastTickTime;
                return Mathf.Clamp01(elapsed / moveDuration);
            }
        }

        private GameSessionData _sessionData;
        private GameStateReceiver _stateReceiver;
        private PredictionSystem _predictionSystem;
        private TerritorySystem _territorySystem;
        private EffectsCoordinator _effectsCoordinator;
        private ColorsRegistry _colorRegistry;
        private PlayerVisualsManager _playerVisualsManager;
        private TrailVisualsManager _trailVisualsManager;
        private CameraController _cameraController;
        private InputService _inputService;
        private IGameUICoordinator _gameUICoordinator;
        private ITerritoryDataProvider _territoryData;
        public void Initialize(ServiceContainer services)
        {
            _colorRegistry = services.Get<ColorsRegistry>();
            _stateReceiver = services.Get<GameStateReceiver>();
            _predictionSystem = services.Get<PredictionSystem>();
            _territorySystem = services.Get<TerritorySystem>();
            _effectsCoordinator = services.Get<EffectsCoordinator>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _trailVisualsManager = services.Get<TrailVisualsManager>();
            _inputService = services.Get<InputService>();
            _gameUICoordinator = services.Get<GameUICoordinator>();
            _sessionData = services.Get<GameSessionData>();
            _territoryData = services.Get<TerritoryData>();
            
            _stateReceiver.OnStateProcessed += OnStateProcessed;
            _stateReceiver.OnTerritoryChanged += OnTerritoryChangedReceived;
        }

        public void Tick()
        {
            if (!_sessionData.IsGameActive)
            {
                return;
            }

            _predictionSystem.Tick();
            _playerVisualsManager.UpdateInterpolation(TickProgress);
        }

        public void TickLate()
        {
            if (_cameraController != null)
            {
                _cameraController.TickLate();
            }
        }

        public void Dispose()
        {
            _stateReceiver.OnStateProcessed -= OnStateProcessed;
            _stateReceiver.OnTerritoryChanged -= OnTerritoryChangedReceived;
            
            _inputService.DisableInput();
            _territorySystem.Dispose();
            _playerVisualsManager.ClearAll();
            _sessionData.SetEndGameData();
            _cameraController?.Dispose();
        }

        private void OnStateProcessed(PaperioState state)
        {
            OnStateRefreshed?.Invoke(state);
        }
        
        private void OnTerritoryChangedReceived(List<TerritoryChange> changes)
        {
            OnTerritoryChanged?.Invoke(changes);
        }
        
        public void OnJoinedGame(PaperioJoinResponse response)
        {
            _sessionData.SetData(
                response.YourPlayerId,
                response.TickRateMs,
                response.MoveIntervalTicks
            );

            if (response.InitialState != null)
            {
                foreach (var player in response.InitialState.Players)
                {
                    _colorRegistry.Register(player.PlayerId);
                }

                _playerVisualsManager.UpdateFromState(response.InitialState, _sessionData.LocalPlayerId);
                _playerVisualsManager.SpawnPlayers();
                _sessionData.SetLocalPlayerCamera(LocalPlayerCamera);
                
                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    _playerVisualsManager.LocalPlayerVisual.SetMoveDuration(_sessionData.MoveDuration);
                }

                _cameraController = FindFirstObjectByType<CameraController>();
                _cameraController.Initialize(_territoryData.Width, _territoryData.Height, config.CellSize);

                if (_playerVisualsManager.LocalPlayerVisual != null)
                {
                    _cameraController.SetLocalTarget(_playerVisualsManager.LocalPlayerVisual.transform);
                }

                _territorySystem.InitializeFromState(response.InitialState);

                _effectsCoordinator.PreparePools();

                foreach (var player in response.InitialState.Players)
                {
                    if (player.PlayerId == _sessionData.LocalPlayerId && player.Position != null)
                    {
                        _predictionSystem.InitializeForGame(
                            new Vector2Int(player.Position.X, player.Position.Y),
                            player.Direction
                        );
                        break;
                    }
                }

                _predictionSystem.SyncToServerTick(response.InitialState.Tick);
            }

            _gameUICoordinator.CreateAndInitializeGameUI(_territoryData);
            
            _inputService.EnableInput();
            _lastTickTime = Time.time;
            _sessionData.SetStartGameData();

            Debug.Log($"[GameWorld] Game started! " +
                      $"LocalPlayer={_sessionData.LocalPlayerId}, " +
                      $"Grid={_territoryData.Width}x{_territoryData.Height}, " +
                      $"TickRate={_sessionData.TickRateMs}ms");

            OnLocalPlayerSpawned?.Invoke(_sessionData.LocalPlayerId);
        }

        public void OnServerStateUpdated(PaperioState state)
        {
            _lastTickTime = Time.time;
            _stateReceiver.ProcessState(state);
        }

        public void OnPlayerEliminated(uint playerId)
        {
            _inputService.DisableInput();
            _trailVisualsManager.RemoveTrail(playerId);
            _effectsCoordinator.OnPlayerEliminated(playerId);
        }

        public void OnPlayerRespawned(uint playerId)
        {
            bool isLocal = playerId == _sessionData.LocalPlayerId;

            _effectsCoordinator.OnPlayerRespawned(playerId);

            if (isLocal && _cameraController != null && LocalPlayerVisual != null)
            {
                _cameraController.SetLocalTarget(LocalPlayerVisual.transform);

                var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
                if (playerData != null)
                {
                    _predictionSystem.ReinitializeAfterRespawn(playerData.GridPosition, Direction.None);
                    LocalPlayerVisual.SetMoveDuration(_sessionData.MoveDuration);
                }
            }

            _inputService.EnableInput();
        }

        public void OnPlayerDisconnectedVisually(uint playerId)
        {
            Debug.Log($"[GameWorld] Player {playerId} disconnected — cleaning up visuals");

            _inputService.DisableInput();
            _trailVisualsManager.RemoveTrail(playerId);
            _playerVisualsManager.DespawnPlayer(playerId);

            var changes = _territorySystem.ClearOwnership(playerId);
            if (changes.Count > 0)
            {
                OnTerritoryChanged?.Invoke(changes);
            }
        }
    }
}