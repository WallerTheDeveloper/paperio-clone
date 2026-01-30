using Core.DISystem;
using Game.Data;
using Game.Server;
using UnityEngine;

namespace Game
{
    public class RoomManager : MonoBehaviour, IDependentObject
    {
        private PlayersContainer _playersContainer;
        public void InjectDependencies(IDependencyProvider provider)
        {
            _playersContainer = provider.GetDependency<PlayersContainer>();
        }

        public void PostInjectionConstruct()
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
                    // OnPlayerJoined?.Invoke(newPlayer);
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

            // Debug.Log($"[GameRoomJoined] Joined room as player {_gameManager.LocalPlayerId}");
        }
    }
}