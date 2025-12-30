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
        [SerializeField] private Canvas overlayCanvas; // Optional for 2D fallback
        [SerializeField] private RectTransform highlightContainer;
        [SerializeField] private GameObject highlightPrefab;
        [SerializeField] private Camera mainCamera;

        [Header("Styling")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 0f, 0.3f); // Yellow, semi-transparent
        [SerializeField] private Color borderColor = new Color(1f, 0.8f, 0f, 0.8f); // Orange border
        [SerializeField] private float borderWidth = 0.005f; // Meters in world space

        [Header("3D Projection")]
        [SerializeField] private bool useHolographicMode = true;
        [SerializeField] private float defaultDistance = 0.6f; // Meters
        [SerializeField] private LayerMask projectionLayerMask = ~0; // All layers

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
            if (mainCamera == null) mainCamera = Camera.main;

            // Create default highlight prefab if not assigned
            if (highlightPrefab == null)
            {
                CreateDefaultHighlightPrefab();
            }

            // Ensure container exists for 2D mode, or root for 3D
            if (highlightContainer == null && !useHolographicMode && overlayCanvas != null)
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

            if (useHolographicMode)
            {
                // 3D World Space Canvas
                Canvas canvas = highlightPrefab.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform rt = highlightPrefab.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * 0.001f; // Scale down for world space
                
                // Background
                GameObject bgObj = new GameObject("Background");
                bgObj.transform.SetParent(highlightPrefab.transform, false);
                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.color = highlightColor;
                RectTransform bgRt = bgObj.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                // Border? Outline component behaves differently in World Space, might need separate sprites
                // For simplicity, we just use color
            }
            else
            {
                // 2D Screen Space
                RectTransform rect = highlightPrefab.AddComponent<RectTransform>();
                Image bgImage = highlightPrefab.AddComponent<Image>();
                bgImage.color = highlightColor;
                Outline outline = highlightPrefab.AddComponent<Outline>();
                outline.effectColor = borderColor;
                outline.effectDistance = new Vector2(2f, -2f);
            }
            
            highlightPrefab.AddComponent<CanvasGroup>();
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
            if (highlightPrefab == null) return;

            GameObject highlight = Instantiate(highlightPrefab, useHolographicMode ? null : highlightContainer);
            highlight.SetActive(true);

            Rect screenRect = MapToScreenSpace(block.BoundingBox);

            if (useHolographicMode)
            {
                SetupHolographicHighlight(highlight, screenRect);
            }
            else
            {
                Setup2DHighlight(highlight, screenRect);
            }

            ConfigAlpha(highlight, 0f);
            activeHighlights.Add(highlight);
        }

        private void SetupHolographicHighlight(GameObject highlight, Rect screenRect)
        {
            Vector2 screenCenter = screenRect.center;
            Ray ray = mainCamera.ScreenPointToRay(screenCenter);
            
            Vector3 position;
            Quaternion rotation;

            if (Physics.Raycast(ray, out RaycastHit hit, 5.0f, projectionLayerMask))
            {
                position = hit.point + (hit.normal * 0.01f); // Floating slightly above surface
                rotation = Quaternion.LookRotation(hit.normal);
            }
            else
            {
                position = ray.GetPoint(defaultDistance);
                rotation = Quaternion.LookRotation(ray.direction * -1); // Face camera
            }

            highlight.transform.position = position;
            highlight.transform.rotation = rotation;

            // Size estimation based on distance
            float distance = Vector3.Distance(mainCamera.transform.position, position);
            // Rough mapping: Screen Pixel Size -> World Size at Distance
            // Frustum height at distance D = 2 * D * tan(fov/2)
            float frustumHeight = 2.0f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldHeight = (screenRect.height / Screen.height) * frustumHeight;
            float worldWidth = (screenRect.width / Screen.width) * frustumHeight * mainCamera.aspect;

            RectTransform rt = highlight.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(worldWidth * 1000f, worldHeight * 1000f); // *1000 because scale is 0.001
            }
        }

        private void Setup2DHighlight(GameObject highlight, Rect screenRect)
        {
            RectTransform rect = highlight.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0, 1); // Top-left pivot
                rect.anchoredPosition = new Vector2(screenRect.x, screenRect.y);
                rect.sizeDelta = new Vector2(screenRect.width, screenRect.height);
            }
        }

        private void ConfigAlpha(GameObject obj, float alpha)
        {
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
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
