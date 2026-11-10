using System;
using System.Collections.Generic;
using UniRes;

namespace ChimeralTheater.Films
{
    /// <summary>A grip button press or release recorded in an act's action layer.</summary>
    public enum ActAction
    {
        LeftGrip = 0,
        LeftRelease = 1,
        RightGrip = 2,
        RightRelease = 3,
    }

    /// <summary>
    /// One recorded performance: timestamped frames of the performer's body, head and hands.
    /// Frames are stored as two parallel flat arrays so the file stays compact:
    /// <see cref="Times"/> holds one timestamp per frame and <see cref="Poses"/> holds
    /// <see cref="FloatsPerFrame"/> floats per frame. On top of the poses sits an action layer:
    /// the grip presses and releases, as parallel <see cref="ActionTimes"/> and <see cref="ActionCodes"/>
    /// arrays. Actions are re-simulated on playback, so a replayed grip grabs whatever prop the
    /// replayed hand is touching at that moment.
    /// </summary>
    public class Act : Resource
    {
        public const int PoseCount = 4;
        public const int FloatsPerPose = 7;
        public const int FloatsPerFrame = PoseCount * FloatsPerPose;

        /// <summary>Position in its take's numbering, used for the display name.</summary>
        [Export] public int Number;

        /// <summary>Optional path to an avatar (.chavi) in the Asset Inventory assigned to this act.</summary>
        [Export] public string AvatarAssetPath = "";

        /// <summary>Number of recorded frames. Stays correct while <see cref="Times"/> and <see cref="Poses"/> are released.</summary>
        [Export] public int FrameCount;

        /// <summary>Seconds from the start of the recording to its last frame.</summary>
        [Export] public float Duration;

        /// <summary>Seconds since the recording started, one per frame, ascending.</summary>
        [Export] public float[] Times = Array.Empty<float>();

        /// <summary>Body, head, left hand and right hand poses, <see cref="FloatsPerFrame"/> floats per frame.</summary>
        [Export] public float[] Poses = Array.Empty<float>();

        /// <summary>Seconds since the recording started, one per action, ascending.</summary>
        [Export] public float[] ActionTimes = Array.Empty<float>();

        /// <summary>One <see cref="ActAction"/> per entry in <see cref="ActionTimes"/>.</summary>
        [Export] public int[] ActionCodes = Array.Empty<int>();

        public int ActionCount => ActionTimes?.Length ?? 0;

        /// <summary>True when nothing has been recorded into this act yet.</summary>
        public bool IsEmpty => FrameCount == 0;

        /// <summary>True when the frame data is in memory, not only in the act's file.</summary>
        public bool HasFramesLoaded => Times != null && Times.Length > 0;

        /// <summary>Replaces the recording. Throws if the pose data does not match the timestamps.</summary>
        public void SetFrames(List<float> times, List<float> poses)
        {
            if (times == null)
                throw new ArgumentNullException(nameof(times));
            if (poses == null)
                throw new ArgumentNullException(nameof(poses));
            if (poses.Count != times.Count * FloatsPerFrame)
                throw new ArgumentException($"Expected {times.Count * FloatsPerFrame} pose floats for {times.Count} frames but got {poses.Count}.");

            Times = times.ToArray();
            Poses = poses.ToArray();
            FrameCount = Times.Length;
            Duration = FrameCount == 0 ? 0f : Times[FrameCount - 1];
        }

        /// <summary>Replaces the action layer. Throws if the codes do not match the timestamps.</summary>
        public void SetActions(List<float> times, List<int> codes)
        {
            if (times == null)
                throw new ArgumentNullException(nameof(times));
            if (codes == null)
                throw new ArgumentNullException(nameof(codes));
            if (times.Count != codes.Count)
                throw new ArgumentException($"Expected {times.Count} action codes but got {codes.Count}.");

            ActionTimes = times.ToArray();
            ActionCodes = codes.ToArray();
        }

        /// <summary>Drops the frame data from memory. The count and duration are kept; reload from the act's file to play it.</summary>
        public void ReleaseFrames()
        {
            Times = Array.Empty<float>();
            Poses = Array.Empty<float>();
            ActionTimes = Array.Empty<float>();
            ActionCodes = Array.Empty<int>();
        }

        /// <summary>
        /// Reads the performer's pose at <paramref name="time"/>, interpolating between the two
        /// surrounding frames. Returns false when the act has no loaded frames or has ended.
        /// </summary>
        public bool TrySample(float time, out ActFrame frame)
        {
            if (!HasFramesLoaded || time > Duration)
            {
                frame = default;
                return false;
            }

            int last = Times.Length - 1;
            if (time <= Times[0])
            {
                frame = ActFrame.ReadFrom(Poses, 0);
                return true;
            }

            int index = Array.BinarySearch(Times, time);
            if (index < 0)
                index = ~index - 1;

            if (index >= last)
            {
                frame = ActFrame.ReadFrom(Poses, last);
                return true;
            }

            float span = Times[index + 1] - Times[index];
            float t = span > 0f ? (time - Times[index]) / span : 0f;
            frame = ActFrame.Lerp(ActFrame.ReadFrom(Poses, index), ActFrame.ReadFrom(Poses, index + 1), t);
            return true;
        }
    }
}
