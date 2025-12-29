using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace QuickCopyAR.UI
{
    /// <summary>
    /// Floating scan button that follows the user's view.
    /// Provides visual feedback for different states.
    /// </summary>
    public class FloatingButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Button Components")]
        [SerializeField] private Button button;
        [SerializeField] private Image buttonImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image spinnerImage;

        [Header("Icons")]
        [SerializeField] private Sprite scanIcon;
        [SerializeField] private Sprite checkIcon;
        [SerializeField] private Sprite errorIcon;

        [Header("Colors")]
        [SerializeField] private Color readyColor = new Color(0f, 0.8f, 0.8f, 0.8f); // Cyan
        [SerializeField] private Color processingColor = new Color(1f, 0.8f, 0f, 0.8f); // Yellow
        [SerializeField] private Color successColor = new Color(0f, 0.8f, 0.2f, 0.8f); // Green
        [SerializeField] private Color errorColor = new Color(0.8f, 0.2f, 0.2f, 0.8f); // Red
        [SerializeField] private Color hoverColor = new Color(0f, 1f, 1f, 0.9f); // Bright cyan

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 1.5f;
        [SerializeField] private float pulseMinScale = 0.95f;
        [SerializeField] private float pulseMaxScale = 1.05f;
        [SerializeField] private float spinnerSpeed = 360f;

        [Header("Positioning")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float distanceFromCamera = 0.5f;
        [SerializeField] private Vector3 offsetFromCenter = new Vector3(0.15f, -0.1f, 0f);
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private float followSpeed = 5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickSound;

        public event Action OnButtonPressed;

        private enum ButtonState { Ready, Processing, Success, Error }
        private ButtonState currentState = ButtonState.Ready;
        private bool isPulsing = false;
        private Coroutine pulseCoroutine;
        private Vector3 targetPosition;
        private bool isHovered = false;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main?.transform;
            }

            if (button != null)
            {
                button.onClick.AddListener(HandleButtonClick);
            }

            // Initialize spinner as hidden
            if (spinnerImage != null)
            {
                spinnerImage.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            SetReady();
        }

        private void Update()
        {
            UpdatePosition();
            UpdateSpinner();
        }

        private void UpdatePosition()
        {
            if (cameraTransform == null) return;

            // Calculate target position relative to camera
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            Vector3 up = cameraTransform.up;

            targetPosition = cameraTransform.position
                + forward * distanceFromCamera
                + right * offsetFromCenter.x
                + up * offsetFromCenter.y;

            // Smooth follow
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                Time.deltaTime * followSpeed
            );

            // Billboard to camera
            if (billboardToCamera)
            {
                transform.LookAt(cameraTransform.position);
                transform.Rotate(0, 180, 0); // Face towards camera
            }
        }

        private void UpdateSpinner()
        {
            if (spinnerImage != null && spinnerImage.gameObject.activeSelf)
            {
                spinnerImage.transform.Rotate(0, 0, -spinnerSpeed * Time.deltaTime);
            }
        }

        public void SetReady()
        {
            currentState = ButtonState.Ready;
            UpdateVisuals();
            StartPulseAnimation();
            EnableButton(true);
        }

        public void SetProcessing()
        {
            currentState = ButtonState.Processing;
            UpdateVisuals();
            StopPulseAnimation();
            EnableButton(false);
        }

        public void SetSuccess()
        {
            currentState = ButtonState.Success;
            UpdateVisuals();
            StopPulseAnimation();
            StartCoroutine(ReturnToReadyAfterDelay(0.5f));
        }

        public void SetError()
        {
            currentState = ButtonState.Error;
            UpdateVisuals();
            StopPulseAnimation();
            StartCoroutine(ReturnToReadyAfterDelay(0.5f));
        }

        private void UpdateVisuals()
        {
            Color targetColor = readyColor;
            Sprite targetIcon = scanIcon;
            bool showSpinner = false;

            switch (currentState)
            {
                case ButtonState.Ready:
                    targetColor = isHovered ? hoverColor : readyColor;
                    targetIcon = scanIcon;
                    break;
                case ButtonState.Processing:
                    targetColor = processingColor;
                    targetIcon = null;
                    showSpinner = true;
                    break;
                case ButtonState.Success:
                    targetColor = successColor;
                    targetIcon = checkIcon;
                    break;
                case ButtonState.Error:
                    targetColor = errorColor;
                    targetIcon = errorIcon;
                    break;
            }

            if (buttonImage != null)
            {
                buttonImage.color = targetColor;
            }

            if (iconImage != null)
            {
                if (targetIcon != null)
                {
                    iconImage.sprite = targetIcon;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false);
                }
            }

            if (spinnerImage != null)
            {
                spinnerImage.gameObject.SetActive(showSpinner);
            }
        }

        private void StartPulseAnimation()
        {
            if (isPulsing) return;

            isPulsing = true;
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        private void StopPulseAnimation()
        {
            isPulsing = false;
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
            transform.localScale = Vector3.one;
        }

        private IEnumerator PulseCoroutine()
        {
            float t = 0;
            while (isPulsing)
            {
                t += Time.deltaTime * pulseSpeed;
                float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, (Mathf.Sin(t * Mathf.PI) + 1f) / 2f);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        private IEnumerator ReturnToReadyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SetReady();
        }

        private void HandleButtonClick()
        {
            if (currentState != ButtonState.Ready) return;

            PlayClickSound();
            OnButtonPressed?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            HandleButtonClick();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (currentState == ButtonState.Ready)
            {
                UpdateVisuals();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (currentState == ButtonState.Ready)
            {
                UpdateVisuals();
            }
        }

        private void EnableButton(bool enabled)
        {
            if (button != null)
            {
                button.interactable = enabled;
            }
        }

        private void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }
        }

        /// <summary>
        /// Set the distance from camera for the button.
        /// </summary>
        public void SetDistance(float distance)
        {
            distanceFromCamera = distance;
        }

        /// <summary>
        /// Set the offset from center of view.
        /// </summary>
        public void SetOffset(Vector3 offset)
        {
            offsetFromCenter = offset;
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleButtonClick);
            }
        }
    }
}
