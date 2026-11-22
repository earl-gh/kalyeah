Shader "Kalyeah/UI_RadialSkillDisc"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cooldown01 ("Cooldown Progress", Range(0, 1)) = 0.0
        _Arc01 ("Radial Arc Progress", Range(0, 1)) = 0.0
        _RimColor ("Brushed Titanium Rim", Color) = (0.835, 0.875, 0.914, 0.92)
        _RimGlow ("Rim Glow Intensity", Range(0, 2)) = 0.28
        _GlassTint ("Frosted Smoked Glass", Color) = (0.075, 0.105, 0.145, 0.72)
        _InnerRing ("Inner Ring Radius", Range(0, 0.92)) = 0.78
        _LockedDesat ("Locked Desaturation", Range(0, 1)) = 0.0
        _UltPulse ("Ult Ready Pulse", Range(0, 1)) = 0.0
        _ReduceMotion ("Reduce Motion", Float) = 0.0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI_RadialSkillDisc"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _ClipRect;
            half4 _TextureSampleAdd;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Cooldown01;
                float _Arc01;
                half4 _RimColor;
                half _RimGlow;
                half4 _GlassTint;
                float _InnerRing;
                half _LockedDesat;
                half _UltPulse;
                float _ReduceMotion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 localPos   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.localPos = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float r = length(centered);
                float aa = max(fwidth(r), 0.015);
                float outerEdge = 0.96;
                half discMask = (half)(1.0 - smoothstep(outerEdge - aa, outerEdge + aa, r));

                // Clockwise angle starting from 12 o'clock: 0 at top -> 1 full circle (guarded against atan2(0,0) NaN).
                float cwAngle = (r > 1e-4) ? frac(atan2(centered.x, centered.y) / 6.2831853 + 1.0) : 0.0;

                // Concave frosted smoked-glass interior shading + crisp top-left titanium crescent catchlight.
                float radialDepth = saturate(r / outerEdge);
                half3 glassRgb = _GlassTint.rgb * lerp(0.84h, 1.16h, (half)(radialDepth * radialDepth));
                float lightDir = dot(centered, float2(-0.42, 0.90));
                half frostSpec = (half)smoothstep(0.18, 0.92, lightDir) * (1.0h - (half)smoothstep(0.60, 0.96, r)) * 0.22h;
                glassRgb = lerp(glassRgb, half3(0.86h, 0.92h, 0.97h), frostSpec);
                half colA = _GlassTint.a;
                half3 colRgb = glassRgb;

                // Subtle hairline inner brushed-titanium ring.
                if (_InnerRing > 0.08)
                {
                    float innerBand = 1.0 - smoothstep(0.0, max(aa * 1.4, 0.022), abs(r - _InnerRing));
                    colRgb = lerp(colRgb, _RimColor.rgb, (half)innerBand * 0.42h);
                    colA = max(colA, (half)innerBand * 0.65h);
                }

                // Brushed-titanium outer bevel rim with anisotropic metallic reflection.
                float rimStart = 0.87;
                float rimMask = smoothstep(rimStart - aa, rimStart + aa, r) * (1.0 - smoothstep(outerEdge - aa, outerEdge + aa, r));
                float brushed = 0.84 + 0.16 * sin(cwAngle * 12.56637 + 0.9);
                half animPulse = (_ReduceMotion > 0.5)
                    ? 0.55h
                    : (half)(0.5 + 0.5 * sin(_Time.y * 4.2));
                half ultBoost = _UltPulse * lerp(0.35h, 1.0h, animPulse);
                half3 rimRgb = saturate(_RimColor.rgb * (half)brushed * (1.0h + _RimGlow * 0.45h + ultBoost * 0.55h));
                colRgb = lerp(colRgb, rimRgb, (half)rimMask * _RimColor.a);
                colA = lerp(colA, max(colA, _RimColor.a), (half)rimMask);

                // Radial luminous charge / channel arc along outer track (_Arc01 > 0).
                if (_Arc01 > 0.005)
                {
                    float arcTrack = smoothstep(0.83 - aa, 0.83 + aa, r) * (1.0 - smoothstep(0.95 - aa, 0.95 + aa, r));
                    float inArc = 1.0 - smoothstep(_Arc01 - 0.012, _Arc01 + 0.012, cwAngle);
                    half arcMask = (half)(arcTrack * inArc);
                    half3 arcCol = saturate(_RimColor.rgb * 1.35h + half3(0.18h, 0.22h, 0.26h));
                    colRgb = lerp(colRgb, arcCol, arcMask);
                    colA = max(colA, arcMask);
                }

                // Clockwise radial cooldown sweep with luminous sweep frontier line.
                float cd = saturate(_Cooldown01);
                if (cd > 0.002)
                {
                    float inCd = 1.0 - smoothstep(cd - 0.01, cd + 0.01, cwAngle);
                    float interior = 1.0 - smoothstep(rimStart - aa, rimStart + aa, r);
                    colRgb = lerp(colRgb, colRgb * 0.36h + half3(0.02h, 0.03h, 0.05h), (half)(inCd * interior) * 0.82h);
                    float frontier = (1.0 - smoothstep(0.0, 0.016, abs(cwAngle - cd))) * interior * step(cd, 0.992);
                    colRgb = lerp(colRgb, half3(0.92h, 0.96h, 1.0h), (half)frontier * 0.78h);
                }

                // Soft outer Ult-ready halo breathing on rim.
                if (_UltPulse > 0.01h)
                {
                    float dHalo = (r - 0.89) / 0.11;
                    float haloRing = exp(-(dHalo * dHalo));
                    colRgb = saturate(colRgb + _RimColor.rgb * (half)haloRing * ultBoost * 0.42h);
                }

                // Locked / out-of-water desaturation & dimming.
                if (_LockedDesat > 0.001h)
                {
                    half luma = dot(colRgb, half3(0.2126h, 0.7152h, 0.0722h));
                    colRgb = lerp(colRgb, half3(luma, luma, luma) * 0.62h, _LockedDesat);
                }

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;
                half4 col = half4(colRgb * texSample.rgb * input.color.rgb, colA * discMask * texSample.a * input.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.localPos.xy) * step(input.localPos.xy, _ClipRect.zw);
                float2 soft = max(float2(_UIMaskSoftnessX, _UIMaskSoftnessY), float2(1e-4, 1e-4));
                float2 edgeDist = min(input.localPos.xy - _ClipRect.xy, _ClipRect.zw - input.localPos.xy);
                float2 softMask = saturate(edgeDist / soft);
                float clipAlpha = (_UIMaskSoftnessX > 0.0 || _UIMaskSoftnessY > 0.0)
                    ? (softMask.x * softMask.y)
                    : (inside.x * inside.y);
                col.a *= (half)clipAlpha;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001h);
                #endif

                return col;
            }
            ENDHLSL
        }
    }
}
