using ChimeralTheater.Films;
using UnityEngine;
using UnityEngine.UI;

namespace ChimeralTheater.UI
{
    /// <summary>
    /// The floating panel shown while an act is being recorded: a big countdown number during the
    /// three second countdown, then a running REC timer. The panel floats in front of the head
    /// through the shared FloatingUIFollower and shows itself only in the countdown and
    /// recording states.
    /// </summary>
    public class RecordingHUD : MonoBehaviour
    {
        [Header("Prefab UI Elements")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text displayText;
        [SerializeField] private Image backgroundImage;

        [Header("Player References")]
        [SerializeField] private Camera hmd;

        [Header("Head-following Settings")]
        [SerializeField] private FloatingUIFollower follower;

        [Header("Colours")]
        [SerializeField] private Color countdownColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color recordingColor = new Color(1f, 0.25f, 0.25f, 1f);

        public GameObject Panel { get => panel; set => panel = value; }
        public Text DisplayText { get => displayText; set => displayText = value; }
        public Image BackgroundImage { get => backgroundImage; set => backgroundImage = value; }
        public Camera Hmd { get => hmd; set => hmd = value; }
        public FloatingUIFollower Follower { get => follower; set => follower = value; }

        FilmDirector director;
        DirectorState lastSeenState = DirectorState.Idle;

        /// <summary>Binds the HUD to the director it displays. Call this once both exist.</summary>
        public void Bind(FilmDirector filmDirector)
        {
            director = filmDirector;
            RefreshForState(director != null ? director.State : DirectorState.Idle);
        }

        private void Awake()
        {
            ResolveReferences();
            SetVisible(false);
        }

        private void ResolveReferences()
        {
            if (hmd == null)
                hmd = Camera.main;

            if (follower == null && panel != null)
                follower = panel.GetComponent<FloatingUIFollower>();

            if (follower != null)
                follower.Viewer = hmd != null ? hmd.transform : null;
        }

        private void Start()
        {
            // The HUD lives next to the director in the scene; find it so the prefab instance
            // needs no manual wiring.
            if (director == null)
                director = FindFirstObjectByType<FilmDirector>();

            if (director != null)
                director.StateChanged += OnStateChanged;

            RefreshForState(director != null ? director.State : DirectorState.Idle);
        }

        private void OnDestroy()
        {
            if (director != null)
                director.StateChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (follower == null || hmd == null)
                return;

            // Follow the head: the HUD rides the player's view wherever they look.
            follower.Follow(hmd.transform.position, Time.deltaTime);

            if (director != null)
            {
                // The state event fires once per transition; the timer text refreshes every frame.
                if (director.State != lastSeenState)
                    RefreshForState(director.State);

                if (director.State == DirectorState.Countdown)
                    displayText.text = FormatCountdown(director.CountdownRemaining);
                else if (director.State == DirectorState.Recording)
                    displayText.text = FormatTimer(director.SessionTime);
            }
        }

        void OnStateChanged(DirectorState state)
        {
            RefreshForState(state);
        }

        void RefreshForState(DirectorState state)
        {
            lastSeenState = state;
            SetVisible(state == DirectorState.Countdown || state == DirectorState.Recording);

            if (backgroundImage != null)
                backgroundImage.color = state == DirectorState.Recording ? recordingColor : countdownColor;
        }

        void SetVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        static string FormatCountdown(float remaining)
        {
            // Round up so the panel counts 3, 2, 1 rather than showing zero at the end.
            int seconds = Mathf.Max(1, Mathf.CeilToInt(remaining));
            return seconds.ToString();
        }

        static string FormatTimer(float seconds)
        {
            int totalMilliseconds = Mathf.Max(0, Mathf.RoundToInt(seconds * 1000f));
            int minutes = totalMilliseconds / 60000;
            int wholeSeconds = (totalMilliseconds % 60000) / 1000;
            int milliseconds = totalMilliseconds % 1000;
            return $"REC {minutes:00}:{wholeSeconds:00}.{milliseconds:000}";
        }
    }
}
