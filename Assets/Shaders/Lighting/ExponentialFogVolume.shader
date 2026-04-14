Shader "Race/Lighting/ExponentialFogVolume"
{
    Properties
    {
        [MainColor] _BaseColor("Fog Color", Color) = (0.72, 0.8, 0.9, 0.7)
        _Density("Density", Float) = 0.025
        _DensityPower("Density Power", Float) = 1.35
        _MaxOpacity("Max Opacity", Range(0, 1)) = 0.85
        _HeightFalloff("Height Falloff", Float) = 0.03
        _HeightOffset("Height Offset", Float) = -0.35
        _EdgeSoftness("Edge Softness", Range(0.001, 1)) = 0.2
        _NoiseScale("Noise Scale", Float) = 0.015
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.85
        _DetailNoiseScale("Detail Noise Scale", Float) = 0.06
        _DetailNoiseStrength("Detail Noise Strength", Range(0, 1)) = 0.55
        _WindDirection("Wind Direction", Vector) = (1, 0.35, 0, 0)
        _WindSpeed("Wind Speed", Float) = 0.75
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            Name "ForwardFogVolume"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 cameraOS : TEXCOORD2;
                float3 cameraWS : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Density;
                float _DensityPower;
                float _MaxOpacity;
                float _HeightFalloff;
                float _HeightOffset;
                float _EdgeSoftness;
                float _NoiseScale;
                float _NoiseStrength;
                float _DetailNoiseScale;
                float _DetailNoiseStrength;
                float4 _WindDirection;
                float _WindSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = vertexInput.positionWS;
                output.cameraOS = TransformWorldToObject(GetCameraPositionWS());
                output.cameraWS = GetCameraPositionWS();
                return output;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, local.x);
                float nx10 = lerp(n010, n110, local.x);
                float nx01 = lerp(n001, n101, local.x);
                float nx11 = lerp(n011, n111, local.x);
                float nxy0 = lerp(nx00, nx10, local.y);
                float nxy1 = lerp(nx01, nx11, local.y);
                return lerp(nxy0, nxy1, local.z);
            }

            float Fbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                [unroll(4)]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p * frequency) * amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }

                return sum;
            }

            float2 RayBoxIntersection(float3 rayOrigin, float3 rayDirection)
            {
                const float3 boundsMin = float3(-0.5, -0.5, -0.5);
                const float3 boundsMax = float3(0.5, 0.5, 0.5);
                float3 inverseDirection = rcp(max(abs(rayDirection), 1e-5)) * sign(rayDirection);
                float3 t0 = (boundsMin - rayOrigin) * inverseDirection;
                float3 t1 = (boundsMax - rayOrigin) * inverseDirection;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float nearHit = max(max(tMin.x, tMin.y), tMin.z);
                float farHit = min(min(tMax.x, tMax.y), tMax.z);
                return float2(nearHit, farHit);
            }

            float ComputeEdgeFade(float3 sampleOS)
            {
                float3 edgeDistance = 0.5 - abs(sampleOS);
                float normalizedEdge = min(min(edgeDistance.x, edgeDistance.y), edgeDistance.z) / max(_EdgeSoftness, 0.001);
                return saturate(normalizedEdge);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 cameraOS = input.cameraOS;
                float3 cameraWS = input.cameraWS;
                float3 exitPointOS = input.positionOS;
                float3 exitPointWS = input.positionWS;
                float3 rayDirectionOS = normalize(exitPointOS - cameraOS);

                float2 rayHits = RayBoxIntersection(cameraOS, rayDirectionOS);
                if (rayHits.x > rayHits.y || rayHits.y <= 0.0)
                {
                    discard;
                }

                float3 entryPointOS = cameraOS + rayDirectionOS * max(rayHits.x, 0.0);
                float3 boxExitPointOS = cameraOS + rayDirectionOS * rayHits.y;
                float3 entryPointWS = TransformObjectToWorld(entryPointOS);
                float3 boxExitPointWS = TransformObjectToWorld(boxExitPointOS);

                float boxExitDistance = distance(cameraWS, boxExitPointWS);
                float endDistance = boxExitDistance;
                float startDistance = distance(cameraWS, entryPointWS);
                float pathLength = max(endDistance - startDistance, 0.0);
                if (pathLength <= 0.0001)
                {
                    discard;
                }

                float3 rayDirectionWS = normalize(boxExitPointWS - entryPointWS);
                float stepLength = pathLength / 24.0;
                float3 windDirection = normalize(float3(_WindDirection.x, 0.0, _WindDirection.y) + float3(1e-5, 0.0, 0.0));
                float3 windOffset = windDirection * (_Time.y * _WindSpeed * 4.0);

                float opticalDepth = 0.0;
                [loop]
                for (int i = 0; i < 24; i++)
                {
                    float t = (i + 0.5) / 24.0;
                    float currentDistance = startDistance + pathLength * t;
                    float3 sampleWS = cameraWS + rayDirectionWS * currentDistance;
                    float3 sampleOS = TransformWorldToObject(sampleWS);

                    float edgeFade = ComputeEdgeFade(sampleOS);
                    if (edgeFade <= 0.0)
                    {
                        continue;
                    }

                    float normalizedHeight = saturate((0.5 - sampleOS.y) + (_HeightOffset + 0.5));
                    float heightDensity = exp2(-_HeightFalloff * max(sampleWS.y - entryPointWS.y, 0.0));
                    heightDensity *= saturate(normalizedHeight);

                    float baseNoise = smoothstep(0.35, 0.8, Fbm((sampleWS + windOffset) * _NoiseScale));
                    float detailNoise = smoothstep(0.25, 0.75, Fbm((sampleWS - windOffset * 1.7) * _DetailNoiseScale));
                    float combinedNoise = lerp(1.0, baseNoise, _NoiseStrength);
                    combinedNoise *= lerp(1.0, detailNoise, _DetailNoiseStrength);

                    float sampleDensity = _Density * edgeFade * heightDensity * combinedNoise;
                    opticalDepth += sampleDensity * stepLength;
                }

                float alpha = 1.0 - exp(-pow(max(opticalDepth, 0.0), _DensityPower));
                alpha = saturate(alpha) * _BaseColor.a;
                alpha = min(alpha, _MaxOpacity);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
