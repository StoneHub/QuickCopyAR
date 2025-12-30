using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QuickCopyAR.UI
{
    /// <summary>
    /// Overlays bounding boxes on detected text regions.
    /// Shows during capture freeze to indicate what was detected.
    /// </summary>
    public class TextHighlighter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private RectTransform highlightContainer;
        [SerializeField] private GameObject highlightPrefab;

        [Header("Styling")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0f, 0.3f); // Yellow, semi-transparent
        [SerializeField] private Color borderColor = new Color(1f, 0.8f, 0f, 0.8f); // Orange border
        [SerializeField] private float borderWidth = 2f;

        [Header("Animation")]
        [SerializeField] private float showDuration = 0.5f;
        [SerializeField] private float fadeInTime = 0.1f;
        [SerializeField] private float fadeOutTime = 0.2f;

        [Header("Screen Mapping")]
        [SerializeField] private int sourceImageWidth = 1920;
        [SerializeField] private int sourceImageHeight = 1080;

        private List<GameObject> activeHighlights = new List<GameObject>();
        private Coroutine displayCoroutine;

        private void Awake()
        {
            // Create default highlight prefab if not assigned
            if (highlightPrefab == null)
            {
                CreateDefaultHighlightPrefab();
            }

            // Ensure container exists
            if (highlightContainer == null && overlayCanvas != null)
            {
                GameObject container = new GameObject("HighlightContainer");
                container.transform.SetParent(overlayCanvas.transform, false);
                highlightContainer = container.AddComponent<RectTransform>();
                highlightContainer.anchorMin = Vector2.zero;
                highlightContainer.anchorMax = Vector2.one;
                highlightContainer.sizeDelta = Vector2.zero;
            }
        }

        private void CreateDefaultHighlightPrefab()
        {
            highlightPrefab = new GameObject("HighlightPrefab");
            highlightPrefab.SetActive(false);

            RectTransform rect = highlightPrefab.AddComponent<RectTransform>();

            // Background image
            Image bgImage = highlightPrefab.AddComponent<Image>();
            bgImage.color = highlightColor;

            // Border (using Outline component)
            Outline outline = highlightPrefab.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(borderWidth, -borderWidth);

            // Canvas group for fading
            highlightPrefab.AddComponent<CanvasGroup>();

            // Keep as resource
            DontDestroyOnLoad(highlightPrefab);
        }

        /// <summary>
        /// Show highlights for detected text blocks.
        /// </summary>
        public void ShowHighlights(List<OCR.TextBlock> textBlocks)
        {
            if (textBlocks == null || textBlocks.Count == 0) return;

            // Cancel any existing display
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            // Clear existing highlights
            ClearHighlights();

            // Create new highlights
            foreach (var block in textBlocks)
            {
                CreateHighlight(block);
            }

            // Start display coroutine
            displayCoroutine = StartCoroutine(DisplayCoroutine());
        }

        private void CreateHighlight(OCR.TextBlock block)
        {
            if (highlightPrefab == null || highlightContainer == null) return;

            // Instantiate highlight
            GameObject highlight = Instantiate(highlightPrefab, highlightContainer);
            highlight.SetActive(true);

            RectTransform rect = highlight.GetComponent<RectTransform>();
            if (rect != null)
            {
                // Convert image coordinates to screen coordinates
                Rect screenRect = MapToScreenSpace(block.BoundingBox);

                // Set anchored position and size
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0, 1); // Top-left pivot
                rect.anchoredPosition = new Vector2(screenRect.x, screenRect.y);
                rect.sizeDelta = new Vector2(screenRect.width, screenRect.height);
            }

            // Set initial alpha to 0
            CanvasGroup canvasGroup = highlight.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            activeHighlights.Add(highlight);
        }

        private Rect MapToScreenSpace(Rect imageRect)
        {
            // Map from source image coordinates to screen coordinates
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            float scaleX = screenWidth / sourceImageWidth;
            float scaleY = screenHeight / sourceImageHeight;

            // Note: Image origin is typically top-left, Unity UI origin is bottom-left
            // So we need to flip Y coordinate
            float x = imageRect.x * scaleX;
            float y = screenHeight - (imageRect.y * scaleY) - (imageRect.height * scaleY);
            float width = imageRect.width * scaleX;
            float height = imageRect.height * scaleY;

            return new Rect(x, y, width, height);
        }

        private IEnumerator DisplayCoroutine()
        {
            // Fade in all highlights
            yield return StartCoroutine(FadeHighlights(0f, 1f, fadeInTime));

            // Wait for display duration
            yield return new WaitForSeconds(showDuration);

            // Fade out all highlights
            yield return StartCoroutine(FadeHighlights(1f, 0f, fadeOutTime));

            // Clear highlights
            ClearHighlights();
            displayCoroutine = null;
        }

        private IEnumerator FadeHighlights(float fromAlpha, float toAlpha, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = Mathf.Lerp(fromAlpha, toAlpha, t);

                foreach (var highlight in activeHighlights)
                {
                    if (highlight != null)
                    {
                        CanvasGroup canvasGroup = highlight.GetComponent<CanvasGroup>();
                        if (canvasGroup != null)
                        {
                            canvasGroup.alpha = alpha;
                        }
                    }
                }

                yield return null;
            }

            // Set final alpha
            foreach (var highlight in activeHighlights)
            {
                if (highlight != null)
                {
                    CanvasGroup canvasGroup = highlight.GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = toAlpha;
                    }
                }
            }
        }

        /// <summary>
        /// Clear all active highlights.
        /// </summary>
        public void ClearHighlights()
        {
            foreach (var highlight in activeHighlights)
            {
                if (highlight != null)
                {
                    Destroy(highlight);
                }
            }
            activeHighlights.Clear();
        }

        /// <summary>
        /// Set the source image dimensions for coordinate mapping.
        /// </summary>
        public void SetSourceDimensions(int width, int height)
        {
            sourceImageWidth = width;
            sourceImageHeight = height;
        }

        /// <summary>
        /// Set the highlight color.
        /// </summary>
        public void SetHighlightColor(Color fillColor, Color border)
        {
            highlightColor = fillColor;
            borderColor = border;
        }

        /// <summary>
        /// Set how long highlights are displayed.
        /// </summary>
        public void SetDisplayDuration(float duration)
        {
            showDuration = duration;
        }

        private void OnDestroy()
        {
            ClearHighlights();

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }
        }
    }
}
