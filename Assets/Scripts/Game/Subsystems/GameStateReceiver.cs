using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems.Rendering;
using UnityEngine;

namespace Game.Subsystems
{
    public interface IGameStateReceiver
    {
        public event Action<PaperioState> OnStateProcessed;

        public event Action<List<TerritoryChange>> OnTerritoryChanged;
    }
    public class GameStateReceiver : IService, IGameStateReceiver
    {
        public event Action<PaperioState> OnStateProcessed;

        public event Action<List<TerritoryChange>> OnTerritoryChanged;

        private IGameSessionData _sessionData;
        private ColorsRegistry _colorRegistry;
        private ITerritoryStateHandler _territoryStateHandler;
        private PredictionSystem _predictionSystem;
        private PlayerVisualsManager _playerVisualsManager;
        private TrailVisualsManager _trailVisualsManager;
        public void Initialize(ServiceContainer services)
        {
            _colorRegistry = services.Get<ColorsRegistry>();
            _territoryStateHandler = services.Get<TerritorySystem>();
            _predictionSystem = services.Get<PredictionSystem>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _trailVisualsManager = services.Get<TrailVisualsManager>();

            var gameWorld = services.Get<GameWorld>();
            _sessionData = gameWorld.GameSessionData;
        }

        public void Dispose() { }

        public void ProcessState(PaperioState state)
        {
            if (!_sessionData.IsGameActive)
            {
                return;
            }

            foreach (var player in state.Players)
            {
                _colorRegistry.Register(player.PlayerId);
            }

            var territoryChanges = _territoryStateHandler.ProcessStateUpdate(state);
            if (territoryChanges.Count > 0)
            {
                OnTerritoryChanged?.Invoke(territoryChanges);
            }

            ReconcileLocalPlayer(state);

            _playerVisualsManager.UpdateFromState(state, _sessionData.LocalPlayerId);

            UpdateTrails(state);

            OnStateProcessed?.Invoke(state);
        }

        private void ReconcileLocalPlayer(PaperioState state)
        {
            foreach (var player in state.Players)
            {
                if (player.PlayerId == _sessionData.LocalPlayerId && player.Position != null)
                {
                    var serverPos = new Vector2Int(player.Position.X, player.Position.Y);
                    _predictionSystem.Reconcile(state.Tick, serverPos, player.Direction);
                    _predictionSystem.SyncToServerTick(state.Tick);
                    break;
                }
            }
        }

        private void UpdateTrails(PaperioState state)
        {
            foreach (var player in state.Players)
            {
                var gridPoints = new List<Vector2Int>();
                foreach (var trailPos in player.Trail)
                {
                    gridPoints.Add(new Vector2Int(trailPos.X, trailPos.Y));
                }

                Color playerColor = _colorRegistry.GetColorOf(player.PlayerId);
                _trailVisualsManager.UpdatePlayerTrail(player.PlayerId, gridPoints, playerColor);
            }
        }
    }
}