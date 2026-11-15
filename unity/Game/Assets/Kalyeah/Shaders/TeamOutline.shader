Shader "Kalyeah/TeamOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.20, 0.50, 1.00, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.12)) = 0.014
        _DistanceComp ("Distance Compensation", Range(0.0, 1.0)) = 0.30
        _MaxDistance ("Max Outline Distance", Range(20.0, 260.0)) = 150.0
        _WobbleAmount ("Komiks Pen Wobble", Range(0.0, 0.5)) = 0.08
        _WobbleFreq ("Komiks Wobble Frequency", Range(1.0, 40.0)) = 13.5
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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _DistanceComp;
                float _MaxDistance;
                float _WobbleAmount;
                float _WobbleFreq;
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
                half2 edgeStroke : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                float3 toCam = GetCameraPositionWS() - posWS;
                float dist = max(length(toCam), 0.1);
                float3 viewDir = (unity_OrthoParams.w > 0.5) ? -GetViewForwardDir() : (toCam / dist);

                // Shoulder-view distance stabilization (team_outline.gdshader + arcane_ink_outline.gdshader):
                // crisp ~1.8 px silhouette at 4.15 m without thick halos across Tabing-Ilog.
                float distScale = lerp(0.92, clamp(dist / 7.6, 0.76, 1.28), _DistanceComp);
                distScale *= 1.0 - smoothstep(_MaxDistance * 0.68, _MaxDistance, dist);

                // Spatial komiks pen-pressure modulation anchored in world space (zero temporal shimmer).
                float w1 = sin(posWS.x * _WobbleFreq + posWS.y * (_WobbleFreq * 1.37) + posWS.z * (_WobbleFreq * 0.83));
                float w2 = cos(posWS.z * (_WobbleFreq * 2.13) - posWS.x * (_WobbleFreq * 1.61) + posWS.y * (_WobbleFreq * 0.91));
                float wobble = w1 * 0.65 + w2 * 0.35;
                float width = _OutlineWidth * distScale * max(1.0 + _WobbleAmount * wobble, 0.62);

                // Project rim strictly perpendicular to viewDir so negative-ndotv backfaces on double-sided
                // cloth/masks never push toward the camera, and bias depth behind front-facing surfaces.
                float ndotv = dot(normWS, viewDir);
                float3 rimWS = normWS - viewDir * ndotv;
                float rimLen = length(rimWS);
                float graze = smoothstep(0.16, 0.52, rimLen);
                float3 pushDir = (rimLen > 0.0001) ? (rimWS / rimLen) : float3(0.0, 0.0, 0.0);

                posWS += pushDir * (width * graze);
                posWS -= viewDir * 0.020;

                output.positionCS = TransformWorldToHClip(posWS);
                output.edgeStroke = half2((half)graze, (half)(wobble * 0.5 + 0.5));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half edgeSoft = input.edgeStroke.x;
                half stroke = input.edgeStroke.y;

                // Sunlit Philippine street team read (blue ally / red enemy) with subtle komiks ink density.
                half3 sunlitTeam = lerp(_OutlineColor.rgb, lerp(_OutlineColor.rgb, half3(1.0, 0.98, 0.94), 0.12h), edgeSoft * 0.45h);
                sunlitTeam *= lerp(0.92h, 1.06h, stroke * 0.35h);

                // Optional Merienda buff rim accent + crisp white-gold hit confirm flash.
                sunlitTeam = lerp(sunlitTeam, _AuraColor.rgb, saturate(_AuraColor.a * 0.45h));
                sunlitTeam = lerp(sunlitTeam, half3(1.0, 0.97, 0.88), saturate(_HitFlash));

                half alpha = _OutlineColor.a * lerp(0.86h, 0.96h, edgeSoft);
                alpha = max(alpha, saturate(_HitFlash));
                half3 finalRgb = MixFog(sunlitTeam, input.fogFactor);
                return half4(finalRgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
