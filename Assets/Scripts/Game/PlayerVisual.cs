using Game.Data;
using Game.Paperio;
using Network;
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
        
        [Header("Interpolation")]
        [SerializeField] private float renderDelayTicks = 1f;
        [SerializeField] private float maxExtrapolationTicks = 3f;
        [SerializeField] private int snapshotBufferSize = 10;
        
        private MaterialPropertyBlock _propertyBlock;
        private Transform _transform;
        
        private uint _playerId;
        private bool _isLocalPlayer;
        private bool _isAlive = true;
        private Color _playerColor;
        private float _baseScale;
        
        private Vector3 _previousPosition;
        private Vector3 _targetPosition;
        
        private InterpolationBuffer _interpolationBuffer;
        
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
        
        public void Initialize(
            uint playerId,
            string playerName,
            Color color,
            Vector3 worldPosition,
            bool isLocalPlayer,
            float tickDurationSeconds = 0.05f)
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
            
            if (!isLocalPlayer)
            {
                _interpolationBuffer = new InterpolationBuffer(
                    bufferSize: snapshotBufferSize,
                    renderDelayTicks: renderDelayTicks,
                    maxExtrapolationTicks: maxExtrapolationTicks,
                    tickDurationSeconds: tickDurationSeconds
                );
            }
            else
            {
                _interpolationBuffer = null;
            }
            
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
                      (isLocalPlayer ? " [LOCAL]" : $" [REMOTE, buffer={snapshotBufferSize}, delay={renderDelayTicks} ticks]"));
        }

        public void UpdateFromData(PlayerData playerData, Vector3 worldPosition, uint tick = 0)
        {
            if (_isLocalPlayer)
            {
                _previousPosition = _targetPosition;
                _targetPosition = worldPosition;
            }
            else
            {
                _interpolationBuffer?.AddSnapshot(
                    tick,
                    worldPosition,
                    playerData.Direction,
                    playerData.Alive
                );
            }
            
            if (playerData.Direction != _currentDirection)
            {
                _currentDirection = playerData.Direction;
                if (_isLocalPlayer)
                {
                    UpdateDirectionIndicator();
                }
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
            
            if (_isLocalPlayer)
            {
                Vector3 interpolatedPos = Vector3.Lerp(_previousPosition, _targetPosition, tickProgress);
                _transform.position = interpolatedPos;
            }
            else
            {
                if (_interpolationBuffer != null)
                {
                    var result = _interpolationBuffer.Sample(tickProgress);
                    
                    if (result.IsValid)
                    {
                        _transform.position = result.Position;
                        
                        if (result.Direction != _currentDirection)
                        {
                            _currentDirection = result.Direction;
                            UpdateDirectionIndicator();
                        }
                    }
                }
            }
        }

        public void SnapToPosition(Vector3 worldPosition)
        {
            _previousPosition = worldPosition;
            _targetPosition = worldPosition;
            _transform.position = worldPosition;
            
            _interpolationBuffer?.Clear();
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
                Direction.Up => 0f,
                Direction.Down => 180f,
                Direction.Left => 270f,
                Direction.Right => 90f,
                _ => directionIndicator.localEulerAngles.y
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
            
            _interpolationBuffer?.Clear();
            _interpolationBuffer = null;
            
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
            if (_isLocalPlayer)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_previousPosition, _targetPosition);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_targetPosition, 0.2f);
            }
            else if (_interpolationBuffer != null)
            {
                // Draw buffer state for debugging
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_transform.position, 0.15f);
                
                Gizmos.color = _interpolationBuffer.Count >= 2 ? Color.green : Color.red;
                Gizmos.DrawWireSphere(_transform.position + Vector3.up * 0.5f, 0.1f);
            }
        }
    }
}