using Core.Services;
using Game.Paperio;
using Network;
using UnityEngine;

namespace Input
{
    public class InputService : MonoBehaviour, IService
    {
        private MessageSender _messageSender;
        private Direction _lastSentDirection = Direction.None;
    
        private PlayerInputActions _playerInputActions;
    
        public void Initialize(ServiceContainer services)
        {
            _messageSender = services.Get<MessageSender>();
        
            _playerInputActions = new PlayerInputActions();
            _playerInputActions.Enable();
        }
    
        public void Tick()
        {
            if (!_messageSender.IsConnected)
            {
                return;
            }
        
            var input = _playerInputActions.Player.Move.ReadValue<Vector2>();
            var direction = ConvertToDirection(input);
        
            if (direction != _lastSentDirection)
            {
                _messageSender.SendDirection(direction);
                _lastSentDirection = direction;
            }
        }
    
        private Direction ConvertToDirection(Vector2 input)
        {
            if (input.magnitude < 0.1f) return Direction.None;
        
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x > 0 ? Direction.Right : Direction.Left;
            }
            return input.y > 0 ? Direction.Up : Direction.Down;
        }
    

        public void Dispose()
        {
            _playerInputActions?.Player.Disable();
            _playerInputActions?.Dispose();
        }
    }
}