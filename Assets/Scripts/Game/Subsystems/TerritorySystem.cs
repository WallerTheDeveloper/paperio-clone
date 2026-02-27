using System;
using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Paperio;
using Game.Subsystems.Rendering;
using Game.UI.Territory;
using UnityEngine;

namespace Game.Subsystems
{
    public interface ITerritoryStateHandler
    {
        List<TerritoryChange> ProcessStateUpdate(PaperioState state);
    }

    public interface ITerritoryEventsHandler
    {
        Action<int, Color> OnLocalClaim { get; set; }
    }
    
    public class TerritorySystem : IService, ITerritoryStateHandler, ITerritoryEventsHandler
    {
        public Action<int, Color> OnLocalClaim { get; set; }
        
        public TerritoryData Data => _territoryData;

        private TerritoryData _territoryData;
        private TerritoryRenderer _territoryRenderer;
        private TerritoryClaim _territoryClaim;
        private IColorDataProvider _colorDataProvider;
        private PlayerVisualsManager _playerVisualsManager;
        private IGameSessionData _sessionData;
        private GameWorldConfig _config;
        public void Initialize(ServiceContainer services)
        {
            _territoryRenderer = services.Get<TerritoryRenderer>();
            _territoryClaim = services.Get<TerritoryClaim>();
            _colorDataProvider = services.Get<ColorsRegistry>();
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            
            var gameWorld = services.Get<GameWorld>();
            _sessionData = gameWorld.GameSessionData;
            _config = gameWorld.Config;
        }

        public void Dispose()
        {
            _territoryClaim?.FinishAllImmediately();
            _territoryData?.Clear();
        }

        public void InitializeFromState(PaperioState initialState)
        {
            int width = (int)initialState.GridWidth;
            int height = (int)initialState.GridHeight;

            _territoryData = new TerritoryData(
                width,
                height,
                _config.CellSize,
                _config.NeutralColor,
                ownerId => _colorDataProvider.GetTerritoryColor(ownerId, _config.NeutralColor));
            
            if (initialState.Territory != null)
            {
                var changes = _territoryData.ApplyFullState(initialState.Territory);
                Debug.Log($"[TerritorySystem] Initial territory: {_territoryData.ClaimedCells} cells claimed");
            }

            _territoryRenderer.CreateTerritory();
            _territoryClaim.Prepare();
        }

        public List<TerritoryChange> ProcessStateUpdate(PaperioState state)
        {
            if (_territoryData == null)
            {
                return new List<TerritoryChange>();
            }

            List<TerritoryChange> changes;

            bool isDelta = state.StateType == StateType.StateDelta;

            if (isDelta && state.TerritoryChanges.Count > 0)
            {
                changes = _territoryData.ApplyDeltaChanges(state.TerritoryChanges);
            }
            else if (!isDelta && state.Territory != null && state.Territory.Count > 0)
            {
                changes = _territoryData.ApplyFullState(state.Territory);
            }
            else
            {
                return new List<TerritoryChange>();
            }

            if (changes.Count > 0)
            {
                ApplyVisuals(changes);
            }

            return changes;
        }

        public List<TerritoryChange> ClearOwnership(uint playerId)
        {
            if (_territoryData == null) return new List<TerritoryChange>();

            var changes = _territoryData.ClearOwnership(playerId);
            if (changes.Count > 0)
            {
                _territoryRenderer.FlushToMesh(changes.Count);
                _territoryClaim.SyncNonAnimatedColors();
            }

            return changes;
        }

        private void ApplyVisuals(List<TerritoryChange> changes)
        {
            _territoryRenderer.FlushToMesh(changes.Count);
            _territoryClaim.SyncNonAnimatedColors();

            var playerData = _playerVisualsManager.PlayersContainer.TryGetPlayerById(changes[0].NewOwner);
            if (playerData != null)
            {
                _territoryClaim.AddWave(changes, playerData.PlayerId, playerData.Color);

                int localClaimCount = 0;
                Color localColor = Color.white;

                foreach (var change in changes)
                {
                    if (change.NewOwner == _sessionData.LocalPlayerId)
                    {
                        localClaimCount++;
                        if (localColor == Color.white)
                        {
                            localColor = _colorDataProvider.GetColorOf(_sessionData.LocalPlayerId);
                        }
                    }
                }

                if (localClaimCount > 0)
                {
                    OnLocalClaim?.Invoke(localClaimCount, localColor);
                }
            }
        }
    }
}