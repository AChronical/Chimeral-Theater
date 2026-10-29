using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Maps an arbitrary rig onto Unity's humanoid bone slots (<see cref="HumanBodyBones"/>).
    ///
    /// Unity's own auto-mapper only exists in the editor's model importer, so this is the runtime stand-in.
    /// Names are only used to find the anchors (hips, head, hands, feet, fingers); the limb and spine
    /// chains between them come from the hierarchy, which keeps it working across naming conventions
    /// (Mixamo, VRM, Rigify, 3ds Max Biped, Unreal, Mesh2Motion).
    /// </summary>
    public static class HumanoidMappingHelper
    {
        static readonly string[] FingerKeys = { "thumb", "index", "middle", "ring", "little" };
        static readonly string[] FingerAliases = { "thumb", "index", "middle", "ring", "pinky|little|small" };
        static readonly string[] FingerPhases = { "Proximal", "Intermediate", "Distal" };

        /// <summary>
        /// Returns an array indexed by <see cref="HumanBodyBones"/> holding the bone index for each slot,
        /// or -1 when the slot is unmapped.
        /// </summary>
        public static int[] Map(string[] boneNames, int[] parents)
        {
            var map = new int[(int)HumanBodyBones.LastBone];
            for (int i = 0; i < map.Length; i++)
                map[i] = -1;

            if (boneNames == null || boneNames.Length == 0)
                return map;

            int count = boneNames.Length;
            var rig = new Rig(boneNames, parents);

            int head = rig.Shallowest(i => Has(rig.Core[i], "head") && !Has(rig.Core[i], "end", "top", "nub", "tip"));
            int leftHand = rig.Shallowest(i => rig.Side[i] < 0 && IsHand(rig.Core[i]));
            int rightHand = rig.Shallowest(i => rig.Side[i] > 0 && IsHand(rig.Core[i]));
            int leftFoot = rig.Shallowest(i => rig.Side[i] < 0 && IsFoot(rig.Core[i]));
            int rightFoot = rig.Shallowest(i => rig.Side[i] > 0 && IsFoot(rig.Core[i]));

            int hips = rig.Shallowest(i => Has(rig.Core[i], "hips", "pelvis"));
            if (hips < 0 && leftFoot >= 0 && rightFoot >= 0)
                hips = rig.CommonAncestor(leftFoot, rightFoot);

            Set(map, HumanBodyBones.Hips, hips);
            Set(map, HumanBodyBones.Head, head);

            // Spine chain: everything strictly between the hips and the head.
            if (hips >= 0 && head >= 0 && rig.IsAncestor(hips, head))
            {
                List<int> path = rig.PathBetween(hips, head);
                if (path.Count >= 2 && Has(rig.Core[path[path.Count - 1]], "neck"))
                {
                    Set(map, HumanBodyBones.Neck, path[path.Count - 1]);
                    path.RemoveAt(path.Count - 1);
                }
                else if (path.Count >= 3)
                {
                    Set(map, HumanBodyBones.Neck, path[path.Count - 1]);
                    path.RemoveAt(path.Count - 1);
                }

                if (path.Count >= 1) Set(map, HumanBodyBones.Spine, path[0]);
                if (path.Count >= 2) Set(map, HumanBodyBones.Chest, path[1]);
                if (path.Count >= 3) Set(map, HumanBodyBones.UpperChest, path[path.Count - 1]);
            }

            MapArm(rig, map, leftHand, head, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            MapArm(rig, map, rightHand, head, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            MapLeg(rig, map, leftFoot, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes);
            MapLeg(rig, map, rightFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes);
            MapFingers(rig, map, leftHand, "Left");
            MapFingers(rig, map, rightHand, "Right");

            if (head >= 0)
            {
                Set(map, HumanBodyBones.LeftEye, rig.FirstDescendant(head, i => rig.Side[i] < 0 && Has(rig.Core[i], "eye") && !Has(rig.Core[i], "brow", "lid")));
                Set(map, HumanBodyBones.RightEye, rig.FirstDescendant(head, i => rig.Side[i] > 0 && Has(rig.Core[i], "eye") && !Has(rig.Core[i], "brow", "lid")));
                Set(map, HumanBodyBones.Jaw, rig.FirstDescendant(head, i => Has(rig.Core[i], "jaw")));
            }

            // A bone may only fill one slot.
            var used = new HashSet<int>();
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] < 0 || map[i] >= count)
                    map[i] = -1;
                else if (!used.Add(map[i]))
                    map[i] = -1;
            }

            return map;
        }

        /// <summary>The required humanoid slots that <paramref name="map"/> leaves empty.</summary>
        public static List<string> MissingRequired(int[] map)
        {
            var missing = new List<string>();
            for (int i = 0; i < HumanTrait.BoneCount && i < (int)HumanBodyBones.LastBone; i++)
            {
                if (HumanTrait.RequiredBone(i) && (map == null || i >= map.Length || map[i] < 0))
                    missing.Add(((HumanBodyBones)i).ToString());
            }
            return missing;
        }

        static void MapArm(Rig rig, int[] map, int hand, int head,
            HumanBodyBones shoulder, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones handSlot)
        {
            if (hand < 0)
                return;

            int lowerArm = rig.LimbParent(hand);
            int upperArm = lowerArm >= 0 ? rig.LimbParent(lowerArm) : -1;
            if (lowerArm < 0 || upperArm < 0)
                return;

            Set(map, handSlot, hand);
            Set(map, lower, lowerArm);
            Set(map, upper, upperArm);

            // The shoulder is the next bone up if it branches off the spine rather than being part of it.
            int clavicle = rig.Parents[upperArm];
            if (clavicle >= 0 && (head < 0 || !rig.IsAncestor(clavicle, head)))
                Set(map, shoulder, clavicle);
        }

        static void MapLeg(Rig rig, int[] map, int foot,
            HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones footSlot, HumanBodyBones toes)
        {
            if (foot < 0)
                return;

            int lowerLeg = rig.LimbParent(foot);
            int upperLeg = lowerLeg >= 0 ? rig.LimbParent(lowerLeg) : -1;
            if (lowerLeg < 0 || upperLeg < 0)
                return;

            Set(map, footSlot, foot);
            Set(map, lower, lowerLeg);
            Set(map, upper, upperLeg);
            Set(map, toes, rig.FirstDescendant(foot, i => Has(rig.Core[i], "toe", "ball") && !Has(rig.Core[i], "end", "nub", "tip")));
        }

        static void MapFingers(Rig rig, int[] map, int hand, string side)
        {
            if (hand < 0)
                return;

            for (int f = 0; f < FingerKeys.Length; f++)
            {
                string[] aliases = FingerAliases[f].Split('|');
                int start = rig.FirstDescendant(hand, i => Has(rig.Core[i], aliases) && !Has(rig.Core[i], "end", "nub", "tip"));
                if (start < 0)
                    continue;

                // Walk down the finger.
                var chain = new List<int>();
                for (int b = start; b >= 0 && chain.Count < 4;)
                {
                    chain.Add(b);
                    int next = -1;
                    foreach (int child in rig.Children[b])
                    {
                        if (Has(rig.Core[child], aliases) && !Has(rig.Core[child], "end", "nub", "tip"))
                        {
                            next = child;
                            break;
                        }
                    }
                    b = next;
                }
                // Four joints: either a leaf end bone (Mixamo's Index4) or a leading metacarpal.
                if (chain.Count == 4)
                    chain.RemoveAt(rig.Children[chain[3]].Count == 0 ? 3 : 0);

                for (int p = 0; p < chain.Count && p < 3; p++)
                {
                    string slot = side + Capitalize(FingerKeys[f]) + FingerPhases[p];
                    if (Enum.TryParse(slot, out HumanBodyBones bone))
                        Set(map, bone, chain[p]);
                }
            }
        }

        static void Set(int[] map, HumanBodyBones slot, int bone)
        {
            if (bone >= 0)
                map[(int)slot] = bone;
        }

        static bool IsHand(string core)
        {
            return Has(core, "hand", "wrist") && !Has(core, "thumb", "index", "middle", "ring", "pinky", "little", "finger", "end", "ik", "target", "pole");
        }

        static bool IsFoot(string core)
        {
            return Has(core, "foot", "ankle") && !Has(core, "toe", "ball", "end", "ik", "target", "pole");
        }

        static bool Has(string s, params string[] parts)
        {
            foreach (string part in parts)
                if (s.Contains(part))
                    return true;
            return false;
        }

        static string Capitalize(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Normalized names, sides and hierarchy of a rig.</summary>
        sealed class Rig
        {
            public readonly int[] Parents;
            public readonly string[] Core;
            public readonly int[] Side;
            public readonly List<int>[] Children;
            readonly int[] depth;

            public Rig(string[] names, int[] parents)
            {
                int count = names.Length;
                Parents = new int[count];
                Core = new string[count];
                Side = new int[count];
                Children = new List<int>[count];
                depth = new int[count];

                for (int i = 0; i < count; i++)
                {
                    int p = parents != null && i < parents.Length ? parents[i] : -1;
                    Parents[i] = p >= 0 && p < count && p != i ? p : -1;
                    Children[i] = new List<int>();
                    Classify(names[i] ?? "", out Core[i], out Side[i]);
                }

                for (int i = 0; i < count; i++)
                    if (Parents[i] >= 0)
                        Children[Parents[i]].Add(i);

                for (int i = 0; i < count; i++)
                {
                    int d = 0;
                    for (int p = Parents[i]; p >= 0 && d <= count; p = Parents[p])
                        d++;
                    depth[i] = d;
                }
            }

            /// <summary>Splits "mixamorig:LeftForeArm", "upperarm.L", "Bip001 R Hand" into a core name and a side (-1, 0, 1).</summary>
            static void Classify(string raw, out string core, out int side)
            {
                int colon = raw.LastIndexOf(':');
                if (colon >= 0 && colon < raw.Length - 1)
                    raw = raw.Substring(colon + 1);

                string name = raw.Trim().ToLowerInvariant();
                foreach (char c in " -.|")
                    name = name.Replace(c, '_');

                side = 0;
                var kept = new List<string>();
                foreach (string token in name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token == "l" || token == "left") { side = -1; continue; }
                    if (token == "r" || token == "right") { side = 1; continue; }
                    kept.Add(token);
                }

                core = string.Join("_", kept);
                if (side == 0)
                {
                    if (core.StartsWith("left")) { side = -1; core = core.Substring(4); }
                    else if (core.StartsWith("right")) { side = 1; core = core.Substring(5); }
                    else if (core.EndsWith("left")) { side = -1; core = core.Substring(0, core.Length - 4); }
                    else if (core.EndsWith("right")) { side = 1; core = core.Substring(0, core.Length - 5); }
                }
            }

            public int Shallowest(Func<int, bool> match)
            {
                int best = -1;
                for (int i = 0; i < Core.Length; i++)
                    if (match(i) && (best < 0 || depth[i] < depth[best]))
                        best = i;
                return best;
            }

            /// <summary>Breadth-first, so the nearest matching descendant wins.</summary>
            public int FirstDescendant(int start, Func<int, bool> match)
            {
                var queue = new Queue<int>(Children[start]);
                while (queue.Count > 0)
                {
                    int b = queue.Dequeue();
                    if (match(b))
                        return b;
                    foreach (int c in Children[b])
                        queue.Enqueue(c);
                }
                return -1;
            }

            /// <summary>The parent of a limb bone, skipping twist / roll helper bones in between.</summary>
            public int LimbParent(int bone)
            {
                int p = Parents[bone];
                while (p >= 0 && (Core[p].Contains("twist") || Core[p].Contains("roll")))
                    p = Parents[p];
                return p;
            }

            public bool IsAncestor(int ancestor, int bone)
            {
                for (int p = Parents[bone]; p >= 0; p = Parents[p])
                    if (p == ancestor)
                        return true;
                return false;
            }

            public int CommonAncestor(int a, int b)
            {
                for (int p = Parents[a]; p >= 0; p = Parents[p])
                    if (IsAncestor(p, b))
                        return p;
                return -1;
            }

            /// <summary>Bones strictly between <paramref name="ancestor"/> and <paramref name="bone"/>, top-down.</summary>
            public List<int> PathBetween(int ancestor, int bone)
            {
                var path = new List<int>();
                for (int p = Parents[bone]; p >= 0 && p != ancestor; p = Parents[p])
                    path.Insert(0, p);
                return path;
            }
        }
    }
}
