using System;
using ChimeralTheater.Films.Avatars;
using UniRes;
using UnityEngine;

namespace ChimeralTheater.Films.Props
{
    /// <summary>
    /// Self-contained UniRes resource for a physics prop converted from a 3D model (.glb, .gltf).
    /// Holds only the mesh and its materials; any skeleton in the source is dropped. Spawned props
    /// get a mesh collider and default Unity rigidbody physics.
    /// </summary>
    public class ChpropResource : Resource
    {
        [Export] public string PropName = "";
        [Export] public string SourcePath = "";
        [Export] public string SourceFormat = "";
        [Export] public string CreatedAt = "";

        // Mesh geometry
        [Export] public float[] VertexPositions = Array.Empty<float>();
        [Export] public float[] Normals = Array.Empty<float>();
        [Export] public float[] UVs = Array.Empty<float>();
        [Export] public int[] Triangles = Array.Empty<int>();

        // Submesh geometry & materials
        [Export] public int[] SubmeshTriangleCounts = Array.Empty<int>();
        [Export] public float[] SubmeshColors = Array.Empty<float>();

        // Material properties
        [Export] public Color BaseColor = Color.white;
        [Export] public byte[] TextureBytes = Array.Empty<byte>();

        public int VertexCount => VertexPositions != null ? VertexPositions.Length / 3 : 0;

        /// <summary>Copies the mesh and material data of a loaded model, leaving its bones behind.</summary>
        public static ChpropResource FromModel(ChaviResource model)
        {
            return new ChpropResource
            {
                PropName = model.AvatarName,
                SourcePath = model.SourcePath,
                SourceFormat = model.SourceFormat,
                CreatedAt = model.CreatedAt,
                VertexPositions = model.VertexPositions,
                Normals = model.Normals,
                UVs = model.UVs,
                Triangles = model.Triangles,
                SubmeshTriangleCounts = model.SubmeshTriangleCounts,
                SubmeshColors = model.SubmeshColors,
                BaseColor = model.BaseColor,
                TextureBytes = model.TextureBytes,
            };
        }

        /// <summary>Scales the mesh uniformly so its longest side is <paramref name="size"/> metres, and moves it so
        /// its bounds are centred on the prop's origin. Models often sit far from their own origin, which
        /// would make a prop appear away from where it was spawned and rotate around an off-centre pivot.</summary>
        public void FitToSize(float size)
        {
            if (VertexCount == 0 || size <= 0f)
                return;

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = -min;
            for (int i = 0; i < VertexPositions.Length; i += 3)
            {
                var v = new Vector3(VertexPositions[i], VertexPositions[i + 1], VertexPositions[i + 2]);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            Vector3 extent = max - min;
            float longest = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            if (longest <= 0f)
                return;

            float scale = size / longest;
            Vector3 center = (min + max) * 0.5f;
            var scaled = new float[VertexPositions.Length];
            for (int i = 0; i < scaled.Length; i += 3)
            {
                scaled[i] = (VertexPositions[i] - center.x) * scale;
                scaled[i + 1] = (VertexPositions[i + 1] - center.y) * scale;
                scaled[i + 2] = (VertexPositions[i + 2] - center.z) * scale;
            }
            VertexPositions = scaled;
        }

        public Mesh BuildMesh() => AsModel().BuildMesh();

        public Material[] BuildMaterials() => AsModel().BuildMaterials();

        /// <summary>A bone-less model view of this prop, so mesh and material building is shared with avatars.</summary>
        ChaviResource AsModel()
        {
            return new ChaviResource
            {
                AvatarName = PropName,
                VertexPositions = VertexPositions,
                Normals = Normals,
                UVs = UVs,
                Triangles = Triangles,
                SubmeshTriangleCounts = SubmeshTriangleCounts,
                SubmeshColors = SubmeshColors,
                BaseColor = BaseColor,
                TextureBytes = TextureBytes,
            };
        }
    }
}
