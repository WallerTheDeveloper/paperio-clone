using System;
using Core.DISystem;
using UnityEngine;

namespace Core.GameStates
{
    public abstract class GameState : MonoBehaviour
    { 
        public abstract Action TriggerStateSwitch { get; set; }
        public abstract void Initialize(IDependencyContainer container);
        protected virtual IDependencyProvider DependencyProvider { get; set; }
        public abstract void TickState();
        public abstract void Stop();
    }
}