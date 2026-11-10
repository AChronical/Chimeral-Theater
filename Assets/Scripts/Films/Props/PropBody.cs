using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// Marks a spawned prop in the scene: which .chprop it came from and which grabber, if any, is
    /// holding it. A prop held by one hand cannot be grabbed by another until it is released.
    /// </summary>
    public class PropBody : MonoBehaviour
    {
        public string AssetPath { get; set; } = "";
        public Rigidbody Body { get; set; }
        public PropGrabber HeldBy { get; set; }

        public bool IsHeld => HeldBy != null;
    }
}
