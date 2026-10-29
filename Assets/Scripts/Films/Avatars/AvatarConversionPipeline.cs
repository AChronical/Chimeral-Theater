using System;
using System.IO;
using System.Threading.Tasks;
using UniRes;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Converts .glb / .gltf models (e.g. Mesh2Motion exports) into .chavi avatar resources in the
    /// Asset Inventory directory, and loads / deletes them again.
    /// </summary>
    public static class AvatarConversionPipeline
    {
        /// <summary>Converts a model and saves it to the Asset Inventory. Returns the avatar, or null and an error.</summary>
        public static async Task<(ChaviResource chavi, string error)> ConvertAsync(string sourcePath)
        {
            string error;
            ChaviResource chavi;

            if (!File.Exists(sourcePath))
            {
                error = $"Source file not found: {sourcePath}";
                return (null, error);
            }

            if (!AssetStorePaths.IsSupportedModelFile(sourcePath))
            {
                error = $"Unsupported model format: '{Path.GetExtension(sourcePath).ToLowerInvariant()}'. Supported: .glb, .gltf";
                return (null, error);
            }

            try
            {
                chavi = await GltfModelLoader.LoadAsync(sourcePath);
                if (chavi == null || chavi.VertexCount == 0)
                {
                    error = "Conversion failed: model contains no geometry.";
                    return (null, error);
                }

                if (!chavi.HasBones)
                {
                    error = "Conversion failed: model has no skin/skeleton. Export a rigged model (e.g. from Mesh2Motion).";
                    return (null, error);
                }

                if (!HumanoidRig.Validate(chavi, out string humanoidError))
                {
                    error = $"Conversion failed: {humanoidError}";
                    return (null, error);
                }

                string dest = AssetStorePaths.GetInventoryPathForAvatar(chavi.AvatarName);
                ResourceSaver.Save(chavi, dest, SaveFlags.ChangePath | SaveFlags.Binary);

                int mapped = 0;
                foreach (int bone in chavi.HumanBoneMap)
                    if (bone >= 0)
                        mapped++;

                Debug.Log($"[AvatarConversion] '{Path.GetFileName(sourcePath)}' -> '{dest}': " +
                          $"{chavi.VertexCount} verts, {chavi.Triangles.Length / 3} tris, {chavi.BoneNames.Length} bones, " +
                          $"{chavi.SubmeshTriangleCounts.Length} submeshes, {mapped} humanoid bones mapped");
                return (chavi, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AvatarConversion] Failed converting '{sourcePath}': {ex}");
                error = $"Conversion error: {ex.Message}";
                return (null, error);
            }
        }

        public static ChaviResource LoadAvatar(string avatarNameOrPath)
        {
            if (string.IsNullOrEmpty(avatarNameOrPath))
                return null;

            string fullPath = File.Exists(avatarNameOrPath)
                ? avatarNameOrPath
                : AssetStorePaths.GetInventoryPathForAvatar(Path.GetFileNameWithoutExtension(avatarNameOrPath));

            if (!File.Exists(fullPath))
                return null;

            try
            {
                return ResourceLoader.Load<ChaviResource>(fullPath, CacheMode.Ignore);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AvatarConversion] Failed to load avatar from '{fullPath}': {ex.Message}");
                return null;
            }
        }

        public static bool DeleteAvatar(string avatarNameOrPath)
        {
            string fullPath = File.Exists(avatarNameOrPath)
                ? avatarNameOrPath
                : AssetStorePaths.GetInventoryPathForAvatar(Path.GetFileNameWithoutExtension(avatarNameOrPath));

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return true;
            }

            return false;
        }
    }
}
