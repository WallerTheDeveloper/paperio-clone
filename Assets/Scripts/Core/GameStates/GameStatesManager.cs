using System.Collections.Generic;
using Core.DISystem;
using Core.GameStates.Types;
using MonoSingleton;
using UnityEngine;

namespace Core.GameStates
{
    public class GameStatesManager : MonoSingleton<GameStatesManager>, ISystem
    {
        [SerializeField] private GameInitialize gameInitialize;
        [SerializeField] private GameConnect gameConnect;
        [SerializeField] private GameRoomJoined gameRoomJoined;
        [SerializeField] private GameRoomUpdate gameRoomUpdate;
        [SerializeField] private GameStarting gameStarting;
        [SerializeField] private GamePlaying gamePlaying;
        [SerializeField] private GameEnded gameEnded;
        [SerializeField] private GameDisconnected gameDisconnected;
        
        private HashSet<GameState> _gameStates;
        private Queue<GameState> _pendingStates;
        
        private GameState _currentState;
        
        private IDependencyContainer _dependencyContainer;

        public void Initialize()
        {
            _dependencyContainer = new DependencyContainer();
            
            // Must be in order of switching
            _gameStates = new HashSet<GameState>
            {
                gameInitialize,
                gameConnect,
                gameRoomJoined,
                gameRoomUpdate,
                gameStarting,
                gamePlaying,
                gameEnded,
                gameDisconnected,
            };
            
            _pendingStates = new Queue<GameState>();

            foreach (var gameState in _gameStates)
            {
                _pendingStates.Enqueue(gameState);
            }

            var nextState = _pendingStates.Peek();
            _currentState = nextState;
            Debug.Log("Current state: " + _currentState.GetType()); 

            _currentState.TriggerStateSwitch += SwitchState;
            _currentState.Initialize(_dependencyContainer);
        }

        public void Run()
        { }
        
        private void SwitchState()
        {
            _currentState.Stop();
            _currentState.TriggerStateSwitch -= SwitchState;
            
            _pendingStates.Dequeue();
            var nextState = _pendingStates.Peek();
            Debug.Log("Switch state to " + nextState.GetType()); 
            
            _currentState = nextState;

            Debug.Log("Current state: " + _currentState.GetType()); 

            _currentState.TriggerStateSwitch += SwitchState;

            _currentState.Initialize(_dependencyContainer);
        }
    
        private void Update()
        {
            if (_currentState == null)
            {
                Debug.Log("Current state is null. Did you forget to trigger state switch?");
                return;
            }
            _currentState.TickState();
        }
    }
}