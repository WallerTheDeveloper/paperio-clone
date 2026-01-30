using System;
using Core.DISystem;
using Game.Paperio;
using Network;
using UnityEngine;

namespace Input
{
    /// <summary>
    /// Handles player input from both keyboard (desktop) and touch (mobile).
    /// Think of this as a "translator" that converts raw input events
    /// into game actions (direction changes).
    /// 
    /// Keyboard: Arrow keys OR WASD
    /// Mobile: Swipe in direction to move
    /// </summary>
    public class InputHandler : MonoBehaviour, IDependency
    {
        [Header("Touch Settings")]
        [SerializeField] private float swipeThreshold = 50f;  // Min pixels for a swipe
        [SerializeField] private float swipeTimeLimit = 0.5f; // Max time for a swipe (seconds)

        [Header("Input Buffering")]
        [SerializeField] private bool enableInputBuffer = true;
        [SerializeField] private float inputBufferTime = 0.1f; // Buffer inputs for smoother gameplay

        // Touch tracking
        private Vector2 _touchStartPos;
        private float _touchStartTime;
        private bool _isTouching;

        // Current direction state
        private Direction _currentDirection = Direction.None;
        private Direction _lastSentDirection = Direction.None;
        private float _lastInputTime;

        // Input buffering
        private Direction _bufferedDirection = Direction.None;
        private float _bufferExpireTime;

        // Events
        public event Action<Direction> OnDirectionChanged;

        // Properties
        public Direction CurrentDirection => _currentDirection;
        public bool IsInputEnabled { get; set; } = true;
        
        public void Initialize()
        {
        }

        public void Deinitialize()
        {
        }

        public void Tick()
        {
            if (!IsInputEnabled) return;

            // Process keyboard input (desktop)
            ProcessKeyboardInput();

            // Process touch input (mobile)
            ProcessTouchInput();

            // Send buffered input if timer expired
            ProcessInputBuffer();
        }
        
        #region Keyboard Input

        private void ProcessKeyboardInput()
        {
            Direction newDirection = Direction.None;

            // Arrow keys
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W))
            {
                newDirection = Direction.Up;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                newDirection = Direction.Down;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                newDirection = Direction.Left;
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D))
            {
                newDirection = Direction.Right;
            }

            if (newDirection != Direction.None)
            {
                HandleDirectionInput(newDirection);
            }
        }

        #endregion

        #region Touch Input

        private void ProcessTouchInput()
        {
            // Handle mouse input as touch for desktop testing
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                StartTouch(UnityEngine.Input.mousePosition);
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                EndTouch(UnityEngine.Input.mousePosition);
            }

            // Handle actual touch input
            if (UnityEngine.Input.touchCount > 0)
            {
                Touch touch = UnityEngine.Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        StartTouch(touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EndTouch(touch.position);
                        break;
                }
            }
        }

        private void StartTouch(Vector2 position)
        {
            _touchStartPos = position;
            _touchStartTime = Time.time;
            _isTouching = true;
        }

        private void EndTouch(Vector2 position)
        {
            if (!_isTouching) return;
            _isTouching = false;

            // Check if this was a valid swipe (within time limit)
            float swipeTime = Time.time - _touchStartTime;
            if (swipeTime > swipeTimeLimit)
            {
                return; // Too slow, not a swipe
            }

            // Calculate swipe vector
            Vector2 swipeVector = position - _touchStartPos;
            float swipeDistance = swipeVector.magnitude;

            // Check if swipe was long enough
            if (swipeDistance < swipeThreshold)
            {
                return; // Too short, not a swipe
            }

            // Determine direction from swipe angle
            Direction direction = GetDirectionFromSwipe(swipeVector);
            HandleDirectionInput(direction);
        }

        private Direction GetDirectionFromSwipe(Vector2 swipeVector)
        {
            // Normalize and determine primary axis
            float absX = Mathf.Abs(swipeVector.x);
            float absY = Mathf.Abs(swipeVector.y);

            if (absX > absY)
            {
                // Horizontal swipe
                return swipeVector.x > 0 ? Direction.Right : Direction.Left;
            }
            else
            {
                // Vertical swipe
                return swipeVector.y > 0 ? Direction.Up : Direction.Down;
            }
        }

        #endregion

        #region Direction Handling

        private void HandleDirectionInput(Direction newDirection)
        {
            if (newDirection == Direction.None) return;

            // Prevent reversing direction (can't go from Up to Down directly)
            if (IsOpposite(_currentDirection, newDirection))
            {
                Debug.Log($"[InputHandler] Blocked reverse: {_currentDirection} -> {newDirection}");
                return;
            }

            // Buffer input for smoother gameplay
            if (enableInputBuffer)
            {
                _bufferedDirection = newDirection;
                _bufferExpireTime = Time.time + inputBufferTime;
            }
            else
            {
                ApplyDirection(newDirection);
            }

            _lastInputTime = Time.time;
        }

        private void ProcessInputBuffer()
        {
            if (!enableInputBuffer) return;

            // Check if we have a buffered input ready to send
            if (_bufferedDirection != Direction.None && Time.time >= _bufferExpireTime)
            {
                ApplyDirection(_bufferedDirection);
                _bufferedDirection = Direction.None;
            }
        }

        private void ApplyDirection(Direction direction)
        {
            // Only send if different from last sent
            if (direction == _lastSentDirection) return;

            _currentDirection = direction;
            _lastSentDirection = direction;

            // Notify listeners
            OnDirectionChanged?.Invoke(direction);

            // Send to server
            if (MessageSender.Instance != null && MessageSender.Instance.IsJoined)
            {
                MessageSender.Instance.SendDirection(direction);
                Debug.Log($"[InputHandler] Sent direction: {direction}");
            }
        }

        private bool IsOpposite(Direction a, Direction b)
        {
            return (a == Direction.Up && b == Direction.Down) ||
                   (a == Direction.Down && b == Direction.Up) ||
                   (a == Direction.Left && b == Direction.Right) ||
                   (a == Direction.Right && b == Direction.Left);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Reset input state (e.g., when respawning).
        /// </summary>
        public void ResetInput()
        {
            _currentDirection = Direction.None;
            _lastSentDirection = Direction.None;
            _bufferedDirection = Direction.None;
            _isTouching = false;
        }

        /// <summary>
        /// Force a direction change (e.g., from UI buttons).
        /// </summary>
        public void SetDirection(Direction direction)
        {
            HandleDirectionInput(direction);
        }

        /// <summary>
        /// Get debug information about input state.
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Current: {_currentDirection}\n" +
                   $"Buffered: {_bufferedDirection}\n" +
                   $"Touching: {_isTouching}";
        }

        #endregion

        #region 3D Utilities

        /// <summary>
        /// Convert direction enum to 3D world vector.
        /// In our coordinate system: X is right, Y is up, Z is forward.
        /// </summary>
        public static Vector3 DirectionToVector3(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:    return Vector3.forward;  // +Z
                case Direction.Down:  return Vector3.back;     // -Z
                case Direction.Left:  return Vector3.left;     // -X
                case Direction.Right: return Vector3.right;    // +X
                default:              return Vector3.zero;
            }
        }

        /// <summary>
        /// Convert direction enum to 2D grid offset.
        /// </summary>
        public static Vector2Int DirectionToGridOffset(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:    return new Vector2Int(0, 1);   // +Y in grid
                case Direction.Down:  return new Vector2Int(0, -1);  // -Y in grid
                case Direction.Left:  return new Vector2Int(-1, 0);  // -X in grid
                case Direction.Right: return new Vector2Int(1, 0);   // +X in grid
                default:              return Vector2Int.zero;
            }
        }

        /// <summary>
        /// Get rotation for an object facing a direction (Y-axis up).
        /// </summary>
        public static Quaternion DirectionToRotation(Direction direction)
        {
            Vector3 forward = DirectionToVector3(direction);
            if (forward == Vector3.zero) return Quaternion.identity;
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// Convert a 3D movement vector to the closest cardinal direction.
        /// Useful for converting analog input to grid-based movement.
        /// </summary>
        public static Direction Vector3ToDirection(Vector3 movement)
        {
            if (movement.sqrMagnitude < 0.01f) return Direction.None;

            // Project onto XZ plane
            float absX = Mathf.Abs(movement.x);
            float absZ = Mathf.Abs(movement.z);

            if (absX > absZ)
            {
                return movement.x > 0 ? Direction.Right : Direction.Left;
            }
            else
            {
                return movement.z > 0 ? Direction.Up : Direction.Down;
            }
        }

        #endregion
    }
}
