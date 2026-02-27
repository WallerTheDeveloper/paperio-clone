using System;
using Core.Services;
using Game.Data;
using Game.Server;
using Game.Subsystems;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameRunning : GameState
    {
        public override Action TriggerStateSwitch { get; set; }

        private MessageSender _messageSender;
        private ServerStateHandler _serverStateHandler;
        private GameSessionCoordinator _coordinator;
        private IGameSessionDataProvider _gameSessionData;

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            _serverStateHandler = container.Get<ServerStateHandler>();
            _coordinator = container.Get<GameSessionCoordinator>();
            _gameSessionData = container.Get<GameSessionData>();

            if (_gameSessionData.IsGameActive)
            {
                Debug.Log("[GameRunning] Reconnection detected — resetting game state");
                _coordinator.Dispose();
            }

            _serverStateHandler.ResetForReconnect();

            _serverStateHandler.OnJoinedGame += _coordinator.OnJoinedGame;
            _serverStateHandler.OnStateUpdated += _coordinator.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated += _coordinator.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned += _coordinator.OnPlayerRespawned;
            _messageSender.OnPlayerDisconnected += HandlePlayerDisconnected;

            if (!_serverStateHandler.HasJoinedGame)
            {
                _messageSender.SendReady();
            }
            else
            {
                Debug.Log("[GameRunning] Already joined game (reconnect path), skipping SendReady");
            }
        }

        public override void Tick()
        {
        }

        public override void Stop()
        {
            _serverStateHandler.OnJoinedGame -= _coordinator.OnJoinedGame;
            _serverStateHandler.OnStateUpdated -= _coordinator.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated -= _coordinator.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned -= _coordinator.OnPlayerRespawned;
            _messageSender.OnPlayerDisconnected -= HandlePlayerDisconnected;
        }
        
        private void HandlePlayerDisconnected(PlayerDisconnected obj)
        {
            _coordinator.OnPlayerDisconnected(obj.PlayerId);
        }
    }
}