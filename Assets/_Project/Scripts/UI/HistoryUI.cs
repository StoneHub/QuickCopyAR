using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace QuickCopyAR.UI
{
    /// <summary>
    /// Displays a scrollable list of previous OCR results.
    /// Toggleable UI that follows the user or stays fixed in space.
    /// </summary>
    public class HistoryUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private System.HistoryManager historyManager;
        [SerializeField] private GameObject historyPanel;
        [SerializeField] private RectTransform listContainer;
        [SerializeField] private GameObject historyItemPrefab;
        [SerializeField] private System.ClipboardManager clipboardManager;
        [SerializeField] private ToastNotification toastNotification;

        [Header("Settings")]
        [SerializeField] private bool showOnStart = false;

        private List<GameObject> activeItems = new List<GameObject>();

        private void Start()
        {
            if (historyPanel != null)
            {
                historyPanel.SetActive(showOnStart);
            }

            if (historyManager != null)
            {
                historyManager.OnHistoryChanged += RefreshUI;
            }

            RefreshUI();
        }

        public void ToggleVisibility()
        {
            if (historyPanel != null)
            {
                bool newState = !historyPanel.activeSelf;
                historyPanel.SetActive(newState);
                
                if (newState)
                {
                    RefreshUI();
                }
            }
        }

        public void RefreshUI()
        {
            if (historyManager == null || listContainer == null || historyItemPrefab == null) return;

            // Clear old items
            foreach (var item in activeItems)
            {
                Destroy(item);
            }
            activeItems.Clear();

            // Create new items
            var history = historyManager.GetHistory();
            foreach (var data in history)
            {
                GameObject itemObj = Instantiate(historyItemPrefab, listContainer);
                activeItems.Add(itemObj);

                // Setup item UI
                TextMeshProUGUI textComp = itemObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = $"[{data.Timestamp}]\n{data.Preview}";
                }

                Button itemButton = itemObj.GetComponent<Button>();
                if (itemButton != null)
                {
                    string fullText = data.Text;
                    itemButton.onClick.AddListener(() => CopyFromHistory(fullText));
                }
            }
        }

        private void CopyFromHistory(string text)
        {
            if (clipboardManager != null)
            {
                clipboardManager.CopyToClipboard(text);
                
                if (toastNotification != null)
                {
                    string preview = text.Length > 20 ? text.Substring(0, 17) + "..." : text;
                    toastNotification.Show($"Re-copied: {preview}");
                }
            }
        }

        private void OnDestroy()
        {
            if (historyManager != null)
            {
                historyManager.OnHistoryChanged -= RefreshUI;
            }
        }
    }
}
