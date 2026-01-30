using System;
using Core.DISystem;
using Game;
using MonoSingleton;
using Network;

namespace Core.GameStates.Types
{
    public class GameDisconnected : GameState
    {
        private Game.Game _game;
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }
        public override void Initialize(IDependencyContainer container)
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
            _game = MonoSingletonRegistry.Get<Game.Game>();
            
            _messageSender.OnDisconnected += OnDisconnected;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        {
            _messageSender.OnDisconnected -= OnDisconnected;
        }
        
        private void OnDisconnected()
        {
            _game.Disconnect();
        }
    }
}