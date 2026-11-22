Shader "Kalyeah/UI_TacticalGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (0.055, 0.075, 0.102, 1.0)
        _RectSize ("Rect Size (0=Auto)", Vector) = (0, 0, 0, 0)
        _CornerCut ("Corner Cut (px)", Float) = 4.0
        _BorderWidth ("Border Width (px)", Float) = 1.0
        _BorderColor ("Border Color", Color) = (0.835, 0.875, 0.914, 0.24)
        _TopHighlight ("Top Catchlight", Range(0, 1)) = 0.14
        _AccentColor ("Left Accent Color", Color) = (0.961, 0.945, 0.910, 1.0)
        _AccentWidth ("Left Accent Width (px)", Float) = 3.0
        _GlassAlpha ("Glass Alpha", Range(0, 1)) = 0.74
        _NoiseGrain ("Frosted Grain", Range(0, 0.15)) = 0.025

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
            Name "UI_TacticalGlass"

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
                float4 _RectSize;
                float _CornerCut;
                float _BorderWidth;
                half4 _BorderColor;
                half _TopHighlight;
                half4 _AccentColor;
                float _AccentWidth;
                half _GlassAlpha;
                half _NoiseGrain;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float SignedChamferBox(float2 pos, float2 size, float majorCut, float minorCut)
            {
                const float invSqrt2 = 0.70710678;
                float d = min(min(pos.x, size.x - pos.x), min(pos.y, size.y - pos.y));
                if (majorCut > 0.05)
                {
                    d = min(d, (pos.x + (size.y - pos.y) - majorCut) * invSqrt2);
                    d = min(d, ((size.x - pos.x) + pos.y - majorCut) * invSqrt2);
                }
                if (minorCut > 0.05)
                {
                    d = min(d, ((size.x - pos.x) + (size.y - pos.y) - minorCut) * invSqrt2);
                    d = min(d, (pos.x + pos.y - minorCut) * invSqrt2);
                }
                return d;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.localPos = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 duvX = float2(ddx(input.uv.x), ddy(input.uv.x));
                float2 duvY = float2(ddx(input.uv.y), ddy(input.uv.y));
                float2 autoSize = clamp(
                    float2(1.0 / max(length(duvX), 1e-5), 1.0 / max(length(duvY), 1e-5)),
                    float2(8.0, 8.0),
                    float2(4096.0, 4096.0));
                float2 size = (_RectSize.x > 1.0 && _RectSize.y > 1.0) ? _RectSize.xy : autoSize;

                float2 pos = input.uv * size;
                float maxCut = min(size.x, size.y) * 0.42;
                float majorCut = clamp(_CornerCut, 0.0, maxCut);
                float minorCut = majorCut > 1.5 ? clamp(majorCut * 0.35, 0.0, maxCut * 0.5) : 0.0;

                float d = SignedChamferBox(pos, size, majorCut, minorCut);
                float aa = max(fwidth(d), 0.75);
                half shapeMask = (half)smoothstep(-aa * 0.5, aa * 0.5, d);

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;

                // Smoked obsidian glass body with subtle brushed-titanium vertical frost gradient and micro-grain.
                half ny = (half)saturate(input.uv.y);
                half3 baseRgb = _Color.rgb * lerp(0.90h, 1.08h, ny);
                half grain = (half)(Hash21(floor(pos)) - 0.5) * _NoiseGrain;
                baseRgb = saturate(baseRgb + grain);
                half baseAlpha = saturate(_Color.a * _GlassAlpha);

                // Crisp 1 px brushed-titanium top catchlight along upper lip.
                float distTop = size.y - pos.y;
                float topBand = (1.0 - smoothstep(0.0, max(1.4, _BorderWidth + 0.6), abs(distTop - max(1.0, _BorderWidth))));
                float topTaper = smoothstep(0.0, 0.14, input.uv.x) * (1.0 - smoothstep(0.86, 1.0, input.uv.x));
                half topGlow = (half)(topBand * lerp(0.45, 1.0, topTaper)) * _TopHighlight;
                half3 glassRgb = lerp(baseRgb, half3(0.88h, 0.93h, 0.98h), topGlow);
                half glassA = saturate(baseAlpha + topGlow * 0.35h);

                // Subtle grounded bottom inner shade for crisp elevation over sunlit 3D world.
                half bottomShade = (1.0h - (half)smoothstep(0.0, 4.0, pos.y)) * 0.16h;
                glassRgb *= (1.0h - bottomShade);

                // Pixel-true outer hairline border.
                half borderMask = (_BorderWidth > 0.05)
                    ? (half)(1.0 - smoothstep(_BorderWidth - aa * 0.5, _BorderWidth + aa * 0.5, d))
                    : 0.0h;
                half3 colRgb = lerp(glassRgb, _BorderColor.rgb, borderMask * _BorderColor.a);
                half colA = lerp(glassA, max(glassA, _BorderColor.a), borderMask);

                // Crisp editorial left accent tick bar (A1 paper-white or Vermilion indicator).
                if (_AccentWidth > 0.05 && _AccentColor.a > 0.001h)
                {
                    float accentMaskX = 1.0 - smoothstep(_AccentWidth - aa * 0.5, _AccentWidth + aa * 0.5, pos.x);
                    half accentMix = (half)accentMaskX * _AccentColor.a;
                    colRgb = lerp(colRgb, _AccentColor.rgb, accentMix);
                    colA = max(colA, accentMix);
                }

                half4 col = half4(colRgb * texSample.rgb * input.color.rgb, colA * shapeMask * texSample.a * input.color.a);

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
