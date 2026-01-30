using System;
using System.Collections;
using Core.DISystem;
using Game;
using MonoSingleton;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameStarting : GameState
    {
        private Game.Game _game;
        private MessageSender _messageSender;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(IDependencyContainer container)
        {
            _messageSender = MonoSingletonRegistry.Get<MessageSender>();
            _game = MonoSingletonRegistry.Get<Game.Game>();
            
            _messageSender.OnGameStarting += OnGameStarting;
            
            TriggerStateSwitch?.Invoke();
        }

        public override void TickState()
        { }

        public override void Stop()
        {
            _messageSender.OnGameStarting -= OnGameStarting;
        }
        
        private void OnGameStarting(Game.Server.GameStarting gameStarting)
        {
            StartCoroutine(StartPlayingAfterCountdown());
            IEnumerator StartPlayingAfterCountdown()
            {
                Debug.Log($"[GameStarting] Game starting in {gameStarting.CountdownSeconds} seconds!");
                yield return new WaitForSeconds(gameStarting.CountdownSeconds);
                _game.StartPlaying();
            }
        }
    }
}