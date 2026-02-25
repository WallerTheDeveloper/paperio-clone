using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public interface IGameWorldDataProvider
    {
        public GameWorldConfig Config { get; }
        public TerritoryData Territory { get; }
        public Dictionary<uint, Color> PlayerColors { get; }
        public IGameSessionData GameSessionData { get; }
        public Camera LocalPlayerCamera { get; }
    }
}