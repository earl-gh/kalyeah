Shader "Kalyeah/TeamOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1.00, 0.22, 0.24, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.12)) = 0.014
        _DistanceComp ("Distance Compensation", Range(0.0, 1.0)) = 0.45
        _DistanceBoost ("Distance Brightness Boost", Range(0.0, 1.0)) = 0.40
        _DepthStart ("Depth Boost Start (m)", Float) = 12.0
        _DepthEnd ("Depth Boost End (m)", Float) = 45.0
        _MaxDistance ("Max Outline Distance", Range(20.0, 260.0)) = 150.0
        _HitFlash ("Hit Flash", Range(0.0, 1.0)) = 0.0
        _AuraColor ("Merienda Aura Color", Color) = (0.0, 0.0, 0.0, 0.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+15"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TeamOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _DistanceComp;
                half _DistanceBoost;
                float _DepthStart;
                float _DepthEnd;
                float _MaxDistance;
                half _HitFlash;
                half4 _AuraColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 edgeDepth : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                float3 toCam = GetCameraPositionWS() - posWS;
                float dist = max(length(toCam), 0.1);
                float3 viewDir = (unity_OrthoParams.w > 0.5) ? -GetViewForwardDir() : (toCam / dist);

                // Clean VALORANT silhouette outline: crisp up close, slightly wider & brighter at 12m..45m, zero wobble.
                float distScale = lerp(0.86, clamp(dist / 7.2, 0.72, 1.48), _DistanceComp);
                float fadeStart = max(_MaxDistance * 0.75, 1.0);
                distScale *= 1.0 - smoothstep(fadeStart, max(_MaxDistance, fadeStart + 1.0), dist);
                float depthSpan = max(_DepthEnd - _DepthStart, 1.0);
                float distGlow = saturate((dist - _DepthStart) / depthSpan);

                float width = _OutlineWidth * distScale;

                // Project rim strictly perpendicular to viewDir so double-sided cloth/masks never push toward camera.
                float ndotv = dot(normWS, viewDir);
                float3 rimWS = normWS - viewDir * ndotv;
                float rimLen = length(rimWS);
                float graze = smoothstep(0.16, 0.52, rimLen);
                float3 pushDir = (rimLen > 0.0001) ? (rimWS / rimLen) : float3(0.0, 0.0, 0.0);

                posWS += pushDir * (width * graze);
                posWS -= viewDir * 0.020;

                output.positionCS = TransformWorldToHClip(posWS);
                output.edgeDepth = half2((half)graze, (half)distGlow);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half edgeSoft = input.edgeDepth.x;
                half distGlow = input.edgeDepth.y;

                // Colorblind-safe Red / Yellow / Purple highlight aligned with Kalyeah/Kid; distance-boosted across 12m..45m.
                #ifdef KALYEAH_QUALITY_LOW
                half3 highlight = _OutlineColor.rgb * (1.0h + _DistanceBoost * distGlow);
                #else
                half3 highlight = lerp(_OutlineColor.rgb, lerp(_OutlineColor.rgb, half3(1.0h, 0.98h, 0.95h), 0.18h), edgeSoft * 0.45h);
                highlight *= 1.0h + _DistanceBoost * distGlow;
                #endif

                highlight = lerp(highlight, _AuraColor.rgb, saturate(_AuraColor.a * 0.45h));
                highlight = lerp(highlight, half3(1.0h, 0.97h, 0.88h), saturate(_HitFlash));

                half alpha = saturate(_OutlineColor.a * lerp(0.84h, 0.98h, edgeSoft) * (1.0h + _DistanceBoost * 0.35h * distGlow));
                alpha = max(alpha, saturate(_HitFlash));
                half3 finalRgb = MixFog(highlight, input.fogFactor);
                return half4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
