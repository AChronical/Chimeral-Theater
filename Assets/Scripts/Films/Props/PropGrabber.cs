using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// One hand that can hold a prop: a live controller or a replayed act's hand. The hand is an
    /// invisible kinematic anchor that follows the hand's pose; gripping joins the nearest free prop
    /// it touches to that anchor with a joint, at the offset where it was grabbed, so the prop keeps
    /// simulating and collides with other props while held. Releasing removes the joint and throws the
    /// prop with the hand's own measured velocity, so a throw does not depend on how many physics
    /// steps happened to run in the last frame.
    /// </summary>
    public class PropGrabber
    {
        /// <summary>How close to the hand a prop's collider must be to be grabbed, in metres.</summary>
        public const float GrabRadius = 0.08f;

        static readonly Collider[] Hits = new Collider[16];

        readonly GameObject anchorObject;
        readonly Rigidbody anchor;
        FixedJoint joint;
        bool placed;

        Vector3 lastPosition;
        Quaternion lastRotation = Quaternion.identity;
        Vector3 handVelocity;
        Vector3 handAngularVelocity;

        /// <summary>How much each new frame's velocity counts against the running average (smooths jitter).</summary>
        const float VelocitySmoothing = 0.5f;

        public PropBody Held { get; private set; }

        public PropGrabber(string name)
        {
            anchorObject = RuntimePrefabs.Instantiate(RuntimePrefabs.HandAnchor);
            anchorObject.name = name;
            anchor = anchorObject.GetComponent<Rigidbody>();
        }

        /// <summary>Moves the hand anchor to the hand's current pose, <paramref name="deltaTime"/> seconds after
        /// the previous one, and updates the hand's measured velocity.</summary>
        public void MoveTo(ActPose pose, float deltaTime)
        {
            Quaternion rotation = Sanitize(pose.Rotation);
            if (!placed)
            {
                Teleport(pose);
                placed = true;
                lastPosition = pose.Position;
                lastRotation = rotation;
                return;
            }

            if (deltaTime > 1e-5f)
            {
                Vector3 velocity = (pose.Position - lastPosition) / deltaTime;
                (rotation * Quaternion.Inverse(lastRotation)).ToAngleAxis(out float degrees, out Vector3 axis);
                if (degrees > 180f)
                    degrees -= 360f;
                Vector3 angular = float.IsInfinity(axis.x) || float.IsNaN(axis.x) || Mathf.Abs(degrees) < 1e-3f ? Vector3.zero : axis * (degrees * Mathf.Deg2Rad / deltaTime);

                handVelocity = Vector3.Lerp(handVelocity, velocity, VelocitySmoothing);
                handAngularVelocity = Vector3.Lerp(handAngularVelocity, angular, VelocitySmoothing);
                lastPosition = pose.Position;
                lastRotation = rotation;
            }

            anchor.MovePosition(pose.Position);
            anchor.MoveRotation(rotation);
        }

        /// <summary>
        /// Grips at the hand's pose: grabs the nearest prop whose collider is within
        /// <see cref="GrabRadius"/> and that no other hand holds. Returns false if nothing was grabbed.
        /// </summary>
        public bool Grip(ActPose pose)
        {
            if (Held != null)
                return false;

            PropBody target = FindNearestFreeProp(pose.Position);
            if (target == null)
                return false;

            // The joint locks the prop at its current offset from the anchor, so the anchor must be
            // exactly at the hand when the joint is made.
            Teleport(pose);
            placed = true;

            joint = target.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = anchor;
            target.HeldBy = this;
            Held = target;
            return true;
        }

        /// <summary>Lets go of the held prop, throwing it with the hand's velocity: the hand's linear velocity
        /// plus the spin's contribution at the prop's centre of mass, and the hand's spin.</summary>
        public void Release()
        {
            if (joint != null)
                SafeDestroy(joint);
            joint = null;

            if (Held != null)
            {
                Rigidbody body = Held.Body;
                if (body != null && !body.isKinematic)
                {
                    Vector3 lever = body.worldCenterOfMass - lastPosition;
                    body.linearVelocity = handVelocity + Vector3.Cross(handAngularVelocity, lever);
                    body.angularVelocity = handAngularVelocity;
                }

                Held.HeldBy = null;
            }
            Held = null;
        }

        public void Destroy()
        {
            Release();
            if (anchorObject != null)
                SafeDestroy(anchorObject);
        }

        /// <summary>Destroy only works in play mode; edit-mode tests need DestroyImmediate.</summary>
        internal static void SafeDestroy(Object target)
        {
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        void Teleport(ActPose pose)
        {
            Quaternion rotation = Sanitize(pose.Rotation);
            anchorObject.transform.SetPositionAndRotation(pose.Position, rotation);
            anchor.position = pose.Position;
            anchor.rotation = rotation;
        }

        /// <summary>Unity rejects zero-length quaternions with an error; recorded data can contain one
        /// (an untracked hand), so fall back to identity and normalise everything else.</summary>
        static Quaternion Sanitize(Quaternion q)
        {
            float lengthSq = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (lengthSq < 1e-8f || float.IsNaN(lengthSq))
                return Quaternion.identity;

            return Quaternion.Normalize(q);
        }

        static PropBody FindNearestFreeProp(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, GrabRadius, Hits, ~0, QueryTriggerInteraction.Ignore);

            PropBody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                PropBody prop = Hits[i].GetComponentInParent<PropBody>();
                if (prop == null || prop.IsHeld)
                    continue;

                float distance = (Hits[i].ClosestPoint(point) - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = prop;
                }
            }

            return best;
        }
    }
}
