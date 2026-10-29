using UnityEngine;

namespace ChimeralTheater.Films
{
    /// <summary>
    /// Materials for meshes built at runtime (avatars, props, stand-ins). They are cloned from a template
    /// in Resources rather than made with Shader.Find, because a build only contains the shaders that
    /// some included asset references.
    /// </summary>
    public static class RuntimeMaterials
    {
        const string LitTemplatePath = "ChimeralTheater/Lit";
        static Material litTemplate;

        /// <summary>A new URP Lit material with the given base colour.</summary>
        public static Material CreateLit(Color color, string name = null)
        {
            if (litTemplate == null)
                litTemplate = Resources.Load<Material>(LitTemplatePath);

            Material material;
            if (litTemplate != null)
            {
                material = new Material(litTemplate);
            }
            else
            {
                Debug.LogError($"[RuntimeMaterials] Missing Resources/{LitTemplatePath}.mat; falling back to Shader.Find, which fails in builds.");
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            if (!string.IsNullOrEmpty(name))
                material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
