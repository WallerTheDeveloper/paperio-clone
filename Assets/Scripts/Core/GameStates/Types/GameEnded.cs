using System;
using Core.DISystem;
using Game;
using MonoSingleton;
using Network;

namespace Core.GameStates.Types
{
    public class GameEnded : GameState
    {
        private MessageSender _messageSender;
        private Game.Game _game;
        public override Action TriggerStateSwitch { get; set; }
        public override void Initialize(IDependencyContainer container)
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
            
            _messageSender.OnGameEnded += _game.HandleGameEnd;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        {
        }

        public override void Stop()
        {
            _messageSender.OnGameEnded -= _game.HandleGameEnd;
        }
    }
}