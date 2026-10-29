using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Common interface for performer avatars during playback (stand-in spheres or converted .chavi avatars).
    /// </summary>
    public interface IAvatarInstance
    {
        bool IsAlive { get; }
        Transform Root { get; }
        void Apply(in ActFrame frame);
        void Hide();
        void Destroy();
    }
}
