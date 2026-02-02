using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Server;
using UnityEngine;

namespace Network
{
    public class ServerStateHandler : MonoBehaviour, IService
    {
        public event Action<PaperioJoinResponse> OnJoinedGame;
        public event Action<PaperioState> OnStateUpdated;
        public event Action<uint> OnPlayerEliminated;
        public event Action<uint> OnPlayerRespawned;
        
        private uint _lastReceivedTick;
        private uint _localPlayerId;
        private uint _tickRateMs;
        private bool _hasJoinedGame;
        
        private PaperioState _currentState;
        
        private MessageSender _messageSender;
        private PlayersContainer _playersContainer;
        public void Initialize(ServiceContainer services)
        {
            _messageSender = services.Get<MessageSender>();
            _playersContainer = services.Get<PlayersContainer>();
            
            _messageSender.OnPaperioStateReceived += HandleStateReceived;
            _messageSender.OnPaperioJoinResponse += HandleJoinResponse;
        }

        public void Tick()
        {
        }

        public void Dispose()
        {
            if (_messageSender != null)
            {
                _messageSender.OnPaperioStateReceived -= HandleStateReceived;
                _messageSender.OnPaperioJoinResponse -= HandleJoinResponse;
            }
        }
        
        private void HandleStateReceived(PaperioState state)
        {
            // Ignore old states (can happen due to UDP packet reordering)
            if (state.Tick <= _lastReceivedTick && _lastReceivedTick != 0)
            {
                Debug.LogWarning($"[GameStateManager] Received old state tick {state.Tick}, current {_lastReceivedTick}");
                return;
            }
            
            ApplyState(state);
        }
        
        private void HandleJoinResponse(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            _tickRateMs = response.TickRateMs;
            _hasJoinedGame = true;
            
            Debug.Log($"[GameStateManager] Joined game as player {_localPlayerId}, tick rate: {_tickRateMs}ms");
            
            if (response.InitialState != null)
            {
                ApplyState(response.InitialState);
            }
            
            OnJoinedGame?.Invoke(response);
        }
        
        private void ApplyState(PaperioState state)
        {
            var previousState = _currentState;
            _currentState = state;
            _lastReceivedTick = state.Tick;
            
            UpdatePlayersFromState(state, previousState);
            
            if (previousState != null)
            {
                DetectStateChanges(previousState, state);
            }
            
            OnStateUpdated?.Invoke(state);
            
            if (state.Tick % 20 == 0)
            {
                Debug.Log($"[GameStateManager] Tick {state.Tick}: {state.Players.Count} players");
            }
        }
        
        private void UpdatePlayersFromState(PaperioState state, PaperioState previousState)
        {
            if (_playersContainer == null) return;
            
            var currentPlayerIds = new HashSet<uint>();
            
            foreach (var protoPlayer in state.Players)
            {
                currentPlayerIds.Add(protoPlayer.PlayerId);
                
                var playerData = _playersContainer.TryGetPlayerById(protoPlayer.PlayerId);
                if (playerData == null)
                {
                    var info = new PlayerInfo
                    {
                        PlayerId = protoPlayer.PlayerId,
                        Name = protoPlayer.Name
                    };
                    playerData = _playersContainer.Register(info);
                }
                
                UpdatePlayerData(playerData, protoPlayer);
            }
            
            // Remove players that are no longer in state
            if (previousState != null)
            {
                foreach (var prevPlayer in previousState.Players)
                {
                    if (!currentPlayerIds.Contains(prevPlayer.PlayerId))
                    {
                        _playersContainer.Unregister(prevPlayer.PlayerId);
                    }
                }
            }
        }
        
        private void UpdatePlayerData(PlayerData playerData, PaperioPlayer protoPlayer)
        {
            if (protoPlayer.Position != null)
            {
                playerData.GridPosition = new Vector2Int(protoPlayer.Position.X, protoPlayer.Position.Y);
            }
            
            playerData.Direction = ConvertDirection(protoPlayer.Direction);
            
            playerData.Trail.Clear();
            foreach (var pos in protoPlayer.Trail)
            {
                playerData.Trail.Add(new Vector2Int(pos.X, pos.Y));
            }
            
            playerData.Alive = protoPlayer.Alive;
            playerData.Score = protoPlayer.Score;
            playerData.Color = UIntToColor(protoPlayer.Color);
            
            Color UIntToColor(uint color)
            {
                float r = ((color >> 24) & 0xFF) / 255f;
                float g = ((color >> 16) & 0xFF) / 255f;
                float b = ((color >> 8) & 0xFF) / 255f;
                float a = (color & 0xFF) / 255f;
                return new Color(r, g, b, a);
            }
            Direction ConvertDirection(Direction protoDir)
            {
                return protoDir switch
                {
                    Direction.Up => Direction.Up,
                    Direction.Down => Direction.Down,
                    Direction.Left => Direction.Left,
                    Direction.Right => Direction.Right,
                    _ => Direction.None
                };
            }
        }
        
        private void DetectStateChanges(PaperioState previous, PaperioState current)
        {
            var prevPlayers = new Dictionary<uint, PaperioPlayer>();
            foreach (var p in previous.Players)
            {
                prevPlayers[p.PlayerId] = p;
            }
            
            foreach (var player in current.Players)
            {
                if (prevPlayers.TryGetValue(player.PlayerId, out var prevPlayer))
                {
                    if (prevPlayer.Alive && !player.Alive)
                    {
                        OnPlayerEliminated?.Invoke(player.PlayerId);
                    }
                    else if (!prevPlayer.Alive && player.Alive)
                    {
                        OnPlayerRespawned?.Invoke(player.PlayerId);
                    }
                }
            }
        }
    }
}