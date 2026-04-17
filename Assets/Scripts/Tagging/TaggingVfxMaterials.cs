using UnityEngine;

namespace Race.Tagging
{
    public static class TaggingVfxMaterials
    {
        private static Material sprayMaterial;
        private static Material cloudMaterial;

        public static Material GetSprayMaterial()
        {
            sprayMaterial ??= CreateMaterial("Tagging/Spray", new Color(1f, 1f, 1f, 1f));
            return sprayMaterial;
        }

        public static Material GetCloudMaterial()
        {
            cloudMaterial ??= CreateMaterial("Tagging/Cloud", new Color(1f, 1f, 1f, 1f));
            return cloudMaterial;
        }

        private static Material CreateMaterial(string name, Color baseColor)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            shader ??= Shader.Find("Particles/Standard Unlit");
            shader ??= Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            Material material = new(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            return material;
        }
    }
}
