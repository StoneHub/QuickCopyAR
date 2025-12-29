using System;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickCopyAR.Core
{
    /// <summary>
    /// Main application controller that coordinates all subsystems.
    /// Singleton pattern ensures single instance throughout app lifecycle.
    /// </summary>
    public class AppManager : MonoBehaviour
    {
        public static AppManager Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private Capture.CaptureManager captureManager;
        [SerializeField] private OCR.OCRProcessor ocrProcessor;
        [SerializeField] private System.ClipboardManager clipboardManager;
        [SerializeField] private UI.FloatingButton floatingButton;
        [SerializeField] private UI.ToastNotification toastNotification;
        [SerializeField] private UI.TextHighlighter textHighlighter;
        [SerializeField] private Input.InputManager inputManager;

        [Header("Settings")]
        [SerializeField] private float freezeDuration = 0.5f;
        [SerializeField] private int previewCharLimit = 30;

        public enum AppState
        {
            Idle,
            Capturing,
            Processing,
            Copied,
            Error
        }

        public AppState CurrentState { get; private set; } = AppState.Idle;
        public event Action<AppState> OnStateChanged;

        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            Utilities.Logger.Log("AppManager", "Initializing QuickCopy AR...");

            try
            {
                // Initialize OCR processor (ML Kit)
                bool ocrReady = await ocrProcessor.InitializeAsync();
                if (!ocrReady)
                {
                    Utilities.Logger.LogError("AppManager", "Failed to initialize OCR processor");
                    ShowToast("OCR initialization failed. Please restart app.");
                    return;
                }

                // Subscribe to input events
                if (inputManager != null)
                {
                    inputManager.OnCaptureTriggered += HandleCaptureTriggered;
                }

                // Subscribe to button events
                if (floatingButton != null)
                {
                    floatingButton.OnButtonPressed += HandleCaptureTriggered;
                }

                isInitialized = true;
                SetState(AppState.Idle);
                Utilities.Logger.Log("AppManager", "QuickCopy AR initialized successfully");
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("AppManager", $"Initialization error: {e.Message}");
                ShowToast("Initialization failed. Please restart app.");
            }
        }

        private async void HandleCaptureTriggered()
        {
            if (!isInitialized || CurrentState != AppState.Idle)
            {
                Utilities.Logger.Log("AppManager", $"Capture ignored - State: {CurrentState}, Initialized: {isInitialized}");
                return;
            }

            await CaptureAndProcessAsync();
        }

        public async Task CaptureAndProcessAsync()
        {
            var startTime = DateTime.Now;
            Utilities.Logger.Log("AppManager", "Starting capture and process...");

            try
            {
                // Phase 1: Capture
                SetState(AppState.Capturing);
                ShowToast("Capturing...");

                Texture2D capturedFrame = await captureManager.CaptureFrameAsync();
                if (capturedFrame == null)
                {
                    throw new Exception("Failed to capture frame");
                }

                // Freeze effect
                await Task.Delay((int)(freezeDuration * 1000));

                // Phase 2: Processing
                SetState(AppState.Processing);
                ShowToast("Processing...");

                OCR.OCRResult result = await ocrProcessor.ProcessImageAsync(capturedFrame);

                // Cleanup captured texture
                Destroy(capturedFrame);

                // Phase 3: Handle result
                if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.RecognizedText))
                {
                    // Highlight detected text regions
                    if (textHighlighter != null && result.TextBlocks != null)
                    {
                        textHighlighter.ShowHighlights(result.TextBlocks);
                    }

                    // Copy to clipboard
                    clipboardManager.CopyToClipboard(result.RecognizedText);

                    // Show success message
                    string preview = TruncateText(result.RecognizedText, previewCharLimit);
                    ShowToast($"Copied: {preview}");

                    SetState(AppState.Copied);
                    PlaySuccessFeedback();

                    var processingTime = DateTime.Now - startTime;
                    Utilities.Logger.Log("AppManager",
                        $"OCR completed in {processingTime.TotalMilliseconds:F0}ms. " +
                        $"Text length: {result.RecognizedText.Length} chars");
                }
                else if (!result.IsSuccess)
                {
                    throw new Exception(result.ErrorMessage ?? "Unknown OCR error");
                }
                else
                {
                    ShowToast("No text detected. Point at clearer text.");
                    SetState(AppState.Error);
                    PlayErrorFeedback();
                }
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("AppManager", $"Capture/process error: {e.Message}");

                string userMessage = GetUserFriendlyErrorMessage(e.Message);
                ShowToast(userMessage);
                SetState(AppState.Error);
                PlayErrorFeedback();
            }

            // Return to idle after brief delay
            await Task.Delay(500);
            SetState(AppState.Idle);
        }

        private void SetState(AppState newState)
        {
            if (CurrentState != newState)
            {
                Utilities.Logger.Log("AppManager", $"State: {CurrentState} -> {newState}");
                CurrentState = newState;
                OnStateChanged?.Invoke(newState);
                UpdateUIForState(newState);
            }
        }

        private void UpdateUIForState(AppState state)
        {
            if (floatingButton != null)
            {
                switch (state)
                {
                    case AppState.Idle:
                        floatingButton.SetReady();
                        break;
                    case AppState.Capturing:
                    case AppState.Processing:
                        floatingButton.SetProcessing();
                        break;
                    case AppState.Copied:
                        floatingButton.SetSuccess();
                        break;
                    case AppState.Error:
                        floatingButton.SetError();
                        break;
                }
            }
        }

        private void ShowToast(string message)
        {
            if (toastNotification != null)
            {
                toastNotification.Show(message);
            }
            Utilities.Logger.Log("Toast", message);
        }

        private void PlaySuccessFeedback()
        {
            if (inputManager != null)
            {
                inputManager.PlayHapticSuccess();
            }
        }

        private void PlayErrorFeedback()
        {
            if (inputManager != null)
            {
                inputManager.PlayHapticError();
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Replace newlines with spaces for preview
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength) + "...";
        }

        private string GetUserFriendlyErrorMessage(string technicalMessage)
        {
            if (technicalMessage.Contains("model"))
            {
                return "Downloading OCR model... Please wait and try again.";
            }
            if (technicalMessage.Contains("memory") || technicalMessage.Contains("OutOfMemory"))
            {
                return "Out of memory. Please restart the app.";
            }
            if (technicalMessage.Contains("camera") || technicalMessage.Contains("passthrough"))
            {
                return "Camera access error. Check permissions.";
            }
            return "Error processing image. Please try again.";
        }

        private void OnDestroy()
        {
            if (inputManager != null)
            {
                inputManager.OnCaptureTriggered -= HandleCaptureTriggered;
            }
            if (floatingButton != null)
            {
                floatingButton.OnButtonPressed -= HandleCaptureTriggered;
            }
        }

        private void OnApplicationQuit()
        {
            Utilities.Logger.Log("AppManager", "Application quitting, cleaning up...");
            ocrProcessor?.Cleanup();
        }
    }
}
