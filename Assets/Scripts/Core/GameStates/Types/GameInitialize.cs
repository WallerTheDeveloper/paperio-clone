using System;
using System.Collections.Generic;
using Core.DISystem;
using Game;
using Game.Data;
using Input;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameInitialize : GameState
    {
        [SerializeField] private Game.Game game;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private RoomManager roomManager;
        private PlayersContainer _playersContainer;
        public override Action TriggerStateSwitch { get; set; }
        
        private IDependencyContainer _container;
        public override void Initialize(IDependencyContainer container)
        {
            _playersContainer = new PlayersContainer();
            
            _container = container;
            
            _container.Register(roomManager, new List<IDependency>
            {
                _playersContainer
            });
            _container.Register(game, new List<IDependency>
            {
                inputHandler,
                _playersContainer
            });
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        { }
    }
}