using System;
using UnityEngine;

namespace QuickCopyAR.OCR
{
    /// <summary>
    /// Bridge between Unity C# and Android ML Kit (Java/Kotlin).
    /// Uses AndroidJavaClass/AndroidJavaObject for native calls.
    /// </summary>
    public class AndroidMLKitPlugin : IDisposable
    {
        private const string PLUGIN_CLASS = "com.flyingchangesfarm.quickcopy.OCRBridge";

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject ocrBridge;
        private AndroidJavaClass bitmapFactoryClass;
#endif

        private bool isInitialized = false;
        private bool isDisposed = false;

        /// <summary>
        /// Initialize the ML Kit text recognizer on Android.
        /// </summary>
        public bool Initialize()
        {
            if (isInitialized) return true;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Utilities.Logger.Log("AndroidMLKitPlugin", "Initializing Android ML Kit plugin...");

                // Get the Unity activity
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    // Create OCRBridge instance
                    using (AndroidJavaClass pluginClass = new AndroidJavaClass(PLUGIN_CLASS))
                    {
                        ocrBridge = pluginClass.CallStatic<AndroidJavaObject>("getInstance", activity);
                    }

                    if (ocrBridge == null)
                    {
                        Utilities.Logger.LogError("AndroidMLKitPlugin", "Failed to create OCRBridge instance");
                        return false;
                    }

                    // Cache BitmapFactory for image conversion
                    bitmapFactoryClass = new AndroidJavaClass("android.graphics.BitmapFactory");

                    isInitialized = true;
                    Utilities.Logger.Log("AndroidMLKitPlugin", "Android ML Kit plugin initialized successfully");
                    return true;
                }
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("AndroidMLKitPlugin", $"Initialization error: {e.Message}");
                return false;
            }
#else
            Utilities.Logger.Log("AndroidMLKitPlugin", "Android ML Kit not available on this platform");
            return false;
#endif
        }

        /// <summary>
        /// Process image bytes and return recognized text.
        /// </summary>
        /// <param name="imageData">JPEG encoded image bytes</param>
        /// <returns>Recognized text or error message prefixed with "ERROR:"</returns>
        public string ProcessImage(byte[] imageData)
        {
            if (!isInitialized)
            {
                return "ERROR: Plugin not initialized";
            }

            if (imageData == null || imageData.Length == 0)
            {
                return "ERROR: Image data is empty";
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Utilities.Logger.Log("AndroidMLKitPlugin", $"Processing image: {imageData.Length} bytes");

                // Convert byte array to Android Bitmap
                AndroidJavaObject bitmap = bitmapFactoryClass.CallStatic<AndroidJavaObject>(
                    "decodeByteArray",
                    imageData,
                    0,
                    imageData.Length
                );

                if (bitmap == null)
                {
                    return "ERROR: Failed to decode image data";
                }

                try
                {
                    // Call ML Kit OCR
                    string result = ocrBridge.Call<string>("processImage", bitmap);

                    Utilities.Logger.Log("AndroidMLKitPlugin", $"OCR result length: {result?.Length ?? 0}");
                    return result ?? "";
                }
                finally
                {
                    // Always recycle the bitmap to free memory
                    bitmap.Call("recycle");
                    bitmap.Dispose();
                }
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("AndroidMLKitPlugin", $"Processing error: {e.Message}");
                return $"ERROR: {e.Message}";
            }
#else
            return "ERROR: Android ML Kit not available on this platform";
#endif
        }

        /// <summary>
        /// Process a Unity Texture2D directly.
        /// </summary>
        public string ProcessTexture(Texture2D texture, int jpegQuality = 90)
        {
            if (texture == null)
            {
                return "ERROR: Texture is null";
            }

            try
            {
                byte[] imageData = texture.EncodeToJPG(jpegQuality);
                return ProcessImage(imageData);
            }
            catch (Exception e)
            {
                return $"ERROR: Failed to encode texture: {e.Message}";
            }
        }

        /// <summary>
        /// Check if the ML Kit model is downloaded and ready.
        /// </summary>
        public bool IsModelReady()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!isInitialized || ocrBridge == null) return false;

            try
            {
                return ocrBridge.Call<bool>("isModelReady");
            }
            catch
            {
                return false;
            }
#else
            return true;
#endif
        }

        /// <summary>
        /// Get JSON result with bounding boxes.
        /// </summary>
        public string ProcessImageWithBounds(byte[] imageData)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!isInitialized) return "ERROR: Plugin not initialized";

            try
            {
                AndroidJavaObject bitmap = bitmapFactoryClass.CallStatic<AndroidJavaObject>(
                    "decodeByteArray",
                    imageData,
                    0,
                    imageData.Length
                );

                if (bitmap == null)
                {
                    return "ERROR: Failed to decode image data";
                }

                try
                {
                    // Call the method that returns JSON with bounds
                    return ocrBridge.Call<string>("processImageWithBounds", bitmap);
                }
                finally
                {
                    bitmap.Call("recycle");
                    bitmap.Dispose();
                }
            }
            catch (Exception e)
            {
                return $"ERROR: {e.Message}";
            }
#else
            return "ERROR: Android ML Kit not available on this platform";
#endif
        }

        public void Dispose()
        {
            if (isDisposed) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (ocrBridge != null)
                {
                    ocrBridge.Call("close");
                    ocrBridge.Dispose();
                    ocrBridge = null;
                }

                if (bitmapFactoryClass != null)
                {
                    bitmapFactoryClass.Dispose();
                    bitmapFactoryClass = null;
                }

                Utilities.Logger.Log("AndroidMLKitPlugin", "Android ML Kit plugin disposed");
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("AndroidMLKitPlugin", $"Disposal error: {e.Message}");
            }
#endif

            isInitialized = false;
            isDisposed = true;
        }

        ~AndroidMLKitPlugin()
        {
            Dispose();
        }
    }
}
