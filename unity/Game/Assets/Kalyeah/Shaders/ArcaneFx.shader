Shader "Kalyeah/ArcaneFx"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Tint ("Burst Tint", Color) = (1, 1, 1, 1)
        _InkColor ("Komiks Ink Edge", Color) = (0.20, 0.15, 0.14, 0.94)
        _Phase ("Lifetime Phase", Range(0, 1)) = 0.0
        _SoftFade ("Soft Fade", Range(0, 1)) = 1.0
        _FresnelSoftness ("Fresnel Softness", Range(0, 1)) = 0.16
        _SunlitLift ("Sunlit Core Lift", Range(0, 0.4)) = 0.10
        _ScrollSpeed ("Stroke Scroll Speed", Range(-4, 4)) = 0.90
        _StrokeScale ("Stroke World Scale", Float) = 2.40
        _PosterizeSteps ("Komiks Gouache Steps", Range(2, 8)) = 4.0
        _HardEdgeWidth ("Hard Edge Width", Range(0, 1)) = 0.0
        _DashCount ("Perimeter Dash Count (0=Solid)", Float) = 0.0
        _EdgeBoost ("Hard Edge Clarity Boost", Range(0, 1)) = 0.25
        _InteriorAlpha ("Hard-Edge Interior Fill Alpha", Range(0, 1)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ArcaneFxForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Tint;
                half4 _InkColor;
                half _Phase;
                half _SoftFade;
                half _FresnelSoftness;
                half _SunlitLift;
                half _ScrollSpeed;
                half _StrokeScale;
                half _PosterizeSteps;
                half _HardEdgeWidth;
                float _DashCount;
                half _EdgeBoost;
                half _InteriorAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #ifndef KALYEAH_QUALITY_LOW
            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float VfxNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #ifdef KALYEAH_QUALITY_LOW
                // Riot VFX minspec fairness: strip cosmetic swirl noise on Low while keeping 100% boundary accuracy.
                float stroke = 0.5;
                float steppedStroke = 0.5;
                #else
                float t = fmod(_Time.y, 120.0);
                float stroke = VfxNoise(input.positionWS.xz * _StrokeScale + float2(-t * _ScrollSpeed, input.positionWS.y * 1.8));
                float steps = max((float)_PosterizeSteps, 2.0);
                float steppedStroke = floor(stroke * steps) / steps;
                #endif

                half3 bodyCol = input.color.rgb * _Tint.rgb * _BaseColor.rgb;
                half baseAlpha = input.color.a * _Tint.a * _BaseColor.a * _SoftFade;

                // Hard-edge geometric boundary mode (Riot Sova/Viper/Omen clarity):
                // Razor-sharp fwidth-AA perimeter across all 4 tiers, solid for allies (_DashCount <= 0.5)
                // and angle-independent dashed for enemies (_DashCount > 0.5).
                float edgeStart = saturate(1.0 - (float)_HardEdgeWidth);
                float aaY = max(fwidth(input.uv.y), 0.004);
                half hardEdge = (half)smoothstep(edgeStart - aaY, edgeStart + aaY, input.uv.y);

                float dashCoord = input.uv.x * max(_DashCount, 1.0);
                float aaX = clamp(fwidth(dashCoord), 0.01, 0.25);
                float dashCenterDist = abs(frac(dashCoord) - 0.5);
                half rawDash = (half)(1.0 - smoothstep(0.29 - aaX, 0.29 + aaX, dashCenterDist));
                half dashMask = (_DashCount > 0.5) ? rawDash : 1.0h;

                half hardMode = step(0.001h, _HardEdgeWidth);

                // Path A: Hard-edge geometric zone / telegraph / trail boundary
                #ifdef KALYEAH_QUALITY_LOW
                half interiorGarnish = 1.0h;
                #else
                half interiorGarnish = 0.92h + (half)steppedStroke * 0.08h;
                #endif
                half3 edgeCol = bodyCol * (1.0h + _EdgeBoost) + half3(0.10h, 0.10h, 0.10h) * _EdgeBoost;
                half3 geoCol = lerp(bodyCol * interiorGarnish, edgeCol, hardEdge);
                half geoAlpha = saturate(baseAlpha * lerp(_InteriorAlpha * interiorGarnish, dashMask, hardEdge));

                // Path B: Painted combat burst (stars, puffs, streaks, heal cross)
                half luma = dot(input.color.rgb, half3(0.299h, 0.587h, 0.114h));
                half inkMask = 1.0h - smoothstep(0.18h, 0.42h, luma);
                half coreMask = smoothstep(0.28h, 0.72h, luma);

                half3 sunlitCore = lerp(bodyCol, half3(1.0h, 0.97h, 0.88h), saturate(_SunlitLift + (half)steppedStroke * 0.08h));
                half3 burstCol = lerp(bodyCol, sunlitCore, coreMask);
                burstCol = lerp(burstCol, _InkColor.rgb, inkMask * _InkColor.a);

                float nLen = length(input.normalWS);
                float3 viewDir = SafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float ndv = (nLen > 0.001) ? abs(dot(input.normalWS / nLen, viewDir)) : 1.0;
                half rimKeep = lerp(1.0h, (half)smoothstep(0.04, 0.32, ndv), _FresnelSoftness);

                half tailErode = 1.0h - smoothstep(0.68h, 1.0h, _Phase) * (1.0h - (half)stroke) * 0.40h;
                half burstAlpha = saturate(baseAlpha * rimKeep * tailErode);

                half3 finalCol = lerp(burstCol, geoCol, hardMode);
                half finalAlpha = lerp(burstAlpha, geoAlpha, hardMode);
                return half4(saturate(finalCol), finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
