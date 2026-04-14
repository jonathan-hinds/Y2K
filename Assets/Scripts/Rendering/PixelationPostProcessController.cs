using UnityEngine;
using UnityEngine.Rendering;

namespace Race.Rendering
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class PixelationPostProcessController : MonoBehaviour
    {
        [SerializeField] private Material effectMaterial;
        [SerializeField] private bool applyInEditMode = true;

        private PixelationSettingsSnapshot appliedSettings;

        public Material EffectMaterial => effectMaterial;

        private void OnEnable()
        {
            ApplyCurrentSettings(force: true);
        }

        private void OnDisable()
        {
            ApplySettings(PixelationSettingsSnapshot.Disabled, force: true);
        }

        private void OnValidate()
        {
            ApplyCurrentSettings(force: true);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying && !applyInEditMode)
            {
                return;
            }

            ApplyCurrentSettings(force: false);
        }

        [ContextMenu("Apply Pixelation Settings")]
        public void ApplyImmediate()
        {
            ApplyCurrentSettings(force: true);
        }

        public void SetEffectMaterial(Material material, bool applyImmediately = true)
        {
            effectMaterial = material;
            if (applyImmediately)
            {
                ApplyCurrentSettings(force: true);
            }
        }

        private void ApplyCurrentSettings(bool force)
        {
            PixelationVolume volume = VolumeManager.instance?.stack?.GetComponent<PixelationVolume>();
            PixelationSettingsSnapshot nextSettings = PixelationSettingsSnapshot.FromVolume(volume);
            ApplySettings(nextSettings, force);
        }

        private void ApplySettings(PixelationSettingsSnapshot nextSettings, bool force)
        {
            if (effectMaterial == null)
            {
                appliedSettings = nextSettings;
                return;
            }

            if (!force && appliedSettings.Equals(nextSettings))
            {
                return;
            }

            effectMaterial.SetFloat(PixelationShaderIds.Blend, nextSettings.Blend);
            effectMaterial.SetFloat(PixelationShaderIds.VerticalResolution, nextSettings.TargetVerticalResolution);
            effectMaterial.SetFloat(PixelationShaderIds.ColorSteps, nextSettings.ColorSteps);
            effectMaterial.SetFloat(PixelationShaderIds.DitherStrength, nextSettings.DitherStrength);

            appliedSettings = nextSettings;
        }

        private readonly struct PixelationSettingsSnapshot
        {
            public static readonly PixelationSettingsSnapshot Disabled = new(0f, 320, 48, 0f);

            public PixelationSettingsSnapshot(float blend, int targetVerticalResolution, int colorSteps, float ditherStrength)
            {
                Blend = blend;
                TargetVerticalResolution = targetVerticalResolution;
                ColorSteps = colorSteps;
                DitherStrength = ditherStrength;
            }

            public float Blend { get; }
            public int TargetVerticalResolution { get; }
            public int ColorSteps { get; }
            public float DitherStrength { get; }

            public static PixelationSettingsSnapshot FromVolume(PixelationVolume volume)
            {
                if (volume == null || !volume.IsActive())
                {
                    return Disabled;
                }

                return new PixelationSettingsSnapshot(
                    volume.blend.value,
                    volume.targetVerticalResolution.value,
                    volume.colorSteps.value,
                    volume.ditherStrength.value);
            }
        }
    }
}
