using Game.Data;
using UnityEngine;
using Game.Paperio;
using Input;

namespace Game.Rendering
{
    /// <summary>
    /// Visual representation of a player in 3D.
    /// Handles interpolated movement, rotation, and visual state.
    /// 
    /// Think of this as the "avatar" - the visual body that represents
    /// a player on the game board. It smoothly follows the server state
    /// rather than teleporting.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private MeshRenderer trailRenderer;
        [SerializeField] private LineRenderer trailLineRenderer;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Visual Settings")]
        [SerializeField] private float bodyScale = 0.8f;
        [SerializeField] private float trailWidth = 0.3f;
        [SerializeField] private float trailHeight = 0.1f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem moveParticles;
        [SerializeField] private ParticleSystem deathParticles;
        [SerializeField] private ParticleSystem respawnParticles;

        // State
        private uint _playerId;
        private Vector3 _targetPosition;
        private Vector3 _previousPosition;
        private Quaternion _targetRotation;
        private Direction _currentDirection;
        private bool _isLocalPlayer;
        private bool _isAlive = true;
        private float _interpolationProgress;
        private float _tickDuration;

        // Materials
        private Material _bodyMaterial;
        private Material _trailMaterial;

        public uint PlayerId => _playerId;
        public bool IsLocalPlayer => _isLocalPlayer;

        private void Awake()
        {
            // Create default body if not assigned
            if (bodyRenderer == null)
            {
                CreateDefaultBody();
            }

            // Cache materials
            if (bodyRenderer != null)
            {
                _bodyMaterial = bodyRenderer.material;
            }

            // Setup trail line renderer
            if (trailLineRenderer != null)
            {
                trailLineRenderer.startWidth = trailWidth;
                trailLineRenderer.endWidth = trailWidth;
            }
        }

        private void Update()
        {
            if (!_isAlive) return;

            // Interpolate position
            UpdatePosition();
            
            // Update rotation
            UpdateRotation();
        }

        #region Setup

        /// <summary>
        /// Initialize this visual for a specific player.
        /// </summary>
        public void Initialize(uint playerId, Color color, bool isLocalPlayer, float tickRate)
        {
            _playerId = playerId;
            _isLocalPlayer = isLocalPlayer;
            _tickDuration = 1f / tickRate;

            SetColor(color);

            // Local player might have different visual treatment
            if (_isLocalPlayer)
            {
                // Optionally make local player slightly larger or add outline
                transform.localScale = Vector3.one * bodyScale * 1.1f;
            }
            else
            {
                transform.localScale = Vector3.one * bodyScale;
            }

            gameObject.name = $"Player_{playerId}{(isLocalPlayer ? " (Local)" : "")}";
        }

        private void CreateDefaultBody()
        {
            // Create a rounded cube-like shape (using a cube primitive)
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyGo.name = "Body";
            bodyGo.transform.SetParent(transform);
            bodyGo.transform.localPosition = Vector3.up * 0.5f;
            bodyGo.transform.localScale = Vector3.one;
            
            // Remove collider
            var collider = bodyGo.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            
            bodyRenderer = bodyGo.GetComponent<MeshRenderer>();
        }

        #endregion

        #region State Updates

        /// <summary>
        /// Update from server state.
        /// </summary>
        public void UpdateFromPlayerData(PlayerData playerData)
        {
            // Store previous for interpolation
            _previousPosition = _targetPosition;
            _targetPosition = playerData.WorldPosition;
            _interpolationProgress = 0f;

            // Update direction
            if (playerData.Direction != Direction.None)
            {
                _currentDirection = playerData.Direction;
                _targetRotation = InputHandler.DirectionToRotation(playerData.Direction);
            }

            // Handle alive state change
            if (_isAlive && !playerData.Alive)
            {
                OnDeath();
            }
            else if (!_isAlive && playerData.Alive)
            {
                OnRespawn();
            }

            _isAlive = playerData.Alive;

            // Update trail
            UpdateTrail(playerData);
        }

        /// <summary>
        /// Set immediate position (for initial spawn).
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            _targetPosition = position;
            _previousPosition = position;
            _interpolationProgress = 1f;
        }

        /// <summary>
        /// Set player color.
        /// </summary>
        public void SetColor(Color color)
        {
            if (_bodyMaterial != null)
            {
                _bodyMaterial.color = color;
            }

            if (_trailMaterial != null)
            {
                _trailMaterial.color = new Color(color.r, color.g, color.b, 0.7f);
            }

            if (trailLineRenderer != null)
            {
                trailLineRenderer.startColor = color;
                trailLineRenderer.endColor = color;
            }
        }

        #endregion

        #region Movement & Rotation

        private void UpdatePosition()
        {
            // Progress interpolation
            _interpolationProgress += Time.deltaTime / _tickDuration;
            _interpolationProgress = Mathf.Clamp01(_interpolationProgress);

            // Apply curve for smoother movement
            float curvedProgress = movementCurve.Evaluate(_interpolationProgress);
            
            // Interpolate position
            transform.position = Vector3.Lerp(_previousPosition, _targetPosition, curvedProgress);

            // Emit particles while moving
            if (moveParticles != null && _interpolationProgress < 1f)
            {
                if (!moveParticles.isPlaying)
                {
                    moveParticles.Play();
                }
            }
            else if (moveParticles != null && moveParticles.isPlaying)
            {
                moveParticles.Stop();
            }
        }

        private void UpdateRotation()
        {
            // Smoothly rotate towards target
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                _targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        #endregion

        #region Trail

        private void UpdateTrail(PlayerData playerData)
        {
            if (trailLineRenderer == null) return;

            var trailPoints = playerData.TrailWorld;
            
            if (trailPoints.Count == 0)
            {
                trailLineRenderer.positionCount = 0;
                return;
            }

            // Add current position to trail
            trailLineRenderer.positionCount = trailPoints.Count + 1;
            
            for (int i = 0; i < trailPoints.Count; i++)
            {
                trailLineRenderer.SetPosition(i, trailPoints[i]);
            }
            
            // Connect to current position
            trailLineRenderer.SetPosition(trailPoints.Count, transform.position);
        }

        /// <summary>
        /// Clear the visual trail.
        /// </summary>
        public void ClearTrail()
        {
            if (trailLineRenderer != null)
            {
                trailLineRenderer.positionCount = 0;
            }
        }

        #endregion

        #region Death & Respawn

        private void OnDeath()
        {
            Debug.Log($"[PlayerVisual] Player {_playerId} died");

            // Play death effect
            if (deathParticles != null)
            {
                deathParticles.transform.position = transform.position;
                deathParticles.Play();
            }

            // Hide body
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = false;
            }

            // Clear trail
            ClearTrail();
        }

        private void OnRespawn()
        {
            Debug.Log($"[PlayerVisual] Player {_playerId} respawned");

            // Play respawn effect
            if (respawnParticles != null)
            {
                respawnParticles.transform.position = transform.position;
                respawnParticles.Play();
            }

            // Show body
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
            }
        }

        /// <summary>
        /// Set alive state directly.
        /// </summary>
        public void SetAlive(bool alive)
        {
            if (alive == _isAlive) return;

            _isAlive = alive;
            
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = alive;
            }
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // Clean up instantiated materials
            if (_bodyMaterial != null)
            {
                Destroy(_bodyMaterial);
            }
            if (_trailMaterial != null)
            {
                Destroy(_trailMaterial);
            }
        }

        #endregion
    }
}
