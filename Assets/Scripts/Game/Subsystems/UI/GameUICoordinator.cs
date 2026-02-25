using Core.Services;
using Game.Data;
using Game.UI.Leaderboard;
using UnityEngine;
using Utils;

namespace Game.Subsystems.UI
{
    public class GameUICoordinator : MonoBehaviour, IService
    {
        [SerializeField] private LeaderboardUI leaderboardUI;

        private IGameStateReceiver _stateReceiver;
        private IColorDataProvider _colorDataProvider;
        private IGameSessionData _gameSessionData;
        public void Initialize(ServiceContainer services)
        {
            _stateReceiver = services.Get<GameStateReceiver>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _gameSessionData = services.Get<GameWorld>().GameSessionData;
        }

        public void CreateAndInitializeGameUI()
        {
            var hud = GameObject.FindWithTag(Constants.Tags.HUD);
            
            var leaderboardUIInstance = Instantiate(leaderboardUI, hud.transform);
            
            leaderboardUIInstance.Bind(_stateReceiver, _colorDataProvider, _gameSessionData);
        }
        
        public void Dispose()
        {
            leaderboardUI.Unbind();
        }
    }
}