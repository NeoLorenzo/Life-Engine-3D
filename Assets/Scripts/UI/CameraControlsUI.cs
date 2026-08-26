using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using LifeEngine.Cameras;
using LifeEngine.Core;
using LifeEngine.SimulatedHumans;

namespace LifeEngine.UI
{
    /// <summary>
    /// HUD controller for switching simulation camera modes via UI buttons
    /// or hotkeys (1: First Person, 2: Third Person, 3: Sky).
    /// </summary>
    public class CameraControlsUI : MonoBehaviour
    {
        [Header("Mode Buttons")]
        public Button firstPersonButton;
        public Button thirdPersonButton;
        public Button skyButton;

        [Header("UI Container")]
        [Tooltip("Optional container GameObject to toggle. If null, a CanvasGroup on this GameObject is used.")]
        public GameObject controlsContainer;

        [Header("Visual Feedback Colors")]
        public Color activeBackgroundColor = new Color(0.12f, 0.58f, 0.95f, 0.95f);
        public Color inactiveBackgroundColor = new Color(0.14f, 0.15f, 0.18f, 0.85f);
        public Color highlightedBackgroundColor = new Color(0.24f, 0.28f, 0.35f, 0.95f);
        public Color disabledBackgroundColor = new Color(0.10f, 0.10f, 0.12f, 0.40f);
        public Color activeLabelColor = Color.white;
        public Color inactiveLabelColor = new Color(0.90f, 0.90f, 0.90f, 1f);

        private SimulationCameraController cameraController;
        private CanvasGroup canvasGroup;
        private bool hasSelectedHuman = false;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && controlsContainer == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            AgentSelector.OnAgentSelected -= HandleAgentSelected;
            AgentSelector.OnAgentSelected += HandleAgentSelected;

            var selector = Object.FindFirstObjectByType<AgentSelector>();
            if (selector != null)
            {
                hasSelectedHuman = selector.CurrentlySelected != null;
            }
            RefreshUI();
        }

        private void OnDisable()
        {
            AgentSelector.OnAgentSelected -= HandleAgentSelected;
        }

        private void Start()
        {
            cameraController = SimulationCameraController.Instance;

            // Auto-discover buttons in hierarchy if not manually assigned
            if (firstPersonButton == null) firstPersonButton = FindButtonByName("FirstPersonButton", "FPButton", "Button_FP");
            if (thirdPersonButton == null) thirdPersonButton = FindButtonByName("ThirdPersonButton", "TPButton", "Button_TP");
            if (skyButton == null) skyButton = FindButtonByName("SkyButton", "Sky_Button", "Button_Sky");

            // Attach click listeners
            if (firstPersonButton != null) firstPersonButton.onClick.AddListener(() => RequestMode(CameraMode.FirstPerson));
            if (thirdPersonButton != null) thirdPersonButton.onClick.AddListener(() => RequestMode(CameraMode.ThirdPerson));
            if (skyButton != null) skyButton.onClick.AddListener(() => RequestMode(CameraMode.Sky));

            if (cameraController != null)
            {
                cameraController.OnCameraModeChanged += HandleCameraModeChanged;
            }

            var selector = Object.FindFirstObjectByType<AgentSelector>();
            if (selector != null)
            {
                hasSelectedHuman = selector.CurrentlySelected != null;
            }

            RefreshUI();
        }

        private void OnDestroy()
        {
            if (cameraController != null)
            {
                cameraController.OnCameraModeChanged -= HandleCameraModeChanged;
            }
            AgentSelector.OnAgentSelected -= HandleAgentSelected;
        }

        private void Update()
        {
            if (cameraController == null)
            {
                cameraController = SimulationCameraController.Instance;
                if (cameraController != null)
                {
                    cameraController.OnCameraModeChanged += HandleCameraModeChanged;
                    RefreshUI();
                }
            }

            var selector = Object.FindFirstObjectByType<AgentSelector>();
            bool currentHasSelection = (selector != null && selector.CurrentlySelected != null);
            if (hasSelectedHuman != currentHasSelection)
            {
                hasSelectedHuman = currentHasSelection;
                RefreshUI();
            }

            HandleHotkeys();
        }

        private void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                RequestMode(CameraMode.FirstPerson);
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                RequestMode(CameraMode.ThirdPerson);
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                RequestMode(CameraMode.Sky);
            }
        }

        private void RequestMode(CameraMode mode)
        {
            if (cameraController == null) cameraController = SimulationCameraController.Instance;
            if (cameraController == null) return;

            cameraController.SetMode(mode);
            RefreshUI();
        }

        private void HandleAgentSelected(HumanBrain brain)
        {
            hasSelectedHuman = brain != null;
            RefreshUI();
        }

        private void HandleCameraModeChanged(CameraMode mode)
        {
            RefreshUI();
        }

        public void RefreshUI()
        {
            var selector = Object.FindFirstObjectByType<AgentSelector>();
            if (selector != null)
            {
                hasSelectedHuman = selector.CurrentlySelected != null;
            }

            // Toggle visibility container / CanvasGroup
            bool isVisible = hasSelectedHuman;
            if (controlsContainer != null)
            {
                controlsContainer.SetActive(isVisible);
            }
            else
            {
                if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = isVisible ? 1f : 0f;
                    canvasGroup.interactable = isVisible;
                    canvasGroup.blocksRaycasts = isVisible;
                }
            }

            CameraMode currentMode = cameraController != null ? cameraController.CurrentMode : CameraMode.Sky;

            UpdateButtonState(firstPersonButton, currentMode == CameraMode.FirstPerson, hasSelectedHuman);
            UpdateButtonState(thirdPersonButton, currentMode == CameraMode.ThirdPerson, hasSelectedHuman);
            UpdateButtonState(skyButton, currentMode == CameraMode.Sky, true);
        }

        private void UpdateButtonState(Button btn, bool isActive, bool isAvailable)
        {
            if (btn == null) return;

            btn.interactable = isAvailable;

            Color bg = isActive ? activeBackgroundColor : (isAvailable ? inactiveBackgroundColor : disabledBackgroundColor);

            ColorBlock colors = btn.colors;
            colors.normalColor = bg;
            colors.highlightedColor = isActive ? activeBackgroundColor : highlightedBackgroundColor;
            colors.pressedColor = activeBackgroundColor;
            colors.selectedColor = bg;
            colors.disabledColor = disabledBackgroundColor;
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            var image = btn.GetComponent<Image>();
            if (image != null)
            {
                image.color = bg;
            }

            // Update child text / TMP components to ensure high-contrast readable labels
            Color textCol = isActive ? activeLabelColor : (isAvailable ? inactiveLabelColor : new Color(0.6f, 0.6f, 0.6f, 0.4f));
            var texts = btn.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].color = textCol;
            }

            var tmps = btn.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                tmps[i].color = textCol;
            }
        }

        private Button FindButtonByName(params string[] names)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                foreach (var n in names)
                {
                    if (b.gameObject.name.Equals(n, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return b;
                    }
                }
            }
            return null;
        }
    }
}
