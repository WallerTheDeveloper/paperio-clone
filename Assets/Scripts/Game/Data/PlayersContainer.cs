using System.Collections.Generic;
using Core.Services;
using Game.Server;

namespace Game.Data
{
    public class PlayersContainer : IService
    {
        private readonly Dictionary<uint, PlayerData> _playersContainer = new();

        public void Initialize(ServiceContainer services)
        { }

        public void Tick()
        { }

        public void Dispose()
        { }

        public void Register(PlayerInfo playerInfo)
        {
            var newPlayer = new PlayerData
            {
                PlayerId = playerInfo.PlayerId,
                Name = playerInfo.Name,
                IsReady = playerInfo.Ready
            };
            _playersContainer[playerInfo.PlayerId] = newPlayer;
        }

        public bool Unregister(uint playerID)
        {
            return _playersContainer.Remove(playerID);
        }

        public PlayerData TryGetPlayerById(uint playerId)
        {
            _playersContainer.TryGetValue(playerId, out PlayerData player);
            return player;
        }
        
        public IEnumerable<PlayerData> GetAlivePlayers()
        {
            foreach (var player in _playersContainer.Values)
            {
                if (player.Alive)
                {
                    yield return player;
                }
            }
        }

        public void Clear()
        {
            _playersContainer.Clear();
        }
    }
}