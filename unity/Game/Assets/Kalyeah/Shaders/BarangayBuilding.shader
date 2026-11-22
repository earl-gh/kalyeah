Shader "Kalyeah/BarangayBuilding"
{
    Properties
    {
        [MainColor] _BaseColor ("Wall Stucco Color", Color) = (0.875, 0.824, 0.745, 1.0)
        _PlinthColor ("Concrete Plinth Color", Color) = (0.55, 0.53, 0.49, 1.0)
        _ShutterColor ("Jalousie Wood Shutter Color", Color) = (0.43, 0.31, 0.21, 1.0)
        _TrimColor ("Frieze / Awning Trim Color", Color) = (0.76, 0.72, 0.65, 1.0)
        _RoofColor ("Galvanized Yero Roof Color", Color) = (0.64, 0.68, 0.72, 1.0)
        _SkyFillColor ("Cool Azure Sky Fill", Color) = (0.384, 0.596, 0.847, 1.0)
        _GroundFillColor ("Warm Earth Fill", Color) = (0.490, 0.431, 0.361, 1.0)
        _PlinthHeight ("Plinth Height (m)", Float) = 0.45
        _CanopyStart ("Upper Canopy Height (m)", Float) = 2.40
        _AmbientMin ("Ambient Floor Min", Range(0.15, 0.60)) = 0.28
        _AmbientMax ("Ambient Clamp Max", Range(0.50, 1.40)) = 0.94
        _ShadowFloor ("Shadow Luminance Floor", Range(0.20, 0.65)) = 0.38
        _RoofSpecStrength ("Yero Roof Specular Strength", Range(0.0, 1.0)) = 0.48
        _DetailStrength ("Facade Detail Strength", Range(0.0, 1.0)) = 0.85
        _WindowScale ("Shutter Bay Spacing (m)", Float) = 2.6
        _YeroFreq ("Yero Rib Frequency", Float) = 14.0
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

        // Identical UnityPerMaterial CBUFFER across ForwardLit, ShadowCaster, and DepthOnly preserves SRP Batcher.
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _PlinthColor;
            half4 _ShutterColor;
            half4 _TrimColor;
            half4 _RoofColor;
            half4 _SkyFillColor;
            half4 _GroundFillColor;
            float _PlinthHeight;
            float _CanopyStart;
            half _AmbientMin;
            half _AmbientMax;
            half _ShadowFloor;
            half _RoofSpecStrength;
            half _DetailStrength;
            float _WindowScale;
            float _YeroFreq;
        CBUFFER_END

        // Global clarity uniforms driven by MatchBoot / MatchView (outside UnityPerMaterial for SRP Batcher safety).
        float _KalyeahAmbientMin;
        float _KalyeahAmbientMax;
        float4 _KalyeahSunDir;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BldgVert
            #pragma fragment BldgFrag
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #ifndef KALYEAH_QUALITY_LOW
            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise2(float2 p)
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

            Varyings BldgVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 BldgFrag(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 posWS = input.positionWS;
                float3 normalWS = SafeNormalize(input.normalWS);
                float3 absN = abs(normalWS);

                // Tri-planar horizontal facade axis (zero UVs required on box or procedural meshes)
                float wallU = (absN.x > absN.z) ? posWS.z : posWS.x;
                float h = max(posWS.y, 0.0);

                // Top-surface mask for corrugated galvanized yero metal roofs (N.y > 0.65)
                half roofMask = (half)smoothstep(0.58, 0.72, normalWS.y);

                // ---- Band 1 (0.0 - 0.45 m): Damp concrete plinth & ground contact AO skirt ----
                float plinthH = max(_PlinthHeight, 0.15);
                half plinthMask = (half)(1.0 - smoothstep(plinthH - 0.03, plinthH + 0.03, h));
                half plinthLip = (half)(1.0 - smoothstep(0.015, 0.055, abs(h - plinthH)));
                half groundSkirtAo = (half)lerp(0.74, 1.0, smoothstep(0.0, 0.36, h));

                // ---- Band 2 (0.45 - 2.40 m): Ultra-clean smooth warm Manila stucco (zero eye-level clutter) ----
                half3 stucco = _BaseColor.rgb;
                #ifndef KALYEAH_QUALITY_LOW
                float broadTrowel = ValueNoise2(float2(wallU * 0.24, h * 0.30)) - 0.5;
                float softPlaster = ValueNoise2(float2(wallU * 0.85, h * 0.95)) - 0.5;
                stucco *= (half)(1.0 + (broadTrowel * 0.055 + softPlaster * 0.022) * _DetailStrength);
                #endif

                half3 plinthCol = _PlinthColor.rgb;
                #ifndef KALYEAH_QUALITY_LOW
                float plinthSeam = 1.0 - smoothstep(0.012, 0.032, abs(frac(wallU * 0.42) - 0.5));
                plinthCol *= (half)(1.0 - plinthSeam * 0.14 * _DetailStrength);
                #endif

                half3 wallCol = lerp(stucco, plinthCol, plinthMask);
                wallCol = lerp(wallCol, _TrimColor.rgb * 1.05h, plinthLip * 0.65h);
                wallCol *= groundSkirtAo;

                // ---- Band 3 (> 2.40 m): Awning ledge shadow, frieze band, recessed wooden jalousie shutters ----
                float canopyY = max(_CanopyStart, plinthH + 0.8);
                half awningDropShadow = (half)(smoothstep(canopyY - 0.34, canopyY - 0.04, h) * (1.0 - smoothstep(canopyY - 0.02, canopyY + 0.04, h)));
                wallCol *= (1.0h - awningDropShadow * 0.22h);

                half friezeBand = (half)(smoothstep(canopyY - 0.02, canopyY + 0.03, h) * (1.0 - smoothstep(canopyY + 0.16, canopyY + 0.22, h)));
                wallCol = lerp(wallCol, _TrimColor.rgb, friezeBand * 0.82h);

                half wallRoughness = 0.86h;
                float3 shadeNormalWS = normalWS;

                // Recessed wooden jalousie shutter bays strictly above _CanopyStart + 0.25 m (never inside combat band!)
                if (h > canopyY + 0.22 && roofMask < 0.98h)
                {
                    float baySpacing = max(_WindowScale, 1.4);
                    float bayU = abs(frac(wallU / baySpacing + 0.5) - 0.5) * baySpacing;
                    float localStoryH = fmod(max(h - (canopyY + 0.25), 0.0), 2.75);

                    float winHalfW = baySpacing * 0.26;
                    float winBot = 0.16;
                    float winTop = 1.62;
                    float inWinX = 1.0 - smoothstep(winHalfW - 0.04, winHalfW + 0.04, bayU);
                    float inWinY = smoothstep(winBot - 0.04, winBot + 0.04, localStoryH) * (1.0 - smoothstep(winTop - 0.04, winTop + 0.04, localStoryH));
                    float winMask = inWinX * inWinY;

                    // Outer stucco/wood frame + sunlit sill highlight
                    float frameHalfW = winHalfW + 0.11;
                    float inFrameX = 1.0 - smoothstep(frameHalfW - 0.03, frameHalfW + 0.03, bayU);
                    float inFrameY = smoothstep(winBot - 0.11, winBot - 0.05, localStoryH) * (1.0 - smoothstep(winTop + 0.05, winTop + 0.11, localStoryH));
                    float frameMask = saturate(inFrameX * inFrameY - winMask);
                    float sillMask = inFrameX * (1.0 - smoothstep(0.02, 0.07, abs(localStoryH - (winBot - 0.05))));

                    half3 shutterCol = _ShutterColor.rgb;
                    #ifndef KALYEAH_QUALITY_LOW
                    // Crisp horizontal louvered jalousie slats + central vertical mullion
                    float slats = 0.5 + 0.5 * sin(localStoryH * 38.0);
                    float mullion = 1.0 - smoothstep(0.03, 0.06, bayU);
                    float recessAo = lerp(0.70, 1.0, smoothstep(winTop, winTop - 0.28, localStoryH));
                    shutterCol *= (half)(lerp(0.82, 1.08, slats) * recessAo);
                    shutterCol = lerp(shutterCol, _ShutterColor.rgb * 0.72h, (half)(mullion * 0.75 * _DetailStrength));
                    #endif

                    wallCol = lerp(wallCol, _TrimColor.rgb * 0.92h, (half)(frameMask * 0.75 * _DetailStrength));
                    wallCol = lerp(wallCol, _TrimColor.rgb * 1.12h, (half)(sillMask * 0.72 * _DetailStrength));
                    wallCol = lerp(wallCol, shutterCol, (half)(winMask * 0.92 * _DetailStrength));
                    wallRoughness = lerp(wallRoughness, 0.62h, (half)(winMask * 0.75));
                }

                // ---- Top faces (N.y > 0.65): Corrugated galvanized yero metal ribs ----
                half3 roofCol = _RoofColor.rgb;
                half roofRoughness = 0.34h;
                if (roofMask > 0.01h)
                {
                    float ribCoord = posWS.x * _YeroFreq;
                    #ifndef KALYEAH_QUALITY_LOW
                    float ribWave = sin(ribCoord);
                    float ribSlope = cos(ribCoord);
                    float panelSeam = 1.0 - smoothstep(0.015, 0.045, abs(frac(posWS.z * 0.48) - 0.5));
                    float roofVar = ValueNoise2(posWS.xz * 0.25) - 0.5;
                    roofCol *= (half)(lerp(0.87, 1.09, ribWave * 0.5 + 0.5) + roofVar * 0.06 * _DetailStrength);
                    roofCol *= (half)(1.0 - panelSeam * 0.16 * _DetailStrength);
                    float3 ribNormal = SafeNormalize(normalWS + float3(ribSlope * 0.16 * _DetailStrength, 0.0, 0.0));
                    shadeNormalWS = SafeNormalize(lerp(normalWS, ribNormal, (float)roofMask));
                    #else
                    float ribWaveLow = sin(ribCoord) * 0.5 + 0.5;
                    roofCol *= (half)lerp(0.92, 1.06, ribWaveLow);
                    #endif
                }

                half3 albedo = lerp(wallCol, roofCol, roofMask);
                half roughness = lerp(wallRoughness, roofRoughness, roofMask);

                // ---- 10:30 AM Sunlit Manila Lighting + Anti-Black-Wall Hemisphere Floor ----
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light mainLight = GetMainLight(shadowCoord);
                half rawNdotL = (half)dot(shadeNormalWS, mainLight.direction);
                half ndotl = saturate(rawNdotL);
                // Soft wrapped diffuse contribution keeps shaded architectural corners sculpted without pitch-black faces
                half wrapped = saturate(rawNdotL * 0.5h + 0.5h);
                half shadowAtten = lerp(_ShadowFloor, 1.0h, (half)(mainLight.shadowAttenuation * mainLight.distanceAttenuation));
                half3 directDiffuse = (half3)mainLight.color * (lerp(ndotl, wrapped * 0.72h, 0.18h) * shadowAtten);

                // Built-in _KalyeahAmbientMin >= 0.28 + cool azure sky (#6298D8) / warm earth (#7D6E5C) hemisphere fallback
                half ambMin = (_KalyeahAmbientMin > 0.001) ? max((half)_KalyeahAmbientMin, _AmbientMin) : _AmbientMin;
                ambMin = max(ambMin, 0.28h);
                half ambMax = (_KalyeahAmbientMax > 0.001) ? (half)_KalyeahAmbientMax : _AmbientMax;
                ambMax = max(ambMax, ambMin + 0.08h);

                half hemiT = saturate((half)shadeNormalWS.y * 0.5h + 0.5h);
                half3 hemiFallback = lerp(_GroundFillColor.rgb, _SkyFillColor.rgb, hemiT) * (ambMin * 1.35h);

                half3 rawSH = max((half3)SampleSH(shadeNormalWS), half3(0.0h, 0.0h, 0.0h));
                half shLum = dot(rawSH, half3(0.299h, 0.587h, 0.114h));
                half shValid = smoothstep(0.02h, 0.10h, shLum);
                half safeShLum = max(shLum, 0.001h);
                half clampedShLum = clamp(safeShLum, ambMin, ambMax);
                half3 shCol = rawSH * (clampedShLum / safeShLum);
                half3 ambient = lerp(hemiFallback, max(shCol, hemiFallback * 0.92h), shValid);
                ambient = clamp(ambient, half3(ambMin * 0.85h, ambMin * 0.85h, ambMin * 0.85h), half3(ambMax, ambMax, ambMax));

                half3 additionalDiffuse = half3(0.0h, 0.0h, 0.0h);
                #ifdef _ADDITIONAL_LIGHTS
                uint addCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < addCount; ++li)
                {
                    Light addL = GetAdditionalLight(li, posWS);
                    half addNdotL = saturate((half)dot(shadeNormalWS, addL.direction));
                    additionalDiffuse += (half3)addL.color * (addNdotL * (half)(addL.distanceAttenuation * addL.shadowAttenuation) * 0.65h);
                }
                #endif

                // Galvanized yero roof sky sheen & sun specular highlight (stripped on Low for mobile speed)
                half3 specular = half3(0.0h, 0.0h, 0.0h);
                #ifndef KALYEAH_QUALITY_LOW
                if (roofMask > 0.02h)
                {
                    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                    float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                    half ndoth = saturate((half)dot(shadeNormalWS, halfDir));
                    half ndotv = saturate((half)dot(shadeNormalWS, viewDirWS));
                    half fresnel = pow(max(1.0h - ndotv, 0.0001h), 3.0h);
                    half specPow = lerp(12.0h, 42.0h, 1.0h - roughness);
                    half sunSpec = pow(max(ndoth, 0.0001h), specPow) * ndotl * shadowAtten;
                    specular = ((half3)mainLight.color * sunSpec * 0.85h + _SkyFillColor.rgb * (0.38h + fresnel * 0.62h) * 0.35h)
                        * (_RoofSpecStrength * roofMask);
                }
                #endif

                half3 lit = albedo * (directDiffuse + ambient + additionalDiffuse) + specular;
                lit = MixFog(lit, input.fogFactor);
                return half4(lit, 1.0h);
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
            Cull Back

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
            Cull Back

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

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
