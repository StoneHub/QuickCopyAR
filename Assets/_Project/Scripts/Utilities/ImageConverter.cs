using UnityEngine;

namespace QuickCopyAR.Utilities
{
    /// <summary>
    /// Image processing utilities for texture conversion and enhancement.
    /// Handles downscaling, format conversion, and image optimization.
    /// </summary>
    public static class ImageConverter
    {
        /// <summary>
        /// Downscale texture to fit within max dimensions while maintaining aspect ratio.
        /// Creates a new texture - caller must destroy the original if no longer needed.
        /// </summary>
        public static Texture2D Downscale(Texture2D source, int maxWidth, int maxHeight)
        {
            if (source == null) return null;

            // Calculate scale factor
            float widthScale = (float)maxWidth / source.width;
            float heightScale = (float)maxHeight / source.height;
            float scale = Mathf.Min(widthScale, heightScale, 1f); // Don't upscale

            if (scale >= 1f)
            {
                // No downscaling needed, return a copy
                Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                copy.SetPixels(source.GetPixels());
                copy.Apply();
                return copy;
            }

            int newWidth = Mathf.RoundToInt(source.width * scale);
            int newHeight = Mathf.RoundToInt(source.height * scale);

            // Ensure minimum size
            newWidth = Mathf.Max(newWidth, 1);
            newHeight = Mathf.Max(newHeight, 1);

            // Use RenderTexture for GPU-accelerated scaling
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            // Read pixels from render texture
            Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            scaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            scaled.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return scaled;
        }

        /// <summary>
        /// Apply auto-contrast enhancement to improve OCR accuracy.
        /// Stretches histogram to use full brightness range.
        /// </summary>
        public static Texture2D AutoContrast(Texture2D input)
        {
            if (input == null) return null;

            Color[] pixels = input.GetPixels();
            int pixelCount = pixels.Length;

            // Find min/max brightness using sampling for performance
            float minBrightness = 1f;
            float maxBrightness = 0f;
            int sampleStep = Mathf.Max(1, pixelCount / 10000); // Sample up to 10000 pixels

            for (int i = 0; i < pixelCount; i += sampleStep)
            {
                float brightness = pixels[i].grayscale;
                if (brightness < minBrightness) minBrightness = brightness;
                if (brightness > maxBrightness) maxBrightness = brightness;
            }

            float range = maxBrightness - minBrightness;

            // Only apply if there's enough range to stretch
            if (range < 0.01f)
            {
                return input;
            }

            // Skip if already using most of the range
            if (minBrightness < 0.1f && maxBrightness > 0.9f)
            {
                return input;
            }

            // Stretch histogram
            float invRange = 1f / range;
            for (int i = 0; i < pixelCount; i++)
            {
                Color p = pixels[i];
                float brightness = p.grayscale;
                float normalized = (brightness - minBrightness) * invRange;
                normalized = Mathf.Clamp01(normalized);

                // Apply same factor to all channels to preserve color (if any)
                float factor = brightness > 0.001f ? normalized / brightness : 1f;
                pixels[i] = new Color(
                    Mathf.Clamp01(p.r * factor),
                    Mathf.Clamp01(p.g * factor),
                    Mathf.Clamp01(p.b * factor),
                    p.a
                );
            }

            input.SetPixels(pixels);
            input.Apply();
            return input;
        }

        /// <summary>
        /// Convert texture to grayscale for OCR processing.
        /// Can improve accuracy by removing color distractions.
        /// </summary>
        public static Texture2D ToGrayscale(Texture2D input)
        {
            if (input == null) return null;

            Color[] pixels = input.GetPixels();

            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = pixels[i].grayscale;
                pixels[i] = new Color(gray, gray, gray, 1f);
            }

            input.SetPixels(pixels);
            input.Apply();
            return input;
        }

