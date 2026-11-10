using System;
using System.IO;
using System.Threading.Tasks;
using ChimeralTheater.Films.Avatars;
using UniRes;
using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// Converts .glb / .gltf models into .chprop prop resources in the Asset Inventory directory, and
    /// loads / deletes them again. Rigged models are accepted; only their mesh is kept. Every prop is
    /// auto-fitted so its longest side is <see cref="TargetSize"/>.
    /// </summary>
    public static class PropConversionPipeline
    {
        /// <summary>Every converted prop is scaled so its longest side is this many metres.</summary>
        public const float TargetSize = 1.0f;

        /// <summary>Converts a model and saves it to the Asset Inventory. Returns the prop, or null and an error.</summary>
        public static async Task<(ChpropResource prop, string error)> ConvertAsync(string sourcePath)
        {
            string error;
            ChpropResource prop;

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
                ChaviResource model = await GltfModelLoader.LoadAsync(sourcePath, bakeNodeTransforms: true);
                if (model == null || model.VertexCount == 0)
                {
                    error = "Conversion failed: model contains no geometry.";
                    return (null, error);
                }

                prop = ChpropResource.FromModel(model);
                prop.FitToSize(TargetSize);
                string dest = AssetStorePaths.GetInventoryPathForProp(prop.PropName);
                ResourceSaver.Save(prop, dest, SaveFlags.ChangePath | SaveFlags.Binary);

                Debug.Log($"[PropConversion] '{Path.GetFileName(sourcePath)}' -> '{dest}': " +
                          $"{prop.VertexCount} verts, {prop.Triangles.Length / 3} tris, {prop.SubmeshTriangleCounts.Length} submeshes");
                return (prop, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PropConversion] Failed converting '{sourcePath}': {ex}");
                error = $"Conversion error: {ex.Message}";
                return (null, error);
            }
        }

        public static ChpropResource LoadProp(string propNameOrPath)
        {
            if (string.IsNullOrEmpty(propNameOrPath))
                return null;

            string fullPath = ResolvePath(propNameOrPath);
            if (!File.Exists(fullPath))
                return null;

            try
            {
                return ResourceLoader.Load<ChpropResource>(fullPath, CacheMode.Ignore);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PropConversion] Failed to load prop from '{fullPath}': {ex.Message}");
                return null;
            }
        }

        public static bool DeleteProp(string propNameOrPath)
        {
            string fullPath = ResolvePath(propNameOrPath);
            if (!File.Exists(fullPath))
                return false;

            File.Delete(fullPath);
            return true;
        }

        static string ResolvePath(string propNameOrPath)
        {
            return File.Exists(propNameOrPath)
                ? propNameOrPath
                : AssetStorePaths.GetInventoryPathForProp(Path.GetFileNameWithoutExtension(propNameOrPath));
        }
    }
}
