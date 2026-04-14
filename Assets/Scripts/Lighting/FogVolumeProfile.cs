using UnityEngine;

namespace Race.Lighting
{
    [CreateAssetMenu(
        fileName = "FogVolumeProfile",
        menuName = "Race/Lighting/Fog Volume Profile")]
    public sealed class FogVolumeProfile : ScriptableObject
    {
        [Header("Color")]
        [SerializeField] private Color fogColor = new(0.72f, 0.8f, 0.9f, 0.7f);

        [Header("Density")]
        [SerializeField, Min(0f)] private float density = 0.025f;
        [SerializeField, Min(0.01f)] private float densityPower = 1.35f;
        [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.85f;

        [Header("Vertical Falloff")]
        [SerializeField, Min(0f)] private float heightFalloff = 0.03f;
        [SerializeField, Range(-0.5f, 0.5f)] private float heightOffset = -0.35f;

        [Header("Volume Shape")]
        [SerializeField, Range(0.001f, 1f)] private float edgeSoftness = 0.2f;

        [Header("Noise")]
        [SerializeField, Min(0.01f)] private float noiseScale = 0.015f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.85f;
        [SerializeField, Min(0.01f)] private float detailNoiseScale = 0.06f;
        [SerializeField, Range(0f, 1f)] private float detailNoiseStrength = 0.55f;
        [SerializeField] private Vector2 windDirection = new(1f, 0.35f);
        [SerializeField, Min(0f)] private float windSpeed = 0.75f;

        public Color FogColor => fogColor;
        public float Density => density;
        public float DensityPower => densityPower;
        public float MaxOpacity => maxOpacity;
        public float HeightFalloff => heightFalloff;
        public float HeightOffset => heightOffset;
        public float EdgeSoftness => edgeSoftness;
        public float NoiseScale => noiseScale;
        public float NoiseStrength => noiseStrength;
        public float DetailNoiseScale => detailNoiseScale;
        public float DetailNoiseStrength => detailNoiseStrength;
        public Vector2 WindDirection => windDirection;
        public float WindSpeed => windSpeed;

        public void ApplyTo(MaterialPropertyBlock propertyBlock)
        {
            if (propertyBlock == null)
            {
                return;
            }

            propertyBlock.SetColor(FogVolumeShaderIds.BaseColor, fogColor);
            propertyBlock.SetFloat(FogVolumeShaderIds.Density, density);
            propertyBlock.SetFloat(FogVolumeShaderIds.DensityPower, densityPower);
            propertyBlock.SetFloat(FogVolumeShaderIds.MaxOpacity, maxOpacity);
            propertyBlock.SetFloat(FogVolumeShaderIds.HeightFalloff, heightFalloff);
            propertyBlock.SetFloat(FogVolumeShaderIds.HeightOffset, heightOffset);
            propertyBlock.SetFloat(FogVolumeShaderIds.EdgeSoftness, edgeSoftness);
            propertyBlock.SetFloat(FogVolumeShaderIds.NoiseScale, noiseScale);
            propertyBlock.SetFloat(FogVolumeShaderIds.NoiseStrength, noiseStrength);
            propertyBlock.SetFloat(FogVolumeShaderIds.DetailNoiseScale, detailNoiseScale);
            propertyBlock.SetFloat(FogVolumeShaderIds.DetailNoiseStrength, detailNoiseStrength);
            propertyBlock.SetVector(FogVolumeShaderIds.WindDirection, new Vector4(windDirection.x, windDirection.y, 0f, 0f));
            propertyBlock.SetFloat(FogVolumeShaderIds.WindSpeed, windSpeed);
        }
    }
}
