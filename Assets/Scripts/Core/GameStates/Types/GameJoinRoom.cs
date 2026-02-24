using System;
using Core.Services;
using Game.Server;
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
             
             _messageSender.OnRoomJoined += OnRoomJoined;
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            _messageSender.OnRoomJoined -= OnRoomJoined;
        }
        
        private void OnRoomJoined(RoomJoined obj)
        {
            TriggerStateSwitch?.Invoke();
        }
    }
}