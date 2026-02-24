using System;
using Core.Services;
using Network;

namespace Core.GameStates.Types
{
    public class GameConnect : GameState
    {
        public override Action TriggerStateSwitch { get; set; }
        
        private MessageSender _messageSender;
        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            _messageSender.OnConnected += OnConnected;
            
            _messageSender.Connect();
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _messageSender.OnConnected -= OnConnected;
        }

        private void OnConnected()
        {
            TriggerStateSwitch?.Invoke();
        }
    }
}