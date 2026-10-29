using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Runtime avatar spawned from a <see cref="ChaviResource"/>. The skeleton is turned into a Unity
    /// humanoid (<see cref="HumanoidRig"/>) and posed from a 3-point frame (head and both hands) by the
    /// Animator's humanoid IK, run through a manually evaluated <see cref="PlayableGraph"/>.
    ///
    /// Bone axes differ between rigs, so the tracked head and hand rotations are composed with each
    /// bone's T-pose orientation and written after IK.
    ///
    /// In self mode (the performer's own avatar, worn or shown while recording) the head bone is scaled
    /// to zero so the camera is never inside the avatar's head.
    /// </summary>
    public class ChaviAvatar : IAvatarInstance
    {
        const float EyeAboveHeadBone = 0.1f;

        readonly GameObject root;
        readonly ChaviResource resource;
        readonly bool selfMode;
        readonly Transform[] bones;
        readonly int[] humanMap;
        readonly List<Material> materials = new List<Material>();
        readonly Mesh builtMesh;
        readonly SkinnedMeshRenderer meshRenderer;

        readonly Avatar avatar;
        readonly Animator animator;
        PlayableGraph graph;
        AnimationScriptPlayable ikPlayable;

        readonly Transform headBone;
        readonly Transform leftHand;
        readonly Transform rightHand;

        readonly Quaternion headOffset = Quaternion.identity;
        readonly Quaternion leftHandOffset = Quaternion.identity;
        readonly Quaternion rightHandOffset = Quaternion.identity;

        /// <summary>The head bone's T-pose position in the root's space at scale 1.</summary>
        readonly Vector3 restHeadLocal = new Vector3(0f, 1.5f, 0f);
        readonly Vector3 restLeftShoulder = new Vector3(-0.2f, 1.4f, 0f);
        readonly Vector3 restRightShoulder = new Vector3(0.2f, 1.4f, 0f);
        float scale = 1f;
        bool calibrated;
        float yaw;

        public bool IsAlive => root != null;
        public Transform Root => root != null ? root.transform : null;
        public ChaviResource Resource => resource;
        public bool IsHumanoid => avatar != null;
        public Animator Animator => animator;
        public Transform HeadBone => headBone;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public bool IsSelf => selfMode;

        public ChaviAvatar(ChaviResource resource, int actNumber, bool selfMode = false)
        {
            this.resource = resource ?? throw new ArgumentNullException(nameof(resource));
            this.selfMode = selfMode;

            // The shell prefab holds the Animator and the skinned mesh renderer; only the bones, which
            // differ per model, are created from the resource.
            root = RuntimePrefabs.Instantiate(RuntimePrefabs.AvatarShell);
            root.name = selfMode
                ? $"Self Avatar ({resource.AvatarName})"
                : $"Act {actNumber} Avatar ({resource.AvatarName})";
            animator = root.GetComponent<Animator>();
            var smr = meshRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);

            Mesh mesh = builtMesh = resource.BuildMesh();
            Material[] mats = resource.BuildMaterials();
            materials.AddRange(mats);
            smr.sharedMesh = mesh;
            smr.sharedMaterials = mats;

            if (!resource.HasBones)
            {
                animator.enabled = false;
                return;
            }

            bones = HumanoidRig.CreateBones(resource, root.transform);
            humanMap = resource.GetHumanBoneMap();

            avatar = HumanoidRig.Build(root.transform, bones, humanMap, out string error);
            if (avatar == null)
                Debug.LogWarning($"[ChaviAvatar] '{resource.AvatarName}': {error} It will follow the head without IK.");

            headBone = HumanoidRig.Get(bones, humanMap, HumanBodyBones.Head);
            leftHand = HumanoidRig.Get(bones, humanMap, HumanBodyBones.LeftHand);
            rightHand = HumanoidRig.Get(bones, humanMap, HumanBodyBones.RightHand);

            // Root is at the origin, unscaled and T-posed here.
            Transform leftUpperArm = HumanoidRig.Get(bones, humanMap, HumanBodyBones.LeftUpperArm);
            Transform rightUpperArm = HumanoidRig.Get(bones, humanMap, HumanBodyBones.RightUpperArm);
            if (leftUpperArm != null)
                restLeftShoulder = leftUpperArm.position;
            if (rightUpperArm != null)
                restRightShoulder = rightUpperArm.position;
            if (headBone != null)
            {
                headOffset = headBone.rotation;
                restHeadLocal = headBone.position;
                restHeadLocal.y = Mathf.Max(0.3f, restHeadLocal.y);
            }

            leftHandOffset = HandOffset(HumanoidRig.Get(bones, humanMap, HumanBodyBones.LeftLowerArm), leftHand);
            rightHandOffset = HandOffset(HumanoidRig.Get(bones, humanMap, HumanBodyBones.RightLowerArm), rightHand);

            smr.bones = bones;
            Transform hips = HumanoidRig.Get(bones, humanMap, HumanBodyBones.Hips);
            smr.rootBone = hips != null ? hips : bones[0];

            if (avatar == null)
            {
                animator.enabled = false;
            }
            else
            {
                animator.avatar = avatar;

                graph = PlayableGraph.Create(root.name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                ikPlayable = AnimationScriptPlayable.Create(graph, new HumanoidIKJob());
                AnimationPlayableOutput.Create(graph, "Humanoid IK", animator).SetSourcePlayable(ikPlayable);
                graph.Play();
            }
        }

        /// <summary>
        /// Maps a controller rotation to a hand bone rotation. A controller held pointing forward is taken
        /// to point along the fingers (rest finger direction = hand's first child, else the forearm direction),
        /// with its up axis on world up, so the offset is inverse(reference) * restRotation.
        /// </summary>
        static Quaternion HandOffset(Transform lowerArm, Transform hand)
        {
            if (hand == null)
                return Quaternion.identity;

            Vector3 dir = Vector3.zero;
            if (hand.childCount > 0)
                dir = hand.GetChild(0).position - hand.position;
            if (dir.sqrMagnitude < 1e-8f && lowerArm != null)
                dir = hand.position - lowerArm.position;
            if (dir.sqrMagnitude < 1e-8f)
                return Quaternion.identity;

            dir.Normalize();
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion reference = Quaternion.LookRotation(dir, up);
            return Quaternion.Inverse(reference) * hand.rotation;
        }

        public void Apply(in ActFrame frame)
        {
            if (!IsAlive)
                return;

            meshRenderer.enabled = true;

            if (bones == null)
            {
                root.transform.SetPositionAndRotation(frame.Body.Position, frame.Body.Rotation);
                return;
            }

            bool hasHead = frame.Head.Position.sqrMagnitude > 0.001f;
            float floorY = frame.Body.Position.y;
            Transform rootT = root.transform;

            // Fit the avatar to the performer once, from their first tracked pose, so crouching later
            // does not shrink it.
            if (!calibrated && hasHead)
            {
                float eyeHeight = frame.Head.Position.y - floorY;
                if (eyeHeight > 0.5f)
                    scale = Mathf.Clamp((eyeHeight - EyeAboveHeadBone) / restHeadLocal.y, 0.6f, 1.6f);
                calibrated = true;
            }

            Vector3 position;
            Quaternion rotation;
            if (hasHead)
            {
                Vector3 fwd = frame.Head.Rotation * Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f)
                    yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;

                // Place the root so the head bone sits under the tracked head.
                rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 headOffsetWorld = rotation * (restHeadLocal * scale);
                position = new Vector3(frame.Head.Position.x - headOffsetWorld.x, floorY, frame.Head.Position.z - headOffsetWorld.z);
            }
            else
            {
                position = frame.Body.Position;
                rotation = Quaternion.Euler(0f, frame.Body.Rotation.eulerAngles.y, 0f);
            }

            if (graph.IsValid())
            {
                // The humanoid stream poses the body relative to the world origin, so solve with the root
                // at identity and the goals in root space, then move the root into place.
                Matrix4x4 toLocal = Matrix4x4.TRS(position, rotation, Vector3.one * scale).inverse;
                rootT.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rootT.localScale = Vector3.one;

                bool hasLeft = frame.LeftHand.Position.sqrMagnitude > 0.001f;
                bool hasRight = frame.RightHand.Position.sqrMagnitude > 0.001f;
                ikPlayable.SetJobData(new HumanoidIKJob
                {
                    HasHead = hasHead,
                    HasLeftHand = hasLeft,
                    HasRightHand = hasRight,
                    LeftHandPosition = toLocal.MultiplyPoint3x4(frame.LeftHand.Position),
                    RightHandPosition = toLocal.MultiplyPoint3x4(frame.RightHand.Position),
                    LeftElbowHint = ElbowHint(restLeftShoulder, -1f),
                    RightElbowHint = ElbowHint(restRightShoulder, 1f),
                    LookAtPosition = toLocal.MultiplyPoint3x4(frame.Head.Position + frame.Head.Rotation * Vector3.forward * 2f)
                });
                graph.Evaluate(0f);
            }

            rootT.SetPositionAndRotation(position, rotation);
            rootT.localScale = Vector3.one * scale;

            if (graph.IsValid())
            {
                bool hasLeft = frame.LeftHand.Position.sqrMagnitude > 0.001f;
                bool hasRight = frame.RightHand.Position.sqrMagnitude > 0.001f;
                if (hasLeft && leftHand != null)
                    leftHand.rotation = frame.LeftHand.Rotation * leftHandOffset;
                if (hasRight && rightHand != null)
                    rightHand.rotation = frame.RightHand.Rotation * rightHandOffset;
            }

            if (headBone != null)
            {
                if (hasHead)
                    headBone.rotation = frame.Head.Rotation * headOffset;
                headBone.localScale = selfMode ? Vector3.zero : Vector3.one;
            }
        }

        /// <summary>Elbows hang down, back and slightly outward (root space).</summary>
        static Vector3 ElbowHint(Vector3 shoulder, float side)
        {
            return shoulder + new Vector3(side * 0.3f, -0.6f, -0.4f);
        }

        /// <summary>Hides the mesh only: deactivating the object would make the Animator rebind its
        /// humanoid on the next show, which shifts the solved pose for a frame.</summary>
        public void Hide()
        {
            if (IsAlive)
                meshRenderer.enabled = false;
        }

        public void Destroy()
        {
            if (graph.IsValid())
                graph.Destroy();

            HumanoidRig.DestroyObject(root);
            HumanoidRig.DestroyObject(avatar);

            foreach (Material material in materials)
                HumanoidRig.DestroyObject(material);
            materials.Clear();

            HumanoidRig.DestroyObject(builtMesh);
        }
    }
}
