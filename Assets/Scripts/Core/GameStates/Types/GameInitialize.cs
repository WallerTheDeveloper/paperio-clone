using System;
using Core.Services;
using Game;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameInitialize : GameState
    {
        [SerializeField] private RoomManager roomManager;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        { }
    }
}