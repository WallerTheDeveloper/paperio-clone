using System;
using Core.Services;
using Network;

namespace Core.GameStates.Types
{
    public class GameConnect : GameState
    {
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            
            ConnectToServer(_messageSender);

            _messageSender.OnConnected += OnConnected;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        {}

        public override void Stop()
        {
            _messageSender.OnConnected -= OnConnected;
        }

        private void ConnectToServer(MessageSender messageSender)
        {
            if (messageSender.Connect())
            {
                if (messageSender.IsConnected)
                {
                    messageSender.SendJoinRoom();
                }
            }
        }

        private void OnConnected()
        { }
    }
}