using System;
using Core.GameStates;
using Core.Services;
using Game;
using Game.Data;
using Game.Effects;
using Game.Subsystems;
using Game.Subsystems.Input;
using Game.Subsystems.Rendering;
using Game.UI;
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
        [SerializeField] private PlayerVisualsManager playerVisualsManager;
        [SerializeField] private EffectsManager effectsManager;
        [SerializeField] private TerritoryRenderer territoryRenderer;
        [SerializeField] private TrailVisualsManager trailVisualsManager;
        [SerializeField] private TerritoryClaim territoryClaim;
        [SerializeField] private MinimapSystem minimapSystem;
        [SerializeField] private TerritoryClaimPopupManager territoryClaimPopupManager;
        [SerializeField] private LeaderboardUI leaderboardUI;
        
        private PlayersContainer _playersContainer;
        private InputService _inputService;
        
        private PlayerColorRegistry _colorRegistry;
        private GameStateReceiver _stateReceiver;
        private PredictionSystem _predictionSystem;
        private TerritorySystem _territorySystem;
        private EffectsCoordinator _effectsCoordinator;
        
        private ServiceContainer _services;

        private void Awake()
        {
            _services = new ServiceContainer();
            
            _playersContainer = new PlayersContainer();
            _inputService = new InputService();
            _colorRegistry = new PlayerColorRegistry();
            _stateReceiver = new GameStateReceiver();
            _predictionSystem = new PredictionSystem();
            _territorySystem = new TerritorySystem();
            _effectsCoordinator = new EffectsCoordinator();
            
            _services.Register(messageSender);
            _services.Register(_playersContainer);
            _services.Register(serverStateHandler);
            _services.Register(gameStatesManager);
            _services.Register(playerVisualsManager);
            _services.Register(_inputService);
            _services.Register(_colorRegistry);
            _services.Register(effectsManager);
            _services.Register(territoryRenderer);
            _services.Register(trailVisualsManager);
            _services.Register(territoryClaim);
            _services.Register(minimapSystem);
            _services.Register(territoryClaimPopupManager);
            _services.Register(leaderboardUI);
            
            // Game subsystems (depend on the above)
            RegisterGameSubsystems();

            _services.InitDanglingServices();
            return;

            void RegisterGameSubsystems()
            {
                _services.Register(_territorySystem);
                _services.Register(_effectsCoordinator);
                _services.Register(_predictionSystem);
                _services.Register(_stateReceiver);
                
                _services.Register(gameWorld);
            }
        }

        private void Update()
        {
            _services.TickAll();
        }

        private void LateUpdate()
        {
            _services.TickLateAll();
        }

        private void OnDestroy()
        {
            _services.DisposeAll();
        }
    }
}