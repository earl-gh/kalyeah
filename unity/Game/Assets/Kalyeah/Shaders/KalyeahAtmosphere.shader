Shader "Kalyeah/Atmosphere"
{
    Properties
    {
        _HazeColor ("Haze Color", Color) = (0.96, 0.94, 0.87, 1.0)
        _SunGlowColor ("Sun Glow Color", Color) = (1.00, 0.96, 0.86, 1.0)
        _HazeParams ("Haze Params (Start, End, MaxOpacity, HeightFalloff)", Vector) = (45.0, 110.0, 0.28, 0.085)
        _HeightAndSunParams ("Height & Sun Params (BaseY, SunGlowStrength, 0, 0)", Vector) = (0.0, 0.14, 0.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "KalyeahAtmosphereHaze"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex AtmosphereVert
            #pragma fragment AtmosphereFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_AtmosphereColorTex);
            SAMPLER(sampler_AtmosphereColorTex);
            TEXTURE2D_X_FLOAT(_AtmosphereDepthTex);
            SAMPLER(sampler_AtmosphereDepthTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _HazeColor;
                half4 _SunGlowColor;
                float4 _HazeParams;
                float4 _HeightAndSunParams;
                float4 _ColorScaleBias;
                float4 _DepthScaleBias;
            CBUFFER_END

            // Optional global sun direction from MatchBoot.ApplyGlobalClarityGlobals (outside UnityPerMaterial).
            float4 _KalyeahSunDir;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings AtmosphereVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 AtmosphereFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 colorScale = (_ColorScaleBias.x > 0.0 && _ColorScaleBias.y > 0.0)
                    ? _ColorScaleBias.xy
                    : float2(1.0, 1.0);
                float2 depthScale = (_DepthScaleBias.x > 0.0 && _DepthScaleBias.y > 0.0)
                    ? _DepthScaleBias.xy
                    : float2(1.0, 1.0);
                float2 colorUv = uv * colorScale + _ColorScaleBias.zw;
                float2 depthUv = uv * depthScale + _DepthScaleBias.zw;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_AtmosphereColorTex, sampler_AtmosphereColorTex, colorUv);
                float rawDepth = SAMPLE_TEXTURE2D_X(_AtmosphereDepthTex, sampler_AtmosphereDepthTex, depthUv).r;

                // Preserve the sunlit skybox untouched; apply haze strictly to world geometry.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.0001)
                        return sceneColor;
                    float deviceDepth = rawDepth;
                #else
                    if (rawDepth >= 0.9999)
                        return sceneColor;
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 posWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
                float3 camToPos = posWS - _WorldSpaceCameraPos;
                float dist = length(camToPos);

                // Keep the 0-35 m combat lane 100% crisp; separate distant barangay silhouettes at 45-110 m.
                float startDist = max(_HazeParams.x, 1.0);
                float endDist = max(_HazeParams.y, startDist + 1.0);
                float distFactor = smoothstep(startDist, endDist, dist);
                if (distFactor <= 0.0001)
                    return sceneColor;

                float heightAboveBase = max(posWS.y - _HeightAndSunParams.x, 0.0);
                float heightDensity = exp(-heightAboveBase * max(_HazeParams.w, 0.001));
                float heightFactor = lerp(0.38, 1.0, heightDensity);

                float3 sunDir = (_KalyeahSunDir.w > 0.5 && dot(_KalyeahSunDir.xyz, _KalyeahSunDir.xyz) > 1e-4)
                    ? normalize(_KalyeahSunDir.xyz)
                    : normalize(float3(0.412, 0.743, -0.527));
                float3 viewDir = camToPos / max(dist, 1e-4);
                float sunAlign = saturate(dot(viewDir, sunDir));
                float sunAlign2 = sunAlign * sunAlign;
                float sunGlow = (sunAlign2 * sunAlign2) * saturate(_HeightAndSunParams.y);

                half3 hazeRgb = lerp(_HazeColor.rgb, _SunGlowColor.rgb, (half)saturate(sunGlow * 2.0))
                    + _SunGlowColor.rgb * (half)(sunGlow * 0.20);
                float hazeWeight = saturate(distFactor * heightFactor * saturate(_HazeParams.z));

                half3 finalRgb = lerp(sceneColor.rgb, hazeRgb, (half)hazeWeight);
                return half4(finalRgb, sceneColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
