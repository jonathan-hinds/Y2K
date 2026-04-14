using UnityEngine;

namespace Race.Lighting
{
    [CreateAssetMenu(
        fileName = "BuiltInFogProfile",
        menuName = "Race/Lighting/Built-In Fog Profile")]
    public sealed class BuiltInFogProfile : ScriptableObject
    {
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
        [SerializeField] private Color fogColor = new(0.67f, 0.76f, 0.84f, 1f);
        [SerializeField, Min(0f)] private float fogDensity = 0.01f;
        [SerializeField, Min(0f)] private float linearStart = 0f;
        [SerializeField, Min(0f)] private float linearEnd = 300f;

        public bool FogEnabled => fogEnabled;
        public FogMode FogMode => fogMode;
        public Color FogColor => fogColor;
        public float FogDensity => fogDensity;
        public float LinearStart => linearStart;
        public float LinearEnd => linearEnd;

        public void Apply()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = linearStart;
            RenderSettings.fogEndDistance = Mathf.Max(linearEnd, linearStart);
        }
    }
}
