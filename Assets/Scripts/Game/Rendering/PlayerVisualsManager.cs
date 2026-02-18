using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Helpers;
using Input;
using UnityEngine;

namespace Game.Rendering
{
    public class PlayerVisualsManager : MonoBehaviour, IService
    {
        [SerializeField] private GameWorldConfig config;
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private PlayerVisual playerVisualPrefab;
        
        [SerializeField] private Transform visualsContainer;
        
        [SerializeField] private int initialPoolSize = 8;
        [SerializeField] private bool usePooling = true;
        
        private readonly Dictionary<uint, PlayerVisual> _activeVisuals = new();

        private readonly Queue<PlayerVisual> _pool = new();
        
        private PlayerVisual _localPlayerVisual;
        
        private uint _localPlayerId;
        private uint _currentTick;
        public int ActiveCount => _activeVisuals.Count;
        public PlayerVisual LocalPlayerVisual => _localPlayerVisual;
        public PlayersContainer PlayersContainer { get; private set; }
        public IReadOnlyDictionary<uint, PlayerVisual> ActiveVisuals => _activeVisuals;
        
        private readonly Color32[] _playerColors = {
            new(255, 77, 77, 255),   // Red
            new(77, 153, 255, 255),  // Blue  
            new(77, 255, 77, 255),   // Green
            new(255, 255, 77, 255),  // Yellow
            new(255, 77, 255, 255),  // Magenta
            new(77, 255, 255, 255),  // Cyan
            new(255, 153, 77, 255),  // Orange
            new(153, 77, 255, 255),  // Purple
        };
        
        private PlayersContainer _playersContainer;
        public void Initialize(ServiceContainer services)
        {
            _playersContainer = services.Get<PlayersContainer>();
            PlayersContainer = _playersContainer;
            
            // Create container for player game objects if not assigned
            if (visualsContainer == null)
            {
                var containerGO = new GameObject("PlayerVisuals");
                containerGO.transform.SetParent(transform);
                visualsContainer = containerGO.transform;
            }
            Debug.Log($"[PlayerVisualsManager] Initialized with pool size {_pool.Count}");
        }

        public void Tick()
        { }

        public void Dispose()
        {
            ClearAll();
            
            while (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                if (pooled != null)
                {
                    Destroy(pooled.gameObject);
                }
            }
        }

        public void SpawnPlayers()
        {
            if (usePooling)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var visual = CreateNewVisual();
                    visual.gameObject.SetActive(false);
                    _pool.Enqueue(visual);
                }
            }
        }
        private void ActivateOrUpdatePlayer(PaperioPlayer protoPlayer)
        {
            uint playerId = protoPlayer.PlayerId;
            
            if (_activeVisuals.TryGetValue(playerId, out var existingVisual))
            {
                UpdateVisualFromProto(existingVisual, protoPlayer);
            }
            else
            {
                ActivatePlayer(protoPlayer); 
            }
        }

        private void ActivatePlayer(PaperioPlayer protoPlayer)
        {
            if (playerVisualPrefab == null)
            {
                Debug.LogError("[PlayerVisualsManager] PlayerVisual prefab not assigned!");
                return;
            }
            
            PlayerVisual visual = GetFromPoolOrCreate();
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);

            Color color = GetPlayerColor(protoPlayer.PlayerId);
            
            bool isLocal = protoPlayer.PlayerId == _localPlayerId;
            
            visual.Initialize(
                protoPlayer.PlayerId,
                protoPlayer.Name,
                color,
                worldPos,
                isLocal
            );
            
            _activeVisuals[protoPlayer.PlayerId] = visual;
            
            if (isLocal)
            {
                _localPlayerVisual = visual;
                var localPlayerData = _playersContainer.GetAlivePlayers()
                    .FirstOrDefault(p => p.PlayerId == _localPlayerId);
                if (localPlayerData != null)
                {
                    localPlayerData.InputService = visual.GetComponent<InputService>();
                }
            }
            
            Debug.Log($"[PlayerVisualsManager] Activated player: {protoPlayer.Name} (ID: {protoPlayer.PlayerId})" +
                      (isLocal ? " [LOCAL]" : ""));
        }
        public Color GetPlayerColor(uint playerId)
        {
            if (playerId == 0)
            {
                return config.NeutralColor;
            }
            
            var playerData = _playersContainer?.TryGetPlayerById(playerId);
            if (playerData != null && playerData.Color != default)
            {
                return playerData.Color;
            }
            
            int index = (int)((playerId - 1) % _playerColors.Length);
            return _playerColors[index];
        }
        public void DespawnPlayer(uint playerId)
        {
            if (!_activeVisuals.TryGetValue(playerId, out var visual))
            {
                return;
            }
            
            _activeVisuals.Remove(playerId);
            
            if (visual == _localPlayerVisual)
            {
                _localPlayerVisual = null;
            }
            
            if (usePooling)
            {
                visual.ResetForPool();
                _pool.Enqueue(visual);
            }
            else
            {
                Destroy(visual.gameObject);
            }
            
            Debug.Log($"[PlayerVisualsManager] Despawned player: {playerId}");
        }

        private void UpdateVisualFromProto(PlayerVisual visual, PaperioPlayer protoPlayer)
        {
            var playerData = _playersContainer.TryGetPlayerById(protoPlayer.PlayerId);
            if (playerData == null)
            {
                return;
            }
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);
            
            visual.UpdateFromData(playerData, worldPos, _currentTick);
        }

        public void UpdateFromState(PaperioState state, uint localPlayerId)
        {
            _localPlayerId = localPlayerId;
            _currentTick = state.Tick;
            var currentPlayers = new HashSet<uint>();
    
            foreach (var protoPlayer in state.Players)
            {
                currentPlayers.Add(protoPlayer.PlayerId);
                ActivateOrUpdatePlayer(protoPlayer);
            }
    
            var toRemove = new List<uint>();
            foreach (var playerId in _activeVisuals.Keys)
            {
                if (!currentPlayers.Contains(playerId))
                {
                    toRemove.Add(playerId);
                }
            }
    
            foreach (var playerId in toRemove)
            {
                DespawnPlayer(playerId);
            }
        }
        
        public void UpdateInterpolation(float tickProgress)
        {
            foreach (var visual in _activeVisuals.Values)
            {
                visual.UpdateInterpolation(tickProgress);
            }
        }

        public void ClearAll()
        {
            foreach (var visual in _activeVisuals.Values)
            {
                if (usePooling)
                {
                    visual.ResetForPool();
                    _pool.Enqueue(visual);
                }
                else
                {
                    Destroy(visual.gameObject);
                }
            }
            
            _activeVisuals.Clear();
            _localPlayerVisual = null;
            
            Debug.Log("[PlayerVisualsManager] Cleared all visuals");
        }
        
        private Vector3 CalculateWorldPosition(PaperioPlayer protoPlayer)
        {
            if (protoPlayer.Position == null)
            {
                return Vector3.zero;
            }

            return GridHelper.GridToWorld(
                protoPlayer.Position.X,
                protoPlayer.Position.Y,
                config.CellSize,
                playerConfig.PlayerHeight
            );
        }

        private PlayerVisual GetFromPoolOrCreate()
        {
            if (usePooling && _pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            
            return CreateNewVisual();
        }

        private PlayerVisual CreateNewVisual()
        {
            var createdPlayer = Instantiate(playerVisualPrefab, visualsContainer);
            return createdPlayer.GetComponent<PlayerVisual>();
        }
    }
}