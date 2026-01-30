using System.Collections.Generic;
using Core.GameStates.Types;
using Core.Services;
using UnityEngine;

namespace Core.GameStates
{
    public class GameStatesManager : MonoBehaviour, IService
    {
        [SerializeField] private GameInitialize gameInitialize;
        [SerializeField] private GameConnect gameConnect;
        
        private HashSet<GameState> _gameStates;
        private Queue<GameState> _pendingStates;
        
        private GameState _currentState;
        
        private ServiceContainer _serviceContainer;
        public void Initialize(ServiceContainer services)
        {
            _serviceContainer = services;
            // Must be in order of switching
            _gameStates = new HashSet<GameState>
            {
                gameInitialize,
                gameConnect,
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
            _currentState.Initialize(_serviceContainer);
        }

        public void Tick()
        {
            if (_currentState == null)
            {
                Debug.Log("Current state is null. Did you forget to trigger state switch?");
                return;
            }
            _currentState.TickState();
        }

        public void Dispose()
        {
        }
        
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

            _currentState.Initialize(_serviceContainer);
        }
    }
}