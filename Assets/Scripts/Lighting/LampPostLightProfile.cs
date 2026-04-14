using UnityEngine;

namespace Race.Lighting
{
    [CreateAssetMenu(
        fileName = "LampPostLightProfile",
        menuName = "Race/Lighting/Lamp Post Light Profile")]
    public sealed class LampPostLightProfile : ScriptableObject
    {
        [Header("Light")]
        [SerializeField] private LightType lightType = LightType.Point;
        [SerializeField] private Color lightColor = new(1f, 0.8f, 0.58f, 1f);
        [SerializeField, Min(0f)] private float intensity = 6f;
        [SerializeField, Min(0.01f)] private float range = 10f;
        [SerializeField, Min(0f)] private float indirectMultiplier = 1f;
        [SerializeField] private LightmapBakeType bakeType = LightmapBakeType.Baked;
        [SerializeField] private LightShadows shadows = LightShadows.None;
        [SerializeField] private LightRenderMode renderMode = LightRenderMode.Auto;

        [Header("Spot Settings")]
        [SerializeField, Range(1f, 179f)] private float spotAngle = 55f;
        [SerializeField, Range(0f, 179f)] private float innerSpotAngle = 35f;

        public void ApplyTo(Light targetLight)
        {
            if (targetLight == null)
            {
                return;
            }

            targetLight.type = lightType;
            targetLight.color = lightColor;
            targetLight.intensity = intensity;
            targetLight.range = range;
            targetLight.shadows = shadows;
            targetLight.renderMode = renderMode;
            targetLight.bounceIntensity = indirectMultiplier;

#if UNITY_EDITOR
            targetLight.lightmapBakeType = bakeType;
#endif

            if (lightType == LightType.Spot)
            {
                targetLight.spotAngle = spotAngle;
                targetLight.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle);
            }
        }
    }
}
