using System.Collections.Generic;
using Core.Services;
using Game.Paperio;
using UnityEngine;

namespace Game.UI.Leaderboard
{
    public class LeaderboardUI : MonoBehaviour, IService
    {
        [SerializeField] private LeaderboardRow rowPrefab;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private int maxRows = 5;
        [SerializeField] private int refreshEveryTicks = 4;

        private GameWorld _gameWorld;
        private LeaderboardRow[] _rows;
        private uint _localPlayerId;
        private uint _totalCells;
        private int _ticksSinceRefresh;

        private readonly List<LeaderboardEntry> _sortBuffer = new(16);

        public void Initialize(ServiceContainer services)
        {
            _gameWorld = services.Get<GameWorld>();
            _gameWorld.OnGameStarted += HandleGameStarted;
            _gameWorld.OnStateRefreshed += HandleStateUpdated;
        }

        public void Dispose()
        {
            if (_gameWorld == null) return;
            _gameWorld.OnGameStarted -= HandleGameStarted;
            _gameWorld.OnStateRefreshed -= HandleStateUpdated;
        }

        private void Awake()
        {
            BuildRows();
        }

        private void BuildRows()
        {
            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }

            _rows = new LeaderboardRow[maxRows];
            for (var i = 0; i < maxRows; i++)
            {
                _rows[i] = Instantiate(rowPrefab, rowContainer);
                _rows[i].gameObject.SetActive(false);
            }
        }

        private void HandleGameStarted()
        {
            _localPlayerId = _gameWorld.LocalPlayerId;
            _totalCells = _gameWorld.GridWidth * _gameWorld.GridHeight;
        }

        private void HandleStateUpdated(PaperioState state)
        {
            _ticksSinceRefresh++;
            if (_ticksSinceRefresh < refreshEveryTicks)
            {
                return;
            }
            _ticksSinceRefresh = 0;

            BuildSortBuffer(state);
            RefreshRows();
        }

        private void BuildSortBuffer(PaperioState state)
        {
            _sortBuffer.Clear();

            var divisor = _totalCells > 0 ? _totalCells : 1f;

            foreach (var player in state.Players)
            {
                if (!player.Alive)
                {
                    continue;
                }

                var pct = player.Score / divisor * 100f;
                var color = _gameWorld.PlayerColors.TryGetValue(player.PlayerId, out var c)
                    ? c
                    : Color.white;

                _sortBuffer.Add(new LeaderboardEntry(
                    player.PlayerId,
                    player.Name,
                    pct,
                    color,
                    player.PlayerId == _localPlayerId
                ));
            }

            _sortBuffer.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
        }

        private void RefreshRows()
        {
            var visible = Mathf.Min(_sortBuffer.Count, maxRows);

            for (var i = 0; i < maxRows; i++)
            {
                if (i < visible)
                {
                    _rows[i].gameObject.SetActive(true);
                    _rows[i].Populate(i + 1, _sortBuffer[i]);
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }
    }
}