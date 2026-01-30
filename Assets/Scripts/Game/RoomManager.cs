using Core.Services;
using Game.Data;
using Game.Server;
using UnityEngine;

namespace Game
{
    public class RoomManager : MonoBehaviour, IService
    {
        private PlayersContainer _playersContainer;
        public void Initialize(ServiceContainer services)
        {
            _playersContainer = services.Get<PlayersContainer>();
        }

        public void Tick()
        { }

        public void Dispose()
        { }
        
        public void UpdateRoom(RoomUpdate roomUpdate)
        {
            foreach (var playerInfo in roomUpdate.Players)
            {
                var player = _playersContainer.TryGetPlayerById(playerInfo.PlayerId);
                if (player != null)
                {
                    player.IsReady = playerInfo.Ready;
                }
                else
                {
                    _playersContainer.Register(playerInfo);
                }
            }
        }
        
        public void RegisterPlayerInRoom(RoomJoined roomJoined)
        {
            // _gameManager.LocalPlayerId = roomJoined.PlayerId;
            // _gameManager.PlayersContainer.Clear();

            foreach (var playerInfo in roomJoined.Players)
            {
                _playersContainer.Register(playerInfo);
            }
        }
    }
}