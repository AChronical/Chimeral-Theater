using System.Collections.Generic;
using System.IO;
using ChimeralTheater.Films;
using ChimeralTheater.Films.Avatars;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeralTheater.UI
{
    /// <summary>
    /// Drives the Films tab content: the takes list, the open take's acts, and the recording view
    /// with its giant stop button. All rows are cloned from a hidden template object in the
    /// dashboard prefab; the panel itself is stateless and rebuilds its rows whenever the film
    /// director reports a change.
    /// </summary>
    public class FilmsPanel : MonoBehaviour
    {
        [Header("Takes View")]
        [SerializeField] private GameObject takesView;
        [SerializeField] private RectTransform takesListContent;
        [SerializeField] private GameObject takeRowTemplate;
        [SerializeField] private Button addTakeButton;

        [Header("Take Detail View")]
        [SerializeField] private GameObject takeDetailView;
        [SerializeField] private Text takeTitleText;
        [SerializeField] private RectTransform actsListContent;
        [SerializeField] private GameObject actRowTemplate;
        [SerializeField] private Button addActButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button playButton;

        [Header("Avatar Picker View")]
        [SerializeField] private GameObject avatarPickerView;
        [SerializeField] private Text avatarPickerTitleText;
        [SerializeField] private RectTransform avatarListContent;
        [SerializeField] private GameObject avatarRowTemplate;
        [SerializeField] private Button avatarBackButton;

        [Header("Recording View")]
        [SerializeField] private GameObject recordingView;
        [SerializeField] private Button stopRecordingButton;

        [Header("Director")]
        [SerializeField] private FilmDirector director;

        static readonly Color SelectedRowColor = new Color(0.35f, 0.35f, 0.4f, 0.95f);
        static readonly Color NormalRowColor = new Color(0.2f, 0.2f, 0.22f, 0.95f);
        static readonly Color AddButtonColor = new Color(0.2f, 0.45f, 0.25f, 0.95f);
        static readonly Color DisabledColor = new Color(0.25f, 0.25f, 0.27f, 0.6f);
        static readonly Color EmptyActColor = new Color(1f, 0.85f, 0.2f, 1f);
        static readonly Color RecordedActColor = Color.white;

        readonly List<GameObject> takeRows = new List<GameObject>();
        readonly List<GameObject> actRows = new List<GameObject>();
        readonly List<GameObject> avatarRows = new List<GameObject>();

        private Act pickerTargetAct;

        public FilmDirector Director { get => director; set => director = value; }

        // Properties for editor setup
        public GameObject TakesView { get => takesView; set => takesView = value; }
        public RectTransform TakesListContent { get => takesListContent; set => takesListContent = value; }
        public GameObject TakeRowTemplate { get => takeRowTemplate; set => takeRowTemplate = value; }
        public Button AddTakeButton { get => addTakeButton; set => addTakeButton = value; }
        public GameObject TakeDetailView { get => takeDetailView; set => takeDetailView = value; }
        public Text TakeTitleText { get => takeTitleText; set => takeTitleText = value; }
        public RectTransform ActsListContent { get => actsListContent; set => actsListContent = value; }
        public GameObject ActRowTemplate { get => actRowTemplate; set => actRowTemplate = value; }
        public Button AddActButton { get => addActButton; set => addActButton = value; }
        public Button BackButton { get => backButton; set => backButton = value; }
        public Button RecordButton { get => recordButton; set => recordButton = value; }
        public Button PlayButton { get => playButton; set => playButton = value; }
        public GameObject AvatarPickerView { get => avatarPickerView; set => avatarPickerView = value; }
        public Text AvatarPickerTitleText { get => avatarPickerTitleText; set => avatarPickerTitleText = value; }
        public RectTransform AvatarListContent { get => avatarListContent; set => avatarListContent = value; }
        public GameObject AvatarRowTemplate { get => avatarRowTemplate; set => avatarRowTemplate = value; }
        public Button AvatarBackButton { get => avatarBackButton; set => avatarBackButton = value; }
        public GameObject RecordingView { get => recordingView; set => recordingView = value; }
        public Button StopRecordingButton { get => stopRecordingButton; set => stopRecordingButton = value; }

        void Start()
        {
            if (director == null)
                director = FindFirstObjectByType<FilmDirector>();

            if (director != null)
            {
                director.Changed += Rebuild;
                director.StateChanged += OnStateChanged;
            }

            addTakeButton?.onClick.AddListener(() => director?.AddTake());
            addActButton?.onClick.AddListener(() => director?.AddAct());
            backButton?.onClick.AddListener(() => director?.CloseTakeView());
            recordButton?.onClick.AddListener(OnRecordClicked);
            playButton?.onClick.AddListener(() => director?.PlayTake());
            stopRecordingButton?.onClick.AddListener(OnStopRecordingClicked);
            avatarBackButton?.onClick.AddListener(CloseAvatarPicker);

            Rebuild();
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.Changed -= Rebuild;
                director.StateChanged -= OnStateChanged;
            }
        }

        void OnStateChanged(DirectorState state)
        {
            Rebuild();
        }

        void OnRecordClicked()
        {
            if (director == null || !director.CanRecord)
                return;

            director.StartRecording();

            // The performer needs their hands; the dashboard gets out of the way.
            VRDashboard dashboard = FindFirstObjectByType<VRDashboard>();
            dashboard?.Close();
        }

        void OnStopRecordingClicked()
        {
            if (director == null)
                return;

            // Stopping during the countdown cancels the recording; during it, saves the act.
            director.StopRecording();
        }

        void Rebuild()
        {
            if (director == null)
                return;

            ClearRows(takeRows);
            ClearRows(actRows);

            switch (director.State)
            {
                case DirectorState.Countdown:
                case DirectorState.Recording:
                    ShowRecordingView();
                    break;

                default:
                    if (director.OpenTake == null)
                        ShowTakesView();
                    else
                        ShowTakeDetailView();
                    break;
            }
        }

        // --- Views ---

        void ShowTakesView()
        {
            takesView.SetActive(true);
            takeDetailView.SetActive(false);
            avatarPickerView.SetActive(false);
            recordingView.SetActive(false);

            foreach (Take take in director.Takes)
            {
                GameObject row = InstantiateRow(takeRowTemplate, takesListContent, takeRows);
                SetRowText(row, $"Take {take.Number}");
                BindRowButton(row, () => director.OpenTakeView(take));
            }
        }

        void ShowTakeDetailView()
        {
            takesView.SetActive(false);
            takeDetailView.SetActive(true);
            avatarPickerView.SetActive(false);
            recordingView.SetActive(false);

            // Called both from Rebuild() (which already clears rows) and directly after picking
            // an avatar, which doesn't; clearing here too keeps either path from piling up
            // duplicate act rows on top of the ones from the previous call.
            ClearRows(actRows);

            Take take = director.OpenTake;
            if (takeTitleText != null)
                takeTitleText.text = $"Take {take.Number}";

            for (int i = 0; i < take.Acts.Count; i++)
            {
                Act act = take.Acts[i];
                GameObject row = InstantiateRow(actRowTemplate, actsListContent, actRows);

                string avLabel = string.IsNullOrEmpty(act.AvatarAssetPath)
                    ? "Spheres"
                    : Path.GetFileNameWithoutExtension(act.AvatarAssetPath);

                // An act with no recording shows in yellow until something is recorded into it.
                SetRowText(row, $"Act {act.Number} [{avLabel}]", act.IsEmpty ? EmptyActColor : RecordedActColor);

                Act captured = act;
                int index = i;
                BindRowButton(row, () =>
                {
                    if (director.CanEdit)
                        director.SelectAct(captured);
                });

                // The act row template is Label | Avatar | Delete.
                GameObject deleteTarget = row.transform.Find("Delete").gameObject;
                GameObject avatarTarget = row.transform.Find("Avatar").gameObject;
                avatarTarget.GetComponentInChildren<Text>().text = avLabel;
                BindRowButton(avatarTarget, () =>
                {
                    if (director.CanEdit)
                        OpenAvatarPicker(captured);
                });

                BindDeleteButton(deleteTarget, () =>
                {
                    if (director.CanEdit)
                        director.RemoveAct(captured);
                });

                Image background = row.GetComponent<Image>();
                if (background != null)
                    background.color = director.SelectedAct == act ? SelectedRowColor : NormalRowColor;
            }

            RefreshActionButtons();
        }

        void OpenAvatarPicker(Act act)
        {
            pickerTargetAct = act;
            takesView.SetActive(false);
            takeDetailView.SetActive(false);
            recordingView.SetActive(false);

            avatarPickerView.SetActive(true);

            if (avatarPickerTitleText != null)
                avatarPickerTitleText.text = $"Select Avatar for Act {act.Number}";

            ClearRows(avatarRows);

            // Default spheres option
            GameObject defaultRow = InstantiateRow(avatarRowTemplate, avatarListContent, avatarRows);
            SetRowText(defaultRow, "Default Stand-in (Spheres)");
            BindRowButton(defaultRow, () => SelectAvatarForAct(""));

            // Converted .chavi avatars from Asset Inventory
            string[] avatars = AssetStorePaths.GetInventoryAvatars();
            foreach (string avPath in avatars)
            {
                string name = Path.GetFileNameWithoutExtension(avPath);
                GameObject avRow = InstantiateRow(avatarRowTemplate, avatarListContent, avatarRows);
                SetRowText(avRow, $"👤 {name}");
                string capturedPath = avPath;
                BindRowButton(avRow, () => SelectAvatarForAct(capturedPath));
            }
        }

        void SelectAvatarForAct(string avatarPath)
        {
            if (pickerTargetAct != null)
            {
                if (director != null && director.OpenTake != null)
                    ActStore.SetAvatar(director.OpenTake, pickerTargetAct, avatarPath);
                else
                    pickerTargetAct.AvatarAssetPath = avatarPath ?? "";
            }

            CloseAvatarPicker();
        }

        void CloseAvatarPicker()
        {
            pickerTargetAct = null;
            ClearRows(avatarRows);
            ShowTakeDetailView();
        }

        void ShowRecordingView()
        {
            takesView.SetActive(false);
            takeDetailView.SetActive(false);
            avatarPickerView.SetActive(false);
            recordingView.SetActive(true);
        }

        void RefreshActionButtons()
        {
            SetButtonEnabled(addTakeButton, director.CanEdit, AddButtonColor);
            SetButtonEnabled(addActButton, director.CanEdit, AddButtonColor);
            SetButtonEnabled(backButton, director.CanEdit, NormalRowColor);
            SetButtonEnabled(recordButton, director.CanRecord, NormalRowColor);
            SetButtonEnabled(playButton, director.CanPlay, NormalRowColor);
        }

        // --- Row helpers ---

        static GameObject InstantiateRow(GameObject template, RectTransform parent, List<GameObject> rows)
        {
            GameObject row = Instantiate(template, parent);
            row.name = template.name.Replace("Template", "");
            row.SetActive(true);
            rows.Add(row);
            return row;
        }

        static void ClearRows(List<GameObject> rows)
        {
            foreach (GameObject row in rows)
                Destroy(row);

            rows.Clear();
        }

        static void SetRowText(GameObject row, string text, Color? color = null)
        {
            Text label = row.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
                if (color.HasValue)
                    label.color = color.Value;
            }
        }

        static void BindRowButton(GameObject row, UnityEngine.Events.UnityAction onClick)
        {
            Button button = row.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(onClick);
        }

        static void BindDeleteButton(GameObject deleteObject, UnityEngine.Events.UnityAction onClick)
        {
            if (deleteObject == null)
                return;

            Button button = deleteObject.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(onClick);
        }

        static void SetButtonEnabled(Button button, bool enabled, Color enabledColor)
        {
            if (button == null)
                return;

            button.interactable = enabled;

            // Color transitions never fire without an event system, so tint disabled buttons by hand.
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = enabled ? enabledColor : DisabledColor;
        }
    }
}
