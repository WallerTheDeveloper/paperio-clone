using Game.Data;
using Game.Paperio;
using UnityEngine;

namespace Game
{
    public class PlayerVisual : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private Transform directionIndicator;
        [SerializeField] private TextMesh nameLabel;
        
        [Header("Configuration")]
        [SerializeField] private float bodyScale = 0.8f;
        [SerializeField] private float deathFadeDuration = 0.5f;
        [SerializeField] private float respawnScaleDuration = 0.3f;
        [SerializeField] private float localPlayerScaleMultiplier = 1.1f;
        
        private MaterialPropertyBlock _propertyBlock;
        private Transform _transform;
        
        private uint _playerId;
        private bool _isLocalPlayer;
        private bool _isAlive = true;
        private Color _playerColor;
        private float _baseScale;
        
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        private Direction _currentDirection = Direction.None;
        
        private float _deathAnimationProgress;
        private float _respawnAnimationProgress;
        private bool _isPlayingDeathAnimation;
        private bool _isPlayingRespawnAnimation;
        
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        
        public uint PlayerId => _playerId;
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool IsAlive => _isAlive;
        public Color PlayerColor => _playerColor;
        
        public void Initialize(uint playerId, string playerName, Color color, Vector3 worldPosition, bool isLocalPlayer)
        {
            _transform = transform;
            _propertyBlock = new MaterialPropertyBlock();
            _baseScale = bodyScale;
            
            _playerId = playerId;
            _isLocalPlayer = isLocalPlayer;
            _playerColor = color;
            _isAlive = true;
            
            _isPlayingDeathAnimation = false;
            _isPlayingRespawnAnimation = false;
            _deathAnimationProgress = 0f;
            _respawnAnimationProgress = 0f;
            
            _previousPosition = worldPosition;
            _targetPosition = worldPosition;
            _transform.position = worldPosition;
            
            SetColor(color);
            
            if (nameLabel != null)
            {
                nameLabel.text = playerName;
                nameLabel.color = isLocalPlayer ? Color.yellow : Color.white;
            }
            
            float scale = _baseScale * (isLocalPlayer ? localPlayerScaleMultiplier : 1f);
            _transform.localScale = Vector3.one * scale;
            
            gameObject.SetActive(true);
            SetBodyVisible(true);
            
            Debug.Log($"[PlayerVisual] Initialized: {playerName} (ID: {playerId})" + 
                      (isLocalPlayer ? " [LOCAL]" : ""));
        }
        public void UpdateFromData(PlayerData playerData, Vector3 worldPosition)
        {
            _previousPosition = _targetPosition;
            _targetPosition = worldPosition;
            
            if (playerData.Direction != _currentDirection)
            {
                _currentDirection = playerData.Direction;
                UpdateDirectionIndicator();
            }
            
            if (playerData.Color != _playerColor && playerData.Color != default)
            {
                _playerColor = playerData.Color;
                SetColor(_playerColor);
            }
            
            if (playerData.Alive != _isAlive)
            {
                if (playerData.Alive)
                {
                    OnRespawn(worldPosition);
                }
                else
                {
                    OnDeath();
                }
            }
        }

        public void UpdateInterpolation(float tickProgress)
        {
            if (_isPlayingDeathAnimation || _isPlayingRespawnAnimation)
            {
                UpdateAnimations();
                return;
            }
            
            Vector3 interpolatedPos = Vector3.Lerp(_previousPosition, _targetPosition, tickProgress);
            _transform.position = interpolatedPos;
        }

        public void SnapToPosition(Vector3 worldPosition)
        {
            _previousPosition = worldPosition;
            _targetPosition = worldPosition;
            _transform.position = worldPosition;
        }

        private void SetColor(Color color)
        {
            if (bodyRenderer == null) return;
            
            _propertyBlock.SetColor(ColorProperty, color);
            _propertyBlock.SetColor(BaseColorProperty, color);
            bodyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void UpdateDirectionIndicator()
        {
            if (directionIndicator == null) return;
            
            float yRotation = _currentDirection switch
            {
                Direction.Down => 0f,
                Direction.Up => 180f,
                Direction.Left => 270f,
                Direction.Right => 90f,
                _ => directionIndicator.localEulerAngles.y // Keep current
            };
            
            directionIndicator.localRotation = Quaternion.Euler(0, yRotation, 0);
            
            directionIndicator.gameObject.SetActive(_currentDirection != Direction.None);
        }
        private void SetBodyVisible(bool visible)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = visible;
            }
        }

        private void OnDeath()
        {
            _isAlive = false;
            _isPlayingDeathAnimation = true;
            _deathAnimationProgress = 0f;
            
            Debug.Log($"[PlayerVisual] Player {_playerId} death animation started");
        }

        private void OnRespawn(Vector3 newPosition)
        {
            _isAlive = true;
            _isPlayingDeathAnimation = false;
            _isPlayingRespawnAnimation = true;
            _respawnAnimationProgress = 0f;
            
            SnapToPosition(newPosition);
            
            SetBodyVisible(true);
            
            Debug.Log($"[PlayerVisual] Player {_playerId} respawn animation started");
        }

        private void UpdateAnimations()
        {
            if (_isPlayingDeathAnimation)
            {
                _deathAnimationProgress += Time.deltaTime / deathFadeDuration;
                
                if (_deathAnimationProgress >= 1f)
                {
                    _isPlayingDeathAnimation = false;
                    SetBodyVisible(false);
                }
                else
                {
                    float scale = _baseScale * (1f - _deathAnimationProgress);
                    _transform.localScale = Vector3.one * scale;
                    
                    Color fadedColor = _playerColor;
                    fadedColor.a = 1f - _deathAnimationProgress;
                    SetColor(fadedColor);
                }
            }
            else if (_isPlayingRespawnAnimation)
            {
                _respawnAnimationProgress += Time.deltaTime / respawnScaleDuration;
                
                if (_respawnAnimationProgress >= 1f)
                {
                    _isPlayingRespawnAnimation = false;
                    float finalScale = _baseScale * (_isLocalPlayer ? localPlayerScaleMultiplier : 1f);
                    _transform.localScale = Vector3.one * finalScale;
                    SetColor(_playerColor);
                }
                else
                {
                    float scale = _baseScale * _respawnAnimationProgress * 
                                  (_isLocalPlayer ? localPlayerScaleMultiplier : 1f);
                    _transform.localScale = Vector3.one * scale;
                }
            }
        }

        public void ResetForPool()
        {
            _playerId = 0;
            _isLocalPlayer = false;
            _isAlive = true;
            _isPlayingDeathAnimation = false;
            _isPlayingRespawnAnimation = false;
            _currentDirection = Direction.None;
            
            _transform.localScale = Vector3.one * _baseScale;
            SetBodyVisible(true);
            
            if (nameLabel != null)
            {
                nameLabel.text = "";
            }
            
            gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_previousPosition, _targetPosition);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_targetPosition, 0.2f);
        }
    }
}