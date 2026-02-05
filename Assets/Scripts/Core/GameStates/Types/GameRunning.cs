using System;
using System.Collections;
using Core.Services;
using Game;
using Game.Effects;
using Network;

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
            
            if (!_messageSender.IsJoined)
            {
                StartCoroutine(WaitForJoinRoom());
            }
            IEnumerator WaitForJoinRoom()
            {
                while (!_messageSender.IsJoined)
                {
                    yield return null;
                }
                
                _messageSender.SendReady();
            }
            
            _serverStateHandler.OnJoinedGame += _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated += _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated += _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned += _gameWorld.OnPlayerRespawned;
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _serverStateHandler.OnJoinedGame -= _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated -= _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated -= _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned -= _gameWorld.OnPlayerRespawned;
        }
    }
}