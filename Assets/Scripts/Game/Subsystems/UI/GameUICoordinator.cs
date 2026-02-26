using Core.Services;
using Game.Data;
using Game.Subsystems.Rendering;
using Game.UI.Leaderboard;
using Game.UI.Territory;
using UnityEngine;
using Utils;

namespace Game.Subsystems.UI
{
    public class GameUICoordinator : MonoBehaviour, IService
    {
        [SerializeField] private LeaderboardUI leaderboardUIPrefab;
        [SerializeField] private TerritoryClaimPopupManager territoryClaimPopupManagerPrefab;
        
        private LeaderboardUI _leaderboardUI;
        private TerritoryClaimPopupManager _territoryClaimPopupManager;
        
        private bool _gameUiInitialized = false;
        
        private IGameStateReceiver _stateReceiver;
        private IColorDataProvider _colorDataProvider;
        private IGameSessionData _gameSessionData;
        private IGameWorldDataProvider _gameWorldDataProvider;
        private ITerritoryEventsHandler _territoryEventsHandler;
        private IPlayerVisualsDataProvider _playerVisualsData;
        public void Initialize(ServiceContainer services)
        {
            _stateReceiver = services.Get<GameStateReceiver>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _gameWorldDataProvider = services.Get<GameWorld>();
            _gameSessionData = services.Get<GameWorld>().GameSessionData;
            _territoryEventsHandler = services.Get<TerritorySystem>();
            _playerVisualsData = services.Get<PlayerVisualsManager>();
        }

        public void CreateAndInitializeGameUI()
        {
            var hud = GameObject.FindWithTag(Constants.Tags.HUD);
            
            _leaderboardUI = Instantiate(leaderboardUIPrefab, hud.transform);
            _territoryClaimPopupManager = Instantiate(territoryClaimPopupManagerPrefab, hud.transform);
            
            _leaderboardUI.Bind(_stateReceiver, _colorDataProvider, _gameSessionData);
            _territoryClaimPopupManager.Bind(_territoryEventsHandler, _gameWorldDataProvider, _playerVisualsData);

            _gameUiInitialized = true;
        }

        public void Tick()
        {
            if (_gameUiInitialized)
            {
                _territoryClaimPopupManager.Tick();
            }
        }

        public void Dispose()
        {
            _gameUiInitialized = false;
            _leaderboardUI.Unbind();
            _territoryClaimPopupManager.Unbind();
        }
    }
}