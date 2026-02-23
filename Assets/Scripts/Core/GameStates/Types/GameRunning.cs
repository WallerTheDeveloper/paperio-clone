using System;
using System.Collections;
using Core.Services;
using Game;
using Game.Effects;
using Game.Server;
using Network;
using UnityEngine;

namespace Core.GameStates.Types
{
    public class GameRunning : GameState
    {
        private MessageSender _messageSender;
        private ServerStateHandler _serverStateHandler;
        private GameWorld _gameWorld;
        private EffectsManager _effectsManager;
        public override Action TriggerStateSwitch { get; set; }

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();
            _serverStateHandler = container.Get<ServerStateHandler>();
            _gameWorld = container.Get<GameWorld>();

            if (_gameWorld.IsGameActive)
            {
                Debug.Log("[GameRunning] Reconnection detected — resetting game state");
                _gameWorld.Dispose();
            }

            _serverStateHandler.ResetForReconnect();

            _serverStateHandler.OnJoinedGame += _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated += _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated += _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned += _gameWorld.OnPlayerRespawned;
            _messageSender.OnPlayerDisconnected += HandlePlayerDisconnected;
            
            Debug.Log($"[GameRunning] Initialize — HasJoinedGame={_serverStateHandler.HasJoinedGame}, IsJoined={_messageSender.IsJoined}");
            
            if (!_serverStateHandler.HasJoinedGame)
            {
                if (!_messageSender.IsJoined)
                {
                    Debug.Log("[GameRunning] Not yet joined room, starting WaitForJoinThenReady coroutine");
                    StartCoroutine(WaitForJoinThenReady());
                }
                else
                {
                    Debug.Log("[GameRunning] Already joined room, sending Ready immediately");
                    _messageSender.SendReady();
                }
            }
            else
            {
                Debug.Log("[GameRunning] Already joined game (reconnect path), skipping SendReady");
            }
        }

        private void HandlePlayerDisconnected(PlayerDisconnected obj)
        {
            _gameWorld.OnPlayerDisconnectedVisually(obj.PlayerId);
        }

        private IEnumerator WaitForJoinThenReady()
        {
            float startTime = Time.time;
            float logInterval = 1f;
            float nextLogTime = startTime + logInterval;
            float timeout = 10f;
            
            Debug.Log("[GameRunning] WaitForJoinThenReady: waiting for IsJoined...");
            
            while (!_messageSender.IsJoined)
            {
                if (Time.time >= nextLogTime)
                {
                    float elapsed = Time.time - startTime;
                    Debug.LogWarning($"[GameRunning] Still waiting for IsJoined after {elapsed:F1}s (connected={_messageSender.IsConnected})");
                    nextLogTime = Time.time + logInterval;
                }
                
                if (Time.time - startTime > timeout)
                {
                    Debug.LogError("[GameRunning] Timeout waiting for RoomJoined response! The server may not have sent it, or the client failed to parse it.");
                    yield break;
                }
                
                yield return null;
            }
            
            Debug.Log("[GameRunning] IsJoined became true! Sending Ready...");
            _messageSender.SendReady();
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            StopAllCoroutines();
            
            _serverStateHandler.OnJoinedGame -= _gameWorld.OnJoinedGame;
            _serverStateHandler.OnStateUpdated -= _gameWorld.OnServerStateUpdated;
            _serverStateHandler.OnPlayerEliminated -= _gameWorld.OnPlayerEliminated;
            _serverStateHandler.OnPlayerRespawned -= _gameWorld.OnPlayerRespawned;
            _messageSender.OnPlayerDisconnected -= HandlePlayerDisconnected;
        }
    }
}