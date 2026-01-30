using System;
using Core.DISystem;
using Game;
using MonoSingleton;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameRoomUpdate : GameState
    {
        [SerializeField] private RoomManager roomManager;
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(IDependencyContainer container)
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
            _messageSender.OnRoomUpdate += roomManager.UpdateRoom;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        {
            _messageSender.OnRoomUpdate -= roomManager.UpdateRoom;
        }
    }
}