using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Rendering;
using Helpers;
using Input;
using Network;
using UnityEngine;

namespace Game
{
    public class PredictionSystem : IService
    {
        private ClientPrediction _prediction;
        private GameSessionData _sessionData;
        private PlayerVisualsManager _playerVisualsManager;
        private InputService _inputService;
        private GameWorldConfig _config;

        private uint _estimatedServerTick;
        private float _tickAccumulator;

        public void Initialize(ServiceContainer services)
        {
            _sessionData = services.Get<GameWorld>().SessionData;
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _inputService = services.Get<InputService>();
            _config = services.Get<GameWorld>().Config;

            _inputService.OnDirectionChanged += OnDirectionChanged;
        }

        public void Tick()
        {
            if (!_sessionData.IsGameActive || _prediction == null || _sessionData.TickRateMs == 0)
            {
                return;
            }

            _tickAccumulator += Time.deltaTime;
            float tickDuration = _sessionData.TickRateMs / 1000f;

            while (_tickAccumulator >= tickDuration)
            {
                _tickAccumulator -= tickDuration;
                _estimatedServerTick++;
                _prediction.AdvancePrediction(_estimatedServerTick);
            }

            if (_playerVisualsManager.LocalPlayerVisual != null)
            {
                var predictedWorldPos = GridHelper.GridToWorld(
                    _prediction.PredictedPosition.x,
                    _prediction.PredictedPosition.y,
                    _config.CellSize,
                    _playerVisualsManager.LocalPlayerVisual.transform.position.y
                );
                _playerVisualsManager.LocalPlayerVisual.SetPredictedTarget(predictedWorldPos);
            }
        }

        public void Dispose()
        {
            if (_inputService != null)
            {
                _inputService.OnDirectionChanged -= OnDirectionChanged;
            }
            _prediction = null;
        }

        public void InitializeForGame(Vector2Int startPosition, Direction startDirection)
        {
            _prediction = new ClientPrediction(_sessionData.GridWidth, _sessionData.GridHeight);
            _prediction.Initialize(startPosition, startDirection);
            _prediction.SetMoveInterval(_sessionData.MoveIntervalTicks);

            _estimatedServerTick = 0;
            _tickAccumulator = 0f;
        }

        public void SyncToServerTick(uint serverTick)
        {
            _estimatedServerTick = serverTick;
            _tickAccumulator = 0f;
        }

        public bool Reconcile(uint serverTick, Vector2Int serverPosition, Direction serverDirection)
        {
            if (_prediction == null) return false;

            bool corrected = _prediction.Reconcile(serverTick, serverPosition, serverDirection);

            _estimatedServerTick = serverTick;
            _tickAccumulator = 0f;

            return corrected;
        }

        public void ReinitializeAfterRespawn(Vector2Int position, Direction direction)
        {
            _prediction?.Initialize(position, direction);
            _prediction?.SetMoveInterval(_sessionData.MoveIntervalTicks);
        }
        
        private void OnDirectionChanged(Direction direction)
        {
            Debug.Log($"[PredictionSystem] Input: {direction}, estimated tick: {_estimatedServerTick}");
            _prediction?.RecordInput(direction, _estimatedServerTick + 1);
        }
    }
}