using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace QuickCopyAR.OCR
{
    /// <summary>
    /// Processes images through ML Kit text recognition.
    /// Handles initialization, image conversion, and result parsing.
    /// </summary>
    public class OCRProcessor : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int maxImageWidth = 1920;
        [SerializeField] private int maxImageHeight = 1080;
        [SerializeField] private int jpegQuality = 90;
        [SerializeField] private float minConfidenceThreshold = 0.3f;

        private AndroidMLKitPlugin mlKitPlugin;
        private bool isInitialized = false;
        private bool isProcessing = false;

        public bool IsReady => isInitialized && !isProcessing;

        public async Task<bool> InitializeAsync()
        {
            Utilities.Logger.Log("OCRProcessor", "Initializing ML Kit...");

            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                mlKitPlugin = new AndroidMLKitPlugin();
                bool success = mlKitPlugin.Initialize();

                if (!success)
                {
                    Utilities.Logger.LogError("OCRProcessor", "Failed to initialize ML Kit plugin");
                    return false;
                }

                isInitialized = true;
                Utilities.Logger.Log("OCRProcessor", "ML Kit initialized successfully");
                return true;
#else
                // Editor/non-Android fallback
                await Task.Delay(100); // Simulate initialization
                isInitialized = true;
                Utilities.Logger.Log("OCRProcessor", "OCR initialized (Editor mode - mock)");
                return true;
#endif
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("OCRProcessor", $"ML Kit initialization error: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process an image and return recognized text.
        /// </summary>
        public async Task<OCRResult> ProcessImageAsync(Texture2D image)
        {
            if (!isInitialized)
            {
                return OCRResult.Error("OCR not initialized");
            }

            if (isProcessing)
            {
                return OCRResult.Error("Already processing an image");
            }

            if (image == null)
            {
                return OCRResult.Error("Image is null");
            }

            isProcessing = true;
            var startTime = DateTime.Now;

            try
            {
                Utilities.Logger.Log("OCRProcessor", $"Processing image: {image.width}x{image.height}");

                // Downscale if needed
                Texture2D processImage = image;
                bool needsDestroy = false;

                if (image.width > maxImageWidth || image.height > maxImageHeight)
                {
                    processImage = Utilities.ImageConverter.Downscale(image, maxImageWidth, maxImageHeight);
                    needsDestroy = true;
                    Utilities.Logger.Log("OCRProcessor", $"Downscaled to: {processImage.width}x{processImage.height}");
                }

#if UNITY_ANDROID && !UNITY_EDITOR
                // Process with ML Kit on Android
                OCRResult result = await ProcessWithMLKitAsync(processImage);
#else
                // Mock processing in editor
                OCRResult result = await MockProcessAsync(processImage);
#endif

                // Cleanup
                if (needsDestroy)
                {
                    Destroy(processImage);
                }

                var processingTime = (float)(DateTime.Now - startTime).TotalMilliseconds;
                result.ProcessingTimeMs = processingTime;
                result.ImageWidth = image.width;
                result.ImageHeight = image.height;

                Utilities.Logger.Log("OCRProcessor",
                    $"OCR completed in {processingTime:F0}ms. Found {result.TextBlocks?.Count ?? 0} blocks");

                return result;
            }
            catch (Exception e)
            {
                Utilities.Logger.LogError("OCRProcessor", $"Processing error: {e.Message}");
                return OCRResult.Error(e.Message);
            }
            finally
            {
                isProcessing = false;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async Task<OCRResult> ProcessWithMLKitAsync(Texture2D image)
        {
            // Run ML Kit processing on background thread
            return await Task.Run(() =>
            {
                try
                {
                    // Convert texture to JPEG bytes
                    byte[] imageData = image.EncodeToJPG(jpegQuality);

                    // Process with ML Kit
                    string jsonResult = mlKitPlugin.ProcessImage(imageData);

                    if (string.IsNullOrEmpty(jsonResult))
                    {
                        return OCRResult.Empty();
                    }

                    if (jsonResult.StartsWith("ERROR:"))
                    {
                        return OCRResult.Error(jsonResult.Substring(7));
                    }

                    // Parse JSON result
                    return ParseMLKitResult(jsonResult);
                }
                catch (Exception e)
                {
                    return OCRResult.Error(e.Message);
                }
            });
        }

        private OCRResult ParseMLKitResult(string jsonResult)
        {
            try
            {
                // Parse the JSON result from ML Kit
                // Format: {"text": "...", "blocks": [...]}
                var result = JsonUtility.FromJson<MLKitResponse>(jsonResult);

                if (result == null)
                {
                    // Simple string result (just the text)
                    return OCRResult.Success(jsonResult, null, 0);
                }

                List<TextBlock> blocks = new List<TextBlock>();
                if (result.blocks != null)
                {
                    foreach (var block in result.blocks)
                    {
                        blocks.Add(new TextBlock
                        {
                            Text = block.text,
                            BoundingBox = new Rect(block.left, block.top, block.width, block.height),
                            Confidence = block.confidence
                        });
                    }
                }

                return OCRResult.Success(result.text, blocks, 0);
            }
            catch
            {
                // If JSON parsing fails, treat as plain text
                return OCRResult.Success(jsonResult, null, 0);
            }
        }

        [Serializable]
        private class MLKitResponse
        {
            public string text;
            public MLKitBlock[] blocks;
        }

        [Serializable]
        private class MLKitBlock
        {
            public string text;
            public float left;
            public float top;
            public float width;
            public float height;
            public float confidence;
        }
#endif

        private async Task<OCRResult> MockProcessAsync(Texture2D image)
        {
            // Simulate processing delay in editor
            await Task.Delay(UnityEngine.Random.Range(100, 300));

            // Return mock data for testing
            var mockBlocks = new List<TextBlock>
            {
                new TextBlock
                {
                    Text = "Hello World",
                    BoundingBox = new Rect(100, 100, 200, 50),
                    Confidence = 0.95f
                },
                new TextBlock
                {
                    Text = "Sample OCR Text",
                    BoundingBox = new Rect(100, 200, 250, 50),
                    Confidence = 0.92f
                }
            };

            return OCRResult.Success("Hello World\nSample OCR Text", mockBlocks, 0);
        }

        /// <summary>
        /// Quick processing method that returns just the text.
        /// </summary>
        public async Task<string> RecognizeTextAsync(Texture2D image)
        {
            var result = await ProcessImageAsync(image);
            return result.IsSuccess ? result.RecognizedText : "";
        }

        public void Cleanup()
        {
            Utilities.Logger.Log("OCRProcessor", "Cleaning up ML Kit resources...");

#if UNITY_ANDROID && !UNITY_EDITOR
            mlKitPlugin?.Dispose();
#endif

            isInitialized = false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
