using Core.Services;
using Game.Data;
using Game.Effects;
using Game.Subsystems.Rendering;
using Utils;

namespace Game.Subsystems
{
    public class EffectsCoordinator : IService
    {
        private EffectsManager _effectsManager;
        private PlayerVisualsManager _playerVisualsManager;
        private IGameSessionDataProvider _sessionDataProvider;
        private GameWorldConfig _config;
        public void Initialize(ServiceContainer services)
        {
            _effectsManager = services.Get<EffectsManager>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            
            var gameWorld = services.Get<GameWorld>();
            _sessionDataProvider = services.Get<GameSessionData>();
            _config = gameWorld.Config;
        }

        public void Dispose() { }

        public void PreparePools()
        {
            _effectsManager.PreparePools();
        }
        
        public void OnPlayerEliminated(uint playerId)
        {
            bool isLocal = playerId == _sessionDataProvider.LocalPlayerId;

            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
            if (playerData == null)
            {
                return;
            }

            var worldPos = GridHelper.GridToWorld(
                playerData.GridPosition.x,
                playerData.GridPosition.y,
                _config.CellSize
            );

            var effectData = new EffectData(position: worldPos, color: playerData.Color);
            _effectsManager.PlayEffect(Effect.Death, effectData);

            if (isLocal)
            {
                _effectsManager.PlayEffect(Effect.CameraShake, effectData);
            }
        }

        public void OnPlayerRespawned(uint playerId)
        {
            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(playerId);
            if (playerData == null)
            {
                return;
            }

            var worldPos = GridHelper.GridToWorld(
                playerData.GridPosition.x,
                playerData.GridPosition.y,
                _config.CellSize
            );

            var effectData = new EffectData(position: worldPos, color: playerData.Color);
            _effectsManager.PlayEffect(Effect.Respawn, effectData);
        }
    }
}