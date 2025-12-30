using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuickCopyAR.System
{
    /// <summary>
    /// Manages a history of recognized text.
    /// Stores results in memory and provides persistence across sessions.
    /// </summary>
    public class HistoryManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxHistoryItems = 10;
        [SerializeField] private bool persistToPlayerPrefs = true;

        private List<HistoryItem> history = new List<HistoryItem>();
        private const string PREFS_KEY = "QuickCopy_History";

        [Serializable]
        public class HistoryItem
        {
            public string Text;
            public string Timestamp;
            public string Preview;

            public HistoryItem(string text)
            {
                Text = text;
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Preview = text.Length > 50 ? text.Substring(0, 47) + "..." : text;
                Preview = Preview.Replace('\n', ' ').Replace('\r', ' ');
            }
        }

        public event Action OnHistoryChanged;

        private void Awake()
        {
            if (persistToPlayerPrefs)
            {
                LoadHistory();
            }
        }

        /// <summary>
        /// Add recognized text to history.
        /// </summary>
        public void AddEntry(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Don't add if it's identical to the last entry
            if (history.Count > 0 && history[0].Text == text) return;

            HistoryItem item = new HistoryItem(text);
            history.Insert(0, item);

            // Trim history
            if (history.Count > maxHistoryItems)
            {
                history.RemoveAt(history.Count - 1);
            }

            Utilities.Logger.Log("HistoryManager", $"Added to history: {item.Preview}");
            
            if (persistToPlayerPrefs)
            {
                SaveHistory();
            }

            OnHistoryChanged?.Invoke();
        }

        public List<HistoryItem> GetHistory()
        {
            return new List<HistoryItem>(history);
        }

        public void ClearHistory()
        {
            history.Clear();
            if (persistToPlayerPrefs)
            {
                PlayerPrefs.DeleteKey(PREFS_KEY);
                PlayerPrefs.Save();
            }
            OnHistoryChanged?.Invoke();
        }

        private void SaveHistory()
        {
            try
            {
                HistoryWrapper wrapper = new HistoryWrapper { Items = history };
                string json = JsonUtility.ToJson(wrapper);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("HistoryManager", $"Failed to save history: {e.Message}");
            }
        }

        private void LoadHistory()
        {
            try
            {
                if (PlayerPrefs.HasKey(PREFS_KEY))
                {
                    string json = PlayerPrefs.GetString(PREFS_KEY);
                    HistoryWrapper wrapper = JsonUtility.FromJson<HistoryWrapper>(json);
                    if (wrapper != null && wrapper.Items != null)
                    {
                        history = wrapper.Items;
                    }
                }
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("HistoryManager", $"Failed to load history: {e.Message}");
            }
        }

        [Serializable]
        private class HistoryWrapper
        {
            public List<HistoryItem> Items;
        }
    }
}
