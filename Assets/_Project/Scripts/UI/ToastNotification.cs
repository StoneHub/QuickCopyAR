using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace QuickCopyAR.UI
{
    /// <summary>
    /// Displays temporary toast notifications to the user.
    /// Positioned at top-center of view with fade animations.
    /// </summary>
    public class ToastNotification : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private GameObject toastPanel;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Positioning")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float distanceFromCamera = 0.8f;
        [SerializeField] private Vector3 offsetFromCenter = new Vector3(0f, 0.15f, 0f);
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float followSpeed = 8f;

        [Header("Animation")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Styling")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private int maxCharacters = 100;

        private Coroutine displayCoroutine;
        private Vector3 targetPosition;
        private bool isVisible = false;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = toastPanel?.GetComponent<CanvasGroup>();
                if (canvasGroup == null && toastPanel != null)
                {
                    canvasGroup = toastPanel.AddComponent<CanvasGroup>();
                }
            }

            // Initialize as hidden
            Hide(immediate: true);
        }

        private void Update()
        {
            if (isVisible)
            {
                UpdatePosition();
            }
        }

        private void UpdatePosition()
        {
            if (cameraTransform == null) return;

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            Vector3 up = cameraTransform.up;

            targetPosition = cameraTransform.position
                + forward * distanceFromCamera
                + right * offsetFromCenter.x
                + up * offsetFromCenter.y;

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * followSpeed
            );

            if (billboardToCamera)
            {
                transform.LookAt(cameraTransform.position);
                transform.Rotate(0, 180, 0);
            }
        }

        /// <summary>
        /// Show a toast message with default duration.
        /// </summary>
        public void Show(string message)
        {
            Show(message, displayDuration);
        }

        /// <summary>
        /// Show a toast message with custom duration.
        /// </summary>
        public void Show(string message, float duration)
        {
            if (string.IsNullOrEmpty(message)) return;

            // Truncate if too long
            if (message.Length > maxCharacters)
            {
                message = message.Substring(0, maxCharacters) + "...";
            }

            // Cancel any existing display
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(DisplayCoroutine(message, duration));
        }

        /// <summary>
        /// Show a persistent toast that doesn't auto-hide.
        /// Call Hide() to dismiss.
        /// </summary>
        public void ShowPersistent(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (message.Length > maxCharacters)
            {
                message = message.Substring(0, maxCharacters) + "...";
            }

            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
            }

            displayCoroutine = StartCoroutine(ShowPersistentCoroutine(message));
        }

        private IEnumerator DisplayCoroutine(string message, float duration)
        {
            // Set message
            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = textColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }

            // Show panel
            if (toastPanel != null)
            {
                toastPanel.SetActive(true);
            }

            isVisible = true;

            // Position immediately
            UpdatePosition();
            transform.position = targetPosition;

            // Fade in
            yield return StartCoroutine(FadeCoroutine(0f, 1f, fadeInDuration));

            // Wait for display duration
            yield return new WaitForSeconds(duration);

            // Fade out
            yield return StartCoroutine(FadeCoroutine(1f, 0f, fadeOutDuration));

            // Hide panel
            if (toastPanel != null)
            {
                toastPanel.SetActive(false);
            }

            isVisible = false;
            displayCoroutine = null;
        }

        private IEnumerator ShowPersistentCoroutine(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
                messageText.color = textColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }

            if (toastPanel != null)
            {
                toastPanel.SetActive(true);
            }

            isVisible = true;
            UpdatePosition();
            transform.position = targetPosition;

            yield return StartCoroutine(FadeCoroutine(0f, 1f, fadeInDuration));
        }

        private IEnumerator FadeCoroutine(float fromAlpha, float toAlpha, float duration)
        {
            if (canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = fadeCurve.Evaluate(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
        }

        /// <summary>
        /// Hide the toast notification.
        /// </summary>
        public void Hide(bool immediate = false)
        {
            if (displayCoroutine != null)
            {
                StopCoroutine(displayCoroutine);
                displayCoroutine = null;
            }

            if (immediate)
            {
                if (toastPanel != null)
                {
                    toastPanel.SetActive(false);
                }
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 0f;
                }
                isVisible = false;
            }
            else
            {
                StartCoroutine(HideCoroutine());
            }
        }

        private IEnumerator HideCoroutine()
        {
            yield return StartCoroutine(FadeCoroutine(canvasGroup.alpha, 0f, fadeOutDuration));

            if (toastPanel != null)
            {
                toastPanel.SetActive(false);
            }
            isVisible = false;
        }

        /// <summary>
        /// Update the message text without resetting the display timer.
        /// </summary>
        public void UpdateMessage(string message)
        {
            if (messageText != null && !string.IsNullOrEmpty(message))
            {
                if (message.Length > maxCharacters)
                {
                    message = message.Substring(0, maxCharacters) + "...";
                }
                messageText.text = message;
            }
        }

        /// <summary>
        /// Set the background color for the toast.
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            backgroundColor = color;
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }

        /// <summary>
        /// Set the text color for the toast.
        /// </summary>
        public void SetTextColor(Color color)
        {
            textColor = color;
            if (messageText != null)
            {
                messageText.color = color;
            }
        }
    }
}
