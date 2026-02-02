using System;
using Core.Services;
using Network;

namespace Core.GameStates.Types
{
    public class GameRunning : GameState
    {
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            _messageSender.SendReady();
        }

        public override void TickState()
        { }

        public override void Stop()
        { }
    }
}