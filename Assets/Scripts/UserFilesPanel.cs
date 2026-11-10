using System;
using System.Collections.Generic;
using System.IO;
using ChimeralTheater.Films.Avatars;
using ChimeralTheater.Films.Props;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeralTheater.UI
{
    /// <summary>
    /// Drives the Files tab on the VR Dashboard: allows users to browse their UserFiles/ directory,
    /// navigate down and up subfolders, select .glb and .gltf models (Mesh2Motion exports), and convert them into .chavi avatars or .chprop props.
    /// </summary>
    public class UserFilesPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text currentPathText;
        [SerializeField] private Button upButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private RectTransform fileListContent;
        [SerializeField] private GameObject fileRowTemplate;

        [Header("Detail View")]
        [SerializeField] private GameObject detailView;
        [SerializeField] private Text selectedFileNameText;
        [SerializeField] private Text selectedFileInfoText;
        [SerializeField] private Text conversionStatusText;
        [SerializeField] private Button convertButton;
        [SerializeField] private Button convertPropButton;

        private string currentRelativePath = "";
        private string selectedFilePath = "";
        private readonly List<GameObject> activeRows = new List<GameObject>();

        private static readonly Color FolderRowColor = new Color(0.25f, 0.28f, 0.35f, 0.95f);
        private static readonly Color ModelRowColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        private static readonly Color SelectedRowColor = new Color(0.35f, 0.4f, 0.5f, 1f);

        public RectTransform FileListContent { get => fileListContent; set => fileListContent = value; }
        public GameObject FileRowTemplate { get => fileRowTemplate; set => fileRowTemplate = value; }
        public Text CurrentPathText { get => currentPathText; set => currentPathText = value; }
        public Button UpButton { get => upButton; set => upButton = value; }
        public Button RefreshButton { get => refreshButton; set => refreshButton = value; }
        public GameObject DetailView { get => detailView; set => detailView = value; }
        public Text SelectedFileNameText { get => selectedFileNameText; set => selectedFileNameText = value; }
        public Text SelectedFileInfoText { get => selectedFileInfoText; set => selectedFileInfoText = value; }
        public Text ConversionStatusText { get => conversionStatusText; set => conversionStatusText = value; }
        public Button ConvertButton { get => convertButton; set => convertButton = value; }
        public Button ConvertPropButton { get => convertPropButton; set => convertPropButton = value; }

        private void Start()
        {
            AssetStorePaths.EnsureDirectoriesExist();

            upButton?.onClick.RemoveAllListeners();
            upButton?.onClick.AddListener(NavigateUp);

            refreshButton?.onClick.RemoveAllListeners();
            refreshButton?.onClick.AddListener(Refresh);

            convertButton?.onClick.RemoveAllListeners();
            convertButton?.onClick.AddListener(OnConvertClicked);

            convertPropButton?.onClick.RemoveAllListeners();
            convertPropButton?.onClick.AddListener(OnConvertPropClicked);

            Refresh();
        }

        public void Refresh()
        {
            AssetStorePaths.EnsureDirectoriesExist();
            ClearRows();

            string fullPath = string.IsNullOrEmpty(currentRelativePath)
                ? AssetStorePaths.UserFilesRoot
                : Path.Combine(AssetStorePaths.UserFilesRoot, currentRelativePath);

            if (currentPathText != null)
            {
                currentPathText.text = string.IsNullOrEmpty(currentRelativePath)
                    ? "UserFiles/"
                    : $"UserFiles/{currentRelativePath}/";
            }

            if (upButton != null)
                upButton.interactable = !string.IsNullOrEmpty(currentRelativePath);

            if (!Directory.Exists(fullPath))
                return;

            // 1. List Subdirectories
            string[] subdirs = AssetStorePaths.GetDirectories(currentRelativePath);
            foreach (string subdir in subdirs)
            {
                string dirName = Path.GetFileName(subdir);
                GameObject row = CreateRow($"📁 {dirName}", FolderRowColor);
                string capturedDir = dirName;
                BindClick(row, () => NavigateDown(capturedDir));
            }

            // 2. List 3D Models
            string[] models = AssetStorePaths.GetModelFiles(currentRelativePath);
            foreach (string modelPath in models)
            {
                string fileName = Path.GetFileName(modelPath);
                string capturedPath = modelPath;
                bool isSelected = string.Equals(selectedFilePath, modelPath, StringComparison.OrdinalIgnoreCase);

                GameObject row = CreateRow($"📦 {fileName}", isSelected ? SelectedRowColor : ModelRowColor);
                BindClick(row, () => SelectFile(capturedPath));
            }

            UpdateDetailView();
        }

        public void NavigateDown(string subdirName)
        {
            currentRelativePath = string.IsNullOrEmpty(currentRelativePath)
                ? subdirName
                : Path.Combine(currentRelativePath, subdirName);
            selectedFilePath = "";
            Refresh();
        }

        public void NavigateUp()
        {
            if (string.IsNullOrEmpty(currentRelativePath))
                return;

            int lastSep = currentRelativePath.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSep < 0)
                lastSep = currentRelativePath.LastIndexOf(Path.AltDirectorySeparatorChar);

            currentRelativePath = lastSep > 0 ? currentRelativePath.Substring(0, lastSep) : "";
            selectedFilePath = "";
            Refresh();
        }

        public void SelectFile(string filePath)
        {
            selectedFilePath = filePath;
            Refresh();
        }

        private void UpdateDetailView()
        {
            if (detailView == null)
                return;

            if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
            {
                detailView.SetActive(false);
                return;
            }

            detailView.SetActive(true);
            var fi = new FileInfo(selectedFilePath);

            if (selectedFileNameText != null)
                selectedFileNameText.text = fi.Name;

            if (selectedFileInfoText != null)
            {
                string ext = fi.Extension.ToUpperInvariant();
                double kb = fi.Length / 1024.0;
                selectedFileInfoText.text = $"{ext} Model | {kb:0.0} KB";
            }

            if (conversionStatusText != null)
            {
                string baseName = Path.GetFileNameWithoutExtension(fi.Name);
                bool hasAvatar = File.Exists(AssetStorePaths.GetInventoryPathForAvatar(baseName));
                bool hasProp = File.Exists(AssetStorePaths.GetInventoryPathForProp(baseName));
                conversionStatusText.text = hasAvatar && hasProp ? "Status: Already converted to .chavi and .chprop"
                    : hasAvatar ? "Status: Already converted to .chavi"
                    : hasProp ? "Status: Already converted to .chprop"
                    : "Status: Ready to convert";
            }

            if (convertButton != null)
                convertButton.interactable = true;
            if (convertPropButton != null)
                convertPropButton.interactable = true;
        }

        private async void OnConvertPropClicked()
        {
            if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
                return;

            if (conversionStatusText != null)
                conversionStatusText.text = "Converting model to .chprop...";
            convertPropButton.interactable = false;

            var (prop, error) = await PropConversionPipeline.ConvertAsync(selectedFilePath);
            if (this == null)
                return;

            if (conversionStatusText != null)
                conversionStatusText.text = prop != null
                    ? $"Success! Converted '{prop.PropName}.chprop'"
                    : $"Error: {error}";

            convertPropButton.interactable = true;
        }

        private async void OnConvertClicked()
        {
            if (string.IsNullOrEmpty(selectedFilePath) || !File.Exists(selectedFilePath))
                return;

            if (conversionStatusText != null)
                conversionStatusText.text = "Converting model to .chavi...";

            if (convertButton != null)
                convertButton.interactable = false;

            var (chavi, error) = await AvatarConversionPipeline.ConvertAsync(selectedFilePath);
            if (this == null)
                return;

            if (chavi != null)
            {
                if (conversionStatusText != null)
                    conversionStatusText.text = $"Success! Converted '{chavi.AvatarName}.chavi'";
            }
            else
            {
                if (conversionStatusText != null)
                    conversionStatusText.text = $"Error: {error}";
            }

            if (convertButton != null)
                convertButton.interactable = true;
        }

        private GameObject CreateRow(string labelText, Color bgColor)
        {
            GameObject row = Instantiate(fileRowTemplate, fileListContent);
            row.name = "File Row";
            row.SetActive(true);

            var text = row.GetComponentInChildren<Text>();
            if (text != null)
                text.text = labelText;

            var img = row.GetComponent<Image>();
            if (img != null)
                img.color = bgColor;

            activeRows.Add(row);
            return row;
        }

        private static void BindClick(GameObject row, UnityEngine.Events.UnityAction onClick)
        {
            var btn = row.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(onClick);
        }

        private void ClearRows()
        {
            foreach (var row in activeRows)
            {
                if (row != null)
                    Destroy(row);
            }
            activeRows.Clear();
        }
    }
}
