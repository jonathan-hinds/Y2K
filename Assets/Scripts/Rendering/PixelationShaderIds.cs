using UnityEngine;

namespace Race.Rendering
{
    public static class PixelationShaderIds
    {
        public static readonly int Blend = Shader.PropertyToID("_Blend");
        public static readonly int VerticalResolution = Shader.PropertyToID("_VerticalResolution");
        public static readonly int ColorSteps = Shader.PropertyToID("_ColorSteps");
        public static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
    }
}
