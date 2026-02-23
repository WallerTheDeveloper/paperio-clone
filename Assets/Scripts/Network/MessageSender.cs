using System;
using Core.Services;
using Game.Client;
using Game.Paperio;
using Game.Server;
using Google.Protobuf;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// Central hub for all network communication.
    /// Think of this as a "switchboard operator" that routes messages
    /// to the right handlers and coordinates sending messages to the server.
    /// </summary>
    public class MessageSender : MonoBehaviour, IService
    {
        [Header("Connection Settings")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 9000;
        [SerializeField] private string playerName = "";
        [SerializeField] private string roomCode = "";

        [Header("Ping Settings")]
        [SerializeField] private float pingInterval = 1.0f;
        [SerializeField] private float connectionTimeout = 10.0f;

        // Network components
        private UdpClient _udpClient;
        private uint _sendSequence;
        private uint _pingSequence;
        private float _lastPingTime;
        private float _lastPongTime;
        private float _roundTripTime;

        // Connection state
        private bool _isJoined;
        private uint _localPlayerId;
        private string _roomCode;
        private string _reconnectToken;

        // Public properties
        public bool IsConnected => _udpClient?.IsConnected ?? false;
        public bool IsJoined => _isJoined;
        public uint LocalPlayerId => _localPlayerId;
        public string RoomCode => _roomCode;
        public float RoundTripTime => _roundTripTime;
        public string ServerHost => serverHost;
        public int ServerPort => serverPort;

        // Events for game state updates
        public event Action<RoomJoined> OnRoomJoined;
        public event Action<RoomUpdate> OnRoomUpdate;
        public event Action<GameStarting> OnGameStarting;
        public event Action<Game.Server.GameMessage> OnGameMessage;
        public event Action<GameEnded> OnGameEnded;
        public event Action<PlayerLeft> OnPlayerLeft;
        public event Action<Game.Server.Error> OnError;
        public event Action<Pong> OnPong;
        public event Action<PlayerDisconnected> OnPlayerDisconnected;
        public event Action<PlayerReconnected> OnPlayerReconnected;
        public event Action OnConnected;
        public event Action OnDisconnected;

        public event Action<PaperioState> OnPaperioStateReceived;
        public event Action<PaperioJoinResponse> OnPaperioJoinResponse;
        
        public void Initialize(ServiceContainer services)
        {
            // Ensure main thread dispatcher exists
            MainThreadDispatcher.Initialize();

            _udpClient = new UdpClient();
            _udpClient.OnDataReceived += HandleDataReceived;
            _udpClient.OnError += HandleNetworkError;
            _udpClient.OnDisconnected += HandleDisconnected;
        }

        public void Tick()
        {
            // Regular ping to keep connection alive and measure latency
            if (IsConnected && _isJoined)
            {
                if (Time.time - _lastPingTime >= pingInterval)
                {
                    SendPing();
                }

                // Check for connection timeout
                if (Time.time - _lastPongTime > connectionTimeout && _lastPongTime > 0)
                {
                    Debug.LogWarning("[NetworkManager] Connection timeout - no pong received");
                    Disconnect();
                }
            }
        }

        public void Dispose()
        {
            SendLeaveRoom();
            
            if (_udpClient != null)
            {
                _udpClient.OnDataReceived -= HandleDataReceived;
                _udpClient.OnError -= HandleNetworkError;
                _udpClient.OnDisconnected -= HandleDisconnected;
                _udpClient.Dispose();
            }
        }
        
        public void SetPlayerName(string name)
        {
            playerName = name;
        }
        
        #region Public API - Connection

        public bool Connect()
        {
            if (_udpClient.Connect(serverHost, serverPort))
            {
                _sendSequence = 0;
                _lastPongTime = Time.time;
                OnConnected?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Disconnect from the server.
        /// </summary>
        public void Disconnect()
        {
            _isJoined = false;
            _localPlayerId = 0;
            _roomCode = null;
            _reconnectToken = null;
            _udpClient.Disconnect();
        }

        #endregion

        #region Public API - Room Management

        /// <summary>
        /// Join a room. Empty room code creates a new room.
        /// </summary>
        public void SendJoinRoom()
        {
            if (!IsConnected)
            {
                Debug.LogError("[NetworkManager] Cannot join room: not connected");
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                JoinRoom = new JoinRoom
                {
                    RoomCode = roomCode,
                    PlayerName = playerName
                }
            };

            SendMessage(msg);
            Debug.Log($"[NetworkManager] Joining room: {(string.IsNullOrEmpty(roomCode) ? "new" : roomCode)}");
        }

        /// <summary>
        /// Leave the current room.
        /// </summary>
        public void SendLeaveRoom()
        {
            if (!IsConnected || !_isJoined) return;

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                LeaveRoom = new LeaveRoom()
            };

            SendMessage(msg);
            _isJoined = false;
            Debug.Log("[NetworkManager] Leaving room");
        }

        /// <summary>
        /// Signal that player is ready to start the game.
        /// </summary>
        public void SendReady()
        {
            if (!IsConnected || !_isJoined)
            {
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Ready = new Ready()
            };

            SendMessage(msg);
            Debug.Log("[NetworkManager] Sent ready signal");
        }

        /// <summary>
        /// Attempt to reconnect using a saved token.
        /// </summary>
        public void SendReconnect(string token)
        {
            if (!IsConnected)
            {
                Debug.LogError("[NetworkManager] Cannot reconnect: not connected");
                return;
            }

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Reconnect = new Reconnect
                {
                    Token = token,
                    PlayerName = playerName
                }
            };

            SendMessage(msg);
            Debug.Log("[NetworkManager] Attempting reconnect");
        }

        #endregion

        #region Public API - Game Input

        /// <summary>
        /// Send game-specific input (e.g., direction change in Paper.io).
        /// The payload should be a serialized protobuf message specific to the game.
        /// </summary>
        public void SendGameInput(byte[] payload)
        {
            if (!IsConnected || !_isJoined) return;

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                GameMessage = new Game.Client.GameMessage
                {
                    Payload = ByteString.CopyFrom(payload)
                }
            };

            SendMessage(msg);
        }

        /// <summary>
        /// Send direction input for Paper.io specifically.
        /// </summary>
        public void SendDirection(Direction direction)
        {
            var input = new PaperioInput { Direction = direction };
            
            using (var stream = new System.IO.MemoryStream())
            {
                input.WriteTo(stream);
                SendGameInput(stream.ToArray());
            }
        }

        #endregion

        #region Private - Message Handling

        private void HandleDataReceived(byte[] data)
        {
            Debug.Log($"[NetworkManager] HandleDataReceived called with {data.Length} bytes");
            
            try
            {
                var serverMsg = new ServerMessage();
                serverMsg.MergeFrom(new CodedInputStream(data));

                Debug.Log($"[NetworkManager] Parsed ServerMessage: PayloadCase={serverMsg.PayloadCase}, Sequence={serverMsg.Sequence}");

                _udpClient.UpdateReceivedSequence(serverMsg.Sequence);

                switch (serverMsg.PayloadCase)
                {
                    case ServerMessage.PayloadOneofCase.RoomJoined:
                        Debug.Log($"[NetworkManager] >>> RoomJoined received! PlayerId={serverMsg.RoomJoined.PlayerId}, Room={serverMsg.RoomJoined.RoomCode}");
                        HandleRoomJoined(serverMsg.RoomJoined);
                        break;
                    case ServerMessage.PayloadOneofCase.RoomUpdate:
                        Debug.Log("[NetworkManager] >>> RoomUpdate received");
                        OnRoomUpdate?.Invoke(serverMsg.RoomUpdate);
                        break;
                    case ServerMessage.PayloadOneofCase.GameStarting:
                        Debug.Log($"[NetworkManager] >>> GameStarting received! Countdown={serverMsg.GameStarting.CountdownSeconds}");
                        OnGameStarting?.Invoke(serverMsg.GameStarting);
                        break;
                    case ServerMessage.PayloadOneofCase.GameMessage:
                        Debug.Log($"[NetworkManager] >>> GameMessage received! FromPlayer={serverMsg.GameMessage.FromPlayerId}");
                        HandleGameMessage(serverMsg.GameMessage);
                        break;
                    case ServerMessage.PayloadOneofCase.GameEnded:
                        OnGameEnded?.Invoke(serverMsg.GameEnded);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerLeft:
                        OnPlayerLeft?.Invoke(serverMsg.PlayerLeft);
                        break;
                    case ServerMessage.PayloadOneofCase.Error:
                        Debug.Log($"[NetworkManager] >>> Error received: {serverMsg.Error.Message}");
                        HandleServerError(serverMsg.Error);
                        break;
                    case ServerMessage.PayloadOneofCase.Pong:
                        HandlePong(serverMsg.Pong);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerDisconnected:
                        OnPlayerDisconnected?.Invoke(serverMsg.PlayerDisconnected);
                        break;
                    case ServerMessage.PayloadOneofCase.PlayerReconnected:
                        OnPlayerReconnected?.Invoke(serverMsg.PlayerReconnected);
                        break;
                    case ServerMessage.PayloadOneofCase.None:
                        Debug.LogWarning("[NetworkManager] >>> Received message with PayloadCase=None (empty payload!)");
                        Debug.LogWarning($"[NetworkManager] Raw bytes ({data.Length}): {BitConverter.ToString(data, 0, Math.Min(data.Length, 64))}");
                        break;
                    default:
                        Debug.LogWarning($"[NetworkManager] Unknown message type: {serverMsg.PayloadCase}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Error parsing message ({data.Length} bytes): {ex}");
                Debug.LogError($"[NetworkManager] Raw bytes: {BitConverter.ToString(data, 0, Math.Min(data.Length, 64))}");
            }
        }

        private void HandleRoomJoined(RoomJoined roomJoined)
        {
            _isJoined = true;
            _localPlayerId = roomJoined.PlayerId;
            _roomCode = roomJoined.RoomCode;
            _reconnectToken = roomJoined.ReconnectToken;

            Debug.Log($"[NetworkManager] Joined room {_roomCode} as player {_localPlayerId}");
            
            // Save reconnect token for later use
            PlayerPrefs.SetString("ReconnectToken", _reconnectToken);
            PlayerPrefs.Save();

            OnRoomJoined?.Invoke(roomJoined);
        }

        private void HandleGameMessage(Game.Server.GameMessage gameMessage)
        {
            // First, notify general listeners
            OnGameMessage?.Invoke(gameMessage);

            // Try to parse as Paper.io specific messages
            TryParsePaperioMessage(gameMessage.Payload.ToByteArray());
        }

        private void TryParsePaperioMessage(byte[] payload)
        {
            // Try parsing as PaperioState
            try
            {
                var state = new PaperioState();
                state.MergeFrom(new CodedInputStream(payload));
                
                if (state.GridWidth > 0) // Valid state check
                {
                    OnPaperioStateReceived?.Invoke(state);
                    return;
                }
            }
            catch { }

            // Try parsing as PaperioJoinResponse
            try
            {
                var response = new PaperioJoinResponse();
                response.MergeFrom(new CodedInputStream(payload));
                
                if (response.YourPlayerId > 0) // Valid response check
                {
                    OnPaperioJoinResponse?.Invoke(response);
                    return;
                }
            }
            catch { }
        }

        private void HandleServerError(Game.Server.Error error)
        {
            Debug.LogError($"[NetworkManager] Server error: {error.Message}");
            OnError?.Invoke(error);
        }

        private void HandlePong(Pong pong)
        {
            _lastPongTime = Time.time;
            
            // Calculate round-trip time
            ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _roundTripTime = (float)(now - pong.Timestamp);

            OnPong?.Invoke(pong);
        }

        private void HandleNetworkError(string error)
        {
            Debug.LogError($"[NetworkManager] Network error: {error}");
        }

        private void HandleDisconnected()
        {
            _isJoined = false;
            OnDisconnected?.Invoke();
            Debug.Log("[NetworkManager] Disconnected from server");
        }

        #endregion

        #region Private - Sending

        private void SendMessage(ClientMessage msg)
        {
            try
            {
                using (var stream = new System.IO.MemoryStream())
                {
                    msg.WriteTo(stream);
                    _udpClient.Send(stream.ToArray());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Error sending message: {ex}");
            }
        }

        private void SendPing()
        {
            _pingSequence++;
            _lastPingTime = Time.time;

            var msg = new ClientMessage
            {
                Sequence = NextSequence(),
                Ping = new Game.Client.Ping()
                {
                    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Sequence = _pingSequence,
                }
            };

            SendMessage(msg);
        }

        private uint NextSequence()
        {
            return ++_sendSequence;
        }

        #endregion

        #region Debug UI

        /// <summary>
        /// Get connection statistics for debugging.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Connected: {IsConnected}\n" +
                   $"Joined: {_isJoined}\n" +
                   $"Player ID: {_localPlayerId}\n" +
                   $"Room: {_roomCode}\n" +
                   $"RTT: {_roundTripTime:F1}ms\n" +
                   $"Send Seq: {_sendSequence}";
        }

        #endregion
    }
}
