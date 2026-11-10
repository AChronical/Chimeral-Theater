using System.Collections.Generic;
using UnityEngine;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// Samples the performer's poses onto a fixed 120 ticks-per-second grid. Ticks are anchored
    /// to the moment recording starts, so the grid is stable even when render frames drop or
    /// run at other rates: each tick is written from the frame nearest to it, and any tick the
    /// clock skipped past is filled by repeating the last sampled pose. Grip presses and releases
    /// are recorded as actions at the moment the recorder sees them change.
    /// </summary>
    public class ActRecorder
    {
        public const float TicksPerSecond = 120f;
        public const float TickInterval = 1f / TicksPerSecond;

        readonly List<float> times = new List<float>();
        readonly List<float> poses = new List<float>();
        readonly List<float> actionTimes = new List<float>();
        readonly List<int> actionCodes = new List<int>();

        bool leftGripHeld;
        bool rightGripHeld;

        int nextTick;
        float elapsed;

        /// <summary>Half a tick. A frame covers a tick when the clock is within this of it.</summary>
        public const float HalfTick = TickInterval * 0.5f;

        public int FrameCount => times.Count;

        public int ActionCount => actionTimes.Count;

        /// <summary>Advances the recorder by <paramref name="delta"/> seconds, writing ticks for
        /// every grid point the recording clock has passed. Returns the number of ticks written.</summary>
        public int Tick(float delta, Transform body, Transform head, Transform leftHand, Transform rightHand,
            bool leftGrip = false, bool rightGrip = false)
        {
            elapsed += delta;

            if (leftGrip != leftGripHeld)
            {
                AddAction(leftGrip ? ActAction.LeftGrip : ActAction.LeftRelease);
                leftGripHeld = leftGrip;
            }

            if (rightGrip != rightGripHeld)
            {
                AddAction(rightGrip ? ActAction.RightGrip : ActAction.RightRelease);
                rightGripHeld = rightGrip;
            }

            int written = 0;
            while (nextTick * TickInterval <= elapsed + HalfTick)
            {
                times.Add(nextTick * TickInterval);
                ActFrame.Capture(body, head, leftHand, rightHand).AppendTo(poses);
                nextTick++;
                written++;
            }

            return written;
        }

        /// <summary>Copies the recorded frames and actions into the act.</summary>
        public void WriteTo(Act act)
        {
            act.SetFrames(times, poses);
            act.SetActions(actionTimes, actionCodes);
        }

        void AddAction(ActAction action)
        {
            actionTimes.Add(elapsed);
            actionCodes.Add((int)action);
        }

        public void Reset()
        {
            times.Clear();
            poses.Clear();
            actionTimes.Clear();
            actionCodes.Clear();
            leftGripHeld = false;
            rightGripHeld = false;
            nextTick = 0;
            elapsed = 0f;
        }
    }
}
