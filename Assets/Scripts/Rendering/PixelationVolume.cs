using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Race.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Race/Rendering/Pixelation")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class PixelationVolume : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter effectEnabled = new(true);
        public ClampedFloatParameter blend = new(1f, 0f, 1f);
        public NoInterpClampedIntParameter targetVerticalResolution = new(320, 72, 1440);
        public NoInterpClampedIntParameter colorSteps = new(48, 2, 256);
        public ClampedFloatParameter ditherStrength = new(0.15f, 0f, 1f);

        public bool IsActive()
        {
            return active && effectEnabled.value && blend.value > 0f && targetVerticalResolution.value > 0;
        }
    }
}
