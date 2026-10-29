using System;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Which inventory avatar the performer is currently wearing (shown on their own body, following the
    /// headset and controllers). Persisted across sessions in PlayerPrefs; empty means none.
    /// </summary>
    public static class AvatarWearState
    {
        static string cached;
        const string PrefsKey = "ChimeralTheater.WornAvatar";

        public static event Action Changed;

        /// <summary>Path of the worn .chavi, or empty.</summary>
        public static string Worn
        {
            get => cached ?? (cached = PlayerPrefs.GetString(PrefsKey, ""));
            set
            {
                value = value ?? "";
                if (Worn == value)
                    return;

                cached = value;
                PlayerPrefs.SetString(PrefsKey, value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool IsWorn(string avatarPath) =>
            !string.IsNullOrEmpty(avatarPath) && SamePath(Worn, avatarPath);

        public static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;

            return string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(a),
                System.IO.Path.GetFileNameWithoutExtension(b),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
