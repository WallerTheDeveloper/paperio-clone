using Core.Services;
using Game.Data;
using Game.UI.Leaderboard;
using UnityEngine;

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
            
            leaderboardUI.Bind(_stateReceiver, _colorDataProvider, _gameSessionData);
        }

        public void Dispose()
        {
            leaderboardUI.Unbind();
        }
    }
}