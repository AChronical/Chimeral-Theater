using System;
using System.Collections.Generic;
using ChimeralTheater.Films.Avatars;
using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// The props currently spawned in the scene, at most one per .chprop. Spawning drops the prop at
    /// a given floor point (the player's rig) as a physics object; despawning removes it. Nothing here is saved: spawned
    /// props last only for this app session.
    /// </summary>
    public static class PropRegistry
    {
        /// <summary>How far above the origin the bottom of a newly spawned prop starts, so it drops onto the floor.</summary>
        public const float SpawnDropHeight = 0.5f;

        static readonly List<PropBody> spawned = new List<PropBody>();

        public static event Action Changed;

        /// <summary>Every live spawned prop.</summary>
        public static IReadOnlyList<PropBody> Spawned
        {
            get
            {
                spawned.RemoveAll(p => p == null);
                return spawned;
            }
        }

        public static bool IsSpawned(string propPath) => Find(propPath) != null;

        public static PropBody Find(string propPath)
        {
            foreach (PropBody prop in Spawned)
            {
                if (AvatarWearState.SamePath(prop.AssetPath, propPath))
                    return prop;
            }

            return null;
        }

        /// <summary>Spawns the prop above <paramref name="floorPoint"/> so it drops onto it. Returns the existing one
        /// if it is already spawned, or null if it cannot be loaded.</summary>
        public static PropBody Spawn(string propPath, Vector3 floorPoint)
        {
            PropBody existing = Find(propPath);
            if (existing != null)
                return existing;

            ChpropResource resource = PropConversionPipeline.LoadProp(propPath);
            if (resource == null)
            {
                Debug.LogWarning($"[PropRegistry] Could not load prop '{propPath}'.");
                return null;
            }

            Mesh mesh = resource.BuildMesh();

            // Clone the Prop prefab (rigidbody, convex mesh collider, PropBody) already in place: an
            // interpolated Rigidbody writes its own stored pose back onto the transform, so moving the
            // transform afterwards would snap back.
            Vector3 spawnPosition = floorPoint + Vector3.up * (SpawnDropHeight - mesh.bounds.min.y);
            GameObject go = RuntimePrefabs.Instantiate(RuntimePrefabs.Prop, spawnPosition, Quaternion.identity);
            go.name = $"Prop {resource.PropName}";
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = resource.BuildMaterials();
            go.GetComponent<MeshCollider>().sharedMesh = mesh;

            var prop = go.GetComponent<PropBody>();
            prop.AssetPath = propPath;
            prop.Body = go.GetComponent<Rigidbody>();

            spawned.Add(prop);
            Changed?.Invoke();
            return prop;
        }

        public static void Despawn(string propPath)
        {
            PropBody prop = Find(propPath);
            if (prop == null)
                return;

            prop.HeldBy?.Release();
            spawned.Remove(prop);
            PropGrabber.SafeDestroy(prop.gameObject);
            Changed?.Invoke();
        }
    }
}
