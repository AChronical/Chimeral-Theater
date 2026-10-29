using System.IO;
using UniRes;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// Keeps each act's frame data in its own <c>.chact</c> file under <c>user://films</c>, so a
    /// take only holds act headers in memory. Files are scratch data for the current session.
    /// </summary>
    public static class ActStore
    {
        public const string Root = "user://films";
        public const string ActExtension = ".chact";

        public static string PathFor(Take take, Act act)
        {
            return $"{Root}/take_{take.Number}/act_{act.Number}{ActExtension}";
        }

        /// <summary>Writes the act's frames to its file as a compact binary UniRes resource, then releases them from memory.</summary>
        public static void Save(Take take, Act act)
        {
            ResourceSaver.Save(act, PathFor(take, act), SaveFlags.ChangePath | SaveFlags.Binary);
            act.ReleaseFrames();
        }

        /// <summary>
        /// Assigns the avatar that plays this act. The recording is independent of the avatar, so only
        /// the path changes. A recorded act's frames live in its file, not in the take's header, so the
        /// file is rewritten from a fresh load: saving the header alone would overwrite the recording
        /// with no frames.
        /// </summary>
        public static void SetAvatar(Take take, Act act, string avatarPath)
        {
            act.AvatarAssetPath = avatarPath ?? "";
            if (act.IsEmpty || act.IsBuiltIn)
                return;

            Act full = act.HasFramesLoaded ? act : LoadFrames(act);
            if (full == null)
                return;

            full.AvatarAssetPath = act.AvatarAssetPath;
            if (full == act)
            {
                Save(take, act);
                return;
            }

            // No ChangePath: the loaded copy must not take the file's path over, or the take's own act
            // would lose it and could no longer find its recording.
            ResourceSaver.Save(full, PathFor(take, act), SaveFlags.Binary);
        }

        /// <summary>
        /// Reads a fresh copy of the act with its frames from the file. Returns null for an act
        /// that has not been recorded. The caller owns the copy; the take's act is untouched.
        /// </summary>
        public static Act LoadFrames(Act act)
        {
            if (act.IsEmpty || act.IsBuiltIn)
                return null;

            return ResourceLoader.Load<Act>(act.ResourcePath, CacheMode.Ignore);
        }

        public static void Delete(Act act)
        {
            if (act.IsBuiltIn)
                return;

            string file = ResourcePaths.ToFileSystemPath(act.ResourcePath);
            if (File.Exists(file))
                File.Delete(file);
        }

        public static void DeleteTake(Take take)
        {
            foreach (Act act in take.Acts)
                Delete(act);

            string folder = ResourcePaths.ToFileSystemPath($"{Root}/take_{take.Number}");
            if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                Directory.Delete(folder);
        }

        /// <summary>Removes every stored act file. Called at session start since takes are not kept between sessions.</summary>
        public static void ClearAll()
        {
            string folder = ResourcePaths.ToFileSystemPath(Root);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }
}