        /// <summary>
        /// Apply sharpening filter to improve text edge clarity.
        /// </summary>
        public static Texture2D Sharpen(Texture2D input, float strength = 1f)
        {
            if (input == null) return null;

            int width = input.width;
            int height = input.height;
            Color[] original = input.GetPixels();
            Color[] result = new Color[original.Length];

            // Sharpening kernel
            float center = 1f + 4f * strength;
            float edge = -strength;

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int i = y * width + x;

                    Color c = original[i] * center;
                    c += original[i - 1] * edge; // left
                    c += original[i + 1] * edge; // right
                    c += original[i - width] * edge; // up
                    c += original[i + width] * edge; // down

                    result[i] = new Color(
                        Mathf.Clamp01(c.r),
                        Mathf.Clamp01(c.g),
                        Mathf.Clamp01(c.b),
                        1f
                    );
                }
            }

            // Copy edges unchanged
            for (int x = 0; x < width; x++)
            {
                result[x] = original[x]; // Top edge
                result[(height - 1) * width + x] = original[(height - 1) * width + x]; // Bottom edge
            }
            for (int y = 0; y < height; y++)
            {
                result[y * width] = original[y * width]; // Left edge
                result[y * width + width - 1] = original[y * width + width - 1]; // Right edge
            }

            input.SetPixels(result);
            input.Apply();
            return input;
        }

        /// <summary>
        /// Apply threshold to create binary image (good for high contrast text).
        /// </summary>
        public static Texture2D Threshold(Texture2D input, float threshold = 0.5f)
        {
            if (input == null) return null;

            Color[] pixels = input.GetPixels();

            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = pixels[i].grayscale;
                float binary = gray > threshold ? 1f : 0f;
                pixels[i] = new Color(binary, binary, binary, 1f);
            }

            input.SetPixels(pixels);
            input.Apply();
            return input;
        }

        /// <summary>
        /// Encode texture to JPEG bytes.
        /// </summary>
        public static byte[] ToJPEG(Texture2D texture, int quality = 90)
        {
            if (texture == null) return null;
            return texture.EncodeToJPG(quality);
        }

        /// <summary>
        /// Encode texture to PNG bytes.
        /// </summary>
        public static byte[] ToPNG(Texture2D texture)
        {
            if (texture == null) return null;
            return texture.EncodeToPNG();
        }

        /// <summary>
        /// Create texture from JPEG bytes.
        /// </summary>
        public static Texture2D FromJPEG(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(data))
            {
                return texture;
            }

            Object.Destroy(texture);
            return null;
        }

        /// <summary>
        /// Rotate texture by 90, 180, or 270 degrees.
        /// </summary>
        public static Texture2D Rotate(Texture2D source, int degrees)
        {
            if (source == null) return null;

            // Normalize degrees
            degrees = ((degrees % 360) + 360) % 360;

            if (degrees == 0)
            {
                Texture2D copy = new Texture2D(source.width, source.height, source.format, false);
                copy.SetPixels(source.GetPixels());
                copy.Apply();
                return copy;
            }

            int width = source.width;
            int height = source.height;
            Color[] sourcePixels = source.GetPixels();

            int newWidth = (degrees == 90 || degrees == 270) ? height : width;
            int newHeight = (degrees == 90 || degrees == 270) ? width : height;

            Texture2D rotated = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            Color[] rotatedPixels = new Color[newWidth * newHeight];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIndex = y * width + x;
                    int dstX, dstY;

                    switch (degrees)
                    {
                        case 90:
                            dstX = height - 1 - y;
                            dstY = x;
                            break;
                        case 180:
                            dstX = width - 1 - x;
                            dstY = height - 1 - y;
                            break;
                        case 270:
                            dstX = y;
                            dstY = width - 1 - x;
                            break;
                        default:
                            dstX = x;
                            dstY = y;
                            break;
                    }

                    int dstIndex = dstY * newWidth + dstX;
                    rotatedPixels[dstIndex] = sourcePixels[srcIndex];
                }
            }

            rotated.SetPixels(rotatedPixels);
            rotated.Apply();
            return rotated;
        }

        /// <summary>
        /// Crop texture to specified region.
        /// </summary>
        public static Texture2D Crop(Texture2D source, Rect region)
        {
            if (source == null) return null;

            int x = Mathf.Clamp((int)region.x, 0, source.width - 1);
            int y = Mathf.Clamp((int)region.y, 0, source.height - 1);
            int width = Mathf.Clamp((int)region.width, 1, source.width - x);
            int height = Mathf.Clamp((int)region.height, 1, source.height - y);

            Color[] pixels = source.GetPixels(x, y, width, height);

            Texture2D cropped = new Texture2D(width, height, TextureFormat.RGB24, false);
            cropped.SetPixels(pixels);
            cropped.Apply();
            return cropped;
        }
    }
}
