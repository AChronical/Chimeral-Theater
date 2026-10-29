using System.Collections.Generic;
using UnityEngine;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// The stand-in that emulates a recorded performer's appearance while an act plays back:
    /// a sphere for the head and one for each hand, cloned from the SphereStandIn prefab. Each act
    /// gets its own hue so simultaneous stand-ins can be told apart.
    /// </summary>
    public class SphereAvatar : ChimeralTheater.Films.Avatars.IAvatarInstance
    {
        readonly Transform root;
        readonly Transform head;
        readonly Transform leftHand;
        readonly Transform rightHand;
        readonly List<Material> materials = new List<Material>();

        public bool IsAlive => root != null;

        public Transform Root => root;

        public SphereAvatar(int actNumber)
        {
            root = RuntimePrefabs.Instantiate(RuntimePrefabs.SphereStandIn).transform;
            root.name = $"Act {actNumber} Stand-in";

            Color color = GetColor(actNumber);
            head = Tint(root.Find("Head"), color);
            leftHand = Tint(root.Find("Left Hand"), color);
            rightHand = Tint(root.Find("Right Hand"), color);
        }

        public void Apply(in ActFrame frame)
        {
            if (!IsAlive)
                return;

            Show();
            SetPose(root, frame.Body);
            SetPose(head, frame.Head);
            SetPose(leftHand, frame.LeftHand);
            SetPose(rightHand, frame.RightHand);
        }

        /// <summary>Hides the stand-in without destroying it, for when its act has ended but the session is still running.</summary>
        public void Hide()
        {
            if (IsAlive && root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
        }

        private void Show()
        {
            if (!root.gameObject.activeSelf)
                root.gameObject.SetActive(true);
        }

        public void Destroy()
        {
            if (IsAlive)
                DestroyObject(root.gameObject);

            foreach (Material material in materials)
                DestroyObject(material);

            materials.Clear();
        }

        static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>A distinct hue per act number, skipping the low-saturation band near zero.</summary>
        static Color GetColor(int actNumber)
        {
            float hue = Mathf.Repeat(0.02f + actNumber * 0.17f, 0.83f);
            return Color.HSVToRGB(hue, 0.9f, 1f);
        }

        static void SetPose(Transform target, in ActPose pose)
        {
            target.SetPositionAndRotation(pose.Position, pose.Rotation);
        }

        Transform Tint(Transform sphere, Color color)
        {
            var material = RuntimeMaterials.CreateLit(color);
            materials.Add(material);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            return sphere;
        }
    }
}
