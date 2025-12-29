using System;
using UnityEngine;

namespace QuickCopyAR.Input
{
    /// <summary>
    /// Handles all input methods: Controller triggers and hand pinch gestures.
    /// Provides unified capture trigger event and haptic feedback.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        [Header("Controller Settings")]
        [SerializeField] private bool useControllerInput = true;
        [SerializeField] private OVRInput.Button captureButton = OVRInput.Button.PrimaryIndexTrigger;
        [SerializeField] private OVRInput.Controller preferredController = OVRInput.Controller.RTouch;

        [Header("Hand Tracking Settings")]
        [SerializeField] private bool useHandTracking = true;
        [SerializeField] private float pinchThreshold = 0.8f;
        [SerializeField] private float pinchReleaseThreshold = 0.5f;
        [SerializeField] private float pinchCooldown = 0.5f;

        [Header("Haptic Feedback")]
        [SerializeField] private float captureHapticDuration = 0.1f;
        [SerializeField] private float captureHapticIntensity = 0.3f;
        [SerializeField] private float successHapticDuration = 0.1f;
        [SerializeField] private float successHapticIntensity = 0.5f;
        [SerializeField] private float errorHapticDuration = 0.3f;
        [SerializeField] private float errorHapticIntensity = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip captureClickSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip errorSound;

        public event Action OnCaptureTriggered;

        private bool isPinching = false;
        private float lastPinchTime = 0f;
        private OVRHand leftHand;
        private OVRHand rightHand;

        private void Start()
        {
            // Find OVRHand components if hand tracking is enabled
            if (useHandTracking)
            {
                FindHandComponents();
            }
        }

        private void FindHandComponents()
        {
            // Try to find OVRHand components in the scene
            OVRHand[] hands = FindObjectsOfType<OVRHand>();
            foreach (var hand in hands)
            {
                if (hand.HandType == OVRHand.Hand.HandLeft)
                {
                    leftHand = hand;
                }
                else if (hand.HandType == OVRHand.Hand.HandRight)
                {
                    rightHand = hand;
                }
            }

            if (leftHand == null && rightHand == null)
            {
                Utilities.Logger.LogWarning("InputManager", "No OVRHand components found. Hand tracking disabled.");
                useHandTracking = false;
            }
            else
            {
                Utilities.Logger.Log("InputManager", "Hand tracking initialized");
            }
        }

        private void Update()
        {
            // Check controller input
            if (useControllerInput)
            {
                CheckControllerInput();
            }

            // Check hand tracking input
            if (useHandTracking)
            {
                CheckHandTrackingInput();
            }
        }

        private void CheckControllerInput()
        {
            // Check preferred controller first
            if (OVRInput.GetDown(captureButton, preferredController))
            {
                TriggerCapture(preferredController);
                return;
            }

            // Check other controller as fallback
            OVRInput.Controller otherController = preferredController == OVRInput.Controller.RTouch
                ? OVRInput.Controller.LTouch
                : OVRInput.Controller.RTouch;

            if (OVRInput.GetDown(captureButton, otherController))
            {
                TriggerCapture(otherController);
            }
        }

        private void CheckHandTrackingInput()
        {
            // Check cooldown
            if (Time.time - lastPinchTime < pinchCooldown)
            {
                return;
            }

            // Check right hand pinch
            if (rightHand != null && rightHand.IsTracked)
            {
                float rightPinchStrength = rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

                if (!isPinching && rightPinchStrength > pinchThreshold)
                {
                    isPinching = true;
                    TriggerCapture(OVRInput.Controller.RHand);
                }
                else if (isPinching && rightPinchStrength < pinchReleaseThreshold)
                {
                    isPinching = false;
                }
            }

            // Check left hand pinch if right hand not pinching
            if (!isPinching && leftHand != null && leftHand.IsTracked)
            {
                float leftPinchStrength = leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);

                if (leftPinchStrength > pinchThreshold)
                {
                    isPinching = true;
                    TriggerCapture(OVRInput.Controller.LHand);
                }
                else if (isPinching && leftPinchStrength < pinchReleaseThreshold)
                {
                    isPinching = false;
                }
            }
        }

        private void TriggerCapture(OVRInput.Controller controller)
        {
            lastPinchTime = Time.time;

            Utilities.Logger.Log("InputManager", $"Capture triggered by {controller}");

            // Play capture haptic
            PlayHaptic(controller, captureHapticDuration, captureHapticIntensity);

            // Play capture sound
            PlaySound(captureClickSound);

            // Invoke capture event
            OnCaptureTriggered?.Invoke();
        }

        /// <summary>
        /// Play haptic feedback for successful operation.
        /// </summary>
        public void PlayHapticSuccess()
        {
            // Double pulse for success
            PlayHaptic(preferredController, successHapticDuration, successHapticIntensity);

            // Second pulse after short delay
            StartCoroutine(DelayedHaptic(successHapticDuration * 2, successHapticDuration, successHapticIntensity));

            PlaySound(successSound);
        }

        /// <summary>
        /// Play haptic feedback for error.
        /// </summary>
        public void PlayHapticError()
        {
            PlayHaptic(preferredController, errorHapticDuration, errorHapticIntensity);
            PlaySound(errorSound);
        }

        private System.Collections.IEnumerator DelayedHaptic(float delay, float duration, float intensity)
        {
            yield return new WaitForSeconds(delay);
            PlayHaptic(preferredController, duration, intensity);
        }

        private void PlayHaptic(OVRInput.Controller controller, float duration, float intensity)
        {
            // Convert to Touch controller if using hand
            OVRInput.Controller hapticController = controller;
            if (controller == OVRInput.Controller.RHand)
            {
                hapticController = OVRInput.Controller.RTouch;
            }
            else if (controller == OVRInput.Controller.LHand)
            {
                hapticController = OVRInput.Controller.LTouch;
            }

            // Only play haptics on Touch controllers
            if (hapticController == OVRInput.Controller.RTouch ||
                hapticController == OVRInput.Controller.LTouch)
            {
                OVRInput.SetControllerVibration(intensity, intensity, hapticController);
                StartCoroutine(StopHapticAfterDelay(hapticController, duration));
            }
        }

        private System.Collections.IEnumerator StopHapticAfterDelay(OVRInput.Controller controller, float delay)
        {
            yield return new WaitForSeconds(delay);
            OVRInput.SetControllerVibration(0, 0, controller);
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Check if any controller is connected.
        /// </summary>
        public bool IsControllerConnected()
        {
            return OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) ||
                   OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
        }

        /// <summary>
        /// Check if hand tracking is active.
        /// </summary>
        public bool IsHandTrackingActive()
        {
            if (!useHandTracking) return false;

            return (leftHand != null && leftHand.IsTracked) ||
                   (rightHand != null && rightHand.IsTracked);
        }

        /// <summary>
        /// Enable or disable controller input.
        /// </summary>
        public void SetControllerInputEnabled(bool enabled)
        {
            useControllerInput = enabled;
        }

        /// <summary>
        /// Enable or disable hand tracking input.
        /// </summary>
        public void SetHandTrackingEnabled(bool enabled)
        {
            useHandTracking = enabled;
            if (enabled && leftHand == null && rightHand == null)
            {
                FindHandComponents();
            }
        }
    }
}
