using System;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickCopyAR.Capture
{
    /// <summary>
    /// Manages passthrough frame capture from OVRPassthroughLayer.
    /// Handles frame extraction, downscaling, and visual feedback.
    /// </summary>
    public class CaptureManager : MonoBehaviour
    {
        [Header("Capture Settings")]
        [SerializeField] private int targetWidth = 1920;
        [SerializeField] private int targetHeight = 1080;
        [SerializeField] private int jpegQuality = 90;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject flashOverlay;
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private AudioSource captureAudioSource;
        [SerializeField] private AudioClip captureSound;

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        private RenderTexture captureRenderTexture;
        private Texture2D captureTexture;
        private bool isCapturing = false;

        public event Action OnCaptureStarted;
        public event Action<Texture2D> OnCaptureCompleted;
        public event Action<string> OnCaptureError;

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            InitializeCaptureResources();
        }

        private void InitializeCaptureResources()
        {
            // Create reusable render texture for captures
            captureRenderTexture = new RenderTexture(targetWidth, targetHeight, 24, RenderTextureFormat.ARGB32);
            captureRenderTexture.antiAliasing = 1;
            captureRenderTexture.Create();

            // Create reusable texture for reading pixels
            captureTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);

            Utilities.Logger.Log("CaptureManager", $"Initialized capture resources: {targetWidth}x{targetHeight}");
        }

        /// <summary>
        /// Captures the current passthrough frame asynchronously.
        /// Returns a new Texture2D that the caller must destroy when done.
        /// </summary>
        public async Task<Texture2D> CaptureFrameAsync()
        {
            if (isCapturing)
            {
                Utilities.Logger.LogWarning("CaptureManager", "Capture already in progress");
                return null;
            }

            isCapturing = true;
            OnCaptureStarted?.Invoke();

            try
            {
                // Play capture sound
                PlayCaptureSound();

                // Show flash effect
                ShowFlashEffect();

                // Wait for end of frame to ensure rendering is complete
                await Task.Yield();

                // Capture the frame
                Texture2D result = CaptureCurrentFrame();

                if (result == null)
                {
                    throw new Exception("Failed to capture frame from camera");
                }

                OnCaptureCompleted?.Invoke(result);
                Utilities.Logger.Log("CaptureManager", $"Frame captured: {result.width}x{result.height}");

                return result;
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("CaptureManager", $"Capture error: {e.Message}");
                OnCaptureError?.Invoke(e.Message);
                return null;
            }
            finally
            {
                isCapturing = false;
            }
        }

        private Texture2D CaptureCurrentFrame()
        {
            // Method 1: Try to capture from OVR Passthrough if available
            Texture2D passthroughCapture = TryCaptureFromPassthrough();
            if (passthroughCapture != null)
            {
                return passthroughCapture;
            }

            // Method 2: Fallback to screen capture
            return CaptureFromScreen();
        }

        private Texture2D TryCaptureFromPassthrough()
        {
            // Note: OVRPassthroughLayer doesn't directly expose camera texture
            // On Quest, we need to use the central camera or mixed reality capture
            // This is a placeholder for the actual Meta passthrough API

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Try to get OVRPlugin passthrough texture
                // This requires Meta's Mixed Reality Capture API
                // For now, we fall back to screen capture which includes passthrough
                return null;
            }
            catch (Exception e)
            {
                Utilities.Logger.LogWarning("CaptureManager", $"Passthrough capture failed: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        private Texture2D CaptureFromScreen()
        {
            try
            {
                // Store original camera settings
                RenderTexture originalTarget = mainCamera.targetTexture;

                // Render to our capture texture
                mainCamera.targetTexture = captureRenderTexture;
                mainCamera.Render();
                mainCamera.targetTexture = originalTarget;

                // Read pixels from render texture
                RenderTexture.active = captureRenderTexture;
                captureTexture.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                captureTexture.Apply();
                RenderTexture.active = null;

                // Create a new texture copy to return (caller will destroy it)
                Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                result.SetPixels(captureTexture.GetPixels());
                result.Apply();

                // Apply image enhancement
                result = Utilities.ImageConverter.AutoContrast(result);

                return result;
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("CaptureManager", $"Screen capture failed: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Alternative capture method using ScreenCapture API.
        /// Use this for full passthrough capture on Quest.
        /// </summary>
        public Texture2D CaptureScreenshot()
        {
            try
            {
                // Create screenshot texture
                Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);

                // Read screen pixels
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                screenshot.Apply();

                // Downscale if needed
                if (Screen.width > targetWidth || Screen.height > targetHeight)
                {
                    Texture2D downscaled = Utilities.ImageConverter.Downscale(screenshot, targetWidth, targetHeight);
                    Destroy(screenshot);
                    screenshot = downscaled;
                }

                return screenshot;
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("CaptureManager", $"Screenshot failed: {e.Message}");
                return null;
            }
        }

        private void ShowFlashEffect()
        {
            if (flashOverlay != null)
            {
                StartCoroutine(FlashCoroutine());
            }
        }

        private System.Collections.IEnumerator FlashCoroutine()
        {
            flashOverlay.SetActive(true);

            // Quick fade in
            CanvasGroup canvasGroup = flashOverlay.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float elapsed = 0f;
                while (elapsed < flashDuration / 2f)
                {
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / (flashDuration / 2f));
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Quick fade out
                elapsed = 0f;
                while (elapsed < flashDuration / 2f)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / (flashDuration / 2f));
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(flashDuration);
            }

            flashOverlay.SetActive(false);
        }

        private void PlayCaptureSound()
        {
            if (captureAudioSource != null && captureSound != null)
            {
                captureAudioSource.PlayOneShot(captureSound);
            }
        }

        /// <summary>
        /// Gets the raw camera frame as byte array (JPEG encoded).
        /// Useful for direct ML Kit processing.
        /// </summary>
        public byte[] CaptureFrameAsJPEG()
        {
            Texture2D frame = CaptureFromScreen();
            if (frame == null) return null;

            try
            {
                byte[] jpeg = frame.EncodeToJPG(jpegQuality);
                return jpeg;
            }
            finally
            {
                Destroy(frame);
            }
        }

        private void OnDestroy()
        {
            if (captureRenderTexture != null)
            {
                captureRenderTexture.Release();
                Destroy(captureRenderTexture);
            }
            if (captureTexture != null)
            {
                Destroy(captureTexture);
            }
        }
    }
}
