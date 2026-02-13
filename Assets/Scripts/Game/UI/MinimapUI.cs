using Game.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private string percentageFormat = "{0:F1}%";

        private IGameWorldDataProvider _gameData;
        private bool _isInitialized;

        public void Initialize(IGameWorldDataProvider gameData)
        {
            _gameData = gameData;
            _isInitialized = true;
        }

        private void LateUpdate()
        {
            if (!_isInitialized)
            {
                return;
            }
            
            UpdatePercentage();
        }

        private void UpdatePercentage()
        {
            var territory = _gameData.Territory;
            
            float pct = territory.GetOwnershipPercentage(_gameData.LocalPlayerId);
            percentageText.text = string.Format(percentageFormat, pct);
        }
    }
}