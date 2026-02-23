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
        [SerializeField] private GameMenu gameMenu;
        [SerializeField] private GameReconnect gameReconnect;
        [SerializeField] private GameJoinRoom gameJoinRoom;
        [SerializeField] private GameRunning gameRunning;
        
        private Queue<GameState> _pendingStates;
        
        private GameState _currentState;
        
        private ServiceContainer _serviceContainer;
        
        public void Initialize(ServiceContainer services)
        {
            _serviceContainer = services;
            
            _pendingStates = new Queue<GameState>();

            _pendingStates.Enqueue(gameInitialize);
            _pendingStates.Enqueue(gameConnect);
            
            if (GameReconnect.HasSavedToken())
            {
                _pendingStates.Enqueue(gameReconnect);
            }
            else
            {
                _pendingStates.Enqueue(gameMenu);
                _pendingStates.Enqueue(gameJoinRoom);
            }
            
            _pendingStates.Enqueue(gameRunning);

            _currentState = _pendingStates.Peek();
            Debug.Log("[GameStatesManager] Current state: " + _currentState.GetType().Name);

            _currentState.TriggerStateSwitch += SwitchState;
            
            if (gameReconnect != null)
            {
                gameReconnect.OnReconnectFailed = OnReconnectFailed;
            }
            
            _currentState.Initialize(_serviceContainer);
        }

        public void Tick()
        {
            if (_currentState == null)
            {
                Debug.Log("[GameStatesManager] Current state is null. Did you forget to trigger state switch?");
                return;
            }
            _currentState.Tick();
        }

        public void Dispose()
        {
        }
        
        private void SwitchState()
        {
            _currentState.Stop();
            _currentState.TriggerStateSwitch -= SwitchState;
            
            _pendingStates.Dequeue();
            
            if (_pendingStates.Count == 0)
            {
                Debug.LogWarning("[GameStatesManager] No more states in queue");
                _currentState = null;
                return;
            }
            
            var nextState = _pendingStates.Peek();
            Debug.Log($"[GameStatesManager] Switch state: {_currentState.GetType().Name} → {nextState.GetType().Name}"); 
            
            _currentState = nextState;

            _currentState.TriggerStateSwitch += SwitchState;
            _currentState.Initialize(_serviceContainer);
        }
        
        private void OnReconnectFailed()
        {
            Debug.Log("[GameStatesManager] Reconnect failed, rerouting to JoinRoom");
            
            _currentState.Stop();
            _currentState.TriggerStateSwitch -= SwitchState;
            
            _pendingStates.Clear();
            _pendingStates.Enqueue(gameMenu);
            _pendingStates.Enqueue(gameJoinRoom);
            _pendingStates.Enqueue(gameRunning);
            
            _currentState = _pendingStates.Peek();
            Debug.Log($"[GameStatesManager] Rerouted to: {_currentState.GetType().Name}");
            
            _currentState.TriggerStateSwitch += SwitchState;
            _currentState.Initialize(_serviceContainer);
        }
    }
}