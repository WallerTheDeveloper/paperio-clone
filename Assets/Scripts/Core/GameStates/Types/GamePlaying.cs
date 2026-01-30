using System;
using Core.DISystem;
using Input;
using MonoSingleton;

namespace Core.GameStates.Types
{
    public class GamePlaying : GameState
    {
        private Game.Game _game;
        public override Action TriggerStateSwitch { get; set; }
        public override void Initialize(IDependencyContainer container)
        {
            _game = MonoSingletonRegistry.Get<Game.Game>();
            
            _game.EnableGameInput(true);
        }

        public override void TickState()
        {
            TriggerStateSwitch?.Invoke();
        }

        public override void Stop()
        {
            _game.EnableGameInput(false);
        }
    }
}