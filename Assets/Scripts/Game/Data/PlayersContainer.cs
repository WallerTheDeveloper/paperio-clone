using System.Collections.Generic;
using Core.DISystem;
using Game.Server;

namespace Game.Data
{
    public class PlayersContainer : IDependency
    {
        private readonly Dictionary<uint, PlayerData> _playersContainer = new();
        
        public void Initialize()
        { }

        public void Deinitialize()
        { }

        public void Tick()
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
        /// <summary>
        /// Get a specific player's data.
        /// </summary>
        public PlayerData TryGetPlayerById(uint playerId)
        {
            _playersContainer.TryGetValue(playerId, out PlayerData player);
            return player;
        }
        
        /// <summary>
        /// Get all alive players.
        /// </summary>
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