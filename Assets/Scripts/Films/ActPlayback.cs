using ChimeralTheater.Films.Props;
using UnityEngine;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// Plays a single act back through its stand-in: holds a private copy of the act's frames,
    /// advances a shared session clock each tick and samples the act at that time, hiding the
    /// stand-in once the act has run past its end. The act's grip actions are re-simulated
    /// through two replayed hands, so props react in real time instead of following a recording.
    /// </summary>
    public class ActPlayback
    {
        public readonly Act Act;
        public readonly Avatars.IAvatarInstance Avatar;

        readonly PropGrabber leftHand;
        readonly PropGrabber rightHand;

        /// <summary>Index of the next action to fire; the session clock only moves forward.</summary>
        int nextAction;
        float lastEvaluatedTime;

        /// <summary>Time into the take's playback session, shared by every act.</summary>
        public float SessionTime { get; private set; }

        public bool HasEnded => SessionTime > Act.Duration;

        public ActPlayback(Act act)
        {
            Act = act;
            leftHand = new PropGrabber($"Act {act.Number} Left Hand");
            rightHand = new PropGrabber($"Act {act.Number} Right Hand");

            // Try to load the recorded avatar from the act; fall back to a simple sphere if unavailable.
            // This handles cases where the avatar asset has been moved, deleted, or is still loading.
            if (!string.IsNullOrEmpty(act.AvatarAssetPath))
            {
                var chavi = Avatars.AvatarConversionPipeline.LoadAvatar(act.AvatarAssetPath);
                if (chavi != null)
                {
                    Avatar = new Avatars.ChaviAvatar(chavi, act.Number);
                    return;
                }
            }

            Avatar = new SphereAvatar(act.Number);
        }

        /// <summary>Advances to the shared session time. The stand-in is hidden once the act has
        /// run past its end; a session still running with earlier acts keeps going.</summary>
        public void Evaluate(float sessionTime)
        {
            float deltaTime = Mathf.Max(0f, sessionTime - lastEvaluatedTime);
            lastEvaluatedTime = sessionTime;
            SessionTime = sessionTime;

            if (HasEnded)
            {
                leftHand.Release();
                rightHand.Release();
                Avatar.Hide();
                return;
            }

            // TrySample returns false while the frames are unloaded, which only happens
            // after the act's file is gone; treat that as ended rather than posing at zero.
            if (Act.TrySample(sessionTime, out ActFrame frame))
            {
                Avatar.Apply(frame);
                leftHand.MoveTo(frame.LeftHand, deltaTime);
                rightHand.MoveTo(frame.RightHand, deltaTime);
                RunActions(sessionTime, frame);
            }
            else
            {
                Avatar.Hide();
            }
        }

        /// <summary>Fires every recorded grip press or release between the last evaluation and now.</summary>
        void RunActions(float sessionTime, ActFrame frame)
        {
            while (nextAction < Act.ActionCount && Act.ActionTimes[nextAction] <= sessionTime)
            {
                switch ((ActAction)Act.ActionCodes[nextAction])
                {
                    case ActAction.LeftGrip:
                        leftHand.Grip(frame.LeftHand);
                        break;
                    case ActAction.LeftRelease:
                        leftHand.Release();
                        break;
                    case ActAction.RightGrip:
                        rightHand.Grip(frame.RightHand);
                        break;
                    case ActAction.RightRelease:
                        rightHand.Release();
                        break;
                }

                nextAction++;
            }
        }

        public void Dispose()
        {
            leftHand.Destroy();
            rightHand.Destroy();
            Avatar.Destroy();
        }
    }
}
