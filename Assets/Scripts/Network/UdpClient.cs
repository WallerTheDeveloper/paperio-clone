using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// Low-level UDP client for sending and receiving raw bytes.
    /// Think of this as a "postal service" - it just delivers packages,
    /// it doesn't care what's inside them.
    /// </summary>
    public class UdpClient : IDisposable
    {
        private System.Net.Sockets.UdpClient _client;
        private IPEndPoint _serverEndpoint;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
        private bool _isConnected;
        private bool _isDisposed;

        // Sequence tracking for ordered delivery detection
        private uint _sendSequence;
        private uint _lastReceivedSequence;

        // Connection state
        public bool IsConnected => _isConnected && !_isDisposed;
        public uint SendSequence => _sendSequence;
        public uint LastReceivedSequence => _lastReceivedSequence;

        // Events
        public event Action<byte[]> OnDataReceived;
        public event Action<string> OnError;
        public event Action OnDisconnected;

        // Configuration
        private const int ReceiveBufferSize = 4096;
        private const int SendBufferSize = 4096;

        public UdpClient()
        {
            _sendSequence = 0;
            _lastReceivedSequence = 0;
        }

        /// <summary>
        /// Connect to the game server.
        /// Like dialing a phone number - establishes who we're talking to.
        /// </summary>
        /// <param name="host">Server IP address or hostname</param>
        /// <param name="port">Server port number</param>
        public bool Connect(string host, int port)
        {
            if (_isDisposed)
            {
                OnError?.Invoke("Cannot connect: client has been disposed");
                return false;
            }

            try
            {
                // Clean up any existing connection
                Disconnect();

                // Create new UDP socket
                _client = new System.Net.Sockets.UdpClient();
                _client.Client.ReceiveBufferSize = ReceiveBufferSize;
                _client.Client.SendBufferSize = SendBufferSize;

                // Set non-blocking timeout to prevent hanging
                _client.Client.ReceiveTimeout = 5000; // 5 second timeout
                _client.Client.SendTimeout = 1000;    // 1 second timeout

                // Resolve server address
                _serverEndpoint = new IPEndPoint(IPAddress.Parse(host), port);
                
                // "Connect" UDP socket (doesn't actually establish connection,
                // but sets default destination for Send calls)
                _client.Connect(_serverEndpoint);

                _isConnected = true;
                _sendSequence = 0;
                _lastReceivedSequence = 0;

                // Start receiving data in background
                StartReceiving();

                Debug.Log($"[UdpClient] Connected to {host}:{port}");
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Connection failed: {ex.Message}");
                Debug.LogError($"[UdpClient] Connection error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the server.
        /// Hangs up the phone gracefully.
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected) return;

            try
            {
                // Stop the receive loop
                _receiveCts?.Cancel();
                _receiveTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception) { }

            try
            {
                _client?.Close();
                _client?.Dispose();
            }
            catch (Exception) { }

            _client = null;
            _isConnected = false;
            _receiveCts?.Dispose();
            _receiveCts = null;

            OnDisconnected?.Invoke();
            Debug.Log("[UdpClient] Disconnected");
        }

        /// <summary>
        /// Send raw bytes to the server.
        /// Like putting a letter in the mailbox - we hope it arrives,
        /// but with UDP there's no guarantee.
        /// </summary>
        /// <param name="data">The bytes to send</param>
        /// <returns>True if send was initiated successfully</returns>
        public bool Send(byte[] data)
        {
            if (!IsConnected)
            {
                OnError?.Invoke("Cannot send: not connected");
                return false;
            }

            try
            {
                _client.Send(data, data.Length);
                return true;
            }
            catch (SocketException ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");
                Debug.LogWarning($"[UdpClient] Send error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Send data with automatic sequence numbering.
        /// Returns the sequence number used for this message.
        /// </summary>
        public uint SendWithSequence(byte[] data)
        {
            _sendSequence++;
            Send(data);
            return _sendSequence;
        }

        /// <summary>
        /// Start the background receive loop.
        /// Like having someone constantly checking the mailbox for new letters.
        /// </summary>
        private void StartReceiving()
        {
            _receiveCts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token));
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            Debug.Log("[UdpClient] ReceiveLoop STARTED on thread " + 
                      System.Threading.Thread.CurrentThread.ManagedThreadId);

            Task<System.Net.Sockets.UdpReceiveResult> receiveTask = null;

            while (!cancellationToken.IsCancellationRequested && _isConnected)
            {
                try
                {
                    if (receiveTask == null)
                    {
                        receiveTask = _client.ReceiveAsync();
                    }

                    var completedTask = await Task.WhenAny(
                        receiveTask,
                        Task.Delay(1000, cancellationToken)
                    );

                    if (completedTask == receiveTask && !cancellationToken.IsCancellationRequested)
                    {
                        var result = await receiveTask;
                        receiveTask = null;

                        byte[] receivedData = new byte[result.Buffer.Length];
                        Array.Copy(result.Buffer, receivedData, result.Buffer.Length);

                        MainThreadDispatcher.Enqueue(() =>
                        {
                            OnDataReceived?.Invoke(receivedData);
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[UdpClient] ReceiveLoop: OperationCanceledException (normal shutdown)");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("[UdpClient] ReceiveLoop: ObjectDisposedException (socket closed)");
                    break;
                }
                catch (SocketException ex)
                {
                    receiveTask = null;
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Receive error: {ex.Message}"));
                    }
                }
                catch (Exception ex)
                {
                    receiveTask = null;
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Unexpected error: {ex.Message}"));
                    }
                }
            }

            Debug.Log("[UdpClient] Receive loop ended");
        }

        /// <summary>
        /// Track received sequence number for packet loss detection.
        /// </summary>
        /// <param name="sequence">The sequence number from received message</param>
        /// <returns>Number of packets that were likely lost (gap in sequence)</returns>
        public int UpdateReceivedSequence(uint sequence)
        {
            if (sequence == 0)
            {
                // First message or reset
                _lastReceivedSequence = sequence;
                return 0;
            }

            int gap = 0;
            uint expected = _lastReceivedSequence + 1;

            if (sequence > expected)
            {
                gap = (int)(sequence - expected);
                Debug.LogWarning($"[UdpClient] Packet loss detected: {gap} packets missing");
            }
            else if (sequence < expected && sequence != 1)
            {
                // Old/duplicate packet
                Debug.Log($"[UdpClient] Out-of-order or duplicate packet: seq={sequence}, expected={expected}");
                return -1; // Indicate duplicate
            }

            _lastReceivedSequence = sequence;
            return gap;
        }

        /// <summary>
        /// Dispatch an action to Unity's main thread.
        /// Network callbacks happen on background threads, but Unity
        /// API calls must happen on the main thread.
        /// </summary>
        private void DispatchToMainThread(Action action)
        {
            MainThreadDispatcher.Enqueue(action);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Disconnect();
            GC.SuppressFinalize(this);
        }

        ~UdpClient()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Helper class to dispatch actions to the main thread in Unity.
    /// Attach this component to a GameObject that persists across scenes.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly System.Collections.Generic.Queue<Action> _executionQueue = 
            new System.Collections.Generic.Queue<Action>();

        public static void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] Error executing action: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Ensure the dispatcher exists in the scene.
        /// Call this from your game initialization code.
        /// </summary>
        public static void Initialize()
        {
            if (_instance == null)
            {
                var go = new GameObject("[MainThreadDispatcher]");
                _instance = go.AddComponent<MainThreadDispatcher>();
            }
        }
    }
}
