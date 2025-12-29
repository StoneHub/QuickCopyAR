using System;
using UnityEngine;

namespace QuickCopyAR.System
{
    /// <summary>
    /// Manages clipboard operations on Android/Quest.
    /// Uses both Unity built-in and native Android clipboard APIs.
    /// </summary>
    public class ClipboardManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool useNativeClipboard = true;
        [SerializeField] private bool logCopiedContent = true;
        [SerializeField] private int maxLogLength = 100;

        public event Action<string> OnTextCopied;
        public event Action<string> OnCopyError;

        private string lastCopiedText = "";

        /// <summary>
        /// Copy text to system clipboard.
        /// </summary>
        public void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Utilities.Logger.LogWarning("ClipboardManager", "Attempted to copy empty text");
                return;
            }

            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (useNativeClipboard)
                {
                    CopyToClipboardNative(text);
                }
                else
                {
                    CopyToClipboardUnity(text);
                }
#else
                CopyToClipboardUnity(text);
#endif

                lastCopiedText = text;
                OnTextCopied?.Invoke(text);

                if (logCopiedContent)
                {
                    string preview = text.Length > maxLogLength
                        ? text.Substring(0, maxLogLength) + "..."
                        : text;
                    Utilities.Logger.Log("ClipboardManager", $"Copied to clipboard: {preview}");
                }
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("ClipboardManager", $"Copy failed: {e.Message}");
                OnCopyError?.Invoke(e.Message);
            }
        }

        /// <summary>
        /// Copy using Unity's built-in clipboard API.
        /// </summary>
        private void CopyToClipboardUnity(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        /// <summary>
        /// Copy using Android's native clipboard API.
        /// More reliable on Quest devices.
        /// </summary>
        private void CopyToClipboardNative(string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                // Get ClipboardManager service
                using (AndroidJavaObject clipboardManager = context.Call<AndroidJavaObject>(
                    "getSystemService", "clipboard"))
                {
                    if (clipboardManager == null)
                    {
                        throw new Exception("Failed to get ClipboardManager service");
                    }

                    // Create ClipData
                    using (AndroidJavaClass clipDataClass = new AndroidJavaClass("android.content.ClipData"))
                    {
                        AndroidJavaObject clipData = clipDataClass.CallStatic<AndroidJavaObject>(
                            "newPlainText",
                            "QuickCopy OCR",
                            text
                        );

                        if (clipData == null)
                        {
                            throw new Exception("Failed to create ClipData");
                        }

                        // Set the clip to clipboard
                        clipboardManager.Call("setPrimaryClip", clipData);
                    }
                }
            }

            Utilities.Logger.Log("ClipboardManager", "Text copied using native Android clipboard");
#else
            // Fallback for non-Android platforms
            CopyToClipboardUnity(text);
#endif
        }

        /// <summary>
        /// Get text from clipboard.
        /// </summary>
        public string GetFromClipboard()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                return GetFromClipboardNative();
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("ClipboardManager", $"Get clipboard failed: {e.Message}");
                return GUIUtility.systemCopyBuffer;
            }
#else
            return GUIUtility.systemCopyBuffer;
#endif
        }

        private string GetFromClipboardNative()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = activity.Call<AndroidJavaObject>("getApplicationContext"))
            using (AndroidJavaObject clipboardManager = context.Call<AndroidJavaObject>(
                "getSystemService", "clipboard"))
            {
                if (clipboardManager == null)
                {
                    return "";
                }

                // Check if clipboard has data
                bool hasClip = clipboardManager.Call<bool>("hasPrimaryClip");
                if (!hasClip)
                {
                    return "";
                }

                // Get primary clip
                using (AndroidJavaObject clipData = clipboardManager.Call<AndroidJavaObject>("getPrimaryClip"))
                {
                    if (clipData == null)
                    {
                        return "";
                    }

                    int itemCount = clipData.Call<int>("getItemCount");
                    if (itemCount <= 0)
                    {
                        return "";
                    }

                    // Get first item
                    using (AndroidJavaObject item = clipData.Call<AndroidJavaObject>("getItemAt", 0))
                    {
                        if (item == null)
                        {
                            return "";
                        }

                        // Get text from item
                        using (AndroidJavaObject charSequence = item.Call<AndroidJavaObject>("getText"))
                        {
                            if (charSequence == null)
                            {
                                return "";
                            }

                            return charSequence.Call<string>("toString");
                        }
                    }
                }
            }
#else
            return "";
#endif
        }

        /// <summary>
        /// Check if clipboard has text content.
        /// </summary>
        public bool HasClipboardText()
        {
            string content = GetFromClipboard();
            return !string.IsNullOrEmpty(content);
        }

        /// <summary>
        /// Get the last text that was copied in this session.
        /// </summary>
        public string GetLastCopiedText()
        {
            return lastCopiedText;
        }

        /// <summary>
        /// Clear the clipboard.
        /// </summary>
        public void ClearClipboard()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                CopyToClipboardNative("");
                Utilities.Logger.Log("ClipboardManager", "Clipboard cleared");
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("ClipboardManager", $"Clear clipboard failed: {e.Message}");
            }
#else
            GUIUtility.systemCopyBuffer = "";
#endif
        }
    }
}
