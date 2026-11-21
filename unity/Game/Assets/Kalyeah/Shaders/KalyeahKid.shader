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

        // 1. VALORANT Gradient Lambert (single sun light, stylized 3-zone falloff)
        _DiffShadowColor ("Diffuse Core Shadow Color", Color) = (0.64, 0.50, 0.46, 1.0)
        _DiffMidPos ("Diffuse Mid-Tone Stop", Range(0.15, 0.65)) = 0.44
        _DiffHighPos ("Diffuse Highlight Stop", Range(0.50, 0.95)) = 0.78
        _DiffShadowLift ("Diffuse Core Shadow Lift", Range(0.15, 0.65)) = 0.38

        // 2. Fake Skin SSS (warm red core shadow shift + shadow-side ZBrush wax underglow)
        _SkinWarmShift ("Skin Shadow Warm Shift", Range(0.0, 1.0)) = 0.32
        _SkinUnderglowColor ("Skin Wax Underglow Color", Color) = (0.92, 0.34, 0.22, 1.0)
        _SkinUnderglowStrength ("Skin Wax Underglow Strength", Range(0.0, 1.0)) = 0.28

        // 3. Panoramic Sun-Hotspot & Hemisphere Specular (dropped completely on KALYEAH_QUALITY_LOW)
        _SpecStrength ("Panoramic Specular Strength", Range(0.0, 1.5)) = 0.55
        _SpecSkyColor ("Panoramic Sky Spec Color", Color) = (0.76, 0.88, 1.00, 1.0)
        _SpecGroundColor ("Panoramic Ground Spec Color", Color) = (0.48, 0.42, 0.36, 1.0)
        _SpecSunBoost ("Panoramic Sun Hotspot Boost", Range(0.0, 3.0)) = 1.35
        _SpecIndirectShadow ("Specular Shadow Attenuation", Range(0.0, 1.0)) = 0.22

        // 4. Clamped Ambient Min/Max (fallback when global _KalyeahAmbientMin/_Max are unset)
        _AmbientMin ("Ambient Clamp Min", Range(0.05, 0.65)) = 0.28
        _AmbientMax ("Ambient Clamp Max", Range(0.40, 1.50)) = 0.82

        // 5. Up-Facing & Upper-Body Modulated Friend-or-Foe Fresnel
        _FresnelUpWeight ("Fresnel Up-Facing Weight", Range(0.0, 1.0)) = 0.65
        _FresnelHeightMin ("Fresnel Height Ramp Min", Float) = -0.35
        _FresnelHeightMax ("Fresnel Height Ramp Max", Float) = 0.55
        _FresnelPower ("Fresnel Rim Power", Range(1.2, 5.0)) = 2.35
        _EnemyHighlight ("Enemy Highlight Strength", Range(0.0, 1.5)) = 0.0
        _EnemyHighlightColor ("Enemy Highlight Color", Color) = (0.0, 0.0, 0.0, 0.0)
        _AllyFresnelColor ("Ally Fresnel Color", Color) = (0.58, 0.80, 1.00, 1.0)
        _AllyFresnelStrength ("Ally Fresnel Strength", Range(0.0, 1.0)) = 0.28

        // 6. Clamped Depth Brightening & Distance Fresnel Enlargement (12m..45m)
        _DepthStart ("Depth Clarity Start (m)", Float) = 12.0
        _DepthEnd ("Depth Clarity End (m)", Float) = 45.0
        _DepthBrightenMax ("Depth Brighten Max", Range(0.0, 0.6)) = 0.26
        _DepthFresnelBoost ("Depth Fresnel Boost", Range(0.0, 1.5)) = 0.72

        // Gameplay status feedback & culling
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

        // Identical UnityPerMaterial CBUFFER across ForwardLit, ShadowCaster, and DepthOnly preserves URP SRP Batcher fast path.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _DiffShadowColor;
            half4 _SkinUnderglowColor;
            half4 _SpecSkyColor;
            half4 _SpecGroundColor;
            half4 _EnemyHighlightColor;
            half4 _AllyFresnelColor;
            half4 _AuraColor;
            half _BumpScale;
            half _HasNormalMap;
            half _NormalIsRgb;
            half _HasOrmMap;
            half _Smoothness;
            half _Metallic;
            half _OcclusionStrength;
            half _DiffMidPos;
            half _DiffHighPos;
            half _DiffShadowLift;
            half _SkinWarmShift;
            half _SkinUnderglowStrength;
            half _SpecStrength;
            half _SpecSunBoost;
            half _SpecIndirectShadow;
            half _AmbientMin;
            half _AmbientMax;
            half _FresnelUpWeight;
            float _FresnelHeightMin;
            float _FresnelHeightMax;
            half _FresnelPower;
            half _EnemyHighlight;
            half _AllyFresnelStrength;
            float _DepthStart;
            float _DepthEnd;
            half _DepthBrightenMax;
            half _DepthFresnelBoost;
            half _HitFlash;
            half _TaggedDim;
            float _Cull;
        CBUFFER_END

        // Global clarity uniforms driven by MatchBoot (declared outside UnityPerMaterial so Shader.SetGlobal* works cleanly).
        float _KalyeahAmbientMin;
        float _KalyeahAmbientMax;
        float4 _KalyeahEnemyHighlightColor;
        float4 _KalyeahSunDir;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                #ifndef KALYEAH_QUALITY_LOW
                float4 tangentOS  : TANGENT;
                #endif
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                #ifndef KALYEAH_QUALITY_LOW
                float4 tangentWS  : TEXCOORD3;
                #endif
                float  fogFactor  : TEXCOORD4;
                float  localY     : TEXCOORD5;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings KidVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                #ifdef KALYEAH_QUALITY_LOW
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                #else
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normInputs.normalWS;
                float hasTan = step(0.001, dot(input.tangentOS.xyz, input.tangentOS.xyz));
                float tanSign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = float4(normInputs.tangentWS * hasTan, tanSign);
                #endif

                // Height relative to SkinnedMeshRenderer rootBone (Hips = 0m: feet ~ -0.63m, waist ~ 0m, shoulders/crown ~ +0.55..+0.89m).
                float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                output.localY = posInputs.positionWS.y - originWS.y;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                output.color = input.color;
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

                // Riot Asymmetric Low Scaling: drop normal/ORM sampling and specular completely on KALYEAH_QUALITY_LOW.
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
                half ndotv = saturate((half)dot(normalWS, viewDirWS));

                // Single primary sun light only (zero multi-light Blinn-Phong loops).
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half rawNdotL = (half)dot(normalWS, mainLight.direction);
                half shadowAtten = (half)(mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                // 1. Gradient Lambert 3-zone diffuse (remapped NdotL * 0.5 + 0.5 -> core shadow / mid-tone / highlight).
                half ndotl01 = saturate(rawNdotL * 0.5h + 0.5h);
                half lightCoord = ndotl01 * lerp(0.42h, 1.0h, shadowAtten);
                half midLo = max(0.0h, _DiffMidPos - 0.14h);
                half midHi = min(1.0h, _DiffMidPos + 0.10h);
                half tMid = smoothstep(midLo, max(midHi, midLo + 0.02h), lightCoord);
                half highPos = max(_DiffHighPos, _DiffMidPos + 0.06h);
                half highLo = max(0.0h, highPos - 0.14h);
                half highHi = min(1.0h, highPos + 0.10h);
                half tHigh = smoothstep(highLo, max(highHi, highLo + 0.02h), lightCoord);

                half gradLum = lerp(_DiffShadowLift, 0.78h, tMid);
                gradLum = lerp(gradLum, 1.04h, tHigh);
                half3 gradDiffuse = lerp(_DiffShadowColor.rgb, half3(1.0h, 1.0h, 1.0h), tMid) * gradLum;

                // 2. Fake Skin SSS: zero-extra-texture skin mask (vertex color R when authored, warm-albedo heuristic fallback).
                // Unbound COLOR on Android GLES3/Vulkan defaults to (0,0,0,1) or (1,1,1,1); test RGB only so both fall back cleanly.
                half vcolWhiteDiff = abs(input.color.r - 1.0h) + abs(input.color.g - 1.0h) + abs(input.color.b - 1.0h);
                half vcolZeroSum = input.color.r + input.color.g + input.color.b;
                half isDefaultVCol = max(step(vcolWhiteDiff, 0.02h), step(vcolZeroSum, 0.001h));
                half rgDiff = albedo.r - albedo.g;
                half gbDiff = albedo.g - albedo.b;
                half skinWarmOrder = smoothstep(0.015h, 0.10h, rgDiff) * smoothstep(-0.01h, 0.08h, gbDiff);
                half skinNotTooRed = 1.0h - smoothstep(0.32h, 0.52h, rgDiff);
                half skinLum = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                half skinValBand = smoothstep(0.05h, 0.14h, skinLum) * (1.0h - smoothstep(0.78h, 0.94h, skinLum));
                half skinHeuristic = skinWarmOrder * skinNotTooRed * skinValBand * (1.0h - metallic);
                half skinMask = lerp(saturate(input.color.r), skinHeuristic, isDefaultVCol);

                // Warm red terminator & core shadow shift keeps Filipino skin tones warm and alive in shade.
                half shadowBand = saturate(1.0h - lightCoord);
                half terminatorBand = smoothstep(0.05h, 0.55h, lightCoord) * (1.0h - smoothstep(0.55h, 0.92h, lightCoord));
                half warmFactor = skinMask * _SkinWarmShift * saturate(shadowBand * 0.65h + terminatorBand * 0.85h);
                gradDiffuse *= lerp(half3(1.0h, 1.0h, 1.0h), half3(1.14h, 0.86h, 0.76h), warmFactor);

                half3 directDiffuse = (half3)mainLight.color * gradDiffuse;

                // Grazing-shadow ZBrush wax underglow modulated strictly away from direct sunlight.
                half ndotlSat = saturate(rawNdotL) * shadowAtten;
                half invNdotV = 1.0h - ndotv;
                half waxFactor = (1.0h - ndotlSat) * (invNdotV * invNdotV) * skinMask * _SkinUnderglowStrength;
                half3 skinUnderglow = albedo * _SkinUnderglowColor.rgb * waxFactor;

                // 4. Clamped Ambient Min/Max (characters never vanish in dark corners or blow out in bright sun).
                half ambMin = (_KalyeahAmbientMin > 0.001) ? (half)_KalyeahAmbientMin : _AmbientMin;
                half ambMax = (_KalyeahAmbientMax > 0.001) ? (half)_KalyeahAmbientMax : _AmbientMax;
                ambMax = max(ambMax, ambMin + 0.05h);

                half3 rawSH = max((half3)SampleSH(normalWS), half3(0.0h, 0.0h, 0.0h));
                half shLum = max(dot(rawSH, half3(0.299h, 0.587h, 0.114h)), 0.001h);
                half clampedShLum = clamp(shLum, ambMin, ambMax);
                half3 shColor = (shLum > 0.02h)
                    ? (rawSH * (clampedShLum / shLum))
                    : (half3(0.95h, 0.96h, 1.00h) * ambMin);
                half3 diffuseAmbient = clamp(shColor, half3(ambMin * 0.85h, ambMin * 0.85h, ambMin * 0.85h), half3(ambMax * 1.15h, ambMax * 1.15h, ambMax * 1.15h));
                // Split diffuse vs specular indirect attenuation: diffuse AO stays gentle.
                diffuseAmbient *= lerp(0.82h, 1.0h, ao);

                // 3. Panoramic Sun-Hotspot & Hemisphere Specular (Med/High/Ultra ONLY; dropped on KALYEAH_QUALITY_LOW).
                half3 specular = half3(0.0h, 0.0h, 0.0h);
                #ifndef KALYEAH_QUALITY_LOW
                {
                    float sunLenSq = dot(_KalyeahSunDir.xyz, _KalyeahSunDir.xyz);
                    float3 sunDir = (sunLenSq > 0.001) ? (_KalyeahSunDir.xyz * rsqrt(sunLenSq)) : SafeNormalize(mainLight.direction);
                    float3 reflDir = reflect(-viewDirWS, normalWS);

                    // Inverted roughness as specular control (no separate specular texture).
                    half invRough = saturate(1.0h - roughness);
                    half invRough2 = invRough * invRough;

                    half horizonBlur = max(lerp(0.12h, 0.85h, roughness), 0.05h);
                    half skyT = smoothstep(-horizonBlur, horizonBlur, (half)reflDir.y);
                    half3 panoEnv = lerp(_SpecGroundColor.rgb, _SpecSkyColor.rgb, skyT);

                    half rDotSun = saturate((half)dot(reflDir, sunDir));
                    half sunTightness = lerp(6.0h, 48.0h, invRough2);
                    half sunHotspot = pow(max(rDotSun, 0.0001h), sunTightness) * _SpecSunBoost * lerp(0.25h, 1.0h, invRough);
                    half3 panoSpec = panoEnv * 0.45h + (half3)mainLight.color * sunHotspot;

                    // Specular scales down harder in shaded/occluded areas than diffuse indirect.
                    half specShadowAtten = lerp(_SpecIndirectShadow, 1.0h, shadowAtten * ndotl01);
                    half specAo = ao * ao;
                    half grazingSpec = lerp(0.25h, 1.0h, pow(max(invNdotV, 0.0001h), 3.0h));
                    half3 specTint = lerp(half3(1.0h, 0.99h, 0.96h), albedo, metallic);
                    half specMask = max(invRough2 * 0.65h, metallic * 0.85h) * _SpecStrength;
                    specular = panoSpec * specTint * (specMask * grazingSpec * specShadowAtten * specAo);
                }
                #endif

                half3 lit = albedo * (1.0h - metallic * 0.45h) * (directDiffuse + diffuseAmbient) + skinUnderglow + specular;

                // 6. Clamped Depth Brightening (12m..45m) so distant kids never dissolve into background clutter.
                float depthSpan = max(_DepthEnd - _DepthStart, 1.0);
                half depthT = saturate((half)((camDist - _DepthStart) / depthSpan));
                half depthBrighten = clamp(depthT * _DepthBrightenMax, 0.0h, _DepthBrightenMax);
                lit += (albedo * 0.45h + half3(0.18h, 0.19h, 0.21h)) * depthBrighten;

                // 5. Up-Facing (N.y) & Upper-Body Height Modulated Friend-or-Foe Fresnel + Distance Enlargement.
                float heightMax = max(_FresnelHeightMax, _FresnelHeightMin + 0.05);
                half heightBias = lerp(0.30h, 1.0h, (half)smoothstep(_FresnelHeightMin, heightMax, input.localY));
                half upBias = lerp(1.0h, saturate((half)normalWS.y * 0.5h + 0.5h), _FresnelUpWeight);
                half fresnelMod = upBias * heightBias;

                // Subtle neutral-blue Ally Fresnel (calm separation without competing against enemies).
                if (_AllyFresnelStrength > 0.001h)
                {
                    half allyFresnel = pow(max(invNdotV, 0.0001h), _FresnelPower + 0.55h);
                    half allyMask = saturate(allyFresnel * fresnelMod * _AllyFresnelStrength * (1.0h + depthT * 0.25h));
                    lit += _AllyFresnelColor.rgb * allyMask;
                }

                // Optional emissive map (skip texture fetch when _EmissionColor is black).
                half3 emission = half3(0.0h, 0.0h, 0.0h);
                if (dot(_EmissionColor.rgb, half3(1.0h, 1.0h, 1.0h)) > 0.001h)
                {
                    emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                }

                // Distinct colorblind-safe Enemy Fresnel (Red/Yellow/Purple), widened & boosted at 12m..45m.
                half enemyStrength = max(_EnemyHighlight, _EnemyHighlightColor.a);
                if (enemyStrength > 0.001h)
                {
                    half3 globalEnemyCol = (half3)_KalyeahEnemyHighlightColor.rgb;
                    half3 enemyCol = (dot(_EnemyHighlightColor.rgb, half3(1.0h, 1.0h, 1.0h)) > 0.01h)
                        ? _EnemyHighlightColor.rgb
                        : ((dot(globalEnemyCol, half3(1.0h, 1.0h, 1.0h)) > 0.01h)
                            ? globalEnemyCol
                            : half3(1.0h, 0.22h, 0.24h));

                    half enemyPow = lerp(_FresnelPower, max(1.15h, _FresnelPower * 0.55h), depthT);
                    half enemyFresnel = pow(max(invNdotV, 0.0001h), enemyPow);
                    half distBoost = 1.0h + depthT * _DepthFresnelBoost;
                    half enemyMask = saturate(enemyFresnel * fresnelMod * enemyStrength * distBoost);
                    emission += enemyCol * enemyMask;
                }

                // Merienda buff aura, tagged dim, and crisp hit confirm flash.
                if (_AuraColor.a > 0.001h)
                {
                    half auraRim = (0.28h + 0.72h * (invNdotV * invNdotV)) * _AuraColor.a;
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
                Varyings output = (Varyings)0;
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
                Varyings output = (Varyings)0;
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
