Shader "Kalyeah/UI_LiquidGauge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Fill ("Fill", Range(0, 1)) = 1.0
        _TrailFill ("Trail Fill", Range(0, 1)) = 1.0
        _Segments ("Segments", Float) = 4.0
        _FillColor ("Fill Color", Color) = (0.961, 0.945, 0.910, 1.0)
        _TrailColor ("Trail Drain Color", Color) = (1.0, 0.68, 0.24, 0.92)
        _TrackColor ("Track Color", Color) = (0.055, 0.075, 0.102, 0.72)
        _MeniscusGlow ("Meniscus Glow", Range(0, 1)) = 0.65
        _WaveSpeed ("Liquid Wave Speed", Float) = 0.0
        _LowPulse ("Low Urgent Pulse", Range(0, 1)) = 0.0
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
            Name "UI_LiquidGauge"

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
                float _Fill;
                float _TrailFill;
                float _Segments;
                half4 _FillColor;
                half4 _TrailColor;
                half4 _TrackColor;
                half _MeniscusGlow;
                float _WaveSpeed;
                half _LowPulse;
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

                float2 uv = input.uv;
                float pxX = max(length(float2(ddx(uv.x), ddy(uv.x))), 1e-4);
                float pxY = max(length(float2(ddx(uv.y), ddy(uv.y))), 1e-4);

                float fill = saturate(_Fill);
                float trail = max(fill, saturate(_TrailFill));
                float animTime = (_ReduceMotion > 0.5) ? 0.0 : _Time.y;

                // Subtle live liquid surface wave at leading meniscus when _WaveSpeed > 0 (Water bar).
                float waveOffset = (_WaveSpeed > 0.01)
                    ? sin(uv.y * 12.0 - animTime * _WaveSpeed * 4.5) * 0.004
                    : 0.0;
                float fillEdge = saturate(fill + waveOffset * step(0.01, fill) * step(fill, 0.99));

                float fillMask = 1.0 - smoothstep(fillEdge - pxX, fillEdge + pxX, uv.x);
                float trailMask = (1.0 - smoothstep(trail - pxX, trail + pxX, uv.x)) * (1.0 - fillMask);

                // Recessed smoked-obsidian track with crisp top inner shadow.
                half3 trackRgb = _TrackColor.rgb * lerp(1.06h, 0.84h, (half)uv.y);
                half trackA = _TrackColor.a;

                // Crisp bar fill with gentle top specular sheen & optional animated liquid caustic shimmer.
                half3 barRgb = _FillColor.rgb * lerp(0.88h, 1.08h, (half)uv.y);
                if (_WaveSpeed > 0.01)
                {
                    float shimmer = 0.5 + 0.5 * sin(uv.x * 22.0 - animTime * _WaveSpeed * 3.0 + uv.y * 3.0);
                    barRgb = lerp(barRgb, saturate(barRgb * 1.25h + half3(0.10h, 0.16h, 0.20h)), (half)shimmer * 0.22h);
                }

                // Low-HP urgent pulse (gentle vermilion tint).
                if (_LowPulse > 0.001h)
                {
                    half pulseWave = (_ReduceMotion > 0.5)
                        ? 0.65h
                        : (half)(0.5 + 0.5 * sin(animTime * 9.5));
                    half pulseMix = saturate(_LowPulse * lerp(0.35h, 0.85h, pulseWave));
                    barRgb = lerp(barRgb, half3(1.0h, 0.275h, 0.333h), pulseMix);
                }

                // Luminous leading meniscus edge highlight.
                float distToMeniscus = abs(uv.x - fillEdge);
                float hasMeniscus = step(0.015, fill) * step(fill, 0.992);
                half meniscus = (half)(exp(-pow(distToMeniscus / max(pxX * 3.2, 0.008), 2.0)) * hasMeniscus) * _MeniscusGlow;
                barRgb = saturate(barRgb + half3(0.45h, 0.48h, 0.52h) * meniscus);

                // Composite track -> amber delayed drain trail -> live fill.
                half3 colRgb = trackRgb;
                half colA = trackA;
                colRgb = lerp(colRgb, _TrailColor.rgb, (half)trailMask * _TrailColor.a);
                colA = lerp(colA, max(colA, _TrailColor.a), (half)trailMask);
                colRgb = lerp(colRgb, barRgb, (half)fillMask * _FillColor.a);
                colA = lerp(colA, max(colA, _FillColor.a), (half)fillMask);

                // Crisp pixel-true vertical segment ticks (e.g. 4 x 25-HP ticks).
                float segCount = floor(_Segments + 0.5);
                if (segCount > 1.5)
                {
                    float segCoord = uv.x * segCount;
                    float nearestInt = round(segCoord);
                    float isInterior = step(0.5, nearestInt) * step(nearestInt, segCount - 0.5);
                    float distPx = abs(segCoord - nearestInt) / max(pxX * segCount, 1e-4);
                    float gapMask = (1.0 - smoothstep(0.9, 1.8, distPx)) * isInterior;
                    colRgb = lerp(colRgb, half3(0.03h, 0.04h, 0.06h), (half)gapMask * 0.92h);
                    colA = lerp(colA, 0.92h, (half)gapMask);
                }

                // Crisp 1 px top & bottom framing catchlight / hairline edge.
                float distEdgeY = min(uv.y, 1.0 - uv.y) / pxY;
                half frameMask = 1.0h - (half)smoothstep(0.6, 1.4, distEdgeY);
                half3 frameCol = uv.y > 0.5 ? half3(0.84h, 0.88h, 0.92h) : half3(0.02h, 0.03h, 0.05h);
                half frameStrength = uv.y > 0.5 ? 0.22h : 0.48h;
                colRgb = lerp(colRgb, frameCol, frameMask * frameStrength);

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) + _TextureSampleAdd;
                half4 col = half4(colRgb * texSample.rgb * input.color.rgb, colA * texSample.a * input.color.a);

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
