using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "New Player Config", menuName = "Game/Data/Player Config", order = 2)]
    public class PlayerConfig : ScriptableObject
    {
        [Tooltip("Height offset for players above the territory")]
        public float PlayerHeight = 0.5f;
        
        [Tooltip("Height offset for trails above the territory")]
        public float TrailHeight = 0.1f;
    }
}