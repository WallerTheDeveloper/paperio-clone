using System.Collections.Generic;
using Game.Paperio;
using UnityEngine;

namespace Network
{
    public struct PendingInput
    {
        public uint Tick;
        public Direction Direction;
        public Vector2Int PredictedGridPos;
    }

    public class ClientPrediction
    {
        private readonly List<PendingInput> _pendingInputs = new();
        private Vector2Int _predictedPosition;
        private Direction _currentDirection = Direction.None;
        private uint _lastServerTick;

        private readonly uint _gridWidth;
        private readonly uint _gridHeight;

        private int _correctionCount;
        private int _totalReconciliations;

        private const int MaxPredictionError = 3;

        public Vector2Int PredictedPosition => _predictedPosition;
        public Direction CurrentDirection => _currentDirection;
        public int PendingInputCount => _pendingInputs.Count;
        public int CorrectionCount => _correctionCount;

        public ClientPrediction(uint gridWidth, uint gridHeight)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
        }

        public void Initialize(Vector2Int serverPosition, Direction direction)
        {
            _predictedPosition = serverPosition;
            _currentDirection = direction;
            _pendingInputs.Clear();
            _lastServerTick = 0;
            _correctionCount = 0;
            _totalReconciliations = 0;
        }

        public void RecordInput(Direction direction, uint estimatedTick)
        {
            _currentDirection = direction;

            _pendingInputs.Add(new PendingInput
            {
                Tick = estimatedTick,
                Direction = direction,
                PredictedGridPos = _predictedPosition
            });

            if (_pendingInputs.Count > 60)
            {
                _pendingInputs.RemoveAt(0);
            }
        }

        public void AdvancePrediction(uint estimatedTick)
        {
            if (_currentDirection == Direction.None)
            {
                return;
            }

            var delta = DirectionDelta(_currentDirection);
            var newPos = new Vector2Int(
                _predictedPosition.x + delta.x,
                _predictedPosition.y + delta.y
            );

            newPos.x = (int)Mathf.Clamp(newPos.x, 0, _gridWidth - 1);
            newPos.y = (int)Mathf.Clamp(newPos.y, 0, _gridHeight - 1);

            _predictedPosition = newPos;
        }

        public bool Reconcile(uint serverTick, Vector2Int serverPosition, Direction serverDirection)
        {
            _lastServerTick = serverTick;
            _totalReconciliations++;

            _pendingInputs.RemoveAll(input => input.Tick <= serverTick);

            int errorX = Mathf.Abs(_predictedPosition.x - serverPosition.x);
            int errorY = Mathf.Abs(_predictedPosition.y - serverPosition.y);

            if (errorX == 0 && errorY == 0)
            {
                return false;
            }

            _correctionCount++;

            _predictedPosition = serverPosition;
            _currentDirection = serverDirection;

            foreach (var input in _pendingInputs)
            {
                _currentDirection = input.Direction;
                var delta = DirectionDelta(_currentDirection);
                var newPos = new Vector2Int(
                    _predictedPosition.x + delta.x,
                    _predictedPosition.y + delta.y
                );
                newPos.x = (int)Mathf.Clamp(newPos.x, 0, _gridWidth - 1);
                newPos.y = (int)Mathf.Clamp(newPos.y, 0, _gridHeight - 1);
                _predictedPosition = newPos;
            }

            if (_totalReconciliations % 100 == 0)
            {
                Debug.Log($"[ClientPrediction] Stats: {_correctionCount} corrections in {_totalReconciliations} reconciliations " +
                          $"({(float)_correctionCount / _totalReconciliations * 100f:F1}% error rate), " +
                          $"{_pendingInputs.Count} pending inputs");
            }

            return true;
        }

        private static Vector2Int DirectionDelta(Direction dir)
        {
            return dir switch
            {
                Direction.Up => new Vector2Int(0, 1),
                Direction.Down => new Vector2Int(0, -1),
                Direction.Left => new Vector2Int(-1, 0),
                Direction.Right => new Vector2Int(1, 0),
                _ => Vector2Int.zero
            };
        }
    }
}