using System;
using System.Collections.Generic;
using UnityEngine;
using LifeEngine.Core;
using LifeEngine.SimulatedHumans;

namespace LifeEngine.Cameras
{
    /// <summary>
    /// Central camera controller providing First-Person, Third-Person, and Sky camera modes.
    /// Operates on the primary rendering camera and manages transform positioning, clipping,
    /// character renderer visibility, and sky reveal states.
    /// </summary>
    public class SimulationCameraController : MonoBehaviour
    {
        private static SimulationCameraController instance;
        public static SimulationCameraController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindFirstObjectByType<SimulationCameraController>();
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Target Camera")]
        [Tooltip("The rendering camera to manipulate. Defaults to Camera.main if null.")]
        public Camera targetCamera;

        public Camera TargetCamera
        {
            get
            {
                if (targetCamera == null)
                {
                    targetCamera = GetComponent<Camera>();
                    if (targetCamera == null) targetCamera = Camera.main;
                }
                return targetCamera;
            }
        }

        [Header("Sky Camera Configuration")]
        [Tooltip("Authoritative scene anchor defining Sky mode viewpoint position and orientation.")]
        public Transform skyCameraAnchor;
        public float skyFov = 60f;
        public float skyNearClip = 0.3f;
        public float skyFarClip = 1000f;

        [Header("First-Person Configuration")]
        public float firstPersonFov = 75f;
        public float firstPersonNearClip = 0.05f;
        [Tooltip("Local eye offset relative to the humanoid Head bone.")]
        public Vector3 firstPersonEyeOffset = new Vector3(0f, 0.06f, 0.12f);
        [Tooltip("Local pitch/yaw/roll correction applied after the animated head rotation.")]
        [SerializeField]
        private Vector3 firstPersonRotationOffsetEuler = new Vector3(-51.03f, 180f, 180f);
        public Vector3 FirstPersonRotationOffsetEuler
        {
            get => firstPersonRotationOffsetEuler;
            set => firstPersonRotationOffsetEuler = value;
        }
        [Tooltip("Fallback height above human root if humanoid Head bone is unavailable.")]
        public float firstPersonFallbackHeight = 1.65f;

        [Header("Third-Person Configuration")]
        public float thirdPersonFov = 65f;
        public float thirdPersonNearClip = 0.2f;
        [Tooltip("Distance in meters to place the chase camera behind the target.")]
        public float thirdPersonDistanceBehind = 3.5f;
        [Tooltip("Height in meters above the torso anchor.")]
        public float thirdPersonHeightAboveTorso = 1.5f;
        [Tooltip("Vertical look-at target offset relative to torso center.")]
        public float thirdPersonLookTargetAboveTorso = 0.4f;

        public CameraMode CurrentMode { get; private set; } = CameraMode.Sky;
        
        private HumanBrain selectedHuman;
        public HumanBrain SelectedHuman
        {
            get => selectedHuman;
            private set => selectedHuman = value;
        }

        public event Action<CameraMode> OnCameraModeChanged;

        // Fallback sky pose if no SkyCameraAnchor is assigned
        private Vector3 fallbackSkyPosition;
        private Quaternion fallbackSkyRotation;
        private bool hasCapturedFallbackSkyPose = false;

        // State storage for character renderers temporarily hidden in First-Person mode
        private Dictionary<Renderer, bool> hiddenRenderersState = new Dictionary<Renderer, bool>();
        private Dictionary<Renderer, bool> HiddenRenderersState
        {
            get
            {
                if (hiddenRenderersState == null) hiddenRenderersState = new Dictionary<Renderer, bool>();
                return hiddenRenderersState;
            }
        }
        private HumanBrain hiddenHumanBrain;

        // Stable horizontal forward vector for Third-Person mode
        private Vector3 lastStableHorizontalForward = Vector3.forward;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    targetCamera = GetComponent<Camera>();
                }
            }

            // Capture fallback sky pose from startup camera transform
            if (targetCamera != null)
            {
                fallbackSkyPosition = targetCamera.transform.position;
                fallbackSkyRotation = targetCamera.transform.rotation;
                hasCapturedFallbackSkyPose = true;
            }

            if (skyCameraAnchor == null)
            {
                Debug.LogWarning("[SimulationCameraController] SkyCameraAnchor is not assigned in the Inspector. " +
                                 "Using startup camera transform as the fallback Sky mode anchor.");
            }
        }

        private void Start()
        {
            // Start in Sky mode
            SetMode(CameraMode.Sky);
        }

        private void OnEnable()
        {
            AgentSelector.OnAgentSelected -= HandleAgentSelected;
            AgentSelector.OnAgentSelected += HandleAgentSelected;

            var selector = UnityEngine.Object.FindFirstObjectByType<AgentSelector>();
            if (selector != null && selector.CurrentlySelected != null)
            {
                HandleAgentSelected(selector.CurrentlySelected);
            }
        }

        private void OnDisable()
        {
            AgentSelector.OnAgentSelected -= HandleAgentSelected;
            RestoreHiddenRenderers();
        }

        private void OnDestroy()
        {
            RestoreHiddenRenderers();
            if (Instance == this) Instance = null;
        }

        private void HandleAgentSelected(HumanBrain newSelected)
        {
            if (SelectedHuman == newSelected) return;

            // If we were hiding renderers on previous human, restore them
            RestoreHiddenRenderers();

            SelectedHuman = newSelected;

            if (SelectedHuman == null)
            {
                // Graceful fallback to Sky mode if currently following an agent
                if (CurrentMode != CameraMode.Sky)
                {
                    SetMode(CameraMode.Sky);
                }
            }
            else if (CurrentMode == CameraMode.FirstPerson)
            {
                // Hide newly selected human's renderers in First-Person
                HideCharacterRenderers(SelectedHuman);
            }
        }

        /// <summary>
        /// Authoritatively switches the camera mode and manages transition side effects.
        /// </summary>
        public void SetMode(CameraMode newMode)
        {
            // Validate: FirstPerson and ThirdPerson require an active selected human
            if ((newMode == CameraMode.FirstPerson || newMode == CameraMode.ThirdPerson) && SelectedHuman == null)
            {
                var selector = UnityEngine.Object.FindFirstObjectByType<AgentSelector>();
                if (selector != null && selector.CurrentlySelected != null)
                {
                    SelectedHuman = selector.CurrentlySelected;
                }
            }

            if ((newMode == CameraMode.FirstPerson || newMode == CameraMode.ThirdPerson) && SelectedHuman == null)
            {
                Debug.LogWarning($"[SimulationCameraController] Cannot switch to {newMode} mode without a selected human. Remaining in {CurrentMode} mode.");
                return;
            }

            CameraMode previousMode = CurrentMode;
            CurrentMode = newMode;

            // Clean up previous mode state
            if (previousMode == CameraMode.FirstPerson && newMode != CameraMode.FirstPerson)
            {
                RestoreHiddenRenderers();
            }

            // Sky reveal gating
            if (SkyRevealController.Instance != null)
            {
                SkyRevealController.Instance.SetSkyRevealEnabled(newMode == CameraMode.Sky);
            }

            // Apply new mode settings
            if (TargetCamera != null)
            {
                switch (newMode)
                {
                    case CameraMode.FirstPerson:
                        TargetCamera.fieldOfView = firstPersonFov;
                        TargetCamera.nearClipPlane = firstPersonNearClip;
                        HideCharacterRenderers(SelectedHuman);
                        break;

                    case CameraMode.ThirdPerson:
                        TargetCamera.fieldOfView = thirdPersonFov;
                        TargetCamera.nearClipPlane = thirdPersonNearClip;
                        if (SelectedHuman != null)
                        {
                            Vector3 fwd = Vector3.ProjectOnPlane(SelectedHuman.transform.forward, Vector3.up);
                            if (fwd.sqrMagnitude > 0.001f)
                            {
                                lastStableHorizontalForward = fwd.normalized;
                            }
                        }
                        break;

                    case CameraMode.Sky:
                        TargetCamera.fieldOfView = skyFov;
                        TargetCamera.nearClipPlane = skyNearClip;
                        TargetCamera.farClipPlane = skyFarClip;
                        break;
                }
            }

            UpdateCameraTransform();
            OnCameraModeChanged?.Invoke(CurrentMode);
        }

        public void UpdateCameraTransform()
        {
            if (TargetCamera == null) return;

            var selector = UnityEngine.Object.FindFirstObjectByType<AgentSelector>();
            if (selector != null && SelectedHuman != selector.CurrentlySelected)
            {
                HandleAgentSelected(selector.CurrentlySelected);
            }

            // Handle unexpected target destruction mid-follow
            if ((CurrentMode == CameraMode.FirstPerson || CurrentMode == CameraMode.ThirdPerson) && SelectedHuman == null)
            {
                SetMode(CameraMode.Sky);
                return;
            }

            switch (CurrentMode)
            {
                case CameraMode.FirstPerson:
                    UpdateFirstPersonTransform();
                    break;

                case CameraMode.ThirdPerson:
                    UpdateThirdPersonTransform();
                    break;

                case CameraMode.Sky:
                    UpdateSkyTransform();
                    break;
            }
        }

        private void LateUpdate()
        {
            UpdateCameraTransform();
        }

        private void UpdateFirstPersonTransform()
        {
            if (SelectedHuman == null || TargetCamera == null) return;

            Animator animator = SelectedHuman.GetComponentInChildren<Animator>();
            Transform headTransform = null;

            if (animator != null && animator.isHuman)
            {
                headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            if (headTransform != null)
            {
                TargetCamera.transform.position = headTransform.position + headTransform.rotation * firstPersonEyeOffset;
                TargetCamera.transform.rotation = headTransform.rotation * Quaternion.Euler(firstPersonRotationOffsetEuler);
            }
            else
            {
                // Fallback using root transform and height offset
                Vector3 fallbackHead = SelectedHuman.transform.position + Vector3.up * firstPersonFallbackHeight;
                TargetCamera.transform.position = fallbackHead + SelectedHuman.transform.rotation * firstPersonEyeOffset;
                TargetCamera.transform.rotation = SelectedHuman.transform.rotation * Quaternion.Euler(firstPersonRotationOffsetEuler);
            }
        }

        private void UpdateThirdPersonTransform()
        {
            if (SelectedHuman == null || TargetCamera == null) return;

            Animator animator = SelectedHuman.GetComponentInChildren<Animator>();
            Vector3 torsoPos;

            Transform chestTransform = null;
            if (animator != null && animator.isHuman)
            {
                chestTransform = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chestTransform == null)
                {
                    chestTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
                }
            }

            if (chestTransform != null)
            {
                torsoPos = chestTransform.position;
            }
            else
            {
                torsoPos = SelectedHuman.transform.position + Vector3.up * 1.0f;
            }

            // Derive stable horizontal follow basis from root forward
            Vector3 horizontalForward = Vector3.ProjectOnPlane(SelectedHuman.transform.forward, Vector3.up);

            // If agent is lying down / sleeping, root forward may point vertically; maintain stable yaw
            if (horizontalForward.sqrMagnitude > 0.01f)
            {
                lastStableHorizontalForward = horizontalForward.normalized;
            }

            Vector3 camPosition = torsoPos - lastStableHorizontalForward * thirdPersonDistanceBehind + Vector3.up * thirdPersonHeightAboveTorso;
            Vector3 lookTarget = torsoPos + Vector3.up * thirdPersonLookTargetAboveTorso;

            TargetCamera.transform.position = camPosition;
            TargetCamera.transform.rotation = Quaternion.LookRotation(lookTarget - camPosition, Vector3.up);
        }

        private void UpdateSkyTransform()
        {
            if (TargetCamera == null) return;

            if (skyCameraAnchor != null)
            {
                TargetCamera.transform.position = skyCameraAnchor.position;
                TargetCamera.transform.rotation = skyCameraAnchor.rotation;
            }
            else if (hasCapturedFallbackSkyPose)
            {
                TargetCamera.transform.position = fallbackSkyPosition;
                TargetCamera.transform.rotation = fallbackSkyRotation;
            }
        }

        private void HideCharacterRenderers(HumanBrain human)
        {
            RestoreHiddenRenderers();
            if (human == null) return;

            hiddenHumanBrain = human;
            Renderer[] renderers = human.GetComponentsInChildren<Renderer>(true);

            foreach (var rend in renderers)
            {
                // Exclude selection ring visual, Sky reveal rings, held tools, and carried resources
                if (rend.gameObject.name == "SkyRevealRing") continue;
                if (human.selectionVisual != null && rend.transform.IsChildOf(human.selectionVisual.transform)) continue;
                if (human.toolSlot != null && rend.transform.IsChildOf(human.toolSlot)) continue;
                if (human.resourceSlot != null && rend.transform.IsChildOf(human.resourceSlot)) continue;

                HiddenRenderersState[rend] = rend.enabled;
                rend.enabled = false;
            }
        }

        private void RestoreHiddenRenderers()
        {
            foreach (var kvp in HiddenRenderersState)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = kvp.Value;
                }
            }

            HiddenRenderersState.Clear();
            hiddenHumanBrain = null;
        }
    }
}
