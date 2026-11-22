Shader "Kalyeah/BarangayGround"
{
    Properties
    {
        [MainColor] _BaseColor ("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _ArenaRadius ("Arena Radius", Float) = 84.0
        _ArenaHalf ("Arena Half", Vector) = (87.0, 87.0, 0.0, 0.0)
        _OwnBaseCenter ("Own Base Center", Vector) = (0.0, 64.0, 0.0, 0.0)
        _EnemyBaseCenter ("Enemy Base Center", Vector) = (0.0, -64.0, 0.0, 0.0)
        _Wear ("Wear", Range(0.0, 1.0)) = 1.0
        _AlbedoScale ("Albedo Scale", Range(0.5, 1.5)) = 1.0
        _ShadowFloor ("Shadow Luminance Floor", Range(0.15, 0.55)) = 0.34
        _AmbientMin ("Ambient Clamp Min", Range(0.05, 0.50)) = 0.28
        _AmbientMax ("Ambient Clamp Max", Range(0.50, 1.40)) = 0.94
        _DetailStrength ("Surface Detail Strength", Range(0.0, 1.0)) = 0.72
        _SpecStrength ("Road Specular Strength", Range(0.0, 0.35)) = 0.14
        _CarveCreekSlot ("Carve Creek Slot", Float) = 0.0
        _IsCreekBed ("Is Creek Bed", Float) = 0.0
        _ParcelCount ("Parcel Count", Float) = 0.0
        [NoScaleOffset] _ParcelAoTex ("Baked Parcel AO LUT", 2D) = "white" {}
        _HasParcelAoTex ("Has Baked Parcel AO LUT", Float) = 0.0
        _ParcelAoBounds ("Parcel AO Bounds (minX, minZ, invW, invH)", Vector) = (-100.0, -100.0, 0.005, 0.005)
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

        TEXTURE2D(_ParcelAoTex);
        SAMPLER(sampler_ParcelAoTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _ArenaHalf;
            float4 _OwnBaseCenter;
            float4 _EnemyBaseCenter;
            float4 _ParcelAoBounds;
            float _ArenaRadius;
            float _Wear;
            float _AlbedoScale;
            float _ShadowFloor;
            float _AmbientMin;
            float _AmbientMax;
            float _DetailStrength;
            float _SpecStrength;
            float _CarveCreekSlot;
            float _IsCreekBed;
            float _ParcelCount;
            float _HasParcelAoTex;
            float4 _ParcelPose[32];
            float4 _ParcelHalf[32];
        CBUFFER_END

        // Sim ground space uses (simX, simZ) = (worldWS.x, -worldWS.z) per GodotSpace.cs.
        float CanalZ(float x)
        {
            return 2.4 * sin(x * 0.046) + 0.8 * sin(x * 0.105);
        }

        float LaneCenterX(float z)
        {
            float taper = 1.0 - smoothstep(50.0, 62.0, abs(z));
            return 4.2 * sin(z * 0.048) * taper;
        }

        float LaneEastX(float z)
        {
            float taper = 1.0 - smoothstep(56.0, 74.0, abs(z));
            return 34.5 + (3.8 * sin(z * 0.052 + 0.55) - 0.055 * abs(z)) * taper;
        }

        float LaneWestX(float z)
        {
            return -LaneEastX(-z);
        }

        float EskinitaSouthZ(float x)
        {
            return 28.0 + 3.0 * sin(x * 0.058);
        }

        float EskinitaNorthZ(float x)
        {
            return -EskinitaSouthZ(-x);
        }

        float LaneCenterHw(float z)
        {
            float az = abs(z);
            float d1 = (az - 28.0) / 8.5;
            float d2 = z / 8.5;
            float d3 = (az - 62.0) / 10.0;
            return 4.45 + 2.15 * exp(-d1 * d1) + 1.85 * exp(-d2 * d2) + 2.40 * exp(-d3 * d3);
        }

        float LaneSideHw(float z)
        {
            float az = abs(z);
            float d1 = (az - 28.0) / 9.0;
            float d2 = z / 8.5;
            return 4.25 + 1.95 * exp(-d1 * d1) + 1.65 * exp(-d2 * d2);
        }

        float EskinitaHw(float x)
        {
            float ax = abs(x);
            float d1 = ax / 9.0;
            float d2 = (ax - 34.5) / 8.5;
            return 3.55 + 1.55 * exp(-d1 * d1) + 1.45 * exp(-d2 * d2);
        }

        float FordProximity(float x)
        {
            float fw = 1.0 - smoothstep(4.6, 8.8, abs(x - LaneWestX(0.0)));
            float fc = 1.0 - smoothstep(5.2, 9.6, abs(x));
            float fe = 1.0 - smoothstep(4.6, 8.8, abs(x - LaneEastX(0.0)));
            return saturate(max(max(fw, fc), fe));
        }

        void DisplaceCreekVertex(inout float3 posOS, inout float3 normalOS)
        {
            if (_IsCreekBed > 0.5)
            {
                float3 wp0 = TransformObjectToWorld(posOS);
                float localZ = posOS.z;
                float absLocalZ = abs(localZ);
                // Unity worldZ = -simZ, so shifting simZ by +CanalZ(x) shifts Unity Z by -CanalZ(x).
                posOS.z -= CanalZ(wp0.x);
                float profile = 1.0 - smoothstep(2.35, 4.55, absLocalZ);
                float targetDepth = lerp(-0.30, -0.09, FordProximity(wp0.x));
                posOS.y += targetDepth * profile;
                float dzSlope = sign(localZ) * smoothstep(2.35, 4.55, absLocalZ) * (1.0 - smoothstep(4.45, 4.65, absLocalZ)) * (-targetDepth * 0.42);
                normalOS = normalize(float3(0.0, 1.0, -dzSlope));
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex GroundVert
            #pragma fragment GroundFrag
            #pragma multi_compile _ KALYEAH_QUALITY_LOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Optional MatchBoot global ambient clamps (outside UnityPerMaterial to preserve SRP Batcher).
            float _KalyeahAmbientMin;
            float _KalyeahAmbientMax;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 simXZ      : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            float3 SrgbToLinear3(float3 c)
            {
                return pow(max(c, 1e-5), 2.2);
            }

            float BoxSdf(float2 p, float2 halfExt, float radius)
            {
                float2 q = abs(p) - halfExt + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
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

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash12(i);
                float b = Hash12(i + float2(1.0, 0.0));
                float c = Hash12(i + float2(0.0, 1.0));
                float d = Hash12(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FbmOrganic(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    v += amp * VNoise(p);
                    p = Rot2(p, 0.80, -0.60) * 2.03 + float2(19.7, 31.3);
                    amp *= 0.5;
                }
                return v;
            }

            float SafeAtan2(float y, float x)
            {
                return atan2(y, abs(x) < 1e-5 ? (x >= 0.0 ? 1e-5 : -1e-5) : x);
            }
            #endif

            Varyings GroundVert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                DisplaceCreekVertex(posOS, nrmOS);

                float3 posWS = TransformObjectToWorld(posOS);
                float4 posCS = TransformWorldToHClip(posWS);

                output.positionCS = posCS;
                output.positionWS = posWS;
                output.normalWS = TransformObjectToWorldNormal(nrmOS);
                output.simXZ = float2(posWS.x, -posWS.z);
                output.fogFactor = ComputeFogFactor(posCS.z);
                return output;
            }

            half4 GroundFrag(Varyings input) : SV_TARGET
            {
                float2 xz = input.simXZ;
                #ifndef KALYEAH_QUALITY_LOW
                // Compute derivatives before non-uniform clip() for Vulkan/GLES3/D3D11 safety.
                float pxFw = min(length(ddx(xz)), length(ddy(xz)));
                float detailVis = (1.0 - smoothstep(0.055, 0.20, pxFw)) * _DetailStrength;
                #endif

                float cz = CanalZ(xz.x);
                if (_CarveCreekSlot > 0.5 && abs(xz.x) < _ArenaRadius + 3.0 && abs(xz.y - cz) < 4.55)
                {
                    clip(-1.0);
                }
                if (_IsCreekBed > 0.5 && abs(xz.y - cz) > 4.62)
                {
                    clip(-1.0);
                }

                float rDist = length(xz);

                // ---- Curved street carriageways & signed distance to curb (identical across all 4 tiers) ----
                float xc = LaneCenterX(xz.y);
                float xw = LaneWestX(xz.y);
                float xe = LaneEastX(xz.y);
                float zs = EskinitaSouthZ(xz.x);
                float zn = EskinitaNorthZ(xz.x);

                float hwc = LaneCenterHw(xz.y);
                float hws = LaneSideHw(xz.y);
                float hwe = EskinitaHw(xz.x);

                float dcEnd = max(abs(xz.y) - 67.0, 0.0);
                float dCenter = max(abs(xz.x - xc) - hwc, dcEnd);

                float dsEnd = max(abs(xz.y) - 80.0, 0.0);
                float dWest = max(abs(xz.x - xw) - hws, dsEnd);
                float dEast = max(abs(xz.x - xe) - hws, dsEnd);

                float deEnd = max(abs(xz.x) - 80.0, 0.0);
                float dEskS = max(abs(xz.y - zs) - hwe, deEnd);
                float dEskN = max(abs(xz.y - zn) - hwe, deEnd);

                float zConnS = 54.5 + 2.2 * cos(xz.x * 0.062);
                float zConnN = -54.5 - 2.2 * cos(xz.x * 0.062);
                float connEnd = max(abs(xz.x) - 40.0, 0.0);
                float dConn = min(
                    max(abs(xz.y - zConnS) - 4.2, connEnd),
                    max(abs(xz.y - zConnN) - 4.2, connEnd)
                );

                float dNs = min(dCenter, min(dWest, dEast));
                float dEw = min(min(dEskS, dEskN), dConn);
                float cornerK = 2.8;
                float hFillet = max(cornerK - abs(dNs - dEw), 0.0) / cornerK;
                float dRoad = min(dNs, dEw) - hFillet * hFillet * cornerK * 0.25;

                float2 jSw = float2(LaneWestX(28.0), EskinitaSouthZ(-31.0));
                float2 jSc = float2(LaneCenterX(28.0), EskinitaSouthZ(4.0));
                float2 jSe = float2(LaneEastX(28.0), EskinitaSouthZ(36.0));
                float2 jNw = -jSe;
                float2 jNc = -jSc;
                float2 jNe = -jSw;
                float dJunc = min(
                    min(length(xz - jSw), min(length(xz - jSc), length(xz - jSe))),
                    min(length(xz - jNw), min(length(xz - jNc), length(xz - jNe)))
                );

                float swMask = (1.0 - smoothstep(2.45, 2.95, dRoad)) * smoothstep(-0.12, 0.08, dRoad);
                float roadMask = 1.0 - smoothstep(-0.08, 0.08, dRoad);

                #ifdef KALYEAH_QUALITY_LOW
                // VALORANT aggressive Low environment scaling: strip all multi-octave noise, speckles, cracks,
                // bitumen patches, and specular — keep ultra-fast clean warm matte diffuse + crisp lane/landmark reads.
                float3 col = lerp(float3(0.31, 0.27, 0.21), float3(0.25, 0.30, 0.20), smoothstep(28.0, 62.0, rDist) * 0.40);
                col = lerp(col, float3(0.34, 0.32, 0.29), swMask);

                float3 asph = float3(0.25, 0.24, 0.23);
                float cobbleApron = (1.0 - smoothstep(4.8, 7.6, dJunc)) * roadMask;
                asph = lerp(asph, float3(0.31, 0.25, 0.21), cobbleApron * 0.72);

                // Crisp warm-ochre dashed lane centerlines
                float clearJunc = smoothstep(7.2, 9.8, dJunc) * smoothstep(8.2, 10.5, abs(xz.y - cz));
                float dashPat = smoothstep(0.34, 0.40, frac(xz.y * 0.14)) * (1.0 - smoothstep(0.76, 0.82, frac(xz.y * 0.14)));
                float centerStripe =
                    (1.0 - smoothstep(0.09, 0.17, abs(xz.x - xc))) * (1.0 - smoothstep(45.5, 48.0, abs(xz.y))) +
                    (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xw))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y))) +
                    (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xe))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y)));
                asph = lerp(asph, float3(0.45, 0.36, 0.20), saturate(centerStripe) * dashPat * clearJunc * 0.68);

                // Calm cream zebra crosswalks (exact same lane callouts as High)
                float dXwalkEskS = abs(abs(xz.y - zs) - 6.35);
                float dXwalkEskN = abs(abs(xz.y - zn) - 6.35);
                float dXwalkFord = abs(abs(xz.y - cz) - 7.35);
                float dXwalkPlaza = abs(abs(xz.y) - 48.6);
                float dNsXwalk = min(min(dXwalkEskS, dXwalkEskN), min(dXwalkFord, dXwalkPlaza));
                float xwalkBandNs = 1.0 - smoothstep(1.08, 1.26, dNsXwalk);
                float onNsCarriageway = 1.0 - smoothstep(-0.72, -0.28, dNs);
                float localLaneU = xz.x - xc;
                if (abs(xz.x - xw) < abs(localLaneU)) localLaneU = xz.x - xw;
                if (abs(xz.x - xe) < abs(localLaneU)) localLaneU = xz.x - xe;
                float zebraU = frac(localLaneU * 0.92 + 0.25);
                float zebraBarNs = smoothstep(0.20, 0.26, zebraU) * (1.0 - smoothstep(0.74, 0.80, zebraU));
                asph = lerp(asph, float3(0.45, 0.43, 0.38), saturate(xwalkBandNs * zebraBarNs * onNsCarriageway) * 0.68);

                // Clean crisp carriageway white edge lines (Option A1 Manila street clarity, capped below AoE pop threshold)
                float edgeLineLow = smoothstep(-0.56, -0.46, dRoad) * (1.0 - smoothstep(-0.36, -0.26, dRoad)) * clearJunc;
                asph = lerp(asph, float3(0.46, 0.45, 0.42), edgeLineLow * 0.72);

                col = lerp(col, asph, roadMask);

                // Clean curb lip
                float curbLip = smoothstep(-0.03, 0.05, dRoad) * (1.0 - smoothstep(0.20, 0.34, dRoad));
                col = lerp(col, float3(0.39, 0.37, 0.33), curbLip * 0.68);

                // Sub-0.5s South warm terracotta vs North cool sage-cream plaza orientation
                float2 ownBase = _OwnBaseCenter.xy;
                float ownD = length(xz - ownBase);
                float ownPlazaMask = 1.0 - smoothstep(12.6, 14.8, ownD);
                if (ownPlazaMask > 0.004)
                {
                    float2 lo = xz - ownBase;
                    float3 ownStone = lerp(float3(0.37, 0.32, 0.26), float3(0.34, 0.26, 0.21), step(6.2, ownD) * step(ownD, 9.4));
                    float insideBase = 1.0 - smoothstep(9.4, 11.0, ownD);
                    float lineO = max(
                        (1.0 - smoothstep(0.05, 0.12, abs(lo.y))) * step(abs(lo.x), 8.2),
                        1.0 - smoothstep(0.05, 0.12, abs(ownD - 2.3))
                    ) * insideBase;
                    ownStone = lerp(ownStone, float3(0.45, 0.42, 0.37), saturate(lineO) * 0.56);
                    col = lerp(col, ownStone, ownPlazaMask);
                }

                float2 enemyBase = _EnemyBaseCenter.xy;
                float hallD = length(xz - enemyBase);
                float hallMask = 1.0 - smoothstep(12.6, 14.8, hallD);
                if (hallMask > 0.004)
                {
                    float3 plazaStone = lerp(float3(0.33, 0.36, 0.35), float3(0.26, 0.33, 0.32), step(6.2, hallD) * step(hallD, 9.4));
                    col = lerp(col, plazaStone, hallMask);
                }

                // Clean Estero bank silt / ford tint
                int parcelCount = min((int)(_ParcelCount + 0.5), 32);
                bool creekActive = (_IsCreekBed > 0.5) || (_CarveCreekSlot > 0.5) || (parcelCount > 0) || (_HasParcelAoTex > 0.5);
                if (creekActive)
                {
                    float canalAbs = abs(xz.y - cz);
                    float bankZone = 1.0 - smoothstep(3.4, 6.6, canalAbs);
                    float insideWall = (_ArenaRadius > 1.0) ? (1.0 - smoothstep(_ArenaRadius - 0.5, _ArenaRadius + 2.8, rDist)) : 1.0;
                    if (bankZone * insideWall > 0.004)
                    {
                        float fordProx = FordProximity(xz.x);
                        float3 bedCol = lerp(float3(0.25, 0.25, 0.20), float3(0.31, 0.30, 0.27), max(fordProx * 0.85, 1.0 - smoothstep(2.4, 3.8, canalAbs)));
                        col = lerp(col, bedCol, bankZone * insideWall * 0.90);
                    }
                }

                // Soft building contact AO: 1-fetch baked 256x256 LUT when bound, graceful OBB fallback when unbound
                if (_HasParcelAoTex > 0.5)
                {
                    float2 aoUv = (xz - _ParcelAoBounds.xy) * _ParcelAoBounds.zw;
                    float inLut = step(0.0, aoUv.x) * step(aoUv.x, 1.0) * step(0.0, aoUv.y) * step(aoUv.y, 1.0);
                    half4 aoLut = SAMPLE_TEXTURE2D_LOD(_ParcelAoTex, sampler_ParcelAoTex, saturate(aoUv), 0);
                    col *= lerp(1.0, (float)aoLut.r, inLut);
                }
                else if (parcelCount > 0)
                {
                    float minBldgD = 999.0;
                    [loop]
                    for (int i = 0; i < 32; i++)
                    {
                        if (i >= parcelCount) break;
                        float4 pose = _ParcelPose[i];
                        float2 halfE = _ParcelHalf[i].xy;
                        float2 rel = xz - pose.xy;
                        float2 localP = float2(rel.x * pose.z - rel.y * pose.w, rel.x * pose.w + rel.y * pose.z);
                        float2 q = abs(localP) - halfE;
                        float dObb = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                        minBldgD = min(minBldgD, dObb);
                    }
                    col *= lerp(0.64, 1.0, smoothstep(-0.05, 1.35, minBldgD));
                }

                if (_ArenaRadius > 1.0)
                {
                    float rimDelta = rDist - _ArenaRadius;
                    float coping = 1.0 - smoothstep(0.32, 0.82, abs(rimDelta));
                    col = lerp(col, float3(0.37, 0.34, 0.30), coping * 0.55);
                }

                col = clamp(col, 0.18, 0.48);

                // Diffuse-only matte lighting on Low (Riot rule: drop specular on matte Low environments)
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float shadowAtten = lerp(_ShadowFloor, 1.0, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float ambMin = (_KalyeahAmbientMin > 0.001) ? max(_KalyeahAmbientMin, _AmbientMin) : _AmbientMin;
                float ambMax = max((_KalyeahAmbientMax > 0.001) ? _KalyeahAmbientMax : _AmbientMax, ambMin + 0.01);
                float3 hemiFallback = float3(0.384, 0.596, 0.847) * (ambMin * 1.15);
                float3 ambient = max(clamp(SampleSH(normalWS), ambMin, ambMax), hemiFallback);
                float3 diffuseLight = mainLight.color * (ndotl * shadowAtten) + ambient;

                float3 baseLinear = SrgbToLinear3(col) * _BaseColor.rgb * _AlbedoScale;
                float3 litColor = MixFog(baseLinear * diffuseLight, input.fogFactor);
                return half4(litColor, 1.0);
                #else
                float2 warp = float2(
                    FbmOrganic(xz * 0.042 + float2(3.1, 7.4)),
                    FbmOrganic(xz * 0.042 + float2(18.6, -9.2))
                ) - 0.5;
                float macroVar = FbmOrganic(xz * 0.052 + warp * 1.8);
                float midVar = FbmOrganic(xz * 0.23 + warp * 0.9);
                float grit = VNoise(xz * 2.35 + warp * 2.2);
                float gravelChip = VNoise(xz * 6.80 + warp * 1.5);
                float finePebble = VNoise(xz * 15.60 - warp * 1.1);

                // ---- Band 1 Ground Discipline (0.22-0.38 warm mid-dark, calm Valorant low-noise paint) ----
                // 1. Base provincial laterite earth & muted tropical berm grass
                float3 earthWarm = float3(0.28, 0.24, 0.19);
                float3 earthDust = float3(0.35, 0.30, 0.24);
                float3 cloverPatch = float3(0.23, 0.30, 0.19);
                float3 col = lerp(earthWarm, earthDust, smoothstep(0.26, 0.76, macroVar));
                col += ((midVar - 0.5) * 0.022 + (grit - 0.5) * 0.012) * _DetailStrength + (gravelChip - 0.5) * 0.010 * detailVis;
                float grassM = smoothstep(0.56, 0.75, FbmOrganic(xz * 0.14 + float2(11.2, -5.7))) * smoothstep(18.0, 48.0, rDist);
                col = lerp(col, cloverPatch, grassM * 0.52);
                float rough = 0.93;

                // 3. Calm poured-concrete sidewalks (0.31-0.37)
                float3 swBase = lerp(float3(0.32, 0.30, 0.27), float3(0.37, 0.35, 0.31), smoothstep(0.22, 0.78, macroVar));
                swBase += (midVar - 0.5) * 0.018 * _DetailStrength + (grit - 0.5) * 0.012 * detailVis;
                float jointAlong = abs(frac((xz.x * 0.19 + xz.y * 0.23) + warp.x * 0.42) - 0.5);
                float jointSeam = 1.0 - smoothstep(0.008, 0.024, jointAlong);
                swBase = lerp(swBase, float3(0.23, 0.22, 0.20), jointSeam * 0.40);
                col = lerp(col, swBase, swMask);
                rough = lerp(rough, 0.88, swMask);

                // 4. Calm painted barangay asphalt carriageways (0.22-0.29, minimal micro-grit so kids/VFX pop)
                float3 asphDark = float3(0.22, 0.22, 0.23);
                float3 asphWarm = float3(0.28, 0.26, 0.24);
                float3 asph = lerp(asphDark, asphWarm, smoothstep(0.18, 0.82, macroVar));
                float aggStipple = (grit - 0.5) * 0.014 * _DetailStrength + ((gravelChip - 0.5) * 0.014 + (finePebble - 0.5) * 0.010) * detailVis;
                asph += aggStipple;

                // DPWH bitumen utility-trench patches
                float2 patchUv = Rot2(xz, 0.985, 0.174);
                float2 patchCell = floor(patchUv / float2(9.5, 13.5));
                float patchSeed = Hash12(patchCell + float2(7.1, 13.3));
                if (patchSeed > 0.60)
                {
                    float2 patchLocal = (frac(patchUv / float2(9.5, 13.5)) - 0.5) * float2(9.5, 13.5);
                    float2 patchOff = float2(Hash12(patchCell + 2.1) - 0.5, Hash12(patchCell + 5.7) - 0.5) * float2(2.6, 3.4);
                    float2 patchSz = float2(1.4 + 1.3 * Hash12(patchCell + 9.2), 2.2 + 2.4 * Hash12(patchCell + 4.4));
                    float pSdf = BoxSdf(patchLocal - patchOff, patchSz, 0.16);
                    float pFill = 1.0 - smoothstep(-0.04, 0.05, pSdf);
                    float pTarRim = 1.0 - smoothstep(0.012, 0.042, abs(pSdf));
                    asph = lerp(asph, float3(0.20, 0.20, 0.21), pFill * 0.48 * _Wear * _DetailStrength);
                    asph = lerp(asph, float3(0.15, 0.15, 0.16), pTarRim * 0.55 * _Wear * _DetailStrength);
                }

                // Dual tire-wear ruts & subtle center crown
                float offC = abs(abs(xz.x - xc) - 1.55);
                float offW = abs(abs(xz.x - xw) - 1.45);
                float offE = abs(abs(xz.x - xe) - 1.45);
                float offEs = abs(abs(xz.y - zs) - 1.25);
                float offEn = abs(abs(xz.y - zn) - 1.25);
                float minWheel = min(min(offC, min(offW, offE)), min(offEs, offEn));
                float tireTrack = (1.0 - smoothstep(0.28, 1.08, minWheel)) * _Wear;
                float laneCrown = (1.0 - smoothstep(0.25, 0.95, min(abs(xz.x - xc), min(abs(xz.x - xw), abs(xz.x - xe))))) * _Wear;
                asph = lerp(asph, float3(0.19, 0.19, 0.20), tireTrack * 0.30 * _DetailStrength);
                asph += float3(0.014, 0.013, 0.011) * laneCrown * _DetailStrength;

                // Subtle thin tar repair cracks
                float2 crackUvA = xz * 0.58 + warp * 0.65;
                float crackF0 = FbmOrganic(crackUvA);
                float crackA = abs(crackF0 - 0.50);
                float crackGrad = abs(FbmOrganic(crackUvA + float2(0.09, 0.06)) - crackF0);
                float antiBlob = smoothstep(0.010, 0.028, crackGrad);
                float tarPrimary = (1.0 - smoothstep(0.0020, 0.0075, crackA)) * antiBlob * smoothstep(0.34, 0.66, macroVar);
                asph = lerp(asph, float3(0.15, 0.15, 0.16), saturate(tarPrimary) * 0.52 * _Wear * _DetailStrength);

                // 5. Terracotta/ochre brick-cobble intersection aprons, manholes & calm zebra crosswalks
                float cobbleApron = (1.0 - smoothstep(4.8, 7.6, dJunc)) * roadMask;
                if (cobbleApron > 0.005)
                {
                    float2 cp = Rot2(xz, 0.94, 0.34) * float2(1.85, 3.40);
                    float crow = floor(cp.y);
                    cp.x += GlslMod(crow, 2.0) * 0.5;
                    float2 cid = floor(cp);
                    float2 cf = frac(cp);
                    float cMortar = max(1.0 - smoothstep(0.05, 0.15, cf.x), 1.0 - smoothstep(0.07, 0.19, cf.y));
                    float cTone = Hash12(cid);
                    float3 brickCol = lerp(float3(0.28, 0.22, 0.19), float3(0.36, 0.29, 0.23), cTone);
                    brickCol = lerp(brickCol, float3(0.18, 0.17, 0.16), cMortar * 0.48 * _DetailStrength);
                    asph = lerp(asph, brickCol, cobbleApron * 0.85);
                }

                // Warm-ochre dashed lane centerlines
                float clearJunc = smoothstep(7.2, 9.8, dJunc) * smoothstep(8.2, 10.5, abs(xz.y - cz));
                float dashPat = smoothstep(0.34, 0.40, frac(xz.y * 0.14)) * (1.0 - smoothstep(0.76, 0.82, frac(xz.y * 0.14)));
                float centerStripe =
                    (1.0 - smoothstep(0.09, 0.17, abs(xz.x - xc))) * (1.0 - smoothstep(45.5, 48.0, abs(xz.y))) +
                    (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xw))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y))) +
                    (1.0 - smoothstep(0.08, 0.16, abs(xz.x - xe))) * (1.0 - smoothstep(64.0, 70.0, abs(xz.y)));
                float chipPaint = clamp(0.68 + (grit - 0.4) * 0.30 * _DetailStrength, 0.46, 0.92);
                asph = lerp(asph, float3(0.45, 0.36, 0.20), saturate(centerStripe) * dashPat * clearJunc * chipPaint * 0.78);

                // Worn-cream zebra crosswalks (capped so white Rescue timers & AoE rims pop!)
                float dXwalkEskS = abs(abs(xz.y - zs) - 6.35);
                float dXwalkEskN = abs(abs(xz.y - zn) - 6.35);
                float dXwalkFord = abs(abs(xz.y - cz) - 7.35);
                float dXwalkPlaza = abs(abs(xz.y) - 48.6);
                float dNsXwalk = min(min(dXwalkEskS, dXwalkEskN), min(dXwalkFord, dXwalkPlaza));

                float xwalkBandNs = 1.0 - smoothstep(1.08, 1.26, dNsXwalk);
                float stopBarNs = 1.0 - smoothstep(0.08, 0.16, abs(dNsXwalk - 1.58));
                float onNsCarriageway = 1.0 - smoothstep(-0.72, -0.28, dNs);

                float localLaneU = xz.x - xc;
                if (abs(xz.x - xw) < abs(localLaneU)) localLaneU = xz.x - xw;
                if (abs(xz.x - xe) < abs(localLaneU)) localLaneU = xz.x - xe;
                float zebraU = frac(localLaneU * 0.92 + 0.25);
                float zebraBarNs = smoothstep(0.20, 0.26, zebraU) * (1.0 - smoothstep(0.74, 0.80, zebraU));

                float dEwXwalk = min(
                    abs(abs(xz.x - xc) - (hwc + 2.05)),
                    min(abs(abs(xz.x - xw) - (hws + 1.95)), abs(abs(xz.x - xe) - (hws + 1.95)))
                );
                float onEwEskinita = 1.0 - smoothstep(-0.68, -0.24, min(dEskS, dEskN));
                float localEskV = (abs(xz.y - zs) < abs(xz.y - zn)) ? (xz.y - zs) : (xz.y - zn);
                float zebraV = frac(localEskV * 0.92 + 0.25);
                float zebraBarEw = smoothstep(0.20, 0.26, zebraV) * (1.0 - smoothstep(0.74, 0.80, zebraV));
                float xwalkBandEw = (1.0 - smoothstep(1.02, 1.20, dEwXwalk)) * onEwEskinita;

                float zebraWear = clamp(0.76 + (grit - 0.42) * 0.25 * _DetailStrength - tireTrack * 0.18, 0.50, 0.92);
                float xwalkPaint = max(
                    max(xwalkBandNs * zebraBarNs, stopBarNs * 0.80) * onNsCarriageway,
                    xwalkBandEw * zebraBarEw
                ) * zebraWear;
                float3 xwalkCol = float3(0.46, 0.44, 0.39);
                asph = lerp(asph, xwalkCol, saturate(xwalkPaint) * 0.80);

                // Cast-iron DPWH storm-manhole lids
                float2 mhRel = xz - (jSc + float2(-1.35, 1.45));
                float2 mhCand = xz - (jSw + float2(1.45, -1.55));
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - (jSe + float2(-1.45, -1.50));
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - (jNw + float2(1.45, 1.50));
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - (jNc + float2(1.35, -1.45));
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - (jNe + float2(-1.45, 1.55));
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - float2(LaneCenterX(16.4) - 1.15, 16.4);
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;
                mhCand = xz - float2(LaneCenterX(-16.4) + 1.15, -16.4);
                if (dot(mhCand, mhCand) < dot(mhRel, mhRel)) mhRel = mhCand;

                float dMh = length(mhRel);
                float mhMask = 0.0;
                if (dMh < 0.66 && roadMask > 0.5)
                {
                    float collarM = (1.0 - smoothstep(0.58, 0.64, dMh)) * smoothstep(0.46, 0.50, dMh);
                    float lidM = 1.0 - smoothstep(0.45, 0.48, dMh);
                    float rings = smoothstep(-0.2, 0.65, cos(dMh * 44.0));
                    float ribs = smoothstep(-0.1, 0.70, cos(SafeAtan2(mhRel.y, mhRel.x) * 8.0)) * step(0.11, dMh);
                    float3 ironLid = lerp(float3(0.18, 0.17, 0.17), float3(0.29, 0.26, 0.23), max(rings, ribs) * 0.75);
                    asph = lerp(asph, float3(0.31, 0.29, 0.26), collarM * 0.80);
                    asph = lerp(asph, ironLid, lidM * 0.90);
                    mhMask = lidM;
                }

                float gutterBand = smoothstep(-0.54, -0.16, dRoad) * (1.0 - smoothstep(-0.01, 0.07, dRoad));
                asph = lerp(asph, float3(0.17, 0.17, 0.18), gutterBand * 0.58);

                // Crisp painted white carriageway edge stripes (matching Option A1 Manila street clarity)
                float edgeLine = smoothstep(-0.62, -0.52, dRoad) * (1.0 - smoothstep(-0.40, -0.30, dRoad)) * clearJunc;
                asph = lerp(asph, float3(0.48, 0.47, 0.44), edgeLine * chipPaint * 0.78);

                col = lerp(col, asph, roadMask);
                float roadRough = lerp(0.89, 0.74, saturate(tireTrack * 0.40 + gutterBand * 0.35 + mhMask * 0.55));
                rough = lerp(rough, roadRough, roadMask);

                // Beveled concrete curb lip
                float curbLip = smoothstep(-0.03, 0.05, dRoad) * (1.0 - smoothstep(0.20, 0.34, dRoad));
                float curbAo = smoothstep(-0.18, -0.01, dRoad) * (1.0 - smoothstep(0.0, 0.09, dRoad));
                col = lerp(col, float3(0.39, 0.37, 0.33), curbLip * 0.72);
                col *= 1.0 - curbAo * 0.20;

                // 6. South Covered Court Plaza (warm ochre/terracotta) vs North Barangay Hall Plaza (cool sage/cream)
                float2 ownBase = _OwnBaseCenter.xy;
                float ownD = length(xz - ownBase);
                float ownPlazaMask = 1.0 - smoothstep(12.6, 14.8, ownD);
                if (ownPlazaMask > 0.004)
                {
                    float2 lo = xz - ownBase;
                    float radRingO = abs(frac(ownD * 0.42 + warp.x * 0.12) - 0.5);
                    float spokeO = abs(frac(SafeAtan2(lo.y, lo.x) * 3.8197) - 0.5);
                    float jointO = max(1.0 - smoothstep(0.03, 0.08, radRingO), 1.0 - smoothstep(0.02, 0.06, spokeO));
                    float3 ownStone = lerp(float3(0.37, 0.32, 0.26), float3(0.34, 0.26, 0.21), step(6.2, ownD) * step(ownD, 9.4));
                    ownStone = lerp(ownStone, float3(0.22, 0.20, 0.18), jointO * 0.38);
                    float insideBase = 1.0 - smoothstep(9.4, 11.0, ownD);
                    float keyO = 1.0 - smoothstep(-0.08, 0.08, min(
                        BoxSdf(lo - float2(0.0, 7.2), float2(2.4, 2.8), 0.15),
                        BoxSdf(lo - float2(0.0, -7.2), float2(2.4, 2.8), 0.15)
                    ));
                    ownStone = lerp(ownStone, float3(0.38, 0.26, 0.20), keyO * insideBase * 0.42 * chipPaint);
                    float lineO = max(
                        (1.0 - smoothstep(0.05, 0.12, abs(lo.y))) * step(abs(lo.x), 8.2),
                        1.0 - smoothstep(0.05, 0.12, abs(ownD - 2.3))
                    ) * insideBase;
                    ownStone = lerp(ownStone, float3(0.46, 0.43, 0.38), saturate(lineO) * chipPaint * 0.62);
                    col = lerp(col, ownStone, ownPlazaMask);
                    rough = lerp(rough, 0.86, ownPlazaMask);
                }

                float lmMask = 0.0;
                if (_ArenaRadius > 1.0)
                {
                    float2 courtLandmarkC = float2(ownBase.x, _ArenaRadius + 10.2);
                    float lmSdf = BoxSdf(xz - courtLandmarkC, float2(7.4, 7.2), 2.8);
                    lmMask = (1.0 - smoothstep(-0.25, 1.15, lmSdf)) * (1.0 - smoothstep(96.2, 98.0, abs(xz.y)));
                    if (lmMask > 0.004)
                    {
                        float3 lmSlab = lerp(float3(0.34, 0.30, 0.25), float3(0.38, 0.33, 0.28), midVar);
                        col = lerp(col, lmSlab, lmMask * 0.82);
                        rough = lerp(rough, 0.86, lmMask);
                    }
                }

                float2 enemyBase = _EnemyBaseCenter.xy;
                float hallD = length(xz - enemyBase);
                float hallMask = 1.0 - smoothstep(12.6, 14.8, hallD);
                if (hallMask > 0.004)
                {
                    float2 lh = xz - enemyBase;
                    float radRing = abs(frac(hallD * 0.42 + warp.x * 0.12) - 0.5);
                    float spoke = abs(frac(SafeAtan2(lh.y, lh.x) * 3.8197) - 0.5);
                    float pJoint = max(1.0 - smoothstep(0.03, 0.08, radRing), 1.0 - smoothstep(0.02, 0.06, spoke));
                    // Cool sage-slate / cream civic stone temperature for < 0.5 s North orientation (no team blue)
                    float3 plazaStone = lerp(float3(0.33, 0.36, 0.35), float3(0.26, 0.33, 0.32), step(6.2, hallD) * step(hallD, 9.4));
                    plazaStone = lerp(plazaStone, float3(0.20, 0.22, 0.22), pJoint * 0.38);
                    col = lerp(col, plazaStone, hallMask);
                    rough = lerp(rough, 0.85, hallMask);
                }

                // 7. Sunken Estero riverbank silt & cobble fords
                int parcelCount = min((int)(_ParcelCount + 0.5), 32);
                bool creekActive = (_IsCreekBed > 0.5) || (_CarveCreekSlot > 0.5) || (parcelCount > 0) || (_HasParcelAoTex > 0.5);
                if (creekActive)
                {
                    float canalAbs = abs(xz.y - cz + warp.y * 0.38);
                    float bankZone = 1.0 - smoothstep(3.4, 6.8, canalAbs);
                    float insideWall = (_ArenaRadius > 1.0) ? (1.0 - smoothstep(_ArenaRadius - 0.5, _ArenaRadius + 2.8, rDist)) : 1.0;
                    float outerSwale = (_ArenaRadius > 1.0) ? ((1.0 - insideWall) * (1.0 - smoothstep(_ArenaRadius + 4.0, _ArenaRadius + 24.0, rDist))) : 0.0;
                    if (bankZone > 0.004)
                    {
                        if (insideWall > 0.004)
                        {
                            float fordProx = FordProximity(xz.x);
                            float3 bankSilt = lerp(float3(0.26, 0.25, 0.20), float3(0.22, 0.27, 0.20), smoothstep(0.40, 0.76, midVar));
                            float3 riverCobble = lerp(float3(0.27, 0.26, 0.24), float3(0.35, 0.33, 0.29), finePebble);
                            float3 bedCol = lerp(bankSilt, riverCobble, max(fordProx * 0.88, 1.0 - smoothstep(2.4, 3.8, canalAbs)));
                            float wetDark = 1.0 - smoothstep(2.8, 4.4, canalAbs);
                            bedCol *= lerp(1.0, 0.82, wetDark);
                            col = lerp(col, bedCol, bankZone * insideWall * 0.92);
                            rough = lerp(rough, lerp(0.88, 0.42, wetDark), bankZone * insideWall);
                        }
                        if (outerSwale > 0.004)
                        {
                            float3 naturalStream = lerp(float3(0.26, 0.24, 0.19), float3(0.22, 0.29, 0.19), smoothstep(0.35, 0.75, midVar));
                            col = lerp(col, naturalStream, bankZone * outerSwale * 0.72);
                        }
                    }
                }

                // 8. Oriented building storefront aprons & soft contact AO (single-fetch baked 256x256 LUT when bound)
                if (_HasParcelAoTex > 0.5)
                {
                    float2 aoUv = (xz - _ParcelAoBounds.xy) * _ParcelAoBounds.zw;
                    float inLut = step(0.0, aoUv.x) * step(aoUv.x, 1.0) * step(0.0, aoUv.y) * step(aoUv.y, 1.0);
                    half4 aoLut = SAMPLE_TEXTURE2D_LOD(_ParcelAoTex, sampler_ParcelAoTex, saturate(aoUv), 0);
                    float wallAo = lerp(1.0, (float)aoLut.r, inLut);
                    float skirtM = (float)aoLut.g * inLut;
                    float frontApron = (float)aoLut.b * inLut;
                    float nearWall = (float)aoLut.a * inLut;

                    float3 skirtCol = lerp(float3(0.34, 0.32, 0.29), float3(0.37, 0.35, 0.31), frontApron);
                    col = lerp(col, skirtCol, max(skirtM * 0.74, frontApron * 0.80));
                    float mossSkirt = nearWall * smoothstep(0.45, 0.78, midVar);
                    col = lerp(col, float3(0.22, 0.27, 0.19), mossSkirt * 0.30 * _DetailStrength);
                    col *= wallAo;
                }
                else if (parcelCount > 0)
                {
                    float minBldgD = 999.0;
                    float frontApron = 0.0;
                    [loop]
                    for (int i = 0; i < 32; i++)
                    {
                        if (i >= parcelCount) break;
                        float4 pose = _ParcelPose[i];
                        float2 halfE = _ParcelHalf[i].xy;
                        float2 rel = xz - pose.xy;
                        float2 localP = float2(
                            rel.x * pose.z - rel.y * pose.w,
                            rel.x * pose.w + rel.y * pose.z
                        );
                        float2 q = abs(localP) - halfE;
                        float dObb = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
                        if (dObb < minBldgD) minBldgD = dObb;

                        float inFrontX = 1.0 - smoothstep(halfE.x - 0.2, halfE.x + 0.5, abs(localP.x));
                        float inFrontZ = smoothstep(halfE.y - 0.15, halfE.y + 0.15, localP.y) * (1.0 - smoothstep(halfE.y + 1.55, halfE.y + 2.20, localP.y));
                        frontApron = max(frontApron, inFrontX * inFrontZ);
                    }
                    float skirtM = (1.0 - smoothstep(1.15, 2.05, minBldgD)) * smoothstep(-0.35, 0.02, minBldgD);
                    float3 skirtCol = lerp(float3(0.34, 0.32, 0.29), float3(0.37, 0.35, 0.31), frontApron);
                    col = lerp(col, skirtCol, max(skirtM * 0.74, frontApron * 0.80));

                    float wallAo = lerp(0.62, 1.0, smoothstep(-0.05, 1.45, minBldgD));
                    float mossSkirt = (1.0 - smoothstep(0.0, 0.42, abs(minBldgD))) * smoothstep(0.45, 0.78, midVar);
                    col = lerp(col, float3(0.22, 0.27, 0.19), mossSkirt * 0.30 * _DetailStrength);
                    col *= wallAo;
                }

                // 9. Circular 84 m barangay stone coping & outer provincial soil
                if (_ArenaRadius > 1.0)
                {
                    float rimDelta = rDist - _ArenaRadius;
                    float coping = 1.0 - smoothstep(0.32, 0.82, abs(rimDelta));
                    col = lerp(col, float3(0.37, 0.34, 0.30), coping * 0.55);
                    float outside = smoothstep(1.2, 6.5, rimDelta);
                    float hallForecourt = (1.0 - smoothstep(8.2, 10.6, abs(xz.x))) * (1.0 - smoothstep(92.0, 95.5, -xz.y)) * step(xz.y, -80.0);
                    float keepForecourt = max(lmMask * 0.85, hallForecourt);
                    float3 outerSoil = lerp(float3(0.29, 0.26, 0.21), float3(0.23, 0.30, 0.19), smoothstep(0.35, 0.75, macroVar));
                    outerSoil += (midVar - 0.5) * 0.018 * _DetailStrength;
                    col = lerp(col, outerSoil, outside * (1.0 - keepForecourt) * 0.88);
                }

                col = clamp(col, 0.16, 0.48);
                rough = clamp(rough, 0.40, 0.95);

                // Clean asphalt aggregate micro-normal perturbation + clamped 10:30 AM sunlit ambient bounce
                float3 baseNormalWS = normalize(input.normalWS);
                float aggX = (gravelChip - 0.5) * 0.09 + (finePebble - 0.5) * 0.05;
                float aggZ = (grit - 0.5) * 0.07 - (finePebble - 0.5) * 0.05;
                float3 normalWS = normalize(baseNormalWS + float3(aggX, 0.0, aggZ) * (detailVis * lerp(0.35, 1.0, roadMask)));
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float shadowAtten = lerp(_ShadowFloor, 1.0, mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                float ambMin = (_KalyeahAmbientMin > 0.001) ? max(_KalyeahAmbientMin, _AmbientMin) : _AmbientMin;
                float ambMax = max((_KalyeahAmbientMax > 0.001) ? _KalyeahAmbientMax : _AmbientMax, ambMin + 0.01);
                float3 hemiFallback = float3(0.384, 0.596, 0.847) * (ambMin * 1.15);
                float3 ambient = max(clamp(SampleSH(normalWS), ambMin, ambMax), hemiFallback);
                float3 diffuseLight = mainLight.color * (ndotl * shadowAtten) + ambient;

                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; ++li)
                {
                    Light addLight = GetAdditionalLight(li, input.positionWS);
                    diffuseLight += addLight.color * (saturate(dot(normalWS, addLight.direction)) * addLight.distanceAttenuation);
                }
                #endif

                float3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                float specPow = exp2(lerp(8.0, 2.2, rough));
                float specTerm = pow(max(saturate(dot(normalWS, halfDir)), 1e-4), specPow) * (1.0 - rough) * _SpecStrength * ndotl * mainLight.shadowAttenuation;

                float3 baseLinear = SrgbToLinear3(col) * _BaseColor.rgb * _AlbedoScale;
                float3 litColor = baseLinear * diffuseLight + mainLight.color * specTerm;
                litColor = MixFog(litColor, input.fogFactor);
                return half4(litColor, 1.0);
                #endif
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 simXZ      : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                float3 posOS = input.positionOS.xyz;
                float3 nrmOS = input.normalOS;
                DisplaceCreekVertex(posOS, nrmOS);

                float3 posWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(posWS);
                output.simXZ = float2(posWS.x, -posWS.z);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_TARGET
            {
                float2 xz = input.simXZ;
                float cz = CanalZ(xz.x);
                if (_CarveCreekSlot > 0.5 && abs(xz.x) < _ArenaRadius + 3.0 && abs(xz.y - cz) < 4.55)
                {
                    clip(-1.0);
                }
                if (_IsCreekBed > 0.5 && abs(xz.y - cz) > 4.62)
                {
                    clip(-1.0);
                }
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
