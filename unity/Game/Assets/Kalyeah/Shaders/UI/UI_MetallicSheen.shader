Shader "Kalyeah/UI_MetallicSheen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _RectSize ("Rect Size (0=Auto)", Vector) = (0, 0, 0, 0)
        _CornerCut ("Corner Cut (px)", Float) = 10.0
        _BaseTop ("Base Top Color", Color) = (1.0, 0.275, 0.333, 1.0)
        _BaseBottom ("Base Bottom Color", Color) = (0.831, 0.192, 0.251, 1.0)
        _SheenColor ("Sheen Color", Color) = (1.0, 0.96, 0.94, 0.42)
        _SheenSpeed ("Sheen Speed", Float) = 0.85
        _SheenWidth ("Sheen Width", Range(0.04, 0.45)) = 0.14
        _PressAmount ("Press Amount", Range(0, 1)) = 0.0
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
            Name "UI_MetallicSheen"

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
                half4 _BaseTop;
                half4 _BaseBottom;
                half4 _SheenColor;
                float _SheenSpeed;
                float _SheenWidth;
                half _PressAmount;
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
                output.color = input.color * _Color;
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
                float maxCut = min(size.x, size.y) * 0.45;
                float majorCut = clamp(_CornerCut, 0.0, maxCut);
                float minorCut = majorCut > 2.0 ? clamp(majorCut * 0.30, 0.0, maxCut * 0.4) : 0.0;

                float d = SignedChamferBox(pos, size, majorCut, minorCut);
                float aa = max(fwidth(d), 0.75);
                half shapeMask = (half)smoothstep(-aa * 0.5, aa * 0.5, d);

                // Tactile 3 px bottom press-lip compresses cleanly when _PressAmount -> 1.
                float lipPx = lerp(3.5, 1.0, (float)_PressAmount);
                float faceT = saturate((pos.y - lipPx) / max(size.y - lipPx, 1.0));
                half3 faceRgb = lerp(_BaseBottom.rgb, _BaseTop.rgb, (half)faceT);
                half baseAlpha = lerp(_BaseBottom.a, _BaseTop.a, (half)faceT);

                // Subtle lateral curvature shading + crisp grounded bottom press-lip.
                half shoulder = 1.0h - 0.06h * pow(abs((half)input.uv.x * 2.0h - 1.0h), 2.2h);
                faceRgb *= shoulder;
                half inLip = 1.0h - (half)smoothstep(lipPx - 0.7, lipPx + 0.7, pos.y);
                faceRgb = lerp(faceRgb, _BaseBottom.rgb * 0.68h, inLip);

                // Crisp 1.2 px top brushed catchlight.
                float topEdge = 1.0 - smoothstep(0.0, 1.6, abs((size.y - pos.y) - 1.2));
                faceRgb = lerp(faceRgb, half3(1.0h, 0.97h, 0.95h), (half)topEdge * 0.42h);

                // Periodic diagonal metallic sheen sweep (freezes at calm architectural accent when ReduceMotion=1).
                float diag = input.uv.x - (input.uv.y - 0.5) * 0.36;
                float width = max(_SheenWidth, 0.04);
                float sheenStrength = 0.0;
                if (_ReduceMotion > 0.5)
                {
                    float staticBand = exp(-pow((diag - 0.32) / (width * 1.15), 2.0)) * 0.30;
                    sheenStrength = staticBand;
                }
                else
                {
                    float cycle = fmod(_Time.y * max(_SheenSpeed, 0.05), 3.4);
                    float sweepCenter = lerp(-0.45, 1.55, saturate(cycle / 1.05));
                    float activeSweep = step(cycle, 1.10);
                    float mainBand = exp(-pow((diag - sweepCenter) / width, 2.0));
                    float twinBand = exp(-pow((diag - (sweepCenter - width * 1.45)) / (width * 0.32), 2.0)) * 0.55;
                    sheenStrength = (mainBand + twinBand) * activeSweep;
                }
                faceRgb = lerp(faceRgb, _SheenColor.rgb, saturate((half)sheenStrength * _SheenColor.a));

                // Tactile press brightness punch (+10% flash on PointerDown).
                faceRgb = saturate(faceRgb * (1.0h + _PressAmount * 0.12h) + half3(0.04h, 0.03h, 0.03h) * _PressAmount);

                // Clean hairline dark rim to anchor over sunlit Manila backgrounds.
                half rimMask = 1.0h - (half)smoothstep(1.0 - aa * 0.5, 1.0 + aa * 0.5, d);
                faceRgb = lerp(faceRgb, _BaseBottom.rgb * 0.45h, rimMask * 0.72h);

                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;
                half4 col = half4(faceRgb * texSample.rgb * input.color.rgb, baseAlpha * shapeMask * texSample.a * input.color.a);

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
