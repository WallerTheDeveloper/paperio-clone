using System.Collections.Generic;
using Game.Paperio;
using Input;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Client-side player data with both grid and world coordinates.
    /// </summary>
    public class PlayerData
    {
        public uint PlayerId { get; set; }
        public string Name { get; set; } = "";
        
        // Grid coordinates (from server)
        public Vector2Int GridPosition { get; set; }
        
        // World coordinates (for 3D rendering)
        public Vector3 WorldPosition { get; set; }
        public Vector3 PreviousWorldPosition { get; set; }
        public Vector3 TargetWorldPosition { get; set; }
        public float InterpolationTime { get; set; }
        
        public Direction Direction { get; set; } = Direction.None;
        
        // Trail in both coordinate systems
        public List<Vector2Int> Trail { get; set; } = new();
        public List<Vector3> TrailWorld { get; set; } = new();
        
        public bool Alive { get; set; } = true;
        public uint Score { get; set; }
        public Color Color { get; set; } = Color.white;
        public bool IsReady { get; set; }
        public bool IsFinishedGamePreparation { get; set; }
 
        // Services
        public InputService InputService { get; set; }
        
        /// <summary>
        /// Get interpolated world position for smooth movement.
        /// </summary>
        public Vector3 GetInterpolatedPosition(float tickProgress)
        {
            return Vector3.Lerp(PreviousWorldPosition, TargetWorldPosition, tickProgress);
        }
    }
}