using System;
using System.Collections.Generic;
using Core.DISystem;
using Game.Data;
using UnityEngine;
using Game.Paperio;
using Game.Server;
using Helpers;
using Input;
using MonoSingleton;
using Network;

namespace Game
{
    public class Game : MonoSingleton<Game>, IDependentObject
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridWidth = 100;
        [SerializeField] private int gridHeight = 100;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private float playerHeight = 0.5f; // Y position of players above ground

        [Header("Game Settings")]
        [SerializeField] private float tickRateMs = 50f; // 20 ticks per second

        // Game state
        private uint _localPlayerId;
        private uint _currentTick;
        
        public uint LocalPlayerId
        {
            get => _localPlayerId;
            set => _localPlayerId = value;
        }

        public uint CurrentTick => _currentTick;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public float CellSize => cellSize;
        public float PlayerHeight => playerHeight;
        
        // Events
        public event Action<uint> OnTickReceived;
        public event Action<PlayerData> OnLocalPlayerUpdated;
        public event Action<PlayerData> OnPlayerJoined;
        public event Action<uint> OnPlayerLeft;
        public event Action<uint, uint> OnPlayerEliminated; // playerId, killerId
        public event Action<uint> OnPlayerRespawned;

        private void Start()
        {
            // Subscribe to network events
            var net = MessageSender.Instance;
            if (net != null)
            {
                net.OnPlayerLeft += HandlePlayerLeft;
                net.OnError += HandleError;
                net.OnPaperioStateReceived += HandlePaperioState;
                net.OnPaperioJoinResponse += HandlePaperioJoinResponse;
            }
        }

        private void OnDestroy()
        {
            var net = MessageSender.Instance;
            if (net != null)
            {
                net.OnPlayerLeft -= HandlePlayerLeft;
                net.OnError -= HandleError;
                net.OnPaperioStateReceived -= HandlePaperioState;
                net.OnPaperioJoinResponse -= HandlePaperioJoinResponse;
            }
        }

        public void EnableGameInput(bool enable)
        {
            _inputHandler.IsInputEnabled = enable;
        }
        
        #region Dependency Injection
        
        private InputHandler _inputHandler;
        private PlayersContainer _playersContainer;
        public void InjectDependencies(IDependencyProvider provider)
        {
            _inputHandler ??= provider.GetDependency<InputHandler>();
            _playersContainer ??= provider.GetDependency<PlayersContainer>();
        }

        public void PostInjectionConstruct()
        { }
        
        #endregion

        #region Network Event Handlers
        
        public void StartPlaying()
        {
            _inputHandler.ResetInput();
        }

        public void HandleGameEnd(GameEnded gameEnded)
        {
            Debug.Log($"[GameManager] Game ended! Winner: {gameEnded.WinnerId}");
        }

        private void HandlePlayerLeft(Server.PlayerLeft playerLeft)
        {
            if (_playersContainer.Unregister(playerLeft.PlayerId))
            {
                OnPlayerLeft?.Invoke(playerLeft.PlayerId);
                Debug.Log($"[GameManager] Player {playerLeft.PlayerId} left");
            }
        }

        private void HandleError(Server.Error error)
        {
            Debug.LogError($"[GameManager] Server error: {error.Message}");
        }

        public void Disconnect()
        {
            _playersContainer.Clear();
            LocalPlayerId = 0;
        }
        
        #endregion

        #region Paper.io State Handling

        private void HandlePaperioJoinResponse(PaperioJoinResponse response)
        {
            _localPlayerId = response.YourPlayerId;
            tickRateMs = response.TickRateMs;

            if (response.InitialState != null)
            {
                ApplyPaperioState(response.InitialState);
            }

            Debug.Log($"[GameManager] Paper.io join: player={_localPlayerId}, tickRate={tickRateMs}ms");
        }

        private void HandlePaperioState(PaperioState state)
        {
            ApplyPaperioState(state);
        }

        private void ApplyPaperioState(PaperioState state)
        {
            _currentTick = state.Tick;
            gridWidth = (int)state.GridWidth;
            gridHeight = (int)state.GridHeight;

            // Update all players
            foreach (var protoPlayer in state.Players)
            {
                var player = _playersContainer.TryGetPlayerById(protoPlayer.PlayerId);
                if (player != null)
                {
                    UpdatePlayerFromProto(player, protoPlayer);
                }
                // else
                // {
                //     var newPlayer = CreatePlayerFromProto(protoPlayer);
                //     PlayersContainer[protoPlayer.PlayerId] = newPlayer;
                //     OnPlayerJoined?.Invoke(newPlayer);
                // }
                //
                // // Notify if this is the local player
                // if (protoPlayer.PlayerId == _localPlayerId)
                // {
                //     OnLocalPlayerUpdated?.Invoke(PlayersContainer[_localPlayerId]);
                // }
            }

            // TODO: Update territory grid

            OnTickReceived?.Invoke(_currentTick);
        }

        private PlayerData CreatePlayerFromProto(PaperioPlayer proto)
        {
            var player = new PlayerData
            {
                PlayerId = proto.PlayerId,
                Name = proto.Name,
                Alive = proto.Alive,
                Score = proto.Score,
                Color = ColorFromUint(proto.Color)
            };

            if (proto.Position != null)
            {
                player.GridPosition = new Vector2Int(proto.Position.X, proto.Position.Y);
                player.WorldPosition = GridHelper.GridToWorld(player.GridPosition, playerHeight);
            }

            player.Direction = proto.Direction;
            player.Trail.Clear();
            player.TrailWorld.Clear();
            foreach (var pos in proto.Trail)
            {
                var gridPos = new Vector2Int(pos.X, pos.Y);
                player.Trail.Add(gridPos);
                player.TrailWorld.Add(GridHelper.GridToWorld(gridPos, 0.1f)); // Slightly above ground
            }

            return player;
        }

        private void UpdatePlayerFromProto(PlayerData player, PaperioPlayer proto)
        {
            bool wasAlive = player.Alive;
            var previousGridPos = player.GridPosition;

            player.Alive = proto.Alive;
            player.Score = proto.Score;

            if (proto.Position != null)
            {
                player.GridPosition = new Vector2Int(proto.Position.X, proto.Position.Y);
                
                // Store previous for interpolation
                player.PreviousWorldPosition = player.WorldPosition;
                player.TargetWorldPosition = GridHelper.GridToWorld(player.GridPosition, playerHeight);
                player.InterpolationTime = 0f;
            }

            player.Direction = proto.Direction;
            player.Trail.Clear();
            player.TrailWorld.Clear();
            foreach (var pos in proto.Trail)
            {
                var gridPos = new Vector2Int(pos.X, pos.Y);
                player.Trail.Add(gridPos);
                player.TrailWorld.Add(GridHelper.GridToWorld(gridPos, 0.1f));
            }

            // Detect state changes
            if (wasAlive && !player.Alive)
            {
                OnPlayerEliminated?.Invoke(player.PlayerId, 0);
            }
            else if (!wasAlive && player.Alive)
            {
                OnPlayerRespawned?.Invoke(player.PlayerId);
            }
        }

        private Color ColorFromUint(uint color)
        {
            float r = ((color >> 24) & 0xFF) / 255f;
            float g = ((color >> 16) & 0xFF) / 255f;
            float b = ((color >> 8) & 0xFF) / 255f;
            float a = (color & 0xFF) / 255f;
            return new Color(r, g, b, a);
        }

        #endregion
    }
}
