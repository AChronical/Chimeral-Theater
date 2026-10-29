using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace ChimeralTheater.Films.Avatars
{
    /// <summary>
    /// Loads .glb / .gltf models with glTFast and flattens the result into a <see cref="ChaviResource"/>:
    /// one mesh (a submesh per material), per-submesh base colours, the first base colour texture as PNG
    /// and, for skinned models, the skeleton in its bind pose.
    ///
    /// glTFast does the parsing, decompression (Draco, meshopt, KTX) and the conversion into Unity's
    /// coordinate space; this class only reads the Unity objects it creates.
    /// </summary>
    public static class GltfModelLoader
    {
        /// <param name="bakeNodeTransforms">Apply each mesh node's world transform (including parent scale,
        /// e.g. a centimetre-to-metre 0.01) to its vertices. Props need this; avatars keep raw bind-pose
        /// vertices because their skeleton positions them.</param>
        public static async Task<ChaviResource> LoadAsync(string filePath, bool bakeNodeTransforms = false)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Model file not found: {filePath}");

            var settings = new ImportSettings
            {
                AnimationMethod = AnimationMethod.None,
                GenerateMipMaps = false,
                TexturesReadable = true
            };

            // In play mode glTFast spreads the work over frames to keep the headset smooth; that helper
            // needs DontDestroyOnLoad, so the editor loads in one go.
            using var gltf = new GltfImport(deferAgent: Application.isPlaying ? null : new UninterruptedDeferAgent());
            if (!await gltf.LoadFile(filePath, null, settings))
                throw new InvalidDataException("glTFast could not load the file (see the console for details).");

            var container = new GameObject("GltfImport");
            try
            {
                if (!await gltf.InstantiateMainSceneAsync(container.transform))
                    throw new InvalidDataException("The file has no scene to instantiate.");

                var chavi = new ChaviResource
                {
                    AvatarName = Path.GetFileNameWithoutExtension(filePath),
                    SourcePath = filePath,
                    SourceFormat = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };

                var builder = new MeshBuilder(gltf);
                SkinnedMeshRenderer[] skinned = container.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                if (skinned.Length > 0 && !bakeNodeTransforms)
                {
                    builder.AddSkinned(skinned);
                    builder.WriteSkeleton(chavi);
                }
                else
                {
                    Matrix4x4 toContainer = container.transform.worldToLocalMatrix;
                    foreach (MeshFilter filter in container.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        if (filter.sharedMesh != null)
                            builder.AddStatic(filter.sharedMesh, renderer != null ? renderer.sharedMaterials : null,
                                toContainer * filter.transform.localToWorldMatrix);
                    }

                    foreach (SkinnedMeshRenderer smr in skinned)
                    {
                        if (smr.sharedMesh == null)
                            continue;
                        var posed = new Mesh();
                        smr.BakeMesh(posed, true);
                        builder.AddStatic(posed, smr.sharedMaterials,
                            toContainer * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one));
                        UnityEngine.Object.DestroyImmediate(posed);
                    }
                }

                if (builder.VertexCount == 0 || builder.TriangleCount == 0)
                    throw new InvalidDataException("glTF contains no triangle geometry.");

                builder.WriteMesh(chavi);
                return chavi;
            }
            finally
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(container);
                else
                    UnityEngine.Object.DestroyImmediate(container);
            }
        }

        /// <summary>Accumulates meshes into the flat arrays a <see cref="ChaviResource"/> stores.</summary>
        sealed class MeshBuilder
        {
            readonly GltfImport gltf;
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> triangles = new List<int>();
            readonly List<float> weights = new List<float>();
            readonly List<int> joints = new List<int>();
            readonly List<int> submeshCounts = new List<int>();
            readonly List<Color> submeshColors = new List<Color>();
            readonly List<Material> submeshMaterials = new List<Material>();

            // Skeleton: the union of every skinned renderer's bones, with bind poses in the reference space.
            readonly List<Transform> bones = new List<Transform>();
            readonly Dictionary<Transform, int> boneIndex = new Dictionary<Transform, int>();
            readonly List<Matrix4x4> bindposes = new List<Matrix4x4>();
            bool allNormals = true;

            public MeshBuilder(GltfImport gltf) => this.gltf = gltf;

            public int VertexCount => positions.Count;
            public int TriangleCount => triangles.Count / 3;

            /// <summary>Adds an unskinned mesh, transformed by <paramref name="toSpace"/>.</summary>
            public void AddStatic(Mesh mesh, Material[] materials, Matrix4x4 toSpace)
            {
                int offset = Append(mesh, materials, toSpace);
                for (int i = offset; i < positions.Count; i++)
                {
                    weights.Add(1f); weights.Add(0f); weights.Add(0f); weights.Add(0f);
                    joints.Add(0); joints.Add(0); joints.Add(0); joints.Add(0);
                }
            }

            /// <summary>
            /// Adds skinned meshes. Unity skins with bone.localToWorld * bindpose, so the vertices live in the
            /// space the bind poses map from, whatever the renderer's own transform is; renderers sharing a
            /// glTF skin share that space.
            /// </summary>
            public void AddSkinned(SkinnedMeshRenderer[] renderers)
            {
                // Collect the union of all bones first, parents before children.
                var found = new List<(Transform bone, Matrix4x4 bindpose)>();
                var seen = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    if (smr.sharedMesh == null)
                        continue;
                    Matrix4x4[] smrBindposes = smr.sharedMesh.bindposes;
                    Transform[] smrBones = smr.bones;
                    for (int b = 0; b < smrBones.Length; b++)
                        if (smrBones[b] != null && seen.Add(smrBones[b]))
                            found.Add((smrBones[b], b < smrBindposes.Length ? smrBindposes[b] : Matrix4x4.identity));
                }

                int Depth(Transform t)
                {
                    int d = 0;
                    for (Transform p = t.parent; p != null; p = p.parent)
                        d++;
                    return d;
                }
                var ordered = new List<int>();
                for (int i = 0; i < found.Count; i++)
                    ordered.Add(i);
                ordered.Sort((a, b) =>
                {
                    int byDepth = Depth(found[a].bone).CompareTo(Depth(found[b].bone));
                    return byDepth != 0 ? byDepth : a.CompareTo(b);
                });
                foreach (int i in ordered)
                {
                    boneIndex[found[i].bone] = bones.Count;
                    bones.Add(found[i].bone);
                    bindposes.Add(found[i].bindpose);
                }

                foreach (SkinnedMeshRenderer smr in renderers)
                {
                    Mesh mesh = smr.sharedMesh;
                    if (mesh == null)
                        continue;

                    Transform[] smrBones = smr.bones;
                    var remap = new int[smrBones.Length];
                    for (int b = 0; b < smrBones.Length; b++)
                        remap[b] = smrBones[b] != null ? boneIndex[smrBones[b]] : 0;

                    Append(mesh, smr.sharedMaterials, Matrix4x4.identity);

                    BoneWeight[] boneWeights = mesh.boneWeights;
                    for (int v = 0; v < mesh.vertexCount; v++)
                    {
                        if (v < boneWeights.Length)
                        {
                            BoneWeight w = boneWeights[v];
                            float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                            float inv = sum > 1e-6f ? 1f / sum : 0f;
                            weights.Add(sum > 1e-6f ? w.weight0 * inv : 1f);
                            weights.Add(w.weight1 * inv);
                            weights.Add(w.weight2 * inv);
                            weights.Add(w.weight3 * inv);
                            joints.Add(Remap(remap, w.boneIndex0));
                            joints.Add(Remap(remap, w.boneIndex1));
                            joints.Add(Remap(remap, w.boneIndex2));
                            joints.Add(Remap(remap, w.boneIndex3));
                        }
                        else
                        {
                            weights.Add(1f); weights.Add(0f); weights.Add(0f); weights.Add(0f);
                            joints.Add(0); joints.Add(0); joints.Add(0); joints.Add(0);
                        }
                    }
                }
            }

            static int Remap(int[] remap, int slot) => slot >= 0 && slot < remap.Length ? remap[slot] : 0;

            int Append(Mesh mesh, Material[] materials, Matrix4x4 toSpace)
            {
                int offset = positions.Count;
                Matrix4x4 normalMatrix = toSpace.inverse.transpose;
                bool flips = toSpace.determinant < 0f;

                var meshPositions = mesh.vertices;
                var meshNormals = mesh.normals;
                var meshUVs = mesh.uv;

                foreach (Vector3 p in meshPositions)
                    positions.Add(toSpace.MultiplyPoint3x4(p));

                if (meshNormals != null && meshNormals.Length == meshPositions.Length)
                    foreach (Vector3 n in meshNormals)
                        normals.Add(normalMatrix.MultiplyVector(n).normalized);
                else
                    allNormals = false;

                for (int i = 0; i < meshPositions.Length; i++)
                    uvs.Add(meshUVs != null && i < meshUVs.Length ? meshUVs[i] : Vector2.zero);

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles)
                        continue;

                    int[] tris = mesh.GetTriangles(s);
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        triangles.Add(tris[t] + offset);
                        triangles.Add(tris[t + (flips ? 2 : 1)] + offset);
                        triangles.Add(tris[t + (flips ? 1 : 2)] + offset);
                    }
                    submeshCounts.Add(tris.Length / 3 * 3);

                    Material material = materials != null && s < materials.Length ? materials[s] : null;
                    submeshMaterials.Add(material);
                    submeshColors.Add(BaseColor(material));
                }

                return offset;
            }

            /// <summary>The glTF base colour factor of the material glTFast created, read from the source
            /// glTF so it does not depend on which shader glTFast picked.</summary>
            Color BaseColor(Material material)
            {
                var pbr = SourceMaterial(material)?.PbrMetallicRoughness;
                return pbr != null ? pbr.BaseColor : Color.white;
            }

            GLTFast.Schema.MaterialBase SourceMaterial(Material material)
            {
                if (material == null)
                    return null;
                var sources = gltf.GetSourceRoot()?.Materials;
                for (int i = 0; sources != null && i < gltf.MaterialCount && i < sources.Count; i++)
                    if (gltf.GetMaterial(i) == material)
                        return sources[i];
                return null;
            }

            byte[] FirstBaseColorTexture()
            {
                foreach (Material material in submeshMaterials)
                {
                    var info = SourceMaterial(material)?.PbrMetallicRoughness?.BaseColorTexture;
                    if (info == null || info.index < 0 || info.index >= gltf.TextureCount)
                        continue;

                    Texture2D texture = gltf.GetTexture(info.index);
                    if (texture == null || !texture.isReadable)
                        continue;
                    try
                    {
                        return texture.EncodeToPNG();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GltfModelLoader] Could not re-encode texture '{texture.name}': {ex.Message}");
                    }
                }
                return null;
            }

            public void WriteMesh(ChaviResource chavi)
            {
                chavi.VertexPositions = Flatten(positions);
                chavi.Normals = allNormals ? Flatten(normals) : Array.Empty<float>();
                chavi.UVs = new float[uvs.Count * 2];
                for (int i = 0; i < uvs.Count; i++)
                {
                    chavi.UVs[i * 2] = uvs[i].x;
                    chavi.UVs[i * 2 + 1] = uvs[i].y;
                }
                chavi.Triangles = triangles.ToArray();
                chavi.BoneWeights = weights.ToArray();
                chavi.BoneIndices = joints.ToArray();
                chavi.SubmeshTriangleCounts = submeshCounts.ToArray();
                chavi.SubmeshColors = new float[submeshColors.Count * 4];
                for (int i = 0; i < submeshColors.Count; i++)
                {
                    Color c = submeshColors[i];
                    chavi.SubmeshColors[i * 4] = c.r;
                    chavi.SubmeshColors[i * 4 + 1] = c.g;
                    chavi.SubmeshColors[i * 4 + 2] = c.b;
                    chavi.SubmeshColors[i * 4 + 3] = c.a;
                }
                chavi.BaseColor = submeshColors.Count > 0 ? submeshColors[0] : Color.white;
                chavi.TextureBytes = FirstBaseColorTexture() ?? Array.Empty<byte>();
            }

            /// <summary>
            /// Writes the bone hierarchy. The rest pose comes from the inverse bind poses rather than the
            /// node transforms, because exporters such as Mesh2Motion save the skeleton's current pose,
            /// which need not be the pose the mesh was bound in.
            /// </summary>
            public void WriteSkeleton(ChaviResource chavi)
            {
                int count = bones.Count;
                chavi.BoneNames = new string[count];
                chavi.BoneParentIndices = new int[count];
                chavi.BoneLocalPositions = new float[count * 3];
                chavi.BoneLocalRotations = new float[count * 4];
                chavi.Bindposes = new float[count * 16];

                var bindWorld = new Matrix4x4[count];
                for (int b = 0; b < count; b++)
                    bindWorld[b] = bindposes[b].inverse;

                for (int b = 0; b < count; b++)
                {
                    chavi.BoneNames[b] = string.IsNullOrEmpty(bones[b].name) ? $"Joint_{b}" : bones[b].name;

                    // The nearest ancestor that is also a bone; other nodes in between are folded away.
                    int parent = -1;
                    for (Transform p = bones[b].parent; p != null; p = p.parent)
                        if (boneIndex.TryGetValue(p, out parent))
                            break;
                        else
                            parent = -1;
                    chavi.BoneParentIndices[b] = parent;

                    Matrix4x4 local = parent >= 0 ? bindWorld[parent].inverse * bindWorld[b] : bindWorld[b];
                    Vector3 t = local.GetColumn(3);
                    Quaternion r = local.rotation;
                    chavi.BoneLocalPositions[b * 3] = t.x;
                    chavi.BoneLocalPositions[b * 3 + 1] = t.y;
                    chavi.BoneLocalPositions[b * 3 + 2] = t.z;
                    chavi.BoneLocalRotations[b * 4] = r.x;
                    chavi.BoneLocalRotations[b * 4 + 1] = r.y;
                    chavi.BoneLocalRotations[b * 4 + 2] = r.z;
                    chavi.BoneLocalRotations[b * 4 + 3] = r.w;

                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            chavi.Bindposes[b * 16 + row * 4 + col] = bindposes[b][row, col];
                }

                chavi.HumanBoneMap = HumanoidMappingHelper.Map(chavi.BoneNames, chavi.BoneParentIndices);
            }

            static float[] Flatten(List<Vector3> list)
            {
                var result = new float[list.Count * 3];
                for (int i = 0; i < list.Count; i++)
                {
                    result[i * 3] = list[i].x;
                    result[i * 3 + 1] = list[i].y;
                    result[i * 3 + 2] = list[i].z;
                }
                return result;
            }
        }
    }
}
