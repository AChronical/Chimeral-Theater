using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Resolves file system paths for the User Files directory (where raw .glb/.gltf models are placed)
    /// and the Asset Inventory directory (where converted .chavi avatars and .chprop props are cataloged).
    /// Both live outside Assets/ so Unity never imports them: next to the project folder in the editor,
    /// and in <see cref="Application.persistentDataPath"/> in builds (the only writable per-app folder on
    /// every platform, including Quest).
    /// </summary>
    public static class AssetStorePaths
    {
        public const string UserFilesFolderName = "UserFiles";
        public const string AssetInventoryFolderName = "AssetInventory";
        public const string ChaviExtension = ".chavi";
        public const string ChpropExtension = ".chprop";

        private static readonly HashSet<string> SupportedModelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".gltf",
            ".glb"
        };

        public static string AppRoot =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                : Application.persistentDataPath;

        public static string UserFilesRoot => Path.Combine(AppRoot, UserFilesFolderName);

        public static string AssetInventoryRoot => Path.Combine(AppRoot, AssetInventoryFolderName);

        public static void EnsureDirectoriesExist()
        {
            try
            {
                Directory.CreateDirectory(UserFilesRoot);
                Directory.CreateDirectory(AssetInventoryRoot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssetStorePaths] EnsureDirectoriesExist: {ex.Message}");
            }
        }

        public static bool IsSupportedModelFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return SupportedModelExtensions.Contains(Path.GetExtension(path));
        }

        public static string[] GetDirectories(string relativeSubdir = "")
        {
            EnsureDirectoriesExist();
            string target = string.IsNullOrEmpty(relativeSubdir)
                ? UserFilesRoot
                : Path.Combine(UserFilesRoot, relativeSubdir);

            return Directory.Exists(target) ? Directory.GetDirectories(target) : Array.Empty<string>();
        }

        public static string[] GetModelFiles(string relativeSubdir = "")
        {
            EnsureDirectoriesExist();
            string target = string.IsNullOrEmpty(relativeSubdir)
                ? UserFilesRoot
                : Path.Combine(UserFilesRoot, relativeSubdir);

            var results = new List<string>();
            if (Directory.Exists(target))
            {
                foreach (string file in Directory.GetFiles(target))
                {
                    if (IsSupportedModelFile(file))
                        results.Add(file);
                }
            }

            return results.ToArray();
        }

        public static string[] GetInventoryAvatars()
        {
            EnsureDirectoriesExist();
            return Directory.GetFiles(AssetInventoryRoot, $"*{ChaviExtension}");
        }

        public static string[] GetInventoryProps()
        {
            EnsureDirectoriesExist();
            return Directory.GetFiles(AssetInventoryRoot, $"*{ChpropExtension}");
        }

        public static string GetInventoryPathForProp(string propName)
        {
            EnsureDirectoriesExist();
            string fileName = propName.EndsWith(ChpropExtension, StringComparison.OrdinalIgnoreCase)
                ? propName
                : $"{propName}{ChpropExtension}";

            return Path.Combine(AssetInventoryRoot, fileName);
        }

        public static string GetInventoryPathForAvatar(string avatarName)
        {
            EnsureDirectoriesExist();
            string fileName = avatarName.EndsWith(ChaviExtension, StringComparison.OrdinalIgnoreCase)
                ? avatarName
                : $"{avatarName}{ChaviExtension}";

            return Path.Combine(AssetInventoryRoot, fileName);
        }
    }
}
