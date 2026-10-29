using UnityEngine;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// The prefabs that objects spawned from user data are cloned from: an avatar shell, the sphere
    /// stand-in, a prop body and a hand anchor. They live in Resources so plain classes (playback,
    /// grabbers, the prop registry) can reach them without scene references, and so builds include them.
    /// </summary>
    public static class RuntimePrefabs
    {
        const string Folder = "ChimeralTheater/";

        public const string AvatarShell = "AvatarShell";
        public const string SphereStandIn = "SphereStandIn";
        public const string Prop = "Prop";
        public const string HandAnchor = "HandAnchor";

        /// <summary>Clones the named prefab, positioned before any of its components start.</summary>
        public static GameObject Instantiate(string name, Vector3 position, Quaternion rotation)
        {
            var prefab = Resources.Load<GameObject>(Folder + name);
            if (prefab == null)
                throw new MissingReferenceException($"Missing prefab Resources/{Folder}{name}.prefab.");

            GameObject clone = Object.Instantiate(prefab, position, rotation);
            clone.name = name;
            return clone;
        }

        public static GameObject Instantiate(string name) => Instantiate(name, Vector3.zero, Quaternion.identity);

        public static void Destroy(Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
