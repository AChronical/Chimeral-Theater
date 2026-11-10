using System.Collections.Generic;
using ChimeralTheater.Locomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChimeralTheater.UI
{
    public class VRDashboard : MonoBehaviour
    {
        [Header("Prefab UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private LineRenderer laser;
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
        [SerializeField] private VRLocomotion locomotion;
        [SerializeField] private Camera hmd;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Hand-following Settings")]
        [SerializeField] private FloatingUIFollower follower;
        [SerializeField] private float laserLength = 10f;

        private InputAction dashboardAction;
        private InputAction interactAction;
        private InputAction leftPositionAction;
        private InputAction rightPositionAction;
        private InputAction rightRotationAction;

        private RectTransform panelRect;
        private RectTransform settingsTabRect;
        private RectTransform filmsTabRect;
        private RectTransform filesTabRect;
        private RectTransform assetInventoryTabRect;
        private RectTransform locomotionSliderRect;
        private RectTransform rotationSliderRect;

        private bool isOpen;
        private int selectedTab = -1;

        private bool isPointerOnPanel;
        private Vector3 currentPanelHitPoint;
        private Slider activeDraggingSlider;
        private Scrollbar activeDraggingScrollbar;

        private const string MapName = "Chimeral Theater";
        private static readonly Color TabNormalColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        private static readonly Color TabSelectedColor = new Color(0.35f, 0.35f, 0.4f, 1f);
        private static readonly Color TabHoverColor = new Color(0.45f, 0.48f, 0.55f, 1f);

        // Properties for editor setup
        public GameObject Panel { get => panel; set => panel = value; }
        public LineRenderer Laser { get => laser; set => laser = value; }
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
        public VRLocomotion Locomotion { get => locomotion; set => locomotion = value; }
        public Camera Hmd { get => hmd; set => hmd = value; }
        public Transform LeftHand { get => leftHand; set => leftHand = value; }
        public Transform RightHand { get => rightHand; set => rightHand = value; }
        public FloatingUIFollower Follower { get => follower; set => follower = value; }

        private void Awake()
        {
            ResolveReferences();
            ResolveInputActions();
            EnsureFollower();
            EnsureLaserMaterial();
            InitializeUI();
        }

        private void EnsureLaserMaterial()
        {
            if (laser == null)
                return;

            if (laser.sharedMaterial == null || laser.sharedMaterial.shader == null || laser.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    Material mat = new Material(shader);
                    mat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f, 0.9f));
                    laser.material = mat;
                }
            }
        }

        private void ResolveReferences()
        {
            if (hmd == null)
            {
                hmd = Camera.main;
                if (hmd == null)
                    hmd = FindFirstObjectByType<Camera>();
            }

            if (locomotion == null)
            {
                locomotion = FindFirstObjectByType<VRLocomotion>();
            }

            if (leftHand == null)
            {
                GameObject leftHandObj = GameObject.Find("Left Hand");
                if (leftHandObj != null)
                    leftHand = leftHandObj.transform;
            }

            if (rightHand == null)
            {
                GameObject rightHandObj = GameObject.Find("Right Hand");
                if (rightHandObj != null)
                    rightHand = rightHandObj.transform;
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
            interactAction = map.FindAction("UIInteract", false);
            leftPositionAction = map.FindAction("LeftHandPosition", false);
            rightPositionAction = map.FindAction("RightHandPosition", false);
            rightRotationAction = map.FindAction("RightHandRotation", false);
        }

        private void InitializeUI()
        {
            if (panel != null)
            {
                panelRect = panel.GetComponent<RectTransform>();
                Canvas canvas = panel.GetComponent<Canvas>();
                if (canvas != null)
                    canvas.worldCamera = hmd;
            }

            if (settingsTabButton != null)
            {
                settingsTabRect = settingsTabButton.GetComponent<RectTransform>();
                settingsTabButton.onClick.AddListener(() => SelectTab(0));
            }

            if (filmsTabButton != null)
            {
                filmsTabRect = filmsTabButton.GetComponent<RectTransform>();
                filmsTabButton.onClick.AddListener(() => SelectTab(1));
            }

            if (filesTabButton != null)
            {
                filesTabRect = filesTabButton.GetComponent<RectTransform>();
                filesTabButton.onClick.AddListener(() => SelectTab(2));
            }

            if (assetInventoryTabButton != null)
            {
                assetInventoryTabRect = assetInventoryTabButton.GetComponent<RectTransform>();
                assetInventoryTabButton.onClick.AddListener(() => SelectTab(3));
            }

            if (locomotionSlider != null)
            {
                locomotionSliderRect = locomotionSlider.GetComponent<RectTransform>();
                locomotionSlider.onValueChanged.AddListener(OnLocomotionSpeedChanged);
                locomotionSlider.minValue = 1f;
                locomotionSlider.maxValue = 5f;
            }

            if (rotationSlider != null)
            {
                rotationSliderRect = rotationSlider.GetComponent<RectTransform>();
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
            interactAction?.Enable();
            leftPositionAction?.Enable();
            rightPositionAction?.Enable();
            rightRotationAction?.Enable();

            if (dashboardAction != null)
                dashboardAction.performed += ToggleDashboard;

            if (interactAction != null)
            {
                interactAction.performed += BeginInteraction;
                interactAction.canceled += EndInteraction;
            }
        }

        private void OnDisable()
        {
            if (dashboardAction != null)
                dashboardAction.performed -= ToggleDashboard;

            if (interactAction != null)
            {
                interactAction.performed -= BeginInteraction;
                interactAction.canceled -= EndInteraction;
            }

            dashboardAction?.Disable();
            interactAction?.Disable();
            leftPositionAction?.Disable();
            rightPositionAction?.Disable();
            rightRotationAction?.Disable();
        }

        private void Update()
        {
            if (!isOpen)
                return;

            UpdateDashboardPosition();
            UpdateLaserAndPointer();
        }

        private void SetVisualsActive(bool active)
        {
            if (panel != null)
                panel.SetActive(active);

            if (laser != null)
                laser.gameObject.SetActive(active);
        }

        private Vector3 GetLeftHandWorldPosition()
        {
            if (leftHand != null)
                return leftHand.position;

            if (leftPositionAction != null && hmd != null && hmd.transform.parent != null)
            {
                return hmd.transform.parent.TransformPoint(leftPositionAction.ReadValue<Vector3>());
            }

            if (hmd != null)
                return hmd.transform.position + hmd.transform.forward * 0.4f + Vector3.down * 0.2f - hmd.transform.right * 0.2f;

            return Vector3.zero;
        }

        private bool GetRightHandRay(out Vector3 origin, out Vector3 direction)
        {
            if (rightHand != null)
            {
                origin = rightHand.position;
                direction = rightHand.forward;
                return true;
            }

            if (rightPositionAction != null && rightRotationAction != null && hmd != null && hmd.transform.parent != null)
            {
                Transform trackingSpace = hmd.transform.parent;
                origin = trackingSpace.TransformPoint(rightPositionAction.ReadValue<Vector3>());
                Quaternion rot = trackingSpace.rotation * rightRotationAction.ReadValue<Quaternion>();
                direction = rot * Vector3.forward;
                return true;
            }

            if (hmd != null)
            {
                origin = hmd.transform.position + hmd.transform.forward * 0.4f + Vector3.down * 0.2f + hmd.transform.right * 0.2f;
                direction = hmd.transform.forward;
                return true;
            }

            origin = Vector3.zero;
            direction = Vector3.forward;
            return false;
        }

        private void EnsureFollower()
        {
            // The panel itself carries the follow behaviour; make sure it exists and points at
            // the HMD as the viewer, so any floating panel built the same way shares the movement.
            if (follower == null && panel != null)
                follower = panel.GetComponent<FloatingUIFollower>() ?? panel.AddComponent<FloatingUIFollower>();

            if (follower != null)
                follower.Viewer = hmd != null ? hmd.transform : null;
        }

        private void UpdateDashboardPosition()
        {
            if (follower == null || panel == null)
                return;

            follower.Follow(GetLeftHandWorldPosition(), Time.deltaTime);
        }

        private void UpdateLaserAndPointer()
        {
            if (laser == null || !GetRightHandRay(out Vector3 rayOrigin, out Vector3 rayDirection))
                return;

            laser.SetPosition(0, rayOrigin);

            if (panel == null || !panel.activeInHierarchy)
            {
                laser.SetPosition(1, rayOrigin + rayDirection * laserLength);
                ClearPointer();
                return;
            }

            if (panelRect == null)
                panelRect = panel.GetComponent<RectTransform>();

            Ray ray = new Ray(rayOrigin, rayDirection);
            Plane planeFront = new Plane(-panel.transform.forward, panel.transform.position);
            Plane planeBack = new Plane(panel.transform.forward, panel.transform.position);

            float distance = 0f;
            bool hit = planeFront.Raycast(ray, out distance) && distance > 0f;
            if (!hit)
                hit = planeBack.Raycast(ray, out distance) && distance > 0f;

            if (hit && distance <= laserLength)
            {
                Vector3 hitPoint = ray.GetPoint(distance);
                Vector3 localHit = panel.transform.InverseTransformPoint(hitPoint);

                if (panelRect != null && panelRect.rect.Contains(new Vector2(localHit.x, localHit.y)))
                {
                    laser.SetPosition(1, hitPoint);
                    isPointerOnPanel = true;
                    currentPanelHitPoint = hitPoint;
                    ProcessPanelHoverAndDrag(hitPoint);
                    return;
                }
            }

            laser.SetPosition(1, rayOrigin + rayDirection * laserLength);
            ClearPointer();
        }

        private void ProcessPanelHoverAndDrag(Vector3 hitPoint)
        {
            // Update tab hover highlights
            bool overSettings = IsHitInRect(settingsTabRect, hitPoint);
            bool overFilms = IsHitInRect(filmsTabRect, hitPoint);
            bool overFiles = IsHitInRect(filesTabRect, hitPoint);
            bool overInventory = IsHitInRect(assetInventoryTabRect, hitPoint);

            UpdateTabImageColor(settingsTabImage, overSettings, selectedTab == 0);
            UpdateTabImageColor(filmsTabImage, overFilms, selectedTab == 1);
            UpdateTabImageColor(filesTabImage, overFiles, selectedTab == 2);
            UpdateTabImageColor(assetInventoryTabImage, overInventory, selectedTab == 3);

            // Drag slider if active
            if (activeDraggingSlider != null)
            {
                UpdateSliderFromHit(activeDraggingSlider, hitPoint);
            }

            if (activeDraggingScrollbar != null)
                UpdateScrollbarFromHit(activeDraggingScrollbar, hitPoint);
        }

        private void UpdateTabImageColor(Image img, bool isHovered, bool isSelected)
        {
            if (img == null)
                return;

            if (isHovered)
                img.color = TabHoverColor;
            else
                img.color = isSelected ? TabSelectedColor : TabNormalColor;
        }

        private void ClearPointer()
        {
            isPointerOnPanel = false;
            activeDraggingSlider = null;
            activeDraggingScrollbar = null;

            UpdateTabImageColor(settingsTabImage, false, selectedTab == 0);
            UpdateTabImageColor(filmsTabImage, false, selectedTab == 1);
            UpdateTabImageColor(filesTabImage, false, selectedTab == 2);
            UpdateTabImageColor(assetInventoryTabImage, false, selectedTab == 3);
        }

        private void BeginInteraction(InputAction.CallbackContext context)
        {
            if (!isOpen || !isPointerOnPanel)
                return;

            // Any active button under the pointer gets its onClick invoked; the topmost hit
            // wins so a per-row delete button beats its own row button. Tab buttons are wired
            // through onClick too, so this covers every clickable in the panel.
            if (DispatchButtonClick(currentPanelHitPoint))
                return;

            // Grab a list's scrollbar and drag it while the trigger is held.
            Scrollbar bar = FindScrollbarAt(currentPanelHitPoint);
            if (bar != null)
            {
                activeDraggingScrollbar = bar;
                UpdateScrollbarFromHit(bar, currentPanelHitPoint);
                return;
            }

            // Click or start dragging Locomotion Slider
            if (selectedTab == 0 && IsHitInRect(locomotionSliderRect, currentPanelHitPoint))
            {
                activeDraggingSlider = locomotionSlider;
                UpdateSliderFromHit(locomotionSlider, currentPanelHitPoint);
                return;
            }

            // Click or start dragging Rotation Slider
            if (selectedTab == 0 && IsHitInRect(rotationSliderRect, currentPanelHitPoint))
            {
                activeDraggingSlider = rotationSlider;
                UpdateSliderFromHit(rotationSlider, currentPanelHitPoint);
                return;
            }
        }

        /// <summary>Invokes the topmost interactable button containing the hit point. Returns
        /// false when no button was under the pointer.</summary>
        private bool DispatchButtonClick(Vector3 worldHitPoint)
        {
            if (panel == null)
                return false;

            Button[] buttons = panel.GetComponentsInChildren<Button>(false);
            Button best = null;
            int bestDepth = int.MinValue;

            foreach (Button button in buttons)
            {
                if (button == null || !button.IsActive() || !button.interactable)
                    continue;

                RectTransform rect = button.GetComponent<RectTransform>();
                if (rect == null || !IsHitInRect(rect, worldHitPoint) || !IsInsideMasks(button.transform, worldHitPoint))
                    continue;

                // Depth in the hierarchy decides which overlapping button wins: a delete X on
                // top of a row button is deeper in the hierarchy and wins the click. At equal depth
                // the later one in hierarchy order is drawn on top, so it wins.
                int depth = GetHierarchyDepth(button.transform);
                if (depth >= bestDepth)
                {
                    best = button;
                    bestDepth = depth;
                }
            }

            if (best == null)
                return false;

            best.onClick.Invoke();
            return true;
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        /// <summary>True when the point is inside every mask above the element. A list row scrolled out of
        /// its viewport is still in the hierarchy but hidden by the mask, so it must not take clicks.</summary>
        private bool IsInsideMasks(Transform element, Vector3 worldHitPoint)
        {
            for (Transform t = element.parent; t != null; t = t.parent)
            {
                bool clips = (t.TryGetComponent(out Mask mask) && mask.enabled) ||
                             (t.TryGetComponent(out RectMask2D rectMask) && rectMask.enabled);
                if (clips && !IsHitInRect(t as RectTransform, worldHitPoint))
                    return false;
            }

            return true;
        }

        private Scrollbar FindScrollbarAt(Vector3 worldHitPoint)
        {
            if (panel == null)
                return null;

            foreach (Scrollbar bar in panel.GetComponentsInChildren<Scrollbar>(false))
            {
                if (bar.IsActive() && bar.interactable && IsHitInRect(bar.GetComponent<RectTransform>(), worldHitPoint))
                    return bar;
            }

            return null;
        }

        /// <summary>Puts the scrollbar's handle centre under the pointer along the bar.</summary>
        private void UpdateScrollbarFromHit(Scrollbar bar, Vector3 worldHitPoint)
        {
            RectTransform area = bar.handleRect != null && bar.handleRect.parent is RectTransform sliding
                ? sliding
                : bar.GetComponent<RectTransform>();
            Vector3 local = area.InverseTransformPoint(worldHitPoint);

            float handleSpan = area.rect.height * bar.size;
            float t = Mathf.InverseLerp(area.rect.yMin + handleSpan * 0.5f, area.rect.yMax - handleSpan * 0.5f, local.y);
            if (bar.direction == Scrollbar.Direction.TopToBottom)
                t = 1f - t;
            bar.value = Mathf.Clamp01(t);
        }

        private void EndInteraction(InputAction.CallbackContext context)
        {
            activeDraggingSlider = null;
            activeDraggingScrollbar = null;
        }

        private void UpdateSliderFromHit(Slider slider, Vector3 worldHitPoint)
        {
            if (slider == null)
                return;

            RectTransform rect = slider.GetComponent<RectTransform>();
            Vector3 local = rect.InverseTransformPoint(worldHitPoint);

            float t = Mathf.Clamp01(Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x));
            slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, t);
        }

        private bool IsHitInRect(RectTransform rect, Vector3 worldPoint)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
                return false;

            Vector3 local = rect.InverseTransformPoint(worldPoint);
            return rect.rect.Contains(new Vector2(local.x, local.y));
        }

        /// <summary>Open or close the dashboard from code. Closing clears the pointer.</summary>
        public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            SetVisualsActive(false);
            ClearPointer();
        }

        private void ToggleDashboard(InputAction.CallbackContext context)
        {
            isOpen = !isOpen;
            SetVisualsActive(isOpen);

            if (isOpen)
            {
                if (follower != null)
                    follower.Snap(GetLeftHandWorldPosition());
            }
            else
            {
                ClearPointer();
            }
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

            UpdateTabImageColor(settingsTabImage, false, selectedTab == 0);
            UpdateTabImageColor(filmsTabImage, false, selectedTab == 1);
            UpdateTabImageColor(filesTabImage, false, selectedTab == 2);
            UpdateTabImageColor(assetInventoryTabImage, false, selectedTab == 3);
        }

        private void OnLocomotionSpeedChanged(float value)
        {
            if (locomotionValueText != null)
                locomotionValueText.text = $"{value:0.0}x";

            if (locomotion != null)
                locomotion.SetSpeedMultiplier(value);
        }

        private void OnRotationSpeedChanged(float value)
        {
            if (rotationValueText != null)
                rotationValueText.text = $"{value:0.0}x";

            if (locomotion != null)
                locomotion.SetRotationSpeedMultiplier(value);
        }
    }
}
