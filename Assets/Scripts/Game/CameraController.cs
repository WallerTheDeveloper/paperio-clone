using Core.Services;
using Game.Data;
using Helpers;
using UnityEngine;

namespace Game
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private float height = 30f;
        [SerializeField] private float distance = 10f;
        [SerializeField] private float lookAheadDistance = 5f;
        [SerializeField] private float followSmoothTime = 0.15f;
        [SerializeField] private float rotationSmoothTime = 0.3f;
        [SerializeField] private bool followDirection = false;
        [SerializeField] private float minHeight = 20f;
        [SerializeField] private float maxHeight = 50f;
        [SerializeField] private float zoomSmoothTime = 0.5f;
        [SerializeField] private bool useBounds = true;
        
        [SerializeField] private float boundsPadding = 5f;
        
        [SerializeField] private bool enableShake = true;
        
        [SerializeField] private float defaultShakeIntensity = 0.5f;
        
        private Camera _camera;
        private Transform _transform;
        
        private Vector3 _currentVelocity;
        private float _currentZoomVelocity;
        private float _currentRotationVelocity;
        private float _targetHeight;
        private float _currentYaw;
        
        private Bounds _gameBounds;
        private bool _hasBounds;
        
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeTimer;
        
        private bool _isInitialized;
        private Vector3 _initialOffset;

        public bool IsFollowing => target != null && _isInitialized;
        
        public void Initialize(IGameWorldDataProvider gameWorldData)
        {
            _transform = transform;
            _camera = GetComponent<Camera>();
            
            _targetHeight = height;
            _currentYaw = _transform.eulerAngles.y;

            _gameBounds = GridHelper.GetGridBounds(gameWorldData.GridWidth, gameWorldData.GridHeight, gameWorldData.Config.CellSize);

            if (_gameBounds != null)
            {
                _hasBounds = true;
            }
            
            _isInitialized = true;
            
            Debug.Log($"[CameraController] Initialized - Height: {height}, Distance: {distance}");
        }

        public void TickLate()
        {
            if (!_isInitialized)
            {
                return;
            }
            
            height = Mathf.SmoothDamp(height, _targetHeight, ref _currentZoomVelocity, zoomSmoothTime);
            
            if (target != null)
            {
                UpdateFollowPosition();
            }
        }

        public void Dispose()
        { }
        
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            
            if (target != null)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            
            Vector3 targetPos = CalculateTargetPosition(target.position);
            _transform.position = targetPos;
            _currentVelocity = Vector3.zero;
            
            LookAtTarget();
        }
        
        private void UpdateFollowPosition()
        {
            Vector3 targetWorldPos = target.position;
            Vector3 desiredPosition = CalculateTargetPosition(targetWorldPos);
            
            if (useBounds && _hasBounds)
            {
                desiredPosition = ClampToBounds(desiredPosition);
            }
            
            _transform.position = Vector3.SmoothDamp(
                _transform.position,
                desiredPosition,
                ref _currentVelocity,
                followSmoothTime
            );
            
            LookAtTarget();
        }

        private Vector3 CalculateTargetPosition(Vector3 targetPos)
        {
            float yawRad = _currentYaw * Mathf.Deg2Rad;
            
            Vector3 offset;
            if (followDirection && target != null)
            {
                offset = new Vector3(
                    -Mathf.Sin(yawRad) * distance,
                    height,
                    -Mathf.Cos(yawRad) * distance
                );
            }
            else
            {
                offset = new Vector3(0, height, -distance);
            }
            
            return targetPos + offset;
        }

        private void LookAtTarget()
        {
            if (target == null) return;
            
            Vector3 lookTarget = target.position;
            
            Vector3 lookDirection = lookTarget - _transform.position;
            
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                _transform.rotation = targetRotation;
            }
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            float frustumHeight = 2f * height * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * _camera.aspect;
            
            float halfWidth = frustumWidth * 0.5f;
            float halfHeight = frustumHeight * 0.5f;
            
            float minX = _gameBounds.min.x + halfWidth + boundsPadding;
            float maxX = _gameBounds.max.x - halfWidth - boundsPadding;
            float minZ = _gameBounds.min.z + halfHeight + boundsPadding - distance;
            float maxZ = _gameBounds.max.z - halfHeight - boundsPadding - distance;
            
            if (maxX > minX)
            {
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }
            else
            {
                position.x = _gameBounds.center.x;
            }
            
            if (maxZ > minZ)
            {
                position.z = Mathf.Clamp(position.z, minZ, maxZ);
            }
            else
            {
                position.z = _gameBounds.center.z - distance;
            }
            
            return position;
        }

        private void OnDrawGizmosSelected()
        {
            if (_camera == null) return;
            
            float frustumHeight = 2f * height * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * _camera.aspect;
            
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            center.y = 0;
            center.z += distance; // Offset for the angled view
            
            Vector3 size = new Vector3(frustumWidth, 0.1f, frustumHeight);
            Gizmos.DrawWireCube(center, size);
            
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
            }
            
            if (_hasBounds)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(_gameBounds.center, _gameBounds.size);
            }
        }
    }
}