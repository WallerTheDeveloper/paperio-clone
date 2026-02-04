using System;
using Core.Services;
using Game.Rendering;
using Network;

namespace Core.GameStates.Types
{
    public class GameJoinRoom : GameState
    {
        public override Action TriggerStateSwitch { get; set; }
        
        private MessageSender _messageSender;
        public override void Initialize(ServiceContainer container)
        {
             _messageSender = container.Get<MessageSender>();
             
             _messageSender.SendJoinRoom();
             
             TriggerStateSwitch?.Invoke();
        }

        public override void Tick()
        { }

        public override void Stop()
        { }
    }
}