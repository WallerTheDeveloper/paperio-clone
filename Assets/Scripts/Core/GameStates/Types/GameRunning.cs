using System;
using Core.Services;
using Game;
using Game.Effects;
using Game.Server;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameRunning : GameState
    {
        private MessageSender _messageSender;
        private ServerStateHandler _serverStateHandler;
        private GameWorld _gameWorld;
        private EffectsManager _effectsManager;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            _serverStateHandler = container.Get<ServerStateHandler>();
            _gameWorld = container.Get<GameWorld>();

            if (_gameWorld.IsGameActive)
            {
                Debug.Log("[GameRunning] Reconnection detected — resetting game state");
                _gameWorld.Dispose();
            }

            _serverStateHandler.ResetForReconnect();

            _serverStateHandler.OnJoinedGame += _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated += _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated += _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned += _gameWorld.OnPlayerRespawned;
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

        private void HandlePlayerDisconnected(PlayerDisconnected obj)
        {
            _gameWorld.OnPlayerDisconnectedVisually(obj.PlayerId);
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _serverStateHandler.OnJoinedGame -= _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated -= _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated -= _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned -= _gameWorld.OnPlayerRespawned;
            _messageSender.OnPlayerDisconnected -= HandlePlayerDisconnected;
        }
    }
}