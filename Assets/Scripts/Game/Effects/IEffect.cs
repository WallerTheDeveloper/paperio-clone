using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Effects
{
    public interface IEffect
    {
        Effect Type { get; }
        public GameObject GameObject { get; }
        public bool IsPlaying { get; }
        void Prepare(IGameWorldDataProvider gameData);
        void Play(EffectData data);
        void Reset();
        void Stop();
    }
}