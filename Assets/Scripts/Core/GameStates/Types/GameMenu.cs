using System;
using Core.Services;
using Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.GameStates.Types
{
    public class GameMenu : GameState
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button playButton;

        [Header("Settings")]
        [SerializeField] private int maxNameLength = 16;
        [SerializeField] private string defaultName = "Player";

        public override Action TriggerStateSwitch { get; set; }

        private MessageSender _messageSender;

        public override void Initialize(ServiceContainer container)
        {
            _messageSender = container.Get<MessageSender>();

            menuPanel.SetActive(true);

            nameInputField.characterLimit = maxNameLength;
            nameInputField.onValueChanged.AddListener(OnNameChanged);

            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(savedName))
            {
                nameInputField.text = savedName;
            }

            UpdatePlayButtonState();
            playButton.onClick.AddListener(OnPlayClicked);

            nameInputField.Select();
            nameInputField.ActivateInputField();

            Debug.Log("[GameMenu] Menu opened — waiting for player input");
        }

        public override void Tick()
        { }

        public override void Stop()
        {
            menuPanel.SetActive(false);

            nameInputField.onValueChanged.RemoveListener(OnNameChanged);
            playButton.onClick.RemoveListener(OnPlayClicked);
        }

        private void OnNameChanged(string newName)
        {
            UpdatePlayButtonState();
        }

        private void OnPlayClicked()
        {
            string trimmedName = nameInputField.text.Trim();
            if (string.IsNullOrEmpty(trimmedName))
            {
                trimmedName = defaultName;
            }

            PlayerPrefs.SetString("PlayerName", trimmedName);
            PlayerPrefs.Save();

            _messageSender.SetPlayerName(trimmedName);

            Debug.Log($"[GameMenu] Player name set to '{trimmedName}', transitioning to JoinRoom");
            TriggerStateSwitch?.Invoke();
        }

        private void UpdatePlayButtonState()
        {
            playButton.interactable = true;
        }
    }
}