using System;
using Core.Services;

namespace Core.GameStates.Types
{
    public class GameInitialize : GameState
    {
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            TriggerStateSwitch?.Invoke();
        }

        public override void Tick()
        { }

        public override void Stop()
        { }
    }
}