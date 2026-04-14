Shader "Race/Rendering/Pixelation Post Process"
{
    Properties
    {
        _Blend ("Blend", Range(0, 1)) = 1
        _VerticalResolution ("Vertical Resolution", Float) = 320
        _ColorSteps ("Color Steps", Float) = 48
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "PixelationPostProcess"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Blend;
            float _VerticalResolution;
            float _ColorSteps;
            float _DitherStrength;

            float GetBayer4(float2 pixelCoord)
            {
                float2 cell = fmod(pixelCoord, 4.0);

                if (cell.y < 1.0)
                {
                    if (cell.x < 1.0) return 0.0 / 16.0;
                    if (cell.x < 2.0) return 8.0 / 16.0;
                    if (cell.x < 3.0) return 2.0 / 16.0;
                    return 10.0 / 16.0;
                }

                if (cell.y < 2.0)
                {
                    if (cell.x < 1.0) return 12.0 / 16.0;
                    if (cell.x < 2.0) return 4.0 / 16.0;
                    if (cell.x < 3.0) return 14.0 / 16.0;
                    return 6.0 / 16.0;
                }

                if (cell.y < 3.0)
                {
                    if (cell.x < 1.0) return 3.0 / 16.0;
                    if (cell.x < 2.0) return 11.0 / 16.0;
                    if (cell.x < 3.0) return 1.0 / 16.0;
                    return 9.0 / 16.0;
                }

                if (cell.x < 1.0) return 15.0 / 16.0;
                if (cell.x < 2.0) return 7.0 / 16.0;
                if (cell.x < 3.0) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            half3 PosterizeColor(half3 color, float2 pixelCoord)
            {
                float steps = max(2.0, _ColorSteps);
                float levels = steps - 1.0;
                float dither = (GetBayer4(pixelCoord) - 0.5) * (_DitherStrength / steps);

                return floor(saturate(color + dither) * levels + 0.5) / levels;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float2 screenSize = _ScreenParams.xy;
                float verticalResolution = max(1.0, _VerticalResolution);
                float horizontalResolution = max(1.0, round(verticalResolution * (screenSize.x / screenSize.y)));
                float2 pixelResolution = float2(horizontalResolution, verticalResolution);

                float2 pixelCoord = floor(uv * pixelResolution);
                float2 snappedUv = (pixelCoord + 0.5) / pixelResolution;

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 pixelated = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv);
                pixelated.rgb = PosterizeColor(pixelated.rgb, pixelCoord);

                return half4(lerp(original.rgb, pixelated.rgb, saturate(_Blend)), original.a);
            }
            ENDHLSL
        }
    }
}
