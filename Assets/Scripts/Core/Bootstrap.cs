using System;
using Core.GameStates;
using Core.Services;
using Game;
using Game.Data;
using Game.Effects;
using Game.Rendering;
using Game.UI;
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
        [SerializeField] private PlayerVisualsManager playerVisualsManager;
        [SerializeField] private EffectsManager effectsManager;
        [SerializeField] private TerritoryRenderer territoryRenderer;
        [SerializeField] private TrailVisualsManager trailVisualsManager;
        [SerializeField] private TerritoryClaim territoryClaim;
        [SerializeField] private MinimapSystem minimapSystem;
        [SerializeField] private TerritoryClaimPopupManager territoryClaimPopupManager;
        
        private PlayersContainer _playersContainer;
        
        private ServiceContainer _services;
        private void Awake()
        {
            _services = new ServiceContainer();
            _playersContainer = new PlayersContainer();
            
            _services.Register(messageSender);
            _services.Register(_playersContainer);
            _services.Register(serverStateHandler);
            _services.Register(gameStatesManager); 
            _services.Register(playerVisualsManager);
            _services.Register(gameWorld);
            _services.Register(trailVisualsManager);
            _services.Register(effectsManager);
            _services.Register(territoryRenderer);
            _services.Register(territoryClaim);
            _services.Register(minimapSystem);
            _services.Register(territoryClaimPopupManager);
            
            _services.InitDanglingServices();
        }
        
        private void Update()
        {
            foreach (var alivePlayer in _playersContainer.GetAlivePlayers())
            {
                if (!alivePlayer.IsFinishedGamePreparation &&
                    alivePlayer.InputService != null)
                {
                    _services.Register(alivePlayer.InputService);
                    
                    alivePlayer.IsFinishedGamePreparation = true;
                    _services.InitDanglingServices();
                    
                }
            }
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