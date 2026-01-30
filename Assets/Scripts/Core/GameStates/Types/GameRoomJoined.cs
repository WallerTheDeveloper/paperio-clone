using System;
using Core.DISystem;
using Game;
using Game.Server;
using MonoSingleton;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameRoomJoined : GameState
    {
        [SerializeField] private RoomManager roomManager;
        private MessageSender _messageSender;
        
        public override Action TriggerStateSwitch { get; set; }
        public override void Initialize(IDependencyContainer container)
        {
            GetMonoSingletonReferences();

            _messageSender.OnRoomJoined += roomManager.RegisterPlayerInRoom;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        {
            _messageSender.OnRoomJoined -= roomManager.RegisterPlayerInRoom;
        }
        
        private void GetMonoSingletonReferences()
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
        }
    }
}