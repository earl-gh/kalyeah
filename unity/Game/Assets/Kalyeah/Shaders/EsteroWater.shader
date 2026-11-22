Shader "Kalyeah/EsteroWater"
{
    Properties
    {
        _ShallowColor ("Shallow Turquoise Color", Color) = (0.34, 0.78, 0.77, 0.78)
        _DeepColor ("Deep Tropical Teal Color", Color) = (0.12, 0.46, 0.52, 0.88)
        _FoamColor ("Shoreline Foam Color", Color) = (0.90, 0.97, 0.97, 0.94)
        _SiltTint ("Shallow Bank Aqua Tint", Color) = (0.48, 0.76, 0.72, 0.72)
        _CreekHalfWidth ("Creek Half Width", Float) = 3.75
        _CurveScale ("Canal Curve Scale", Float) = 1.0
        _FlowSpeed ("Flow Speed", Float) = 0.55
        _SunHotspotPower ("Sun Hotspot Power", Range(8.0, 96.0)) = 44.0
        _SunHotspotStrength ("Sun Hotspot Strength", Range(0.0, 1.5)) = 0.68
        _FresnelColor ("Edge Fresnel Sky Tint", Color) = (0.68, 0.89, 1.00, 1.0)
        _FresnelStrength ("Edge Fresnel Strength", Range(0.0, 0.7)) = 0.30
        _AmbientMin ("Ambient Clamp Min", Range(0.10, 0.60)) = 0.32
        _AmbientMax ("Ambient Clamp Max", Range(0.60, 1.40)) = 1.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _SiltTint;
                half4 _FresnelColor;
                float _CreekHalfWidth;
                float _CurveScale;
                float _FlowSpeed;
                float _SunHotspotPower;
                float _SunHotspotStrength;
                float _FresnelStrength;
                float _AmbientMin;
                float _AmbientMax;
            CBUFFER_END

            // Optional MatchBoot global clarity uniforms (outside UnityPerMaterial to preserve SRP Batcher).
            float _KalyeahAmbientMin;
            float _KalyeahAmbientMax;
            float4 _KalyeahSunDir;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 simXZ      : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            float CanalZ(float x)
            {
                return (2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105)) * _CurveScale;
            }

            float FordMaskAt(float x)
            {
                float xEast0 = 34.5 + 3.8 * sin(0.55);
                float xWest0 = -xEast0;
                float fw = 1.0 - smoothstep(4.6, 8.8, abs(x - xWest0));
                float fc = 1.0 - smoothstep(5.2, 9.6, abs(x));
                float fe = 1.0 - smoothstep(4.6, 8.8, abs(x - xEast0));
                return saturate(max(max(fw, fc), fe));
            }

            #ifndef KALYEAH_QUALITY_LOW
            float GlslMod(float x, float y)
            {
                return x - y * floor(x / y);
            }

            float2 Rot2(float2 v, float c, float s)
            {
                return float2(c * v.x - s * v.y, s * v.x + c * v.y);
            }

            float3 SrgbToLinear3(float3 c)
            {
                return pow(max(c, 1e-5), 2.2);
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash21(i), Hash21(i + float2(1.0, 0.0)), u.x),
                    lerp(Hash21(i + float2(0.0, 1.0)), Hash21(i + float2(1.0, 1.0)), u.x),
                    u.y
                );
            }

            float StrokeFbm(float2 p)
            {
                float v = 0.0;
                v += 0.56 * ValueNoise(p);
                p = Rot2(p, 0.80, -0.60) * 2.07 + float2(7.1, 3.4);
                v += 0.30 * ValueNoise(p);
                p = Rot2(p, 0.80, -0.60) * 2.03 + float2(15.7, 9.2);
                v += 0.14 * ValueNoise(p);
                return v;
            }
            #endif

            Varyings WaterVert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                float3 wp0 = TransformObjectToWorld(posOS);

                // Unity worldZ = -simZ: shift Unity Z by -CanalZ(x) to align with MapLayoutData.CanalZ.
                posOS.z -= CanalZ(wp0.x);
                if (_CurveScale > 0.01)
                {
                    posOS.y += FordMaskAt(wp0.x) * 0.045;
                }
                #ifndef KALYEAH_QUALITY_LOW
                float t = GlslMod(_Time.y, 120.0);
                float swell = sin(wp0.x * 0.88 + t * 1.60) * cos((wp0.z + posOS.z) * 1.35 - t * 1.15) * 0.010;
                posOS.y += swell;
                #endif

                float3 posWS = TransformObjectToWorld(posOS);
                float4 posCS = TransformWorldToHClip(posWS);

                output.positionCS = posCS;
                output.positionWS = posWS;
                output.simXZ = float2(posWS.x, -posWS.z);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posCS.z);
                return output;
            }

            half4 WaterFrag(Varyings input) : SV_TARGET
            {
                float2 simXZ = input.simXZ;
                float edgeNorm = min(input.uv.y, 1.0 - input.uv.y) * 2.0;
                float edgeFeather = smoothstep(0.0, 0.055, edgeNorm);
                if (edgeFeather < 0.02)
                {
                    clip(-1.0);
                }

                float fordMask = FordMaskAt(simXZ.x);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                #ifdef KALYEAH_QUALITY_LOW
                // VALORANT Low water path: clean sunlit turquoise depth gradient + stepped shoreline foam + sky Fresnel.
                float bankDist = max(edgeNorm * _CreekHalfWidth, 0.0);
                float depthT = saturate(bankDist / max(_CreekHalfWidth * 0.65, 0.5));
                float effDepth = depthT * (1.0 - fordMask * 0.44);

                float3 water = lerp(_SiltTint.rgb, _ShallowColor.rgb, smoothstep(0.04, 0.34, effDepth));
                water = lerp(water, _DeepColor.rgb, smoothstep(0.28, 0.88, effDepth));

                // Clean stepped shoreline foam
                float foamBody = 1.0 - smoothstep(0.26, 0.40, bankDist);
                water = lerp(water, _FoamColor.rgb, foamBody * 0.64);

                float3 normalWS = float3(0.0, 1.0, 0.0);
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float shadowAtten = lerp(0.56, 1.0, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float ambMin = (_KalyeahAmbientMin > 0.001) ? max(_KalyeahAmbientMin, _AmbientMin) : _AmbientMin;
                float ambMax = max((_KalyeahAmbientMax > 0.001) ? _KalyeahAmbientMax : _AmbientMax, ambMin + 0.01);
                float3 skyFallback = _FresnelColor.rgb * (ambMin * 1.15);
                float3 ambient = max(clamp(SampleSH(normalWS), ambMin, ambMax), skyFallback);
                float3 irradiance = mainLight.color * (ndotl * shadowAtten) + ambient;

                float ndotv = saturate(dot(normalWS, viewDirWS));
                float f1 = 1.0 - ndotv;
                float fresnel = (f1 * f1 * f1) * _FresnelStrength * (1.0 - foamBody * 0.55);
                float3 litWater = water * lerp(irradiance, max(irradiance, 0.92), foamBody * 0.55) + _FresnelColor.rgb * fresnel;
                litWater = MixFog(litWater, input.fogFactor);

                float baseAlpha = lerp(_ShallowColor.a, _DeepColor.a, effDepth);
                float alpha = clamp((baseAlpha - fordMask * 0.10 + foamBody * 0.12) * edgeFeather, 0.52, 0.88);
                return half4(litWater, alpha);
                #else
                float t = GlslMod(_Time.y, 120.0);
                float centerZ = CanalZ(simXZ.x);
                float localZ = simXZ.y - centerZ;

                float shoreWarp = (StrokeFbm(float2(simXZ.x * 0.30, input.uv.y * 2.6)) - 0.5) * 0.24;
                float bankDist = max(edgeNorm * _CreekHalfWidth + shoreWarp, 0.0);
                float depthT = saturate(bankDist / max(_CreekHalfWidth * 0.66, 0.5));

                float2 flowUvA = float2(simXZ.x * 0.36 - t * _FlowSpeed * 0.38, localZ * 1.25);
                float2 flowUvB = float2(simXZ.x * 0.54 + t * _FlowSpeed * 0.26 + 9.7, localZ * 1.92);
                float s1 = StrokeFbm(flowUvA);
                float s2 = StrokeFbm(flowUvB);
                float brush = s1 * 0.58 + s2 * 0.42;

                // Crisp bright turquoise tropical canal water (linear color space)
                float effDepth = depthT * (1.0 - fordMask * 0.44);
                float3 water = lerp(_SiltTint.rgb, _ShallowColor.rgb, smoothstep(0.03, 0.34, effDepth));
                water = lerp(water, _DeepColor.rgb, smoothstep(0.28, 0.88, effDepth));

                // Luminous sunlit underwater caustic web matching Option A1 Manila canal
                float causticDelta = abs(s1 - s2);
                float causticWeb = (1.0 - smoothstep(0.015, 0.095, causticDelta)) * (0.55 + 0.45 * fordMask) * (1.0 - effDepth * 0.28);
                float causticHighlight = smoothstep(0.58, 0.78, causticWeb);
                float midWave = smoothstep(0.54, 0.68, brush);
                float crestRipple = smoothstep(0.76, 0.86, brush);

                water = lerp(water, SrgbToLinear3(float3(0.42, 0.84, 0.84)), midWave * 0.28);
                water = lerp(water, SrgbToLinear3(float3(0.62, 0.96, 0.94)), causticWeb * 0.46);
                water = lerp(water, _FoamColor.rgb, crestRipple * 0.36);

                // Bridge / crossing ripple rings at West, Center, and East avenues
                float xEast0 = 34.5 + 3.8 * sin(0.55);
                float2 fWestPt = float2(-xEast0, CanalZ(-xEast0));
                float2 fCenterPt = float2(0.0, CanalZ(0.0));
                float2 fEastPt = float2(xEast0, CanalZ(xEast0));
                float dFord = min(length(simXZ - fWestPt), min(length(simXZ - fCenterPt), length(simXZ - fEastPt)));
                if (dFord < 6.2)
                {
                    float ringWave = frac(dFord * 0.78 - t * 0.46);
                    float wadeRing = (1.0 - smoothstep(0.03, 0.12, abs(ringWave - 0.5))) * (1.0 - smoothstep(1.8, 6.0, dFord));
                    water = lerp(water, _FoamColor.rgb, wadeRing * 0.36);
                }

                // Stepped stylized shoreline foam hugging both concrete walls + secondary ripple ribbon
                float scallop = 0.36 + 0.11 * sin(simXZ.x * 1.45 + t * 1.40) + (s1 - 0.5) * 0.18;
                float foamInner = 1.0 - smoothstep(scallop - 0.04, scallop + 0.03, bankDist);
                float foamStep = (1.0 - smoothstep(scallop + 0.12, scallop + 0.20, bankDist)) * (1.0 - foamInner) * 0.48;
                float echoLine = (1.0 - smoothstep(0.02, 0.06, abs(bankDist - (scallop + 0.34)))) * step(0.46, s2) * 0.42;
                float foamMask = saturate(foamInner * 0.86 + foamStep + echoLine);
                water = lerp(water, _FoamColor.rgb, foamMask);

                float3 normalWS = normalize(float3((s1 - 0.5) * 0.16, 1.0, -(s2 - 0.5) * 0.18));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float shadowAtten = lerp(0.54, 1.0, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float ambMin = (_KalyeahAmbientMin > 0.001) ? max(_KalyeahAmbientMin, _AmbientMin) : _AmbientMin;
                float ambMax = max((_KalyeahAmbientMax > 0.001) ? _KalyeahAmbientMax : _AmbientMax, ambMin + 0.01);
                float3 skyFallback = _FresnelColor.rgb * (ambMin * 1.15);
                float3 ambient = max(clamp(SampleSH(normalWS), ambMin, ambMax), skyFallback);
                float3 irradiance = mainLight.color * (ndotl * shadowAtten) + ambient;

                // Sunlit HDR glints (> 1.2 for gentle URP Bloom catch) + crisp sky Fresnel
                float rough = lerp(0.14, 0.52, saturate(foamInner * 0.82 + fordMask * 0.15));
                float invRough = saturate(1.0 - rough);
                float3 sunDir = (_KalyeahSunDir.w > 0.5 && dot(_KalyeahSunDir.xyz, _KalyeahSunDir.xyz) > 0.01)
                    ? normalize(_KalyeahSunDir.xyz)
                    : mainLight.direction;
                float3 reflDir = reflect(-viewDirWS, normalWS);
                float sunAlign = saturate(dot(reflDir, sunDir));
                float ndotv = saturate(dot(normalWS, viewDirWS));
                float f1 = 1.0 - ndotv;
                float fresnel = (f1 * f1 * f1) * _FresnelStrength;
                float sunHotspot = pow(max(sunAlign, 1e-4), lerp(12.0, _SunHotspotPower, invRough)) * smoothstep(0.35, 0.96, sunAlign);
                float hdrGlint = (sunHotspot * _SunHotspotStrength + causticHighlight * crestRipple * 0.58) * shadowAtten * (1.0 - foamMask * 0.72);
                float3 panoRefl = _FresnelColor.rgb * (fresnel * (1.0 - foamMask * 0.60))
                                + mainLight.color * (hdrGlint * 1.48);

                // Keep shoreline foam bright and crisp even in bridge shade
                float3 litWater = water * lerp(irradiance, max(irradiance, 0.95), foamMask * 0.68) + panoRefl;
                litWater = MixFog(litWater, input.fogFactor);

                float baseAlpha = lerp(_ShallowColor.a, _DeepColor.a, effDepth);
                float alpha = clamp((baseAlpha - fordMask * 0.10 + foamInner * 0.14) * edgeFeather, 0.52, 0.92);
                return half4(litWater, alpha);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
