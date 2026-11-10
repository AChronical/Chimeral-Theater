using ChimeralTheater.Films;
using ChimeralTheater.Locomotion;
using ChimeralTheater.Props;
using ChimeralTheater.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChimeralTheater.Editor
{
    [InitializeOnLoad]
    public static class VRDashboardPrefabBuilder
    {
        private const string PrefabFolderPath = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/VRDashboard.prefab";
        private const string InputAssetPath = "Assets/Input/ChimeralInput.inputactions";

        static VRDashboardPrefabBuilder()
        {
            EditorApplication.delayCall += EnsurePrefabAndSceneSetup;
        }

        [MenuItem("Chimeral/Build VR Dashboard Prefab and Scene Instance")]
        public static void BuildManual()
        {
            EnsurePrefabAndSceneSetup();
        }

        private static void EnsurePrefabAndSceneSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                prefab = CreatePrefab();
            }
            else if (IsPrefabStale(prefab))
            {
                // The prefab predates the films UI or the floating follower: rebuild it at the
                // same path so the asset guid stays put, then replace the scene instance so no
                // stale overrides survive.
                prefab = CreatePrefab();
                ReplaceSceneInstance(prefab);
            }

            SetupSceneInstance(prefab);
        }

        private static bool IsPrefabStale(GameObject prefab)
        {
            if (prefab.GetComponent<VRDashboard>() == null)
                return true;

            if (prefab.GetComponentInChildren<FloatingUIFollower>(true) == null)
                return true;

            if (prefab.GetComponentInChildren<FilmsPanel>(true) == null)
                return true;

            var files = prefab.GetComponentInChildren<UserFilesPanel>(true);
            if (files == null || files.FileListContent == null)
                return true;

            var inventory = prefab.GetComponentInChildren<AssetInventoryPanel>(true);
            if (inventory == null || inventory.InventoryListContent == null)
                return true;

            // Panels only clone templates now, so every button they bind must already be in the prefab.
            if (files.ConvertPropButton == null || inventory.InventoryRowTemplate.transform.Find("Wear") == null)
                return true;
            var films = prefab.GetComponentInChildren<FilmsPanel>(true);
            if (films.AvatarPickerView == null || films.ActRowTemplate.transform.Find("Avatar") == null)
                return true;

            // The prefab predates the switch from hand-placed anchors to layout groups: rebuild
            // so every row goes through Horizontal/VerticalLayoutGroup instead.
            if (prefab.GetComponentInChildren<FilmsPanel>(true)?.TakeDetailView?.GetComponent<VerticalLayoutGroup>() == null)
                return true;

            return false;
        }

        private static void ReplaceSceneInstance(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
                return;

            VRDashboard existing = Object.FindFirstObjectByType<VRDashboard>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        public static GameObject CreatePrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont("Arial", 24);

            GameObject root = new GameObject("VRDashboard");
            VRDashboard dash = root.AddComponent<VRDashboard>();

            // Panel Root with World Space Canvas
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image));
            panel.transform.SetParent(root.transform, false);

            Canvas canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(800f, 500f);
            panel.transform.localScale = Vector3.one * 0.001f;

            Image bg = panel.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.11f, 0.13f, 0.95f);
            bg.raycastTarget = true;

            // Floating panel behaviour shared by every head/hand-following panel
            FloatingUIFollower follower = panel.AddComponent<FloatingUIFollower>();
            follower.AnchorOffset = new Vector3(0f, 0.15f, 0.25f);
            follower.FollowSharpness = 8f;

            // Header Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(40f, -30f);
            titleRect.sizeDelta = new Vector2(500f, 50f);

            Text titleText = titleObj.GetComponent<Text>();
            titleText.text = "Settings";
            titleText.font = font;
            titleText.fontSize = 34;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;

            // Settings Content: a VerticalLayoutGroup stacks one row per setting, so rows never
            // overlap no matter how many settings get added later.
            GameObject settingsContent = new GameObject("Settings Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            settingsContent.transform.SetParent(panel.transform, false);
            RectTransform settingsRect = settingsContent.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(0f, 0.2f);
            settingsRect.anchorMax = new Vector2(1f, 0.82f);
            settingsRect.offsetMin = new Vector2(40f, 0f);
            settingsRect.offsetMax = new Vector2(-40f, 0f);

            VerticalLayoutGroup settingsLayout = settingsContent.GetComponent<VerticalLayoutGroup>();
            settingsLayout.spacing = 20f;
            settingsLayout.childControlWidth = true;
            settingsLayout.childControlHeight = true;
            settingsLayout.childForceExpandWidth = true;
            settingsLayout.childForceExpandHeight = false;
            settingsLayout.childAlignment = TextAnchor.UpperLeft;

            Slider locomotionSlider = CreateSettingSlider(settingsContent.transform, "Locomotion Speed", font, out Text locoValText);
            Slider rotationSlider = CreateSettingSlider(settingsContent.transform, "Rotation Speed", font, out Text rotValText);

            // Films Content
            GameObject filmsContent = new GameObject("Films Content", typeof(RectTransform));
            filmsContent.transform.SetParent(panel.transform, false);
            RectTransform filmsRect = filmsContent.GetComponent<RectTransform>();
            filmsRect.anchorMin = new Vector2(0f, 0.2f);
            filmsRect.anchorMax = new Vector2(1f, 0.82f);
            filmsRect.offsetMin = new Vector2(40f, 0f);
            filmsRect.offsetMax = new Vector2(-40f, 0f);
            filmsContent.SetActive(false);

            FilmsPanel filmsPanel = BuildFilmsUI(filmsContent.transform, font);

            // Files Content
            GameObject filesContent = new GameObject("Files Content", typeof(RectTransform));
            filesContent.transform.SetParent(panel.transform, false);
            RectTransform filesRect = filesContent.GetComponent<RectTransform>();
            filesRect.anchorMin = new Vector2(0f, 0.2f);
            filesRect.anchorMax = new Vector2(1f, 0.82f);
            filesRect.offsetMin = new Vector2(40f, 0f);
            filesRect.offsetMax = new Vector2(-40f, 0f);
            filesContent.SetActive(false);
            UserFilesPanel filesPanel = BuildFilesUI(filesContent.transform, font);

            // Asset Inventory Content
            GameObject inventoryContent = new GameObject("Asset Inventory Content", typeof(RectTransform));
            inventoryContent.transform.SetParent(panel.transform, false);
            RectTransform inventoryRect = inventoryContent.GetComponent<RectTransform>();
            inventoryRect.anchorMin = new Vector2(0f, 0.2f);
            inventoryRect.anchorMax = new Vector2(1f, 0.82f);
            inventoryRect.offsetMin = new Vector2(40f, 0f);
            inventoryRect.offsetMax = new Vector2(-40f, 0f);
            inventoryContent.SetActive(false);
            AssetInventoryPanel inventoryPanel = BuildInventoryUI(inventoryContent.transform, font);

            // Bottom Tabs: a HorizontalLayoutGroup divides the bar evenly between however many
            // tab buttons exist, so adding or removing a tab can never overlap its neighbours.
            float buttonHeight = 70f;
            GameObject tabBar = new GameObject("Tab Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabBar.transform.SetParent(panel.transform, false);
            RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
            tabBarRect.anchorMin = new Vector2(0f, 0f);
            tabBarRect.anchorMax = new Vector2(1f, 0f);
            tabBarRect.pivot = new Vector2(0.5f, 0f);
            tabBarRect.sizeDelta = new Vector2(0f, buttonHeight);
            tabBarRect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup tabBarLayout = tabBar.GetComponent<HorizontalLayoutGroup>();
            tabBarLayout.childControlWidth = true;
            tabBarLayout.childControlHeight = true;
            tabBarLayout.childForceExpandWidth = true;
            tabBarLayout.childForceExpandHeight = true;

            Button settingsTabBtn = CreateTabButton(tabBar.transform, "Settings", font, out Image settingsTabImg);
            Button filmsTabBtn = CreateTabButton(tabBar.transform, "Films", font, out Image filmsTabImg);
            Button filesTabBtn = CreateTabButton(tabBar.transform, "Files", font, out Image filesTabImg);
            Button inventoryTabBtn = CreateTabButton(tabBar.transform, "Inventory", font, out Image inventoryTabImg);

            // Laser
            GameObject laserObj = new GameObject("Laser", typeof(LineRenderer));
            laserObj.transform.SetParent(root.transform, false);
            LineRenderer laser = laserObj.GetComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.005f;
            laser.endWidth = 0.0015f;

            Material laserMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/LaserMaterial.mat");
            if (laserMat != null)
                laser.sharedMaterial = laserMat;

            laser.startColor = new Color(0.1f, 0.85f, 1f, 0.9f);
            laser.endColor = new Color(0.1f, 0.85f, 1f, 0.2f);

            // Wire all references into VRDashboard component
            dash.Panel = panel;
            dash.Laser = laser;
            dash.TabTitleText = titleText;
            dash.SettingsContent = settingsContent;
            dash.FilmsContent = filmsContent;
            dash.FilesContent = filesContent;
            dash.AssetInventoryContent = inventoryContent;
            dash.SettingsTabButton = settingsTabBtn;
            dash.FilmsTabButton = filmsTabBtn;
            dash.FilesTabButton = filesTabBtn;
            dash.AssetInventoryTabButton = inventoryTabBtn;
            dash.SettingsTabImage = settingsTabImg;
            dash.FilmsTabImage = filmsTabImg;
            dash.FilesTabImage = filesTabImg;
            dash.AssetInventoryTabImage = inventoryTabImg;
            dash.LocomotionSlider = locomotionSlider;
            dash.RotationSlider = rotationSlider;
            dash.LocomotionValueText = locoValText;
            dash.RotationValueText = rotValText;
            dash.Follower = follower;

            // Visuals start disabled
            panel.SetActive(false);
            laserObj.SetActive(false);

            // Save Prefab Asset
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VRDashboardPrefabBuilder] Created prefab at {PrefabPath}");
            return savedPrefab;
        }

        private static Slider CreateSettingSlider(Transform parent, string label, Font font, out Text valueLabel)
        {
            // One row: Label (fixed width) | Slider (flexible, fills the middle) | Value (fixed
            // width). The HorizontalLayoutGroup places all three without any manual X math.
            GameObject row = new GameObject(label + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            LayoutElement rowLayoutElement = row.GetComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 40f;
            rowLayoutElement.flexibleHeight = 0f;

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            // Label
            GameObject labelObj = new GameObject(label + " Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            labelObj.transform.SetParent(row.transform, false);
            labelObj.GetComponent<LayoutElement>().preferredWidth = 300f;

            Text labelText = labelObj.GetComponent<Text>();
            labelText.text = label;
            labelText.font = font;
            labelText.fontSize = 24;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;

            // Slider
            GameObject sliderObj = new GameObject(label + " Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObj.transform.SetParent(row.transform, false);
            sliderObj.GetComponent<LayoutElement>().flexibleWidth = 1f;
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(0f, 28f);

            Slider slider = sliderObj.GetComponent<Slider>();
            slider.minValue = 1f;
            slider.maxValue = 5f;
            slider.value = 1f;
            slider.wholeNumbers = false;

            // Background Track
            GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            Stretch(bgRect);
            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.25f, 0.27f, 0.32f, 1f);
            bgImg.raycastTarget = true;
            slider.targetGraphic = bgImg;

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(5f, 0f);
            fillAreaRect.offsetMax = new Vector2(-5f, 0f);

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            Stretch(fillRect);
            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.color = new Color(0.2f, 0.6f, 0.95f, 1f);
            fillImg.raycastTarget = false;
            slider.fillRect = fillRect;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handleObj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObj.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(24f, 36f);
            Image handleImg = handleObj.GetComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            slider.handleRect = handleRect;

            // Value text
            GameObject valObj = new GameObject(label + " Value Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            valObj.transform.SetParent(row.transform, false);
            valObj.GetComponent<LayoutElement>().preferredWidth = 90f;

            valueLabel = valObj.GetComponent<Text>();
            valueLabel.text = "1.0x";
            valueLabel.font = font;
            valueLabel.fontSize = 24;
            valueLabel.color = Color.white;
            valueLabel.alignment = TextAnchor.MiddleRight;
            valueLabel.raycastTarget = false;

            return slider;
        }

        private static Button CreateTabButton(Transform parent, string label, Font font, out Image buttonImage)
        {
            GameObject buttonObj = new GameObject(label + " Tab", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            buttonImage = buttonObj.GetComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            buttonImage.raycastTarget = true;

            Button button = buttonObj.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            colors.highlightedColor = new Color(0.3f, 0.32f, 0.38f, 1f);
            colors.pressedColor = new Color(0.18f, 0.18f, 0.2f, 1f);
            button.colors = colors;

            GameObject textObj = new GameObject(label + " Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());

            Text text = textObj.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return button;
        }

        private static FilmsPanel BuildFilmsUI(Transform filmsContent, Font font)
        {
            // The panel script rides on the Films Content object itself.
            FilmsPanel films = filmsContent.gameObject.AddComponent<FilmsPanel>();

            // --- Takes view: a VerticalLayoutGroup stacks the flexible takes list above a
            // fixed-height footer row holding Add, so the button can never sit on top of the list. ---
            GameObject takesView = new GameObject("Takes View", typeof(RectTransform), typeof(VerticalLayoutGroup));
            takesView.transform.SetParent(filmsContent, false);
            Stretch(takesView.GetComponent<RectTransform>());

            VerticalLayoutGroup takesViewLayout = takesView.GetComponent<VerticalLayoutGroup>();
            takesViewLayout.spacing = 10f;
            takesViewLayout.childControlWidth = true;
            takesViewLayout.childControlHeight = true;
            takesViewLayout.childForceExpandWidth = true;
            takesViewLayout.childForceExpandHeight = false;

            RectTransform takesViewport;
            GameObject takesListRoot = BuildScrollList(takesView.transform, "Takes List", font, out takesViewport);
            LayoutElement takesListLE = takesListRoot.AddComponent<LayoutElement>();
            takesListLE.flexibleHeight = 1f;
            takesListLE.minHeight = 80f;

            GameObject takeTemplate = BuildTakeRowTemplate(takesViewport, font);

            GameObject takesFooterRow = new GameObject("Footer Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            takesFooterRow.transform.SetParent(takesView.transform, false);
            LayoutElement takesFooterLE = takesFooterRow.GetComponent<LayoutElement>();
            takesFooterLE.preferredHeight = 70f;
            takesFooterLE.flexibleHeight = 0f;

            HorizontalLayoutGroup takesFooterLayout = takesFooterRow.GetComponent<HorizontalLayoutGroup>();
            takesFooterLayout.childControlWidth = true;
            takesFooterLayout.childControlHeight = true;
            takesFooterLayout.childForceExpandWidth = false;
            takesFooterLayout.childForceExpandHeight = true;
            takesFooterLayout.childAlignment = TextAnchor.MiddleRight;

            GameObject addTake = BuildAddButton(takesFooterRow.transform, font, out Image _);
            SetFixedWidth(addTake, 160f);

            films.TakesView = takesView;
            films.TakesListContent = takesViewport;
            films.TakeRowTemplate = takeTemplate;
            films.AddTakeButton = addTake.GetComponent<Button>();

            // --- Take detail view: a VerticalLayoutGroup stacks a fixed-height header row, a
            // flexible acts list and a fixed-height footer row, so none can ever overlap another
            // regardless of content size.
            GameObject detailView = new GameObject("Take Detail View", typeof(RectTransform), typeof(VerticalLayoutGroup));
            detailView.transform.SetParent(filmsContent, false);
            Stretch(detailView.GetComponent<RectTransform>());

            VerticalLayoutGroup detailLayout = detailView.GetComponent<VerticalLayoutGroup>();
            detailLayout.spacing = 10f;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;

            // Header row: Back | Title (flexible, fills the middle) | Add Act
            GameObject headerRow = new GameObject("Header Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            headerRow.transform.SetParent(detailView.transform, false);
            LayoutElement headerRowLE = headerRow.GetComponent<LayoutElement>();
            headerRowLE.preferredHeight = 60f;
            headerRowLE.flexibleHeight = 0f;

            HorizontalLayoutGroup headerLayout = headerRow.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 10f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            Button back = BuildPlainButton(headerRow.transform, "Back", font);
            SetFixedWidth(back.gameObject, 140f);

            GameObject titleObj = new GameObject("Take Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            titleObj.transform.SetParent(headerRow.transform, false);
            titleObj.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Text title = titleObj.GetComponent<Text>();
            title.text = "Take 1";
            title.font = font;
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.color = Color.white;
            title.alignment = TextAnchor.MiddleCenter;
            title.raycastTarget = false;
            films.TakeTitleText = title;

            GameObject addAct = BuildAddButton(headerRow.transform, font, out Image _);
            SetFixedWidth(addAct, 160f);

            // Acts list: grows to fill whatever space is left between the header and footer rows.
            RectTransform actsViewport;
            GameObject actsListRoot = BuildScrollList(detailView.transform, "Acts List", font, out actsViewport);
            LayoutElement actsLE = actsListRoot.AddComponent<LayoutElement>();
            actsLE.flexibleHeight = 1f;
            actsLE.minHeight = 80f;

            GameObject actTemplate = BuildActRowTemplate(actsViewport, font);

            // Footer row: Record | flexible spacer | Play
            GameObject footerRow = new GameObject("Footer Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            footerRow.transform.SetParent(detailView.transform, false);
            LayoutElement footerRowLE = footerRow.GetComponent<LayoutElement>();
            footerRowLE.preferredHeight = 70f;
            footerRowLE.flexibleHeight = 0f;

            HorizontalLayoutGroup footerLayout = footerRow.GetComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 10f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = true;

            Button record = BuildPlainButton(footerRow.transform, "Record", font);
            SetFixedWidth(record.gameObject, 240f);

            GameObject footerSpacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            footerSpacer.transform.SetParent(footerRow.transform, false);
            footerSpacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Button play = BuildPlainButton(footerRow.transform, "Play", font);
            SetFixedWidth(play.gameObject, 240f);

            films.TakeDetailView = detailView;
            films.ActsListContent = actsViewport;
            films.ActRowTemplate = actTemplate;
            films.AddActButton = addAct.GetComponent<Button>();
            films.BackButton = back;
            films.RecordButton = record;
            films.PlayButton = play;

            // --- Avatar picker view: title, the avatar list and a Back button ---
            GameObject pickerView = new GameObject("Avatar Picker View", typeof(RectTransform), typeof(VerticalLayoutGroup));
            pickerView.transform.SetParent(filmsContent, false);
            Stretch(pickerView.GetComponent<RectTransform>());

            VerticalLayoutGroup pickerLayout = pickerView.GetComponent<VerticalLayoutGroup>();
            pickerLayout.spacing = 10f;
            pickerLayout.childControlWidth = true;
            pickerLayout.childControlHeight = true;
            pickerLayout.childForceExpandWidth = true;
            pickerLayout.childForceExpandHeight = false;

            GameObject pickerTitleObj = new GameObject("Avatar Picker Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            pickerTitleObj.transform.SetParent(pickerView.transform, false);
            LayoutElement pickerTitleLE = pickerTitleObj.GetComponent<LayoutElement>();
            pickerTitleLE.preferredHeight = 50f;
            pickerTitleLE.flexibleHeight = 0f;
            Text pickerTitle = pickerTitleObj.GetComponent<Text>();
            pickerTitle.text = "Select Avatar";
            pickerTitle.font = font;
            pickerTitle.fontSize = 26;
            pickerTitle.fontStyle = FontStyle.Bold;
            pickerTitle.color = Color.white;
            pickerTitle.alignment = TextAnchor.MiddleCenter;
            pickerTitle.raycastTarget = false;

            RectTransform avatarsViewport;
            GameObject avatarsListRoot = BuildScrollList(pickerView.transform, "Avatars List", font, out avatarsViewport);
            LayoutElement avatarsLE = avatarsListRoot.AddComponent<LayoutElement>();
            avatarsLE.flexibleHeight = 1f;
            avatarsLE.minHeight = 80f;

            GameObject avatarTemplate = BuildTakeRowTemplate(avatarsViewport, font);
            avatarTemplate.name = "Avatar Row Template";

            GameObject pickerFooter = new GameObject("Footer Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            pickerFooter.transform.SetParent(pickerView.transform, false);
            LayoutElement pickerFooterLE = pickerFooter.GetComponent<LayoutElement>();
            pickerFooterLE.preferredHeight = 70f;
            pickerFooterLE.flexibleHeight = 0f;
            HorizontalLayoutGroup pickerFooterLayout = pickerFooter.GetComponent<HorizontalLayoutGroup>();
            pickerFooterLayout.childControlWidth = true;
            pickerFooterLayout.childControlHeight = true;
            pickerFooterLayout.childForceExpandWidth = false;
            pickerFooterLayout.childForceExpandHeight = true;
            pickerFooterLayout.childAlignment = TextAnchor.MiddleRight;

            Button pickerBack = BuildPlainButton(pickerFooter.transform, "Back", font);
            SetFixedWidth(pickerBack.gameObject, 160f);

            films.AvatarPickerView = pickerView;
            films.AvatarPickerTitleText = pickerTitle;
            films.AvatarListContent = avatarsViewport;
            films.AvatarRowTemplate = avatarTemplate;
            films.AvatarBackButton = pickerBack;

            // --- Recording view: one giant stop button ---
            GameObject recordingView = new GameObject("Recording View", typeof(RectTransform));
            recordingView.transform.SetParent(filmsContent, false);
            Stretch(recordingView.GetComponent<RectTransform>());

            Button stop = BuildPlainButton(recordingView.transform, "STOP RECORDING", font,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
            RectTransform stopRect = stop.GetComponent<RectTransform>();
            stopRect.anchorMin = new Vector2(0.1f, 0.1f);
            stopRect.anchorMax = new Vector2(0.9f, 0.9f);
            Image stopImage = stop.GetComponent<Image>();
            stopImage.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);

            films.RecordingView = recordingView;
            films.StopRecordingButton = stop;

            // Wire the panel to the director that the scene setup creates.
            films.Director = Object.FindFirstObjectByType<FilmDirector>();

            // The detail and recording views start hidden; the panel shows the right one itself.
            detailView.SetActive(false);
            pickerView.SetActive(false);
            recordingView.SetActive(false);

            return films;
        }

        private static UserFilesPanel BuildFilesUI(Transform filesContent, Font font)
        {
            UserFilesPanel panel = filesContent.gameObject.AddComponent<UserFilesPanel>();
            Color barButton = new Color(0.25f, 0.28f, 0.35f, 0.95f);

            // Top bar: Up (fixed) | path (flexible) | Refresh (fixed).
            GameObject topBar = BuildTopBar(filesContent);
            panel.UpButton = BuildRowButton(topBar.transform, "Up Button", "\u25B2 Up", font, 20, 80f, barButton);
            panel.CurrentPathText = BuildFlexibleText(topBar.transform, "Current Path", "UserFiles/", font, 20, FontStyle.Normal);
            panel.RefreshButton = BuildRowButton(topBar.transform, "Refresh Button", "\u27F3 Refresh", font, 18, 100f, barButton);

            // File list.
            GameObject list = BuildScrollList(filesContent, "Files List", font, out RectTransform content);
            SetAnchors(list.GetComponent<RectTransform>(), new Vector2(0f, 0.28f), Vector2.one, Vector2.zero, new Vector2(0f, -50f));
            panel.FileListContent = content;
            panel.FileRowTemplate = BuildLabelRowTemplate(content, "File Row Template", font, 44f, 20);

            // Detail view: info column (flexible) | convert column (fixed).
            GameObject detail = new GameObject("Detail View", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            detail.transform.SetParent(filesContent, false);
            SetAnchors(detail.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.26f), Vector2.zero, Vector2.zero);
            detail.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
            HorizontalLayoutGroup detailLayout = detail.GetComponent<HorizontalLayoutGroup>();
            detailLayout.padding = new RectOffset(12, 12, 8, 8);
            detailLayout.spacing = 16f;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            detailLayout.childForceExpandWidth = false;
            detailLayout.childForceExpandHeight = true;

            GameObject info = BuildColumn(detail.transform, "Info Column", 0f, 1f);
            panel.SelectedFileNameText = BuildColumnText(info.transform, "Selected Name", font, 20, FontStyle.Bold, Color.white);
            panel.SelectedFileInfoText = BuildColumnText(info.transform, "Selected Info", font, 16, FontStyle.Normal, new Color(0.75f, 0.75f, 0.75f, 1f));
            panel.ConversionStatusText = BuildColumnText(info.transform, "Conversion Status", font, 15, FontStyle.Normal, new Color(0.3f, 0.85f, 0.9f, 1f));

            GameObject convert = BuildColumn(detail.transform, "Convert Column", 180f, 0f);
            panel.ConvertButton = BuildRowButton(convert.transform, "Convert Button", "Convert to .chavi", font, 20, 0f, new Color(0.2f, 0.55f, 0.3f, 0.95f));
            panel.ConvertPropButton = BuildRowButton(convert.transform, "Convert Prop Button", "Convert to .chprop", font, 20, 0f, new Color(0.25f, 0.4f, 0.65f, 0.95f));

            panel.DetailView = detail;
            detail.SetActive(false);
            return panel;
        }

        private static AssetInventoryPanel BuildInventoryUI(Transform inventoryContent, Font font)
        {
            AssetInventoryPanel panel = inventoryContent.gameObject.AddComponent<AssetInventoryPanel>();

            // Top bar: count (flexible) | Refresh (fixed).
            GameObject topBar = BuildTopBar(inventoryContent);
            topBar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(10, 10, 0, 0);
            panel.CountText = BuildFlexibleText(topBar.transform, "Count Text", "0 Avatars", font, 22, FontStyle.Bold);
            panel.RefreshButton = BuildRowButton(topBar.transform, "Refresh Button", "\u27F3 Refresh", font, 18, 100f, new Color(0.25f, 0.28f, 0.35f, 0.95f));

            GameObject list = BuildScrollList(inventoryContent, "Inventory List", font, out RectTransform content);
            SetAnchors(list.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -50f));
            panel.InventoryListContent = content;

            // Row template: Label (flexible) | Wear | Delete.
            GameObject row = new GameObject("Inventory Row Template", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(content, false);
            LayoutElement rowLE = row.GetComponent<LayoutElement>();
            rowLE.minHeight = 44f;
            rowLE.preferredHeight = 44f;
            row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            row.GetComponent<Button>().transition = Selectable.Transition.None;
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(12, 6, 0, 0);
            rowLayout.spacing = 6f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            BuildFlexibleText(row.transform, "Label", "", font, 20, FontStyle.Normal);
            BuildRowButton(row.transform, "Wear", "Wear", font, 18, 110f, new Color(0.2f, 0.5f, 0.3f, 0.95f));
            BuildRowButton(row.transform, "Delete", "\u2715", font, 18, 40f, new Color(0.8f, 0.25f, 0.25f, 0.9f));
            row.SetActive(false);

            panel.InventoryRowTemplate = row;
            return panel;
        }

        private static GameObject BuildTopBar(Transform parent)
        {
            GameObject bar = new GameObject("Top Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(parent, false);
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 45f);
            rect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return bar;
        }

        private static GameObject BuildColumn(Transform parent, string name, float width, float flexibleWidth)
        {
            GameObject column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            column.transform.SetParent(parent, false);
            LayoutElement le = column.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexibleWidth;
            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return column;
        }

        private static Text BuildColumnText(Transform parent, string name, Font font, int size, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private static Text BuildFlexibleText(Transform parent, string name, string value, Font font, int size, FontStyle style)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Text text = obj.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A button with a centred label, sized by its parent's layout group.</summary>
        private static Button BuildRowButton(Transform parent, string name, string label, Font font, int fontSize, float width, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            if (width > 0f)
                obj.GetComponent<LayoutElement>().preferredWidth = width;
            obj.GetComponent<Image>().color = color;
            Button button = obj.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(obj.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            Text text = textObj.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return button;
        }

        /// <summary>A hidden row with one left-aligned label, cloned by a panel for each item.</summary>
        private static GameObject BuildLabelRowTemplate(RectTransform content, string name, Font font, float height, int fontSize)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            LayoutElement le = row.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            row.GetComponent<Button>().transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(row.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            Stretch(textRect);
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            Text text = textObj.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            row.SetActive(false);
            return row;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static GameObject BuildScrollList(Transform parent, string name, Font font, out RectTransform viewport)
        {
            GameObject listRoot = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            listRoot.transform.SetParent(parent, false);
            RectTransform listRect = listRoot.GetComponent<RectTransform>();
            Stretch(listRect);

            // Right-side scrollbar
            GameObject barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(listRoot.transform, false);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(30f, 0f);

            Image barImage = barObject.GetComponent<Image>();
            barImage.color = new Color(0.15f, 0.16f, 0.18f, 0.9f);
            barImage.raycastTarget = true;

            GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(barObject.transform, false);
            RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(4f, 4f);
            slidingRect.offsetMax = new Vector2(-4f, -4f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(slidingArea.transform, false);
            Stretch(handle.GetComponent<RectTransform>());
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.4f, 0.42f, 0.48f, 1f);
            handleImage.raycastTarget = true;

            Scrollbar bar = barObject.GetComponent<Scrollbar>();
            bar.targetGraphic = handleImage;
            bar.handleRect = handle.GetComponent<RectTransform>();
            bar.direction = Scrollbar.Direction.BottomToTop;

            // Viewport with content rows
            // RectMask2D clips rows past the viewport, so a long list scrolls instead of spilling out of the panel.
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(listRoot.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.offsetMin = new Vector2(0f, 0f);
            viewportRect.offsetMax = new Vector2(-30f, 0f);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0.08f, 0.09f, 0.11f, 0.8f);
            viewportImage.raycastTarget = true;

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = listRoot.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            scroll.verticalScrollbar = bar;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20f;

            viewport = contentRect;
            return listRoot;
        }

        private static GameObject BuildTakeRowTemplate(RectTransform content, Font font)
        {
            GameObject row = new GameObject("Take Row Template", typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(content, false);
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;

            Image background = row.GetComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            background.raycastTarget = true;

            Button button = row.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(row.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            Text label = textObj.GetComponent<Text>();
            label.text = "Take 1";
            label.font = font;
            label.fontSize = 26;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            row.SetActive(false); // template rows stay hidden until the panel clones them
            return row;
        }

        private static GameObject BuildActRowTemplate(RectTransform content, Font font)
        {
            // A HorizontalLayoutGroup lays the row out as Label (flexible) | Avatar | Delete, so none
            // of the three can ever overlap another.
            GameObject row = new GameObject("Act Row Template", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            LayoutElement layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 64f;
            layout.preferredHeight = 64f;

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.padding = new RectOffset(16, 8, 0, 0);
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            Image background = row.GetComponent<Image>();
            background.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            background.raycastTarget = true;

            Button button = row.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObj.transform.SetParent(row.transform, false);
            textObj.GetComponent<LayoutElement>().flexibleWidth = 1f;
            Text label = textObj.GetComponent<Text>();
            label.text = "Act 1";
            label.font = font;
            label.fontSize = 26;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            // Opens the avatar picker for this act; its label shows the act's avatar.
            BuildRowButton(row.transform, "Avatar", "Spheres", font, 18, 160f, new Color(0.28f, 0.32f, 0.42f, 0.95f));

            // Per-row delete button, fixed width at the end of the row.
            GameObject deleteObj = new GameObject("Delete", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            deleteObj.transform.SetParent(row.transform, false);
            deleteObj.GetComponent<LayoutElement>().preferredWidth = 64f;

            Image deleteImage = deleteObj.GetComponent<Image>();
            deleteImage.color = new Color(0.5f, 0.15f, 0.15f, 0.9f);
            deleteImage.raycastTarget = true;

            Button deleteButton = deleteObj.GetComponent<Button>();
            deleteButton.transition = Selectable.Transition.None;

            GameObject deleteText = new GameObject("Label", typeof(RectTransform), typeof(Text));
            deleteText.transform.SetParent(deleteObj.transform, false);
            Stretch(deleteText.GetComponent<RectTransform>());
            Text deleteLabel = deleteText.GetComponent<Text>();
            deleteLabel.text = "X";
            deleteLabel.font = font;
            deleteLabel.fontSize = 28;
            deleteLabel.color = Color.white;
            deleteLabel.alignment = TextAnchor.MiddleCenter;
            deleteLabel.raycastTarget = false;

            row.SetActive(false);
            return row;
        }

        private static GameObject BuildAddButton(Transform parent, Font font, out Image image)
        {
            GameObject button = new GameObject("Add Button", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 10f);
            rect.sizeDelta = new Vector2(160f, 60f);

            image = button.GetComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.25f, 0.95f);
            image.raycastTarget = true;

            Button add = button.GetComponent<Button>();
            add.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(button.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            Text label = textObj.GetComponent<Text>();
            label.text = "+ Add";
            label.font = font;
            label.fontSize = 26;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Button BuildPlainButton(Transform parent, string label, Font font,
            Vector2 anchorMin, Vector2 size, Vector2 pivot, Vector2 anchoredPosition)
        {
            Button button = BuildPlainButton(parent, label, font);

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMin;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            return button;
        }

        /// <summary>Builds a plain button without positioning it; use this for a button that will
        /// live inside a layout group, which sizes and positions its children itself.</summary>
        private static Button BuildPlainButton(Transform parent, string label, Font font)
        {
            GameObject buttonObj = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            Image image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.22f, 0.95f);
            image.raycastTarget = true;

            Button button = buttonObj.GetComponent<Button>();
            button.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject(label + " Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            Stretch(textObj.GetComponent<RectTransform>());
            Text text = textObj.GetComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return button;
        }

        /// <summary>Gives a layout-managed child a fixed width via LayoutElement instead of anchors.</summary>
        private static void SetFixedWidth(GameObject go, float width)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null)
                le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        public static void SetupSceneInstance(GameObject prefab)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
                return;

            // Remove any VRDashboard script from XR Origin if present
            GameObject xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                VRDashboard oldOnXrOrigin = xrOrigin.GetComponent<VRDashboard>();
                if (oldOnXrOrigin != null)
                {
                    Object.DestroyImmediate(oldOnXrOrigin);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            // Find or instantiate VRDashboard prefab instance in scene
            VRDashboard sceneDash = Object.FindFirstObjectByType<VRDashboard>();
            if (sceneDash == null && prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "VRDashboard";
                sceneDash = instance.GetComponent<VRDashboard>();
                EditorSceneManager.MarkSceneDirty(scene);
            }

            if (sceneDash != null)
            {
                // Wire inspector references
                InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
                VRLocomotion loco = Object.FindFirstObjectByType<VRLocomotion>();
                Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                GameObject leftHand = GameObject.Find("Left Hand");
                GameObject rightHand = GameObject.Find("Right Hand");

                sceneDash.InputActions = actions;
                sceneDash.Locomotion = loco;
                sceneDash.Hmd = cam;
                sceneDash.LeftHand = leftHand != null ? leftHand.transform : null;
                sceneDash.RightHand = rightHand != null ? rightHand.transform : null;
                EnsurePropGripInput(actions);

                EditorUtility.SetDirty(sceneDash);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[VRDashboardPrefabBuilder] Configured VRDashboard prefab instance in scene.");
            }
        }

        private static void EnsurePropGripInput(InputActionAsset actions)
        {
            FilmDirector director = Object.FindFirstObjectByType<FilmDirector>();
            if (director == null)
                return;

            PropGripInput grip = director.GetComponent<PropGripInput>();
            if (grip == null)
                grip = director.gameObject.AddComponent<PropGripInput>();
            grip.Director = director;
            grip.InputActions = actions;
            EditorUtility.SetDirty(grip);
        }
    }
}
