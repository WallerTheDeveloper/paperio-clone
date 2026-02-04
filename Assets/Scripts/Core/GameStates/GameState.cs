using System;
using Core.Services;
using UnityEngine;

namespace Core.GameStates
{
    public abstract class GameState : MonoBehaviour
    {
        public abstract Action TriggerStateSwitch { get; set; }
        public abstract void Initialize(ServiceContainer container);
        public abstract void Tick();
        public abstract void Stop();
    }
}