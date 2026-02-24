using System;
using UnityEngine;

namespace Game.Data
{
    public class GameSessionData
    {
        public uint LocalPlayerId { get; private set; }
        public uint GridWidth { get; private set; }
        public uint GridHeight { get; private set; }
        public uint TickRateMs { get; private set; }
        public uint MoveIntervalTicks { get; private set; }
        public bool IsGameActive { get; private set; }

        public event Action OnGameStarted;
        public event Action OnGameEnded;

        public float MoveDuration => MoveIntervalTicks * (TickRateMs / 1000f);
        
        public void SetFromJoinResponse(uint playerId, uint tickRateMs, uint moveIntervalTicks, uint gridWidth, uint gridHeight)
        {
            LocalPlayerId = playerId;
            TickRateMs = tickRateMs;
            MoveIntervalTicks = moveIntervalTicks;
            GridWidth = gridWidth;
            GridHeight = gridHeight;
        }

        public void StartGame()
        {
            IsGameActive = true;
            OnGameStarted?.Invoke();
        }

        public void EndGame()
        {
            IsGameActive = false;
            OnGameEnded?.Invoke();
        }


        public void Reset()
        {
            LocalPlayerId = 0;
            GridWidth = 0;
            GridHeight = 0;
            TickRateMs = 0;
            MoveIntervalTicks = 0;
            IsGameActive = false;
        }
    }
}