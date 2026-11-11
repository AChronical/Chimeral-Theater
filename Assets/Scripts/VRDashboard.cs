using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace ChimeralTheater.UI
{
    /// <summary>
    /// The hand-bound dashboard: a world-space canvas that follows the left hand and is toggled with the
    /// left primary button. Pointing, hovering, clicking, dragging and scrolling go through the XR
    /// Interaction Toolkit (the right hand's <see cref="XRRayInteractor"/> and the XR UI Input Module),
    /// so the panel only manages tabs and settings.
    /// </summary>
    public class VRDashboard : MonoBehaviour
    {
        [Header("Prefab UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text tabTitleText;
        [SerializeField] private GameObject settingsContent;
        [SerializeField] private GameObject filmsContent;
        [SerializeField] private GameObject filesContent;
        [SerializeField] private GameObject assetInventoryContent;
        [SerializeField] private Button settingsTabButton;
        [SerializeField] private Button filmsTabButton;
        [SerializeField] private Button filesTabButton;
        [SerializeField] private Button assetInventoryTabButton;
        [SerializeField] private Image settingsTabImage;
        [SerializeField] private Image filmsTabImage;
        [SerializeField] private Image filesTabImage;
        [SerializeField] private Image assetInventoryTabImage;
        [SerializeField] private Slider locomotionSlider;
        [SerializeField] private Slider rotationSlider;
        [SerializeField] private Text locomotionValueText;
        [SerializeField] private Text rotationValueText;

        [Header("Player References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private ContinuousMoveProvider moveProvider;
        [SerializeField] private ContinuousTurnProvider turnProvider;
        [SerializeField] private Camera hmd;
        [SerializeField] private Transform leftHand;
        [Tooltip("The right hand's UI ray. It is shown only while the dashboard is open.")]
        [SerializeField] private XRRayInteractor uiRay;

        [Header("Hand-following Settings")]
        [SerializeField] private FloatingUIFollower follower;

        private InputAction dashboardAction;

        private float baseMoveSpeed;
        private float baseTurnSpeed;

        private bool isOpen;
        private int selectedTab = -1;

        private const string MapName = "Chimeral Theater";
        private static readonly Color TabNormalColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        private static readonly Color TabSelectedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Color TabHoverColor = new Color(0.45f, 0.48f, 0.55f, 1f);

        // Properties for editor setup
        public GameObject Panel { get => panel; set => panel = value; }
        public Text TabTitleText { get => tabTitleText; set => tabTitleText = value; }
        public GameObject SettingsContent { get => settingsContent; set => settingsContent = value; }
        public GameObject FilmsContent { get => filmsContent; set => filmsContent = value; }
        public GameObject FilesContent { get => filesContent; set => filesContent = value; }
        public GameObject AssetInventoryContent { get => assetInventoryContent; set => assetInventoryContent = value; }
        public Button SettingsTabButton { get => settingsTabButton; set => settingsTabButton = value; }
        public Button FilmsTabButton { get => filmsTabButton; set => filmsTabButton = value; }
        public Button FilesTabButton { get => filesTabButton; set => filesTabButton = value; }
        public Button AssetInventoryTabButton { get => assetInventoryTabButton; set => assetInventoryTabButton = value; }
        public Image SettingsTabImage { get => settingsTabImage; set => settingsTabImage = value; }
        public Image FilmsTabImage { get => filmsTabImage; set => filmsTabImage = value; }
        public Image FilesTabImage { get => filesTabImage; set => filesTabImage = value; }
        public Image AssetInventoryTabImage { get => assetInventoryTabImage; set => assetInventoryTabImage = value; }
        public Slider LocomotionSlider { get => locomotionSlider; set => locomotionSlider = value; }
        public Slider RotationSlider { get => rotationSlider; set => rotationSlider = value; }
        public Text LocomotionValueText { get => locomotionValueText; set => locomotionValueText = value; }
        public Text RotationValueText { get => rotationValueText; set => rotationValueText = value; }
        public InputActionAsset InputActions { get => inputActions; set => inputActions = value; }
        public ContinuousMoveProvider MoveProvider { get => moveProvider; set => moveProvider = value; }
        public ContinuousTurnProvider TurnProvider { get => turnProvider; set => turnProvider = value; }
        public Camera Hmd { get => hmd; set => hmd = value; }
        public Transform LeftHand { get => leftHand; set => leftHand = value; }
        public XRRayInteractor UIRay { get => uiRay; set => uiRay = value; }
        public FloatingUIFollower Follower { get => follower; set => follower = value; }

        private void Awake()
        {
            ResolveReferences();
            ResolveInputActions();
            EnsureFollower();
            InitializeUI();
        }

        private void ResolveReferences()
        {
            if (hmd == null)
            {
                hmd = Camera.main;
                if (hmd == null)
                    hmd = FindFirstObjectByType<Camera>();
            }

            if (moveProvider == null)
                moveProvider = FindFirstObjectByType<ContinuousMoveProvider>();
            if (turnProvider == null)
                turnProvider = FindFirstObjectByType<ContinuousTurnProvider>();
            if (moveProvider != null)
                baseMoveSpeed = moveProvider.moveSpeed;
            if (turnProvider != null)
                baseTurnSpeed = turnProvider.turnSpeed;

            if (uiRay == null)
                uiRay = FindFirstObjectByType<XRRayInteractor>(FindObjectsInactive.Include);

            if (leftHand == null)
            {
                GameObject leftHandObj = GameObject.Find("Left Hand");
                if (leftHandObj != null)
                    leftHand = leftHandObj.transform;
            }
        }

        private void ResolveInputActions()
        {
            if (inputActions == null)
                return;

            InputActionMap map = inputActions.FindActionMap(MapName, false);
            if (map == null)
                return;

            dashboardAction = map.FindAction("DashboardToggle", false);
        }

        private void InitializeUI()
        {
            if (panel != null)
            {
                Canvas canvas = panel.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.worldCamera = hmd;
            }

            if (settingsTabButton != null)
            {
                settingsTabButton.onClick.AddListener(() => SelectTab(0));
            }

            if (filmsTabButton != null)
            {
                filmsTabButton.onClick.AddListener(() => SelectTab(1));
            }

            if (filesTabButton != null)
            {
                filesTabButton.onClick.AddListener(() => SelectTab(2));
            }

            if (assetInventoryTabButton != null)
            {
                assetInventoryTabButton.onClick.AddListener(() => SelectTab(3));
            }

            if (locomotionSlider != null)
            {
                locomotionSlider.onValueChanged.AddListener(OnLocomotionSpeedChanged);
                locomotionSlider.minValue = 1f;
                locomotionSlider.maxValue = 5f;
            }

            if (rotationSlider != null)
            {
                rotationSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
                rotationSlider.minValue = 1f;
                rotationSlider.maxValue = 5f;
            }

            SelectTab(0);

            // Stagnant and disabled on awake
            SetVisualsActive(false);
            isOpen = false;
        }

        private void OnEnable()
        {
            dashboardAction?.Enable();

            if (dashboardAction != null)
                dashboardAction.performed += ToggleDashboard;
        }

        private void OnDisable()
        {
            if (dashboardAction != null)
                dashboardAction.performed -= ToggleDashboard;

            dashboardAction?.Disable();
        }

        private void Update()
        {
            if (!isOpen)
                return;

            UpdateDashboardPosition();
        }

        private void SetVisualsActive(bool active)
        {
            if (panel != null)
                panel.SetActive(active);

            if (uiRay != null)
                uiRay.gameObject.SetActive(active);
        }

        private Vector3 GetLeftHandWorldPosition()
        {
            if (leftHand != null)
                return leftHand.position;

            if (hmd != null)
                return hmd.transform.position + hmd.transform.forward * 0.4f + Vector3.down * 0.2f - hmd.transform.right * 0.2f;

            return Vector3.zero;
        }

        private void EnsureFollower()
        {
            // The panel itself carries the follow behaviour; make sure it exists and points at
            // the HMD as the viewer, so any floating panel built the same way shares the movement.
            if (follower == null && panel != null)
                follower = panel.GetComponent<FloatingUIFollower>();

            if (follower != null)
                follower.Viewer = hmd != null ? hmd.transform : null;
        }

        private void UpdateDashboardPosition()
        {
            if (follower == null || panel == null)
                return;

            follower.Follow(GetLeftHandWorldPosition(), Time.deltaTime);
        }

        /// <summary>Colours a tab through its button's colour tint, so hover comes from the UI system.</summary>
        private static void UpdateTabColors(Button button, Image image, bool isSelected)
        {
            if (button == null)
                return;

            if (image != null)
                image.color = Color.white;

            Color normal = isSelected ? TabSelectedColor : TabNormalColor;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.selectedColor = normal;
            colors.highlightedColor = TabHoverColor;
            colors.pressedColor = TabSelectedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        /// <summary>Closes the dashboard from code.</summary>
        public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            SetVisualsActive(false);
        }

        private void ToggleDashboard(InputAction.CallbackContext context)
        {
            isOpen = !isOpen;
            SetVisualsActive(isOpen);

            if (isOpen && follower != null)
                follower.Snap(GetLeftHandWorldPosition());
        }

        private void SelectTab(int tabIndex)
        {
            selectedTab = tabIndex;

            if (selectedTab == 0)
            {
                if (tabTitleText != null)
                    tabTitleText.text = "Settings";
                if (settingsContent != null) settingsContent.SetActive(true);
                if (filmsContent != null) filmsContent.SetActive(false);
                if (filesContent != null) filesContent.SetActive(false);
                if (assetInventoryContent != null) assetInventoryContent.SetActive(false);
            }
            else if (selectedTab == 1)
            {
                if (tabTitleText != null)
                    tabTitleText.text = "Films";
                if (settingsContent != null) settingsContent.SetActive(false);
                if (filmsContent != null) filmsContent.SetActive(true);
                if (filesContent != null) filesContent.SetActive(false);
                if (assetInventoryContent != null) assetInventoryContent.SetActive(false);
            }
            else if (selectedTab == 2)
            {
                if (tabTitleText != null)
                    tabTitleText.text = "Files";
                if (settingsContent != null) settingsContent.SetActive(false);
                if (filmsContent != null) filmsContent.SetActive(false);
                if (filesContent != null)
                {
                    filesContent.SetActive(true);
                    filesContent.GetComponent<UserFilesPanel>()?.Refresh();
                }
                if (assetInventoryContent != null) assetInventoryContent.SetActive(false);
            }
            else if (selectedTab == 3)
            {
                if (tabTitleText != null)
                    tabTitleText.text = "Asset Inventory";
                if (settingsContent != null) settingsContent.SetActive(false);
                if (filmsContent != null) filmsContent.SetActive(false);
                if (filesContent != null) filesContent.SetActive(false);
                if (assetInventoryContent != null)
                {
                    assetInventoryContent.SetActive(true);
                    assetInventoryContent.GetComponent<AssetInventoryPanel>()?.Refresh();
                }
            }

            UpdateTabColors(settingsTabButton, settingsTabImage, selectedTab == 0);
            UpdateTabColors(filmsTabButton, filmsTabImage, selectedTab == 1);
            UpdateTabColors(filesTabButton, filesTabImage, selectedTab == 2);
            UpdateTabColors(assetInventoryTabButton, assetInventoryTabImage, selectedTab == 3);
        }

        private void OnLocomotionSpeedChanged(float value)
        {
            if (locomotionValueText != null)
                locomotionValueText.text = $"{value:0.0}x";

            if (moveProvider != null)
                moveProvider.moveSpeed = baseMoveSpeed * value;
        }

        private void OnRotationSpeedChanged(float value)
        {
            if (rotationValueText != null)
                rotationValueText.text = $"{value:0.0}x";

            if (turnProvider != null)
                turnProvider.turnSpeed = baseTurnSpeed * value;
        }
    }
}
