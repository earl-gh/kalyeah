Shader "Kalyeah/UI_SoftGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow Color", Color) = (1.0, 0.608, 0.416, 0.70)
        _Softness ("Falloff Softness", Range(1.0, 6.0)) = 2.4
        _PulseSpeed ("Pulse Speed", Float) = 2.6
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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "UI_SoftGlow"

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
                half4 _GlowColor;
                float _Softness;
                float _PulseSpeed;
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
                float falloff = pow(max(saturate(1.0 - r), 1e-5), max(_Softness, 1.0)) * step(r, 0.999);
                float core = pow(max(saturate(1.0 - r * 1.45), 1e-5), max(_Softness * 1.35, 1.5)) * step(r * 1.45, 0.999) * 0.45;
                float edgeClamp = 1.0 - smoothstep(0.88, 1.0, r);

                float pulse = (_PulseSpeed > 0.001 && _ReduceMotion < 0.5)
                    ? (0.82 + 0.18 * sin(_Time.y * _PulseSpeed))
                    : 1.0;

                half intensity = (half)saturate((falloff + core) * edgeClamp * pulse);
                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;
                half3 rgb = _GlowColor.rgb * texSample.rgb * input.color.rgb;
                half alpha = _GlowColor.a * intensity * texSample.a * input.color.a;
                half4 col = half4(rgb, alpha);

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
