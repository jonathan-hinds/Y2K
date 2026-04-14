using UnityEngine;

namespace Race.Lighting
{
    internal static class FogVolumeShaderIds
    {
        internal static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        internal static readonly int Density = Shader.PropertyToID("_Density");
        internal static readonly int DensityPower = Shader.PropertyToID("_DensityPower");
        internal static readonly int HeightFalloff = Shader.PropertyToID("_HeightFalloff");
        internal static readonly int HeightOffset = Shader.PropertyToID("_HeightOffset");
        internal static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        internal static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        internal static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");
        internal static readonly int DetailNoiseScale = Shader.PropertyToID("_DetailNoiseScale");
        internal static readonly int DetailNoiseStrength = Shader.PropertyToID("_DetailNoiseStrength");
        internal static readonly int WindDirection = Shader.PropertyToID("_WindDirection");
        internal static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
        internal static readonly int MaxOpacity = Shader.PropertyToID("_MaxOpacity");
    }
}
