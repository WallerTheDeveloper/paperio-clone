using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class NameLabel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("Layout")]
        [SerializeField] private float heightOffset = 80f;

        [Header("Style")]
        [SerializeField] private Color localPlayerColor = new Color(1f, 0.92f, 0.016f);
        [SerializeField] private Color remotePlayerColor = Color.white;

        public void Setup(string playerName, bool isLocal)
        {
            transform.localPosition = new Vector3(0f, heightOffset, 0f);

            if (label != null)
            {
                label.text = playerName;
                label.color = isLocal ? localPlayerColor : remotePlayerColor;
                // Adjust bounds automatically to fit new text
                label.ForceMeshUpdate();
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void ResetForPool()
        {
            if (label != null)
            {
                label.text = "";
            }
        }
    }
}