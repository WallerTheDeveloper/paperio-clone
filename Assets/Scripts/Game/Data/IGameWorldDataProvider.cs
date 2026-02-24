using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    public interface IGameWorldDataProvider
    {
        public GameWorldConfig Config { get; }
        public TerritoryData Territory { get; }
        public Dictionary<uint, Color> PlayerColors { get; }
        public uint LocalPlayerId { get; }
        public uint GridWidth { get; }
        public uint GridHeight { get; } 
        public Camera LocalPlayerCamera { get; }
    }
}