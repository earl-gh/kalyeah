Shader "Kalyeah/TropicalSky"
{
    Properties
    {
        _SkyZenith ("Sky Zenith", Color) = (0.24, 0.49, 0.80, 1.0)
        _SkyMid ("Sky Mid", Color) = (0.52, 0.72, 0.89, 1.0)
        _SkyHorizon ("Sky Horizon", Color) = (0.93, 0.84, 0.72, 1.0)
        _HorizonGlow ("Horizon Glow", Color) = (0.97, 0.89, 0.76, 1.0)
        _GroundColor ("Ground Bounce", Color) = (0.38, 0.33, 0.28, 1.0)
        _CloudLit ("Cloud Lit Crown", Color) = (0.98, 0.94, 0.86, 1.0)
        _CloudMid ("Cloud Mid", Color) = (0.88, 0.79, 0.70, 1.0)
        _CloudShade ("Cloud Belly Shade", Color) = (0.64, 0.62, 0.69, 1.0)
        _CloudDriftSpeed ("Cloud Drift Speed", Float) = 0.0042
        _Exposure ("Exposure", Range(0.5, 2.0)) = 1.0
        _SunDiscSize ("Sun Disc Sharpness", Range(0.990, 0.9995)) = 0.9978
        _HorizonClarityBand ("Horizon Clarity Band", Range(0.05, 0.45)) = 0.24
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "TropicalSky"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyZenith;
                half4 _SkyMid;
                half4 _SkyHorizon;
                half4 _HorizonGlow;
                half4 _GroundColor;
                half4 _CloudLit;
                half4 _CloudMid;
                half4 _CloudShade;
                float _CloudDriftSpeed;
                float _Exposure;
                float _SunDiscSize;
                float _HorizonClarityBand;
            CBUFFER_END

            // Optional MatchBoot global sun vector (outside UnityPerMaterial to preserve SRP Batcher).
            float4 _KalyeahSunDir;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rayDir     : TEXCOORD0;
            };

            float3 LinearToSrgb3(float3 c)
            {
                return pow(max(c, 1e-5), 1.0 / 2.2);
            }

            float3 SrgbToLinear3(float3 c)
            {
                return pow(max(c, 1e-5), 2.2);
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

            float Hash2(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash2(i), Hash2(i + float2(1.0, 0.0)), u.x),
                    lerp(Hash2(i + float2(0.0, 1.0)), Hash2(i + float2(1.0, 1.0)), u.x),
                    u.y
                );
            }

            float CloudFbm(float2 p)
            {
                float v = 0.0;
                v += 0.54 * Noise2(p);
                p = Rot2(p, 0.80, -0.60) * 2.09 + float2(5.7, 2.3);
                v += 0.30 * Noise2(p);
                p = Rot2(p, 0.80, -0.60) * 2.13 + float2(13.1, 8.9);
                v += 0.16 * Noise2(p);
                return v;
            }

            float SafeAtan2(float y, float x)
            {
                return atan2(y, abs(x) < 1e-5 ? (x >= 0.0 ? 1e-5 : -1e-5) : x);
            }
            #endif

            Varyings SkyVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.rayDir = input.positionOS.xyz;
                return output;
            }

            half4 SkyFrag(Varyings input) : SV_TARGET
            {
                float3 dir = normalize(input.rayDir);
                float up = saturate(dir.y);

                // Blend serene painterly dome in perceptual sRGB space, then encode to Linear at exit.
                float3 zenith = LinearToSrgb3(_SkyZenith.rgb);
                float3 mid = LinearToSrgb3(_SkyMid.rgb);
                float3 horizon = LinearToSrgb3(_SkyHorizon.rgb);
                float3 glow = LinearToSrgb3(_HorizonGlow.rgb);
                float3 ground = LinearToSrgb3(_GroundColor.rgb);

                float3 skyCol = lerp(glow, horizon, smoothstep(0.0, 0.13, up));
                skyCol = lerp(skyCol, mid, smoothstep(0.09, 0.40, up));
                skyCol = lerp(skyCol, zenith, smoothstep(0.34, 0.90, up));

                // Match directional sun from MatchBoot _KalyeahSunDir / URP _MainLightPosition (fallback: SE afternoon sun)
                float3 sunDir = normalize(float3(0.412, 0.743, -0.527));
                if (_KalyeahSunDir.w > 0.5 && dot(_KalyeahSunDir.xyz, _KalyeahSunDir.xyz) > 0.01)
                {
                    sunDir = normalize(_KalyeahSunDir.xyz);
                }
                else if (dot(_MainLightPosition.xyz, _MainLightPosition.xyz) > 0.01)
                {
                    sunDir = normalize(_MainLightPosition.xyz);
                }
                float sunDot = max(dot(dir, sunDir), 0.0);

                // Golden-hour atmospheric scatter & crisp tropical sun disc (preserved across all 4 tiers)
                float discOuter = min(_SunDiscSize, 0.9995);
                float discInner = min(discOuter + 0.0017, 0.9999);
                float sunDotSafe = max(sunDot, 1e-4);
                skyCol += float3(1.00, 0.85, 0.56) * pow(sunDotSafe, 5.5) * 0.22 * (1.0 - smoothstep(0.0, 0.78, up) * 0.38);
                skyCol += float3(1.00, 0.94, 0.80) * smoothstep(discOuter, discInner, sunDot) * 0.74;

                #ifndef KALYEAH_QUALITY_LOW
                // Med/High/Ultra: keep 0-25 deg shoulder throw band serene via _HorizonClarityBand, clouds above
                float drift = GlslMod(_Time.y * _CloudDriftSpeed, 100.0);
                float3 cloudLit = LinearToSrgb3(_CloudLit.rgb);
                float3 cloudMid = LinearToSrgb3(_CloudMid.rgb);
                float3 cloudShade = LinearToSrgb3(_CloudShade.rgb);

                float skyStroke = Noise2(float2(SafeAtan2(-dir.z, dir.x) * 3.8, up * 14.0));
                skyCol *= 0.985 + 0.028 * skyStroke * smoothstep(0.04, max(_HorizonClarityBand, 0.08), up);

                float bandStart = max(0.04, _HorizonClarityBand * 0.48);
                if (dir.y > bandStart)
                {
                    float bandEnd = max(bandStart + 0.10, _HorizonClarityBand);
                    float fadeHorizon = smoothstep(bandStart, bandEnd, dir.y);
                    float2 simDirXZ = float2(dir.x, -dir.z);
                    float2 domeUv = simDirXZ / (dir.y + 0.28);

                    // Layer 1: High windswept golden cirrus streaks
                    float2 streakUv = domeUv * float2(0.78, 2.05) + float2(drift * 0.60, -drift * 0.20);
                    float streakN = CloudFbm(streakUv + float2(Noise2(streakUv * 1.6) * 0.28, 0.0));
                    float streakMask = smoothstep(0.61, 0.81, streakN) * fadeHorizon * (1.0 - smoothstep(0.46, 0.86, up));
                    float3 streakCol = lerp(cloudMid, cloudLit, 0.52 + 0.48 * (sunDot * sunDot));
                    skyCol = lerp(skyCol, streakCol, streakMask * 0.34);

                    // Layer 2: Sculpted painterly cumulus clouds above shoulder-horizon clarity band
                    float2 baseUv = domeUv * 1.08 + float2(drift, drift * 0.32);
                    float warp = CloudFbm(baseUv * 1.48 + float2(3.4, 9.1));
                    float2 brushUv = baseUv + float2(warp - 0.5, (warp - 0.5) * 0.60) * 0.36;
                    float density = CloudFbm(brushUv);
                    float mask = smoothstep(0.51, 0.73, density) * fadeHorizon;

                    float2 sunSimXZ = float2(sunDir.x, -sunDir.z);
                    float2 sunStep = normalize(sunSimXZ + float2(0.001, 0.001)) * 0.11 + float2(0.0, 0.07);
                    float litSample = CloudFbm(brushUv + sunStep);
                    float crown = saturate((density - litSample) * 2.25 + smoothstep(0.52, 0.80, density) * 0.66 + pow(sunDotSafe, 2.4) * 0.24);
                    float paintedStep = lerp(crown, floor(crown * 6.0 + 0.5) / 6.0, 0.18);

                    float3 cCol = lerp(cloudShade, cloudMid, smoothstep(0.14, 0.50, paintedStep));
                    cCol = lerp(cCol, cloudLit, smoothstep(0.48, 0.84, paintedStep));
                    float bellyBand = (1.0 - smoothstep(0.51, 0.61, density)) * mask;
                    cCol = lerp(cCol, cloudShade * 0.93, bellyBand * 0.32);

                    skyCol = lerp(skyCol, cCol, mask * 0.82);
                }
                #endif

                if (dir.y < 0.0)
                {
                    skyCol = lerp(horizon * 0.90, ground, smoothstep(0.0, 0.22, -dir.y));
                }

                float3 linearOut = SrgbToLinear3(saturate(skyCol)) * _Exposure;
                return half4(linearOut, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
