using System.Collections.Generic;
using Core.Services;
using Game.Data;
using Game.Rendering;
using UnityEngine;

namespace Game.UI
{
    public class TerritoryClaimPopupManager : MonoBehaviour, IService
    {
        [Header("Prefab")]
        [SerializeField] private TerritoryClaimPopup popupPrefab;

        [Header("Pool")]
        [SerializeField] private int initialPoolSize = 4;
        [SerializeField] private int maxPoolSize = 10;

        [Header("Claim Threshold")]
        [Tooltip("Minimum cells claimed to show the popup. " +
                 "Prevents spam from trail-only claims (1-2 cells).")]
        [SerializeField] private int minCellsToShow = 3;

        private Canvas _overlayCanvas;

        private readonly Queue<TerritoryClaimPopup> _pool = new();
        private readonly List<TerritoryClaimPopup> _active = new();

        private Camera _mainCamera;
        private IGameWorldDataProvider _gameData;
        private PlayerVisualsManager _playerVisualsManager;
        public void Initialize(ServiceContainer services)
        {
            _playerVisualsManager = services.Get<PlayerVisualsManager>();
            _gameData = services.Get<GameWorld>();

            CreateOverlayCanvas();

            for (int i = 0; i < initialPoolSize; i++)
            {
                var popup = CreatePopup();
                popup.gameObject.SetActive(false);
                _pool.Enqueue(popup);
            }

            _mainCamera = Camera.main;
        }

        public void Tick()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].IsPlaying)
                {
                    var popup = _active[i];
                    popup.ResetForPool();
                    _pool.Enqueue(popup);
                    _active.RemoveAt(i);
                }
            }
        }

        public void TickLate() { }

        public void Dispose()
        {
            foreach (var popup in _active)
            {
                popup.ForceFinish();
            }
            _active.Clear();
            _pool.Clear();
        }

        public void ShowClaimPopup(int cellsClaimed, Color playerColor)
        {
            if (cellsClaimed < minCellsToShow)
            {
                return;
            }

            var localVisual = _playerVisualsManager.LocalPlayerVisual;
            if (localVisual == null)
            {
                return;
            }

            if (_mainCamera == null)
            {
                // TODO: must get reference to camera dedicated to rendering UI, not main
                _mainCamera = Camera.main;
            }
            if (_mainCamera == null)
            {
                return;
            }

            var popup = GetPopup();
            popup.Show(
                localVisual.transform,
                cellsClaimed,
                _gameData.Territory.TotalCells,
                playerColor,
                _mainCamera
            );
            _active.Add(popup);
        }

        private void CreateOverlayCanvas()
        {
            var canvasGO = new GameObject("ClaimPopupCanvas");
            canvasGO.transform.SetParent(transform);

            _overlayCanvas = canvasGO.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 100;

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        }

        private TerritoryClaimPopup GetPopup()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            if (_active.Count < maxPoolSize)
                return CreatePopup();

            var oldest = _active[0];
            oldest.ForceFinish();
            _active.RemoveAt(0);
            return oldest;
        }

        private TerritoryClaimPopup CreatePopup()
        {
            var popup = Instantiate(popupPrefab, _overlayCanvas.transform);
            popup.gameObject.SetActive(false);
            return popup;
        }
    }
}
