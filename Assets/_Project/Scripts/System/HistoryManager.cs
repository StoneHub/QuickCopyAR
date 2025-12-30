using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QuickCopyAR.System
{
    /// <summary>
    /// Manages a history of recognized text.
    /// Stores results in memory and provides persistence to a JSON file.
    /// </summary>
    public class HistoryManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxHistoryItems = 10;
        [SerializeField] private bool persistToFile = true;

        private List<HistoryItem> history = new List<HistoryItem>();
        private string HistoryFilePath => Path.Combine(Application.persistentDataPath, "history.json");

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
            if (persistToFile)
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
            
            if (persistToFile)
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
            if (persistToFile && File.Exists(HistoryFilePath))
            {
                try
                {
                    File.Delete(HistoryFilePath);
                }
                catch (Exception e)
                {
                    Utilities.Logger.LogError("HistoryManager", $"Failed to delete history file: {e.Message}");
                }
            }
            OnHistoryChanged?.Invoke();
        }

        private void SaveHistory()
        {
            try
            {
                HistoryWrapper wrapper = new HistoryWrapper { Items = history };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(HistoryFilePath, json);
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
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    HistoryWrapper wrapper = JsonUtility.FromJson<HistoryWrapper>(json);
                    if (wrapper != null && wrapper.Items != null)
                    {
                        history = wrapper.Items;
                    }
                    Utilities.Logger.Log("HistoryManager", $"Loaded {history.Count} items from history");
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
