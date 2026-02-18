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
        public bool IsGameActive { get; }
        public uint GridWidth { get; }
        public uint GridHeight { get; } 
        public uint TickRateMs { get; }
        public Camera LocalPlayerCamera { get; }
    }
}