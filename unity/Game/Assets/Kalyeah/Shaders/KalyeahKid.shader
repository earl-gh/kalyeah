Shader "Kalyeah/Kid"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0.0, 2.0)) = 1.0
        _HasNormalMap ("Has Normal Map", Float) = 0.0
        _NormalIsRgb ("Normal Is OpenGL RGB", Float) = 0.0
        [NoScaleOffset] _OrmMap ("ORM Map (R=AO G=Rough B=Metal)", 2D) = "white" {}
        _HasOrmMap ("Has ORM Map", Float) = 0.0
        _Smoothness ("Default Smoothness", Range(0.0, 1.0)) = 0.22
        _Metallic ("Default Metallic", Range(0.0, 1.0)) = 0.0
        _OcclusionStrength ("AO Strength", Range(0.0, 1.0)) = 0.55
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 0.0, 0.0, 1.0)

        // Valorant x Fortnite soft-painted shading & rim separation
        _WrapAmount ("Diffuse Wrap", Range(0.0, 0.5)) = 0.28
        _ShadowFloor ("Shadow Luminance Floor", Range(0.20, 0.65)) = 0.44
        _RimColor ("Back Rim Color", Color) = (0.94, 0.97, 1.0, 1.0)
        _RimStrength ("Back Rim Strength", Range(0.0, 1.0)) = 0.32
        _RimPower ("Back Rim Power", Range(1.5, 6.0)) = 3.2

        // Enemy highlight (red / yellow / purple colorblind-safe, brighter at distance) + status feedback
        _EnemyHighlightColor ("Enemy Highlight Color", Color) = (0.0, 0.0, 0.0, 0.0)
        _EnemyHighlightDistBoost ("Enemy Highlight Distance Boost", Range(0.0, 1.0)) = 0.45
        _HitFlash ("Hit Flash", Range(0.0, 1.0)) = 0.0
        _AuraColor ("Merienda Aura Color", Color) = (0.0, 0.0, 0.0, 0.0)
        _TaggedDim ("Tagged Fade", Range(0.0, 1.0)) = 0.0
        _Cull ("Cull Mode", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_OrmMap);
        SAMPLER(sampler_OrmMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        // Identical UnityPerMaterial CBUFFER across all passes preserves URP SRP Batcher fast path.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _RimColor;
            half4 _EnemyHighlightColor;
            half4 _AuraColor;
            half _BumpScale;
            half _HasNormalMap;
            half _NormalIsRgb;
            half _HasOrmMap;
            half _Smoothness;
            half _Metallic;
            half _OcclusionStrength;
            half _WrapAmount;
            half _ShadowFloor;
            half _RimStrength;
            half _RimPower;
            half _EnemyHighlightDistBoost;
            half _HitFlash;
            half _TaggedDim;
            float _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex KidVert
            #pragma fragment KidFrag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float fogFactor   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings KidVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normInputs.normalWS;

                float hasTan = step(0.001, dot(input.tangentOS.xyz, input.tangentOS.xyz));
                float tanSign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(normInputs.tangentWS * hasTan, tanSign);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 KidFrag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedoSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoSample.rgb * _BaseColor.rgb;

                float3 normalWS = SafeNormalize(input.normalWS);
                normalWS = IS_FRONT_VFACE(facing, normalWS, -normalWS);

                half ao = 1.0h;
                half roughness = saturate(1.0h - _Smoothness);
                half metallic = _Metallic;

                #ifndef KALYEAH_QUALITY_LOW
                if (_HasNormalMap > 0.5h && dot(input.tangentWS.xyz, input.tangentWS.xyz) > 0.001)
                {
                    half4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS;
                    if (_NormalIsRgb > 0.5h)
                    {
                        half2 xy = (nSample.rg * 2.0h - 1.0h) * _BumpScale;
                        half z = max(nSample.b * 2.0h - 1.0h, 0.05h);
                        normalTS = normalize(half3(xy, z));
                    }
                    else
                    {
                        normalTS = UnpackNormalScale(nSample, _BumpScale);
                    }
                    float3 tangentWS = SafeNormalize(input.tangentWS.xyz);
                    float3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
                    normalWS = SafeNormalize(
                        tangentWS * normalTS.x +
                        bitangentWS * normalTS.y +
                        normalWS * normalTS.z);
                }

                if (_HasOrmMap > 0.5h)
                {
                    half3 orm = SAMPLE_TEXTURE2D(_OrmMap, sampler_OrmMap, input.uv).rgb;
                    ao = lerp(1.0h, saturate(orm.r), _OcclusionStrength);
                    roughness = clamp(orm.g, 0.16h, 0.96h);
                    metallic = saturate(orm.b);
                }
                #endif

                float3 toCam = GetCameraPositionWS() - input.positionWS;
                float camDist = max(length(toCam), 0.1);
                float3 viewDirWS = toCam / camDist;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Soft wrapped stylized diffuse with warm bounce lift (preserves warm true Filipino skin tones in shade).
                half rawNdotL = (half)dot(normalWS, mainLight.direction);
                half wrappedDiffuse = saturate((rawNdotL + _WrapAmount) / (1.0h + _WrapAmount));
                half shadowAtten = (half)(mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half liftedShadow = lerp(_ShadowFloor, 1.0h, shadowAtten);
                half3 warmShadowTint = lerp(half3(0.97h, 0.92h, 0.89h), half3(1.0h, 1.0h, 1.0h), shadowAtten);

                half3 ambient = (half3)SampleSH(normalWS) * lerp(0.76h, 1.0h, ao);
                half3 directDiffuse = (half3)mainLight.color * (wrappedDiffuse * liftedShadow * warmShadowTint);

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light addLight = GetAdditionalLight(li, input.positionWS);
                    half addNdotL = saturate(((half)dot(normalWS, addLight.direction) + _WrapAmount * 0.5h) / (1.0h + _WrapAmount * 0.5h));
                    directDiffuse += (half3)addLight.color * (addNdotL * (half)(addLight.distanceAttenuation * addLight.shadowAttenuation));
                }
                #endif

                // Stylized soft PBR specular (clean sheen on metal/hair/plastic, matte skin/fabric).
                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half ndoth = saturate((half)dot(normalWS, halfDir));
                half smoothness = saturate(1.0h - roughness);
                half specPow = exp2(lerp(2.4h, 7.6h, smoothness));
                half specStrength = lerp(0.06h, 0.42h, max(smoothness * 0.65h, metallic));
                half3 specTint = lerp(half3(1.0h, 0.98h, 0.95h), albedo, metallic);
                half3 specular = (half3)mainLight.color * specTint * (pow(ndoth, specPow) * specStrength * saturate(rawNdotL + 0.15h) * liftedShadow);

                half3 lit = albedo * (1.0h - metallic * 0.45h) * (directDiffuse + ambient) + specular;

                // Subtle Valorant-style back/sky rim light for clean separation against shaded interiors & walls.
                half ndotv = saturate((half)dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - ndotv, _RimPower);
                half backWeight = saturate(0.55h + 0.45h * (1.0h - saturate(rawNdotL * shadowAtten)));
                half upWeight = saturate((half)normalWS.y * 0.35h + 0.70h);
                lit += _RimColor.rgb * (fresnel * _RimStrength * backWeight * upWeight);

                // Emission + Merienda buff aura + Enemy Fresnel highlight (slightly brighter at distance) + Hit flash.
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                if (_EnemyHighlightColor.a > 0.001h)
                {
                    half distFactor = 1.0h + _EnemyHighlightDistBoost * (half)smoothstep(10.0, 42.0, camDist);
                    half enemyRim = pow(1.0h - ndotv, 2.2h) * _EnemyHighlightColor.a * distFactor;
                    emission += _EnemyHighlightColor.rgb * saturate(enemyRim);
                }
                if (_AuraColor.a > 0.001h)
                {
                    half auraRim = (0.28h + 0.72h * pow(1.0h - ndotv, 2.0h)) * _AuraColor.a;
                    lit = lerp(lit, lit + _AuraColor.rgb * 0.32h, saturate(auraRim));
                }
                if (_TaggedDim > 0.001h)
                {
                    half lum = dot(lit, half3(0.299h, 0.587h, 0.114h));
                    half3 desaturated = lerp(lit, half3(lum, lum, lum) * half3(0.90h, 0.92h, 0.98h), 0.38h * _TaggedDim);
                    lit = lerp(lit, desaturated * 0.82h, _TaggedDim);
                }
                if (_HitFlash > 0.001h)
                {
                    lit = lerp(lit, half3(1.0h, 0.96h, 0.86h), saturate(_HitFlash * 0.65h));
                    emission += half3(0.85h, 0.80h, 0.68h) * (_HitFlash * 0.55h);
                }

                half3 finalRgb = MixFog(lit + emission, input.fogFactor);
                return half4(finalRgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_LightPosition - posWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDirWS));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = posCS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
