using UnityEngine;
using UnityEngine.Animations;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Animation job that poses a humanoid from three tracked points with Unity's built-in humanoid IK:
    /// the stance (T) pose, then hand goals with elbow hints and a look-at that lets the chest and neck
    /// follow the head. Run through an <see cref="AnimationScriptPlayable"/>, so no Animator Controller
    /// asset is needed.
    /// </summary>
    public struct HumanoidIKJob : IAnimationJob
    {
        public bool HasLeftHand;
        public bool HasRightHand;
        public bool HasHead;
        public Vector3 LeftHandPosition;
        public Vector3 RightHandPosition;
        public Vector3 LeftElbowHint;
        public Vector3 RightElbowHint;
        public Vector3 LookAtPosition;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!stream.isHumanStream)
                return;

            AnimationHumanStream human = stream.AsHuman();
            human.ResetToStancePose();

            SetHand(human, AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, HasLeftHand, LeftHandPosition, LeftElbowHint);
            SetHand(human, AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, HasRightHand, RightHandPosition, RightElbowHint);

            // Hand and head rotations are written to the bones after IK, so only positions are solved here.
            human.SetLookAtPosition(LookAtPosition);
            human.SetLookAtClampWeight(0.5f);
            human.SetLookAtBodyWeight(HasHead ? 0.3f : 0f);
            human.SetLookAtHeadWeight(HasHead ? 0.5f : 0f);
            human.SetLookAtEyesWeight(0f);

            human.SolveIK();
        }

        static void SetHand(AnimationHumanStream human, AvatarIKGoal goal, AvatarIKHint hint, bool active, Vector3 position, Vector3 elbow)
        {
            human.SetGoalPosition(goal, position);
            human.SetGoalWeightPosition(goal, active ? 1f : 0f);
            human.SetGoalWeightRotation(goal, 0f);
            human.SetHintPosition(hint, elbow);
            human.SetHintWeightPosition(hint, active ? 1f : 0f);
        }
    }
}
