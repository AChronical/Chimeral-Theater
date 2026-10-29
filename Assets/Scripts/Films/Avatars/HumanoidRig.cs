using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Turns a <see cref="ChaviResource"/> skeleton into a Unity humanoid: builds the transform hierarchy,
    /// puts it in a T-pose, describes it with a <see cref="HumanDescription"/> and builds the
    /// <see cref="Avatar"/> with <see cref="AvatarBuilder.BuildHumanAvatar"/>. This is the runtime version of
    /// what the model importer does for "Animation Type: Humanoid", so every rig ends up in the same
    /// muscle space and can be driven by the Animator's humanoid IK.
    /// </summary>
    public static class HumanoidRig
    {
        /// <summary>Creates one transform per bone under <paramref name="root"/>, at the stored rest pose.
        /// Names are made unique because the avatar binds bones by name.</summary>
        public static Transform[] CreateBones(ChaviResource resource, Transform root)
        {
            int count = resource.BoneNames.Length;
            var bones = new Transform[count];
            var names = new HashSet<string>(StringComparer.Ordinal) { root.name };

            for (int i = 0; i < count; i++)
            {
                string name = string.IsNullOrEmpty(resource.BoneNames[i]) ? $"Joint_{i}" : resource.BoneNames[i];
                string unique = name;
                for (int n = 1; !names.Add(unique); n++)
                    unique = $"{name}_{n}";

                bones[i] = new GameObject(unique).transform;
                bones[i].SetParent(root, false);
            }

            for (int i = 0; i < count; i++)
            {
                int parent = ParentOf(resource, i);
                if (parent >= 0)
                    bones[i].SetParent(bones[parent], false);
            }

            for (int i = 0; i < count; i++)
            {
                if (i * 3 + 2 < resource.BoneLocalPositions.Length)
                {
                    bones[i].localPosition = new Vector3(
                        resource.BoneLocalPositions[i * 3],
                        resource.BoneLocalPositions[i * 3 + 1],
                        resource.BoneLocalPositions[i * 3 + 2]);
                }

                if (i * 4 + 3 < resource.BoneLocalRotations.Length)
                {
                    bones[i].localRotation = new Quaternion(
                        resource.BoneLocalRotations[i * 4],
                        resource.BoneLocalRotations[i * 4 + 1],
                        resource.BoneLocalRotations[i * 4 + 2],
                        resource.BoneLocalRotations[i * 4 + 3]).normalized;
                }
            }

            return bones;
        }

        public static int ParentOf(ChaviResource resource, int bone)
        {
            int[] parents = resource.BoneParentIndices;
            int p = parents != null && bone < parents.Length ? parents[bone] : -1;
            return p >= 0 && p < resource.BoneNames.Length && p != bone ? p : -1;
        }

        /// <summary>
        /// Straightens the arms out to the sides and the legs down, which is the reference pose Unity's
        /// humanoid expects (the importer's "Enforce T-Pose"). Rigs bound in an A-pose would otherwise
        /// have every arm muscle offset. The character is assumed to face +Z.
        /// </summary>
        public static void EnforceTPose(Transform root, Transform[] bones, int[] map)
        {
            Transform Bone(HumanBodyBones b) => Get(bones, map, b);

            Transform lUpper = Bone(HumanBodyBones.LeftUpperArm), rUpper = Bone(HumanBodyBones.RightUpperArm);
            Vector3 right = root.right;
            if (lUpper != null && rUpper != null)
            {
                Vector3 across = rUpper.position - lUpper.position;
                across.y = 0f;
                if (across.sqrMagnitude > 1e-6f)
                    right = across.normalized;
            }

            Align(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), -right);
            Align(Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand), -right);
            Align(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), right);
            Align(Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand), right);
            Align(Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.LeftLowerLeg), Vector3.down);
            Align(Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.LeftFoot), Vector3.down);
            Align(Bone(HumanBodyBones.RightUpperLeg), Bone(HumanBodyBones.RightLowerLeg), Vector3.down);
            Align(Bone(HumanBodyBones.RightLowerLeg), Bone(HumanBodyBones.RightFoot), Vector3.down);
        }

        static void Align(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null)
                return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f)
                return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>Describes the current pose of the hierarchy as the humanoid reference pose.</summary>
        public static HumanDescription Describe(Transform root, Transform[] bones, int[] map)
        {
            var human = new List<HumanBone>();
            for (int slot = 0; slot < map.Length && slot < HumanTrait.BoneCount; slot++)
            {
                int bone = map[slot];
                if (bone < 0 || bone >= bones.Length)
                    continue;

                var hb = new HumanBone
                {
                    humanName = HumanTrait.BoneName[slot],
                    boneName = bones[bone].name
                };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }

            var skeleton = new List<SkeletonBone>(bones.Length + 1)
            {
                new SkeletonBone { name = root.name, position = Vector3.zero, rotation = Quaternion.identity, scale = Vector3.one }
            };
            foreach (Transform t in bones)
            {
                skeleton.Add(new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                });
            }

            return new HumanDescription
            {
                human = human.ToArray(),
                skeleton = skeleton.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };
        }

        /// <summary>
        /// Builds the humanoid avatar for a hierarchy made by <see cref="CreateBones"/>. The hierarchy is
        /// left in the T-pose. Returns null with <paramref name="error"/> set when the rig is not humanoid.
        /// </summary>
        public static Avatar Build(Transform root, Transform[] bones, int[] map, out string error)
        {
            List<string> missing = HumanoidMappingHelper.MissingRequired(map);
            if (missing.Count > 0)
            {
                error = "Not a humanoid rig. Missing bones: " + string.Join(", ", missing);
                return null;
            }

            Vector3 position = root.position;
            Quaternion rotation = root.rotation;
            Vector3 scale = root.localScale;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;

            EnforceTPose(root, bones, map);
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(root.gameObject, Describe(root, bones, map));

            root.SetPositionAndRotation(position, rotation);
            root.localScale = scale;

            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                if (avatar != null)
                    DestroyObject(avatar);
                error = "Unity could not build a humanoid avatar from this rig (see the console for details).";
                return null;
            }

            avatar.name = root.name;
            error = null;
            return avatar;
        }

        /// <summary>Checks that a converted resource builds into a valid humanoid avatar.</summary>
        public static bool Validate(ChaviResource resource, out string error)
        {
            if (resource == null || !resource.HasBones)
            {
                error = "Model has no skeleton.";
                return false;
            }

            var root = new GameObject("HumanoidValidation");
            try
            {
                Transform[] bones = CreateBones(resource, root.transform);
                Avatar avatar = Build(root.transform, bones, resource.GetHumanBoneMap(), out error);
                if (avatar == null)
                    return false;
                DestroyObject(avatar);
                return true;
            }
            finally
            {
                DestroyObject(root);
            }
        }

        public static Transform Get(Transform[] bones, int[] map, HumanBodyBones slot)
        {
            int i = (int)slot;
            if (bones == null || map == null || i < 0 || i >= map.Length)
                return null;
            int bone = map[i];
            return bone >= 0 && bone < bones.Length ? bones[bone] : null;
        }

        internal static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
