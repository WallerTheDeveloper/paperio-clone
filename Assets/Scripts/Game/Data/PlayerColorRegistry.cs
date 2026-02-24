using System.Collections.Generic;
using Core.Services;
using Game.Rendering;
using UnityEngine;

namespace Game.Data
{
    public class PlayerColorRegistry : IService
    {
        private readonly Dictionary<uint, Color> _colors = new();
        
        private PlayerVisualsManager _playerVisualsManager;

        public IReadOnlyDictionary<uint, Color> Colors => _colors;

        public void Initialize(ServiceContainer services)
        {
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
        }

        public void Tick() { }

        public void Dispose()
        {
            _colors.Clear();
        }

        public void Register(uint playerId)
        {
            if (playerId == 0 || _colors.ContainsKey(playerId))
            {
                return;
            }

            Color color = _playerVisualsManager.GetPlayerColor(playerId);
            _colors[playerId] = color;
        }

        public Color GetColor(uint playerId)
        {
            if (playerId == 0)
            {
                return Color.white;
            }
            
            if (_colors.TryGetValue(playerId, out Color color))
            {
                return color;
            }
            
            Color resolved = _playerVisualsManager.GetPlayerColor(playerId);
            _colors[playerId] = resolved;
            return resolved;
        }

        public Color32 GetTerritoryColor(uint ownerId, Color32 neutralColor)
        {
            if (ownerId == 0)
            {
                return neutralColor;
            }

            Color playerColor = GetColor(ownerId);
            return new Color(
                playerColor.r * 0.7f,
                playerColor.g * 0.7f,
                playerColor.b * 0.7f,
                1f
            );
        }
    }
}