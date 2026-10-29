using System;
using System.Collections.Generic;
using UniRes;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Self-contained UniRes resource representing an avatar converted from a 3D model (.glb, .gltf).
    /// Stores metadata, serialized mesh geometry, humanoid bone hierarchy, and material parameters.
    /// </summary>
    public class ChaviResource : Resource
    {
        [Export] public string AvatarName = "";
        [Export] public string SourcePath = "";
        [Export] public string SourceFormat = "";
        [Export] public string CreatedAt = "";

        // Mesh geometry
        [Export] public float[] VertexPositions = Array.Empty<float>();
        [Export] public float[] Normals = Array.Empty<float>();
        [Export] public float[] UVs = Array.Empty<float>();
        [Export] public int[] Triangles = Array.Empty<int>();
        [Export] public float[] BoneWeights = Array.Empty<float>();
        [Export] public int[] BoneIndices = Array.Empty<int>();
        [Export] public float[] Bindposes = Array.Empty<float>();

        // Submesh geometry & materials
        [Export] public int[] SubmeshTriangleCounts = Array.Empty<int>();
        [Export] public float[] SubmeshColors = Array.Empty<float>();

        // Material properties
        [Export] public Color BaseColor = Color.white;
        [Export] public byte[] TextureBytes = Array.Empty<byte>();

        // Bone hierarchy
        [Export] public string[] BoneNames = Array.Empty<string>();
        [Export] public int[] BoneParentIndices = Array.Empty<int>();
        [Export] public float[] BoneLocalPositions = Array.Empty<float>();
        [Export] public float[] BoneLocalRotations = Array.Empty<float>();

        // Humanoid mapping: indexed by HumanBodyBones, each entry an index into BoneNames or -1.
        [Export] public int[] HumanBoneMap = Array.Empty<int>();

        public bool HasBones => BoneNames != null && BoneNames.Length > 0;

        /// <summary>The stored humanoid mapping, or one computed from the bone names for older files.</summary>
        public int[] GetHumanBoneMap()
        {
            if (HumanBoneMap != null && HumanBoneMap.Length == (int)HumanBodyBones.LastBone)
                return HumanBoneMap;
            return HumanoidMappingHelper.Map(BoneNames, BoneParentIndices);
        }

        public int GetHumanBone(HumanBodyBones slot)
        {
            int[] map = GetHumanBoneMap();
            int i = (int)slot;
            return i >= 0 && i < map.Length ? map[i] : -1;
        }
        public int VertexCount => VertexPositions != null ? VertexPositions.Length / 3 : 0;

        /// <summary>
        /// Reconstructs a Unity Mesh from serialized geometry arrays.
        /// </summary>
        public Mesh BuildMesh()
        {
            var mesh = new Mesh
            {
                name = string.IsNullOrEmpty(AvatarName) ? "ChaviMesh" : $"{AvatarName}_Mesh"
            };

            int vCount = VertexCount;
            if (vCount == 0)
                return mesh;

            if (vCount > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            var vertices = new Vector3[vCount];
            for (int i = 0; i < vCount; i++)
            {
                int idx = i * 3;
                vertices[i] = new Vector3(VertexPositions[idx], VertexPositions[idx + 1], VertexPositions[idx + 2]);
            }
            mesh.vertices = vertices;

            if (Normals != null && Normals.Length == vCount * 3)
            {
                var normals = new Vector3[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    int idx = i * 3;
                    normals[i] = new Vector3(Normals[idx], Normals[idx + 1], Normals[idx + 2]);
                }
                mesh.normals = normals;
            }

            if (UVs != null && UVs.Length == vCount * 2)
            {
                var uvs = new Vector2[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    int idx = i * 2;
                    uvs[i] = new Vector2(UVs[idx], UVs[idx + 1]);
                }
                mesh.uv = uvs;
            }

            if (SubmeshTriangleCounts != null && SubmeshTriangleCounts.Length > 1)
            {
                mesh.subMeshCount = SubmeshTriangleCounts.Length;
                int triOffset = 0;
                for (int s = 0; s < SubmeshTriangleCounts.Length; s++)
                {
                    int count = SubmeshTriangleCounts[s];
                    if (triOffset + count <= Triangles.Length)
                    {
                        int[] subTris = new int[count];
                        Array.Copy(Triangles, triOffset, subTris, 0, count);
                        mesh.SetTriangles(subTris, s);
                        triOffset += count;
                    }
                }
            }
            else if (Triangles != null && Triangles.Length > 0)
            {
                mesh.triangles = Triangles;
            }

            if (HasBones && BoneWeights != null && BoneWeights.Length == vCount * 4 &&
                BoneIndices != null && BoneIndices.Length == vCount * 4)
            {
                var weights = new BoneWeight[vCount];
                for (int i = 0; i < vCount; i++)
                {
                    int wIdx = i * 4;
                    weights[i] = new BoneWeight
                    {
                        weight0 = BoneWeights[wIdx],
                        weight1 = BoneWeights[wIdx + 1],
                        weight2 = BoneWeights[wIdx + 2],
                        weight3 = BoneWeights[wIdx + 3],
                        boneIndex0 = BoneIndices[wIdx],
                        boneIndex1 = BoneIndices[wIdx + 1],
                        boneIndex2 = BoneIndices[wIdx + 2],
                        boneIndex3 = BoneIndices[wIdx + 3]
                    };
                }
                mesh.boneWeights = weights;

                if (Bindposes != null && Bindposes.Length == BoneNames.Length * 16)
                {
                    var bindposes = new Matrix4x4[BoneNames.Length];
                    for (int b = 0; b < BoneNames.Length; b++)
                    {
                        int mIdx = b * 16;
                        Matrix4x4 mat = default;
                        for (int r = 0; r < 4; r++)
                            for (int c = 0; c < 4; c++)
                                mat[r, c] = Bindposes[mIdx + r * 4 + c];
                        bindposes[b] = mat;
                    }
                    mesh.bindposes = bindposes;
                }
            }

            if (Normals == null || Normals.Length == 0)
                mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates an array of Materials configured for each submesh of the avatar.
        /// </summary>
        public Material[] BuildMaterials()
        {
            int matCount = (SubmeshColors != null && SubmeshColors.Length >= 4)
                ? SubmeshColors.Length / 4
                : 1;

            if (SubmeshTriangleCounts != null && SubmeshTriangleCounts.Length > matCount)
                matCount = SubmeshTriangleCounts.Length;

            var mats = new Material[matCount];
            for (int i = 0; i < matCount; i++)
            {
                Color c = BaseColor;
                if (SubmeshColors != null && (i + 1) * 4 <= SubmeshColors.Length)
                {
                    c = new Color(
                        SubmeshColors[i * 4 + 0],
                        SubmeshColors[i * 4 + 1],
                        SubmeshColors[i * 4 + 2],
                        SubmeshColors[i * 4 + 3]);
                }

                mats[i] = RuntimeMaterials.CreateLit(c, $"{AvatarName}_Mat_{i}");
            }

            if (TextureBytes != null && TextureBytes.Length > 0 && mats.Length > 0)
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(TextureBytes))
                {
                    foreach (var m in mats)
                        m.mainTexture = tex;
                }
            }

            return mats;
        }

        /// <summary>
        /// Creates a primary Material configured for the avatar.
        /// </summary>
        public Material BuildMaterial()
        {
            var mats = BuildMaterials();
            return mats != null && mats.Length > 0 ? mats[0] : RuntimeMaterials.CreateLit(BaseColor);
        }
    }
}
