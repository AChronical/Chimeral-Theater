using System;
using System.Collections.Generic;
using ChimeralTheater.Films.Avatars;
using UnityEngine;

namespace ChimeralTheater.Films
{
    public enum DirectorState
    {
        Idle,
        Countdown,
        Recording,
        Playing,
    }

    /// <summary>
    /// Owns the films tab's session: the list of takes, which take and act are selected, and the
    /// record/play state machine. The whole thing advances through <see cref="Tick"/>, which the
    /// scene drives from LateUpdate and tests drive directly, so recording, countdown and
    /// playback all share one clock and stay in sync.
    /// </summary>
    public class FilmDirector : MonoBehaviour
    {
        public const float CountdownDuration = 3f;

        /// <summary>Raised whenever the state machine moves between idle, countdown, recording and playing.</summary>
        public event Action<DirectorState> StateChanged;

        /// <summary>Raised whenever the take list, act list or selection changes.</summary>
        public event Action Changed;

        public DirectorState State { get; private set; } = DirectorState.Idle;

        /// <summary>Seconds left on the record countdown while in the countdown state.</summary>
        public float CountdownRemaining { get; private set; }

        /// <summary>Seconds since the recording started, shared with the other acts' playback.</summary>
        public float SessionTime { get; private set; }

        /// <summary>The take whose acts are currently shown. Null in the takes list view.</summary>
        public Take OpenTake { get; private set; }

        /// <summary>The act the record button would record into. Null when nothing is selected.</summary>
        public Act SelectedAct { get; private set; }

        public IReadOnlyList<Take> Takes => takes;

        /// <summary>Number of stand-ins currently on stage (other acts during a recording, all acts during playback).</summary>
        public int StandInCount => playbacks.Count;

        readonly List<Take> takes = new List<Take>();
        readonly List<ActPlayback> playbacks = new List<ActPlayback>();
        readonly ActRecorder recorder = new ActRecorder();
        int nextTakeNumber = 1;

        ChaviAvatar selfAvatar;
        string selfAvatarPath = "";

        /// <summary>The performer's own avatar while one is worn or the selected act has one; null otherwise.</summary>
        public ChaviAvatar SelfAvatar => selfAvatar;

        Transform body;
        Transform head;
        Transform leftHand;
        Transform rightHand;

        void Awake()
        {
            ActStore.ClearAll();
            ResolveRig();
        }

        void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            DisposeSelfAvatar();
        }

        /// <summary>Points the recorder at the performer's transforms. Tests call this directly since Awake does not run in edit mode.</summary>
        public void Configure(Transform playerBody, Transform hmd, Transform left, Transform right)
        {
            body = playerBody;
            head = hmd;
            leftHand = left;
            rightHand = right;
        }

        void ResolveRig()
        {
            var playerBody = GameObject.Find("Player Body");
            var hmd = GameObject.Find("HMD");
            if (hmd == null && Camera.main != null)
                hmd = Camera.main.gameObject;
            var left = GameObject.Find("Left Hand");
            var right = GameObject.Find("Right Hand");

            Configure(
                playerBody ? playerBody.transform : null,
                hmd ? hmd.transform : null,
                left ? left.transform : null,
                right ? right.transform : null);
        }

        // --- Take and act management, allowed only while idle ---

        public bool CanEdit => State == DirectorState.Idle;
        public bool CanRecord => State == DirectorState.Idle && SelectedAct != null;
        public bool CanPlay => State == DirectorState.Idle && OpenTake != null && OpenTake.HasRecordedActs;

        void RequireIdle(string action)
        {
            if (State != DirectorState.Idle)
                throw new InvalidOperationException($"Cannot {action} while the director is {State.ToString().ToLowerInvariant()}.");
        }

        public Take AddTake()
        {
            RequireIdle("add a take");
            var take = new Take { Number = nextTakeNumber++ };
            take.ResourceName = $"Take {take.Number}";
            takes.Add(take);
            Changed?.Invoke();
            return take;
        }

        public void RemoveTake(Take take)
        {
            RequireIdle("remove a take");
            if (take == null)
                throw new ArgumentNullException(nameof(take));

            if (OpenTake == take)
            {
                OpenTake = null;
                SelectedAct = null;
            }

            ActStore.DeleteTake(take);
            takes.Remove(take);
            Changed?.Invoke();
        }

        public void OpenTakeView(Take take)
        {
            RequireIdle("open a take");
            if (take == null)
                throw new ArgumentNullException(nameof(take));

            OpenTake = take;
            SelectedAct = null;
            Changed?.Invoke();
        }

        public void CloseTakeView()
        {
            RequireIdle("close a take");
            OpenTake = null;
            SelectedAct = null;
            Changed?.Invoke();
        }

        public Act AddAct()
        {
            RequireIdle("add an act");
            if (OpenTake == null)
                throw new InvalidOperationException("No take is open.");

            Act act = OpenTake.AddAct();
            SelectedAct = act;
            Changed?.Invoke();
            return act;
        }

        public void RemoveAct(Act act)
        {
            RequireIdle("remove an act");
            if (act == null)
                throw new ArgumentNullException(nameof(act));
            if (OpenTake == null || !OpenTake.Acts.Contains(act))
                throw new ArgumentException("The act does not belong to the open take.");

            ActStore.Delete(act);
            OpenTake.Acts.Remove(act);
            if (SelectedAct == act)
                SelectedAct = null;
            Changed?.Invoke();
        }

        public void SelectAct(Act act)
        {
            RequireIdle("select an act");
            if (act != null && (OpenTake == null || !OpenTake.Acts.Contains(act)))
                throw new ArgumentException("The act does not belong to the open take.");

            SelectedAct = act;
            Changed?.Invoke();
        }

        // --- Recording and playback ---

        /// <summary>Begins a recording of the selected act after a three second countdown. Every other
        /// recorded act in the take is put on stage as a stand-in so the performer can react to it.</summary>
        public void StartRecording()
        {
            if (State != DirectorState.Idle)
                throw new InvalidOperationException($"Cannot start recording while the director is {State.ToString().ToLowerInvariant()}.");
            if (SelectedAct == null)
                throw new InvalidOperationException("Select an act before recording.");

            recorder.Reset();
            SessionTime = 0f;

            StageOtherActs(SelectedAct);

            CountdownRemaining = CountdownDuration;
            SetState(DirectorState.Countdown);
        }

        /// <summary>Stops the current recording. Cancelling during the countdown saves nothing; stopping
        /// during the recording writes the act's frames to its file. Either way the stand-ins leave.</summary>
        public void StopRecording()
        {
            switch (State)
            {
                case DirectorState.Countdown:
                    recorder.Reset();
                    ClearStage();
                    SetState(DirectorState.Idle);
                    break;

                case DirectorState.Recording:
                    recorder.WriteTo(SelectedAct);
                    ActStore.Save(OpenTake, SelectedAct);
                    ClearStage();
                    SetState(DirectorState.Idle);
                    Changed?.Invoke();
                    break;

                default:
                    throw new InvalidOperationException("Nothing is being recorded.");
            }
        }

        /// <summary>Plays the open take back: every recorded act goes on stage at once and the session
        /// runs until the longest act has finished.</summary>
        public void PlayTake()
        {
            if (State != DirectorState.Idle)
                throw new InvalidOperationException($"Cannot play while the director is {State.ToString().ToLowerInvariant()}.");
            if (OpenTake == null)
                throw new InvalidOperationException("No take is open.");
            if (!OpenTake.HasRecordedActs)
                throw new InvalidOperationException("The take has no recorded acts.");

            SessionTime = 0f;
            foreach (Act act in OpenTake.Acts)
            {
                if (act.IsEmpty)
                    continue;

                Act loaded = ActStore.LoadFrames(act);
                if (loaded != null)
                    playbacks.Add(new ActPlayback(loaded));
            }

            SetState(DirectorState.Playing);
        }

        /// <summary>Advances the whole session by <paramref name="dt"/> seconds: the countdown, the
        /// recorder and every stand-in, all sharing <see cref="SessionTime"/>.</summary>
        public void Tick(float dt)
        {
            switch (State)
            {
                case DirectorState.Countdown:
                    CountdownRemaining -= dt;
                    if (CountdownRemaining <= 0f)
                    {
                        // The recording clock starts at zero the moment the countdown ends.
                        SessionTime = 0f;
                        SetState(DirectorState.Recording);
                    }
                    else
                    {
                        // Stand-ins wait at their opening pose so there is something to react to.
                        EvaluateStage(0f);
                    }

                    break;

                case DirectorState.Recording:
                    SessionTime += dt;
                    recorder.Tick(dt, body, head, leftHand, rightHand);
                    EvaluateStage(SessionTime);
                    break;

                case DirectorState.Playing:
                    SessionTime += dt;
                    EvaluateStage(SessionTime);

                    // The take ends when its longest act has run out.
                    float longest = 0f;
                    foreach (ActPlayback playback in playbacks)
                        longest = Mathf.Max(longest, playback.Act.Duration);

                    if (SessionTime > longest)
                    {
                        ClearStage();
                        SetState(DirectorState.Idle);
                        Changed?.Invoke();
                    }

                    break;
            }

            UpdateSelfAvatar();
        }

        /// <summary>The avatar the performer should see themselves as right now: the selected act's avatar
        /// from the countdown through the recording, otherwise whatever they are wearing.</summary>
        string DesiredSelfAvatarPath()
        {
            bool recording = State == DirectorState.Countdown || State == DirectorState.Recording;
            if (recording && SelectedAct != null && !string.IsNullOrEmpty(SelectedAct.AvatarAssetPath))
                return SelectedAct.AvatarAssetPath;

            return AvatarWearState.Worn;
        }

        void UpdateSelfAvatar()
        {
            string desired = DesiredSelfAvatarPath();
            if (head == null)
                desired = "";

            if (desired != selfAvatarPath || (selfAvatar != null && !selfAvatar.IsAlive))
            {
                DisposeSelfAvatar();
                selfAvatarPath = desired;
                if (!string.IsNullOrEmpty(desired))
                {
                    ChaviResource chavi = AvatarConversionPipeline.LoadAvatar(desired);
                    if (chavi != null)
                        selfAvatar = new ChaviAvatar(chavi, 0, selfMode: true);
                    else
                        Debug.LogWarning($"[FilmDirector] Could not load avatar '{desired}' for the performer.");
                }
            }

            if (selfAvatar != null)
                selfAvatar.Apply(ActFrame.Capture(body, head, leftHand, rightHand));
        }

        void DisposeSelfAvatar()
        {
            selfAvatar?.Destroy();
            selfAvatar = null;
            selfAvatarPath = "";
        }

        void StageOtherActs(Act except)
        {
            ClearStage();
            foreach (Act act in OpenTake.Acts)
            {
                if (act.IsEmpty || act == except)
                    continue;

                Act loaded = ActStore.LoadFrames(act);
                if (loaded != null)
                    playbacks.Add(new ActPlayback(loaded));
            }
        }

        void EvaluateStage(float time)
        {
            foreach (ActPlayback playback in playbacks)
                playback.Evaluate(time);
        }

        void ClearStage()
        {
            foreach (ActPlayback playback in playbacks)
                playback.Dispose();

            playbacks.Clear();
        }

        void SetState(DirectorState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
