using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Race.Lighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ExponentialFogVolume : MonoBehaviour
    {
        [SerializeField] private FogVolumeProfile profile;
        [SerializeField] private MeshRenderer fogRenderer;
        [SerializeField] private bool autoApplyInEditor = true;

        [Header("Overrides")]
        [SerializeField] private bool overrideProfile;
        [SerializeField] private Color fogColor = new(0.72f, 0.8f, 0.9f, 0.7f);
        [SerializeField, Min(0f)] private float density = 0.025f;
        [SerializeField, Min(0.01f)] private float densityPower = 1.35f;
        [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.85f;
        [SerializeField, Min(0f)] private float heightFalloff = 0.03f;
        [SerializeField, Range(-0.5f, 0.5f)] private float heightOffset = -0.35f;
        [SerializeField, Range(0.001f, 1f)] private float edgeSoftness = 0.2f;
        [SerializeField, Min(0.01f)] private float noiseScale = 0.015f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.85f;
        [SerializeField, Min(0.01f)] private float detailNoiseScale = 0.06f;
        [SerializeField, Range(0f, 1f)] private float detailNoiseStrength = 0.55f;
        [SerializeField] private Vector2 windDirection = new(1f, 0.35f);
        [SerializeField, Min(0f)] private float windSpeed = 0.75f;

        private MaterialPropertyBlock propertyBlock;

        public FogVolumeProfile Profile => profile;
        public MeshRenderer FogRenderer => fogRenderer;

        private void OnEnable()
        {
            CacheReferences();
            ApplyAuthoring();
        }

        private void Reset()
        {
            CacheReferences();
            ApplyAuthoring();
        }

        private void OnValidate()
        {
            CacheReferences();

            if (Application.isPlaying || !autoApplyInEditor)
            {
                return;
            }

            ApplyAuthoring();
        }

        [ContextMenu("Apply Fog Authoring")]
        public void ApplyAuthoring()
        {
            CacheReferences();
            if (fogRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.Clear();

            if (overrideProfile || profile == null)
            {
                ApplyOverrides(propertyBlock);
            }
            else
            {
                profile.ApplyTo(propertyBlock);
            }

            fogRenderer.SetPropertyBlock(propertyBlock);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(fogRenderer);
#endif
        }

        public void SetProfile(FogVolumeProfile newProfile, bool applyImmediately = true)
        {
            profile = newProfile;
            if (applyImmediately)
            {
                ApplyAuthoring();
            }
        }

        private void CacheReferences()
        {
            if (fogRenderer == null)
            {
                fogRenderer = GetComponentInChildren<MeshRenderer>(true);
            }
        }

        private void ApplyOverrides(MaterialPropertyBlock targetPropertyBlock)
        {
            targetPropertyBlock.SetColor(FogVolumeShaderIds.BaseColor, fogColor);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.Density, density);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.DensityPower, densityPower);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.MaxOpacity, maxOpacity);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.HeightFalloff, heightFalloff);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.HeightOffset, heightOffset);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.EdgeSoftness, edgeSoftness);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.NoiseScale, noiseScale);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.NoiseStrength, noiseStrength);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.DetailNoiseScale, detailNoiseScale);
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.DetailNoiseStrength, detailNoiseStrength);
            targetPropertyBlock.SetVector(FogVolumeShaderIds.WindDirection, new Vector4(windDirection.x, windDirection.y, 0f, 0f));
            targetPropertyBlock.SetFloat(FogVolumeShaderIds.WindSpeed, windSpeed);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
#endif
    }
}
