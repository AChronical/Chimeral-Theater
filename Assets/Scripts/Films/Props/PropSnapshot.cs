using System.Collections.Generic;
using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// Where every spawned prop was at one moment, held in memory only. The director captures one when
    /// a recording or playback starts and restores it when that recording or playback ends.
    /// </summary>
    public class PropSnapshot
    {
        struct Entry
        {
            public PropBody Prop;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        readonly List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        public void Capture()
        {
            entries.Clear();
            foreach (PropBody prop in PropRegistry.Spawned)
            {
                entries.Add(new Entry
                {
                    Prop = prop,
                    Position = prop.transform.position,
                    Rotation = prop.transform.rotation,
                });
            }
        }

        /// <summary>Puts each captured prop that still exists back where it was, at rest. Props spawned since are left alone.</summary>
        public void Restore()
        {
            foreach (Entry entry in entries)
            {
                if (entry.Prop == null)
                    continue;

                entry.Prop.HeldBy?.Release();

                Rigidbody body = entry.Prop.Body;
                entry.Prop.transform.SetPositionAndRotation(entry.Position, entry.Rotation);
                if (body != null)
                {
                    body.position = entry.Position;
                    body.rotation = entry.Rotation;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            entries.Clear();
        }
    }
}
