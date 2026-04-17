using PaintCore;
using PaintIn3D;
using UnityEngine;

namespace Race.Tagging
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class GraffitiPaintSurface : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private CwPaintableMesh paintableMesh;
        [SerializeField] private CwMaterialCloner materialCloner;
        [SerializeField] private CwPaintableMeshTexture paintableTexture;
        [SerializeField] private string slotName = "_BaseMap";
        [SerializeField] private int materialIndex;

        private Texture2D generatedFallbackTexture;

        public CwPaintableMeshTexture PaintableTexture => paintableTexture;

        public static bool TryGetOrCreate(Renderer renderer, out GraffitiPaintSurface surface)
        {
            surface = null;
            if (renderer == null)
            {
                return false;
            }

            surface = renderer.GetComponent<GraffitiPaintSurface>();
            if (surface == null)
            {
                surface = renderer.gameObject.AddComponent<GraffitiPaintSurface>();
            }

            return surface.EnsureReady();
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private bool EnsureReady()
        {
            targetRenderer ??= GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                return false;
            }

            if (!TryResolvePaintSlot(out Material sourceMaterial, out Texture sourceTexture, out int width, out int height))
            {
                Debug.LogWarning($"Graffiti paint setup failed on '{name}' because no supported albedo texture slot was found.", this);
                return false;
            }

            Mesh targetMesh = ResolveMesh();
            if (targetMesh == null)
            {
                Debug.LogWarning($"Graffiti paint setup failed on '{name}' because no mesh was found to paint.", this);
                return false;
            }

            if (!targetMesh.isReadable)
            {
                Debug.LogWarning($"Graffiti paint setup failed on '{name}' because mesh '{targetMesh.name}' is not Read/Write enabled.", this);
                return false;
            }

            Texture paintSource = sourceTexture != null ? sourceTexture : GetOrCreateFallbackTexture(sourceMaterial, width, height);
            if (paintSource == null)
            {
                return false;
            }

            paintableMesh ??= GetComponent<CwPaintableMesh>();
            if (paintableMesh == null)
            {
                paintableMesh = gameObject.AddComponent<CwPaintableMesh>();
            }

            paintableMesh.MaterialApplication = CwPaintableMesh.MaterialApplicationType.ClonerAndTextures;

            materialCloner ??= GetComponent<CwMaterialCloner>();
            if (materialCloner == null)
            {
                materialCloner = gameObject.AddComponent<CwMaterialCloner>();
            }

            materialCloner.Index = materialIndex;

            paintableTexture ??= GetComponent<CwPaintableMeshTexture>();
            if (paintableTexture == null)
            {
                paintableTexture = gameObject.AddComponent<CwPaintableMeshTexture>();
            }

            paintableTexture.Slot = new CwSlot(materialIndex, slotName);
            paintableTexture.Texture = paintSource;
            paintableTexture.Color = Color.white;
            paintableTexture.Width = Mathf.Max(16, width);
            paintableTexture.Height = Mathf.Max(16, height);
            paintableTexture.Format = RenderTextureFormat.ARGB32;
            paintableTexture.Existing = CwPaintableTexture.ExistingType.UseAndKeep;
            paintableTexture.UndoRedo = CwPaintableTexture.UndoRedoType.None;
            paintableTexture.SaveLoad = CwPaintableTexture.SaveLoadType.Manual;
            paintableTexture.AutoDilate = false;

            if (!paintableMesh.IsActivated)
            {
                paintableMesh.Activate();
            }
            else
            {
                if (!materialCloner.Activated)
                {
                    materialCloner.Activate();
                }

                if (!paintableTexture.Activated)
                {
                    paintableTexture.Activate();
                }
            }

            // Re-apply the active paint texture after cloning so the visible material always points at the live painted surface.
            if (paintableTexture != null && paintableTexture.Activated && paintableTexture.Current != null)
            {
                paintableTexture.Current = paintableTexture.Current;
            }

            return paintableTexture != null && paintableTexture.Activated;
        }

        private bool TryResolvePaintSlot(out Material sourceMaterial, out Texture sourceTexture, out int width, out int height)
        {
            sourceMaterial = null;
            sourceTexture = null;
            width = 512;
            height = 512;

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return false;
            }

            materialIndex = Mathf.Clamp(materialIndex, 0, materials.Length - 1);
            Material material = materials[materialIndex];
            if (material == null)
            {
                return false;
            }

            sourceMaterial = material;

            string resolvedSlot = ResolveSlotName(material);
            if (string.IsNullOrEmpty(resolvedSlot))
            {
                return false;
            }

            slotName = resolvedSlot;
            sourceTexture = material.GetTexture(slotName);
            if (sourceTexture != null)
            {
                width = sourceTexture.width;
                height = sourceTexture.height;
            }

            return true;
        }

        private Texture2D GetOrCreateFallbackTexture(Material sourceMaterial, int width, int height)
        {
            if (generatedFallbackTexture != null)
            {
                return generatedFallbackTexture;
            }

            int safeWidth = Mathf.Max(16, width);
            int safeHeight = Mathf.Max(16, height);
            generatedFallbackTexture = new Texture2D(safeWidth, safeHeight, TextureFormat.RGBA32, false)
            {
                name = $"{name}_GraffitiBase",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color baseColor = ResolveBaseColor(sourceMaterial);
            Color32[] pixels = new Color32[safeWidth * safeHeight];
            Color32 color32 = baseColor;
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color32;
            }

            generatedFallbackTexture.SetPixels32(pixels);
            generatedFallbackTexture.Apply(false, false);
            return generatedFallbackTexture;
        }

        private static Color ResolveBaseColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static string ResolveSlotName(Material material)
        {
            if (material.HasProperty("_BaseMap"))
            {
                return "_BaseMap";
            }

            if (material.HasProperty("_MainTex"))
            {
                return "_MainTex";
            }

            return null;
        }

        private Mesh ResolveMesh()
        {
            if (TryGetComponent(out MeshFilter meshFilter))
            {
                return meshFilter.sharedMesh;
            }

            if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            return null;
        }
    }
}
