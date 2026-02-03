using System.Collections.Generic;
using Game.Data;
using Game.Paperio;
using Input;
using UnityEngine;

namespace Game.Rendering
{
    public class PlayerVisualsManager : MonoBehaviour
    {
        [SerializeField] private PlayerVisual playerVisualPrefab;
        
        [SerializeField] private Transform visualsContainer;
        
        [SerializeField] private int initialPoolSize = 8;
        [SerializeField] private bool usePooling = true;
        
        private readonly Dictionary<uint, PlayerVisual> _activeVisuals = new();
        
        private readonly Queue<PlayerVisual> _pool = new();
        
        private uint _localPlayerId;
        private PlayerVisual _localPlayerVisual;
        
        public int ActiveCount => _activeVisuals.Count;
        public PlayerVisual LocalPlayerVisual => _localPlayerVisual;
        
        private GameWorld _gameWorld;
        private PlayersContainer _playersContainer;
        public void Initialize(GameWorld gameWorld, PlayersContainer playersContainer, uint localPlayerId)
        {
            _gameWorld = gameWorld;
            _playersContainer = playersContainer;
            _localPlayerId = localPlayerId;
            
            // Create container if not assigned
            if (visualsContainer == null)
            {
                var containerGO = new GameObject("PlayerVisuals");
                containerGO.transform.SetParent(transform);
                visualsContainer = containerGO.transform;
            }
            
            if (usePooling)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    var visual = CreateNewVisual();
                    visual.gameObject.SetActive(false);
                    _pool.Enqueue(visual);
                }
            }
            
            Debug.Log($"[PlayerVisualsManager] Initialized with pool size {_pool.Count}");
        }

        public void SpawnOrUpdatePlayer(PaperioPlayer protoPlayer)
        {
            uint playerId = protoPlayer.PlayerId;
            
            if (_activeVisuals.TryGetValue(playerId, out var existingVisual))
            {
                UpdateVisualFromProto(existingVisual, protoPlayer);
            }
            else
            {
                SpawnPlayer(protoPlayer);
            }
        }

        private void SpawnPlayer(PaperioPlayer protoPlayer)
        {
            if (playerVisualPrefab == null)
            {
                Debug.LogError("[PlayerVisualsManager] PlayerVisual prefab not assigned!");
                return;
            }
            
            PlayerVisual visual = GetFromPoolOrCreate();
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);
            
            Color color = _gameWorld != null 
                ? _gameWorld.GetPlayerColor(protoPlayer.PlayerId) 
                : Color.white;
            
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
            }
            
            Debug.Log($"[PlayerVisualsManager] Spawned player: {protoPlayer.Name} (ID: {protoPlayer.PlayerId})" +
                      (isLocal ? " [LOCAL]" : ""));
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
            var playerData = _playersContainer?.TryGetPlayerById(protoPlayer.PlayerId);
            if (playerData == null) return;
            
            Vector3 worldPos = CalculateWorldPosition(protoPlayer);
            
            visual.UpdateFromData(playerData, worldPos);
        }

        public void UpdateFromState(PaperioState state)
        {
            var currentPlayers = new HashSet<uint>();
            
            foreach (var protoPlayer in state.Players)
            {
                currentPlayers.Add(protoPlayer.PlayerId);
                SpawnOrUpdatePlayer(protoPlayer);
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

        private Vector3 CalculateWorldPosition(PaperioPlayer protoPlayer)
        {
            if (_gameWorld == null || protoPlayer.Position == null)
            {
                return Vector3.zero;
            }
            
            return _gameWorld.GridToWorld(
                protoPlayer.Position.X,
                protoPlayer.Position.Y,
                _gameWorld.Config.PlayerHeight
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

            foreach (var player in _playersContainer.GetAlivePlayers())
            {
                if (_localPlayerId == player.PlayerId)
                {
                    player.InputService = createdPlayer.GetComponent<InputService>();
                }
            }
            return createdPlayer.GetComponent<PlayerVisual>();
        }

        public PlayerVisual GetVisual(uint playerId)
        {
            _activeVisuals.TryGetValue(playerId, out var visual);
            return visual;
        }

        public bool HasVisual(uint playerId)
        {
            return _activeVisuals.ContainsKey(playerId);
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

        private void OnDestroy()
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
    }
}