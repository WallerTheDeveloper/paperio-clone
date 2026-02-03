using Core.GameStates;
using Core.Services;
using Game;
using Game.Data;
using Input;
using Network;
using UnityEngine;

namespace Core
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private MessageSender messageSender;
        [SerializeField] private GameStatesManager gameStatesManager;
        [SerializeField] private ServerStateHandler serverStateHandler;
        [SerializeField] private GameWorld gameWorld;
        private PlayersContainer _playersContainer;
        private InputService _inputService;
        
        private ServiceContainer _services;
        private void Awake()
        {
            _services = new ServiceContainer();
            _playersContainer = new PlayersContainer();
            _inputService = FindFirstObjectByType<InputService>();
            
            _services.Register(messageSender);
            _services.Register(_playersContainer);
            _services.Register(serverStateHandler);
            _services.Register(gameWorld);
            _services.Register(gameStatesManager);
            _services.Register(_inputService);
            
            _services.InitializeAll();
        }

        private void Update()
        {
            _services.TickAll();
        }

        private void OnDestroy()
        {
            _services.DisposeAll();
        }
    }
}