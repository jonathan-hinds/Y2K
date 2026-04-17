using PaintCore;
using PaintIn3D;
using UnityEngine;

namespace Race.Tagging
{
    [RequireComponent(typeof(Renderer))]
    public sealed class GraffitiPaintSurface : MonoBehaviour
    {
        private const int MinimumPaintTextureSize = 16;

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private CwPaintableMesh paintableMesh;
        [SerializeField] private CwMaterialCloner materialCloner;
        [SerializeField] private CwPaintableMeshTexture paintableTexture;
        [SerializeField] private string slotName = "_BaseMap";
        [SerializeField] private int materialIndex;

        private Texture2D generatedFallbackTexture;
        private RenderTexture generatedTiledSourceTexture;
        private Texture cachedSourceTexture;

        public CwPaintableMeshTexture PaintableTexture => paintableTexture;

        public static bool TryGetOrCreate(Renderer renderer, int materialIndex, out GraffitiPaintSurface surface)
        {
            surface = null;
            if (renderer == null)
            {
                return false;
            }

            GraffitiPaintSurface[] surfaces = renderer.GetComponents<GraffitiPaintSurface>();
            for (int index = 0; index < surfaces.Length; index++)
            {
                GraffitiPaintSurface candidate = surfaces[index];
                if (candidate != null && candidate.materialIndex == materialIndex)
                {
                    surface = candidate;
                    break;
                }
            }

            if (surface == null)
            {
                surface = renderer.gameObject.AddComponent<GraffitiPaintSurface>();
                surface.materialIndex = materialIndex;
            }

            return surface.EnsureReady();
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private void OnDestroy()
        {
            if (generatedTiledSourceTexture != null)
            {
                generatedTiledSourceTexture.Release();
                Destroy(generatedTiledSourceTexture);
                generatedTiledSourceTexture = null;
            }

            if (generatedFallbackTexture != null)
            {
                Destroy(generatedFallbackTexture);
                generatedFallbackTexture = null;
            }
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

            Texture paintSource = ResolvePaintSource(sourceMaterial, sourceTexture, width, height);
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

            ResetComponentBindings();
            if (materialCloner == null)
            {
                materialCloner = gameObject.AddComponent<CwMaterialCloner>();
                materialCloner.Index = materialIndex;
            }

            materialCloner.Index = materialIndex;

            paintableTexture = FindPaintableTexture(materialIndex, slotName);
            if (paintableTexture == null)
            {
                paintableTexture = gameObject.AddComponent<CwPaintableMeshTexture>();
                paintableTexture.Slot = new CwSlot(materialIndex, slotName);
            }

            paintableTexture.Slot = new CwSlot(materialIndex, slotName);
            paintableTexture.Texture = paintSource;
            paintableTexture.Color = Color.white;
            paintableTexture.Width = ResolvePaintTextureDimension(width, GetTextureWidthMultiplier());
            paintableTexture.Height = ResolvePaintTextureDimension(height, GetTextureHeightMultiplier());
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

        private void ResetComponentBindings()
        {
            materialCloner = FindMaterialCloner(materialIndex);
            paintableTexture = FindPaintableTexture(materialIndex, slotName);
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

        private CwMaterialCloner FindMaterialCloner(int targetMaterialIndex)
        {
            CwMaterialCloner[] cloners = GetComponents<CwMaterialCloner>();
            for (int index = 0; index < cloners.Length; index++)
            {
                CwMaterialCloner cloner = cloners[index];
                if (cloner != null && cloner.Index == targetMaterialIndex)
                {
                    return cloner;
                }
            }

            return null;
        }

        private CwPaintableMeshTexture FindPaintableTexture(int targetMaterialIndex, string targetSlotName)
        {
            CwPaintableMeshTexture[] textures = GetComponents<CwPaintableMeshTexture>();
            for (int index = 0; index < textures.Length; index++)
            {
                CwPaintableMeshTexture texture = textures[index];
                if (texture == null)
                {
                    continue;
                }

                if (texture.Slot.Index == targetMaterialIndex && texture.Slot.Name == targetSlotName)
                {
                    return texture;
                }
            }

            return null;
        }

        private Texture ResolvePaintSource(Material sourceMaterial, Texture sourceTexture, int width, int height)
        {
            GraffitiPaintTargetSettings settings = GetComponent<GraffitiPaintTargetSettings>();
            if (sourceTexture != null && settings != null && settings.HasTextureTilingOverrides)
            {
                Texture tiledSource = GetOrCreateTiledSourceTexture(sourceTexture, width, height, settings);
                if (tiledSource != null)
                {
                    return tiledSource;
                }
            }

            return sourceTexture != null ? sourceTexture : GetOrCreateFallbackTexture(sourceMaterial, width, height);
        }

        private Texture GetOrCreateTiledSourceTexture(
            Texture sourceTexture,
            int baseWidth,
            int baseHeight,
            GraffitiPaintTargetSettings settings)
        {
            int targetWidth = ResolvePaintTextureDimension(baseWidth, settings.SourceTextureWidthMultiplier);
            int targetHeight = ResolvePaintTextureDimension(baseHeight, settings.SourceTextureHeightMultiplier);
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return null;
            }

            bool needsRebuild = generatedTiledSourceTexture == null
                || generatedTiledSourceTexture.width != targetWidth
                || generatedTiledSourceTexture.height != targetHeight
                || cachedSourceTexture != sourceTexture;
            if (!needsRebuild)
            {
                return generatedTiledSourceTexture;
            }

            if (generatedTiledSourceTexture != null)
            {
                generatedTiledSourceTexture.Release();
                Destroy(generatedTiledSourceTexture);
            }

            generatedTiledSourceTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_GraffitiSource",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = sourceTexture.filterMode
            };
            generatedTiledSourceTexture.Create();

            Graphics.Blit(
                sourceTexture,
                generatedTiledSourceTexture,
                new Vector2(settings.SourceTextureWidthMultiplier, settings.SourceTextureHeightMultiplier),
                Vector2.zero);

            cachedSourceTexture = sourceTexture;
            return generatedTiledSourceTexture;
        }

        private Texture2D GetOrCreateFallbackTexture(Material sourceMaterial, int width, int height)
        {
            if (generatedFallbackTexture != null)
            {
                return generatedFallbackTexture;
            }

            int safeWidth = Mathf.Max(MinimumPaintTextureSize, width);
            int safeHeight = Mathf.Max(MinimumPaintTextureSize, height);
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

        private int GetTextureWidthMultiplier()
        {
            GraffitiPaintTargetSettings settings = GetComponent<GraffitiPaintTargetSettings>();
            return settings != null ? settings.SourceTextureWidthMultiplier : 1;
        }

        private int GetTextureHeightMultiplier()
        {
            GraffitiPaintTargetSettings settings = GetComponent<GraffitiPaintTargetSettings>();
            return settings != null ? settings.SourceTextureHeightMultiplier : 1;
        }

        private static int ResolvePaintTextureDimension(int baseSize, int multiplier)
        {
            int scaledSize = Mathf.Max(MinimumPaintTextureSize, baseSize) * Mathf.Max(1, multiplier);
            return Mathf.Clamp(scaledSize, MinimumPaintTextureSize, SystemInfo.maxTextureSize);
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
