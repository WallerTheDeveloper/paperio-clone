using System;
using Core.DISystem;
using Game;
using MonoSingleton;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameConnect : GameState
    {
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }
        public override void Initialize(IDependencyContainer container)
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
            
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