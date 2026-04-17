Shader "Race/Tagging/GraffitiTag"
{
    Properties
    {
        _BaseMap("Tag Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _Reveal("Reveal", Range(0,1)) = 0
        _NoiseScale("Noise Scale", Float) = 1.35
        _EdgeSharpness("Edge Sharpness", Range(0.001,0.25)) = 0.065
        _PixelGrid("Pixel Grid", Float) = 72
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _Reveal;
                float _NoiseScale;
                float _EdgeSharpness;
                float _PixelGrid;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float grid = max(1.0, _PixelGrid);
                float2 pixelatedUv = (floor(input.uv * grid) + 0.5) / grid;
                half4 tagSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, pixelatedUv);
                if (tagSample.a <= 0.001h)
                {
                    discard;
                }

                float noiseCellScale = max(1.0, _NoiseScale * 22.0);
                float2 noiseCoord = floor(pixelatedUv * noiseCellScale) + floor(input.positionWS.xy * 4.0);
                float revealNoise = Hash21(noiseCoord);
                float revealMask = smoothstep(
                    revealNoise - _EdgeSharpness,
                    revealNoise + _EdgeSharpness,
                    saturate(_Reveal));
                float alpha = tagSample.a * revealMask * _Tint.a;
                if (alpha <= 0.001)
                {
                    discard;
                }

                return half4(tagSample.rgb * _Tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
