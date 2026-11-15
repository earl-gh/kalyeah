using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Where the game finds what tools/unity_export/sync_assets.sh copied in (Assets/Imported/Resources/...) and what KalyeahBuild.Setup
    /// generated (Assets/Kalyeah/Generated/Resources/KalyeahMaterials). Paths mirror the Godot project's assets/ folder.
    /// </summary>
    public static class KalyeahResources
    {
        sealed class ReferenceMaterialComparer : IEqualityComparer<Material>
        {
            public static readonly ReferenceMaterialComparer Instance = new ReferenceMaterialComparer();
            public bool Equals(Material x, Material y) => ReferenceEquals(x, y);
            public int GetHashCode(Material obj) => RuntimeHelpers.GetHashCode(obj);
        }

        static readonly Dictionary<Material, Material> SanitizedMaterialCache =
            new Dictionary<Material, Material>(64, ReferenceMaterialComparer.Instance);
        static Material s_nullFallbackMaterial;

        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");
        static readonly int GltfBaseTexId = Shader.PropertyToID("baseColorTexture");
        static readonly int GltfBaseTexStId = Shader.PropertyToID("baseColorTexture_ST");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int GltfNormalTexId = Shader.PropertyToID("normalTexture");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int GltfOrmTexId = Shader.PropertyToID("metallicRoughnessTexture");
        static readonly int MetallicGlossMapId = Shader.PropertyToID("_MetallicGlossMap");
        static readonly int GltfOccTexId = Shader.PropertyToID("occlusionTexture");
        static readonly int OcclusionMapId = Shader.PropertyToID("_OcclusionMap");
        static readonly int GltfEmissiveTexId = Shader.PropertyToID("emissiveTexture");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int GltfEmissiveColorId = Shader.PropertyToID("emissiveFactor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int CullModeId = Shader.PropertyToID("_CullMode");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        /// <summary>unity/Data/*.json as the view and the sim read it.</summary>
        public static DataSource Data() => new DataSource(name =>
        {
            TextAsset text = Resources.Load<TextAsset>("Data/" + name);
            return text != null ? text.text : null;
        });

        /// <summary>A .glb that glTFast imported (folder "characters", "barangay", "props" or "weapons"), or null.</summary>
        public static GameObject Model(string folder, string id) => Resources.Load<GameObject>($"models/{folder}/{id}");

        public static Texture2D Texture(string path) => Resources.Load<Texture2D>(path);

        public static Font UiFont() => FontOr("fonts/brand/KalyeahSans-Variable");

        public static Font DisplayFont() => FontOr("fonts/brand/KalyeahDisplay-Regular");

        static Font FontOr(string path)
        {
            Font font = Resources.Load<Font>(path);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        /// <summary>A material KalyeahBuild.Setup made (so its shader is in the build); falls back to a fresh one in the editor.</summary>
        public static Material Material(string name, string shader)
        {
            Material made = Resources.Load<Material>("KalyeahMaterials/" + name);
            if (made != null) return made;
            Shader found = Shader.Find(shader);
            return found != null ? new Material(found) : null;
        }

        public static Vector3 U(SVector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static SVector2 S(Vector2 v) => new SVector2(v.x, v.y);

        /// <summary>A sim ground point (x, z) as a Unity world point (GodotSpace.World).</summary>
        public static Vector3 World(SVector2 ground, float y = 0f) => U(GodotSpace.World(ground, y));

        /// <summary>Object.Destroy in play, DestroyImmediate in the editor's edit mode (the screenshots build the match there).</summary>
        public static void Kill(Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        /// <summary>The world bounds of every renderer under `root` (its size at identity is the model's own size).</summary>
        public static Bounds RenderBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>
        /// Ensures every Renderer under `root` uses a supported URP / glTFast ShaderGraph material, replacing any null, unsupported,
        /// InternalErrorShader, Built-In Standard, or Built-In glTF/* material while preserving baseColorTexture, UV ST, and baseColorFactor.
        /// Cached by source Material instance ID so repeated model spawns share materials and allocate zero per-instance duplicates.
        /// </summary>
        public static void SanitizeModelMaterials(GameObject root)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material src = mats[i];
                    if (src != null && !NeedsMaterialSanitize(src.shader)) continue;
                    Material fixedMat = GetOrCreateSanitizedMaterial(src);
                    if (fixedMat != null && !ReferenceEquals(fixedMat, src))
                    {
                        mats[i] = fixedMat;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = mats;
            }
        }

        static bool NeedsMaterialSanitize(Shader shader)
        {
            if (shader == null || !shader.isSupported) return true;
            string n = shader.name;
            if (string.IsNullOrEmpty(n)) return true;
            return n == "Standard"
                || n.StartsWith("Legacy Shaders/", StringComparison.Ordinal)
                || n.StartsWith("glTF/", StringComparison.Ordinal)
                || n.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static Material GetOrCreateSanitizedMaterial(Material src)
        {
            if (src == null)
            {
                if (s_nullFallbackMaterial != null && !NeedsMaterialSanitize(s_nullFallbackMaterial.shader))
                    return s_nullFallbackMaterial;
                Shader fallbackShader = ResolveValidPbrShader();
                if (fallbackShader == null) return null;
                s_nullFallbackMaterial = new Material(fallbackShader)
                {
                    name = "KalyeahSanitizedDefault",
                    hideFlags = HideFlags.DontSave,
                };
                if (s_nullFallbackMaterial.HasProperty(BaseColorId)) s_nullFallbackMaterial.SetColor(BaseColorId, Color.white);
                if (s_nullFallbackMaterial.HasProperty(GltfBaseColorId)) s_nullFallbackMaterial.SetColor(GltfBaseColorId, Color.white);
                if (s_nullFallbackMaterial.HasProperty(SmoothnessId)) s_nullFallbackMaterial.SetFloat(SmoothnessId, 0.12f);
                if (s_nullFallbackMaterial.HasProperty(CullId)) s_nullFallbackMaterial.SetFloat(CullId, 0f);
                return s_nullFallbackMaterial;
            }

            if (SanitizedMaterialCache.TryGetValue(src, out Material cached) && cached != null && !NeedsMaterialSanitize(cached.shader))
                return cached;

            Shader targetShader = ResolveValidPbrShader();
            if (targetShader == null) return src;

            bool isGltfShaderGraph = targetShader.name != null && targetShader.name.StartsWith("Shader Graphs/glTF-", StringComparison.Ordinal);
            Material replacement = isGltfShaderGraph ? new Material(src) { shader = targetShader } : new Material(targetShader);
            replacement.name = src.name + "_URP";
            replacement.hideFlags = HideFlags.DontSave;
            CopyPbrProperties(src, replacement);
            SanitizedMaterialCache[src] = replacement;
            return replacement;
        }

        static Shader ResolveValidPbrShader()
        {
            Material gltfMat = Resources.Load<Material>("KalyeahMaterials/GltfPbr");
            if (gltfMat != null && !NeedsMaterialSanitize(gltfMat.shader)) return gltfMat.shader;

            Shader sg = Shader.Find("Shader Graphs/glTF-pbrMetallicRoughness");
            if (!NeedsMaterialSanitize(sg)) return sg;

            Material groundMat = Resources.Load<Material>("KalyeahMaterials/Ground");
            if (groundMat != null && !NeedsMaterialSanitize(groundMat.shader)) return groundMat.shader;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (!NeedsMaterialSanitize(urpLit)) return urpLit;

            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            return !NeedsMaterialSanitize(urpUnlit) ? urpUnlit : null;
        }

        static void CopyPbrProperties(Material src, Material dst)
        {
            Color baseCol = FirstColor(src, GltfBaseColorId, BaseColorId, LegacyColorId, Color.white);
            if (dst.HasProperty(GltfBaseColorId)) dst.SetColor(GltfBaseColorId, baseCol);
            if (dst.HasProperty(BaseColorId)) dst.SetColor(BaseColorId, baseCol);
            if (dst.HasProperty(LegacyColorId)) dst.SetColor(LegacyColorId, baseCol);

            Texture baseTex = FirstTexture(src, GltfBaseTexId, BaseMapId, MainTexId);
            Vector4 st = ResolveBaseUvSt(src);

            if (baseTex != null)
            {
                if (dst.HasProperty(GltfBaseTexId))
                {
                    dst.SetTexture(GltfBaseTexId, baseTex);
                    if (dst.HasProperty(GltfBaseTexStId)) dst.SetVector(GltfBaseTexStId, st);
                }
                if (dst.HasProperty(BaseMapId))
                {
                    dst.SetTexture(BaseMapId, baseTex);
                    dst.SetTextureScale(BaseMapId, new Vector2(st.x, st.y));
                    dst.SetTextureOffset(BaseMapId, new Vector2(st.z, st.w));
                }
                if (dst.HasProperty(MainTexId))
                {
                    dst.SetTexture(MainTexId, baseTex);
                    dst.SetTextureScale(MainTexId, new Vector2(st.x, st.y));
                    dst.SetTextureOffset(MainTexId, new Vector2(st.z, st.w));
                }
            }

            Texture normalTex = FirstTexture(src, GltfNormalTexId, BumpMapId, 0);
            if (normalTex != null)
            {
                if (dst.HasProperty(GltfNormalTexId)) dst.SetTexture(GltfNormalTexId, normalTex);
                if (dst.HasProperty(BumpMapId)) dst.SetTexture(BumpMapId, normalTex);
            }

            Texture ormTex = FirstTexture(src, GltfOrmTexId, MetallicGlossMapId, 0);
            if (ormTex != null && dst.HasProperty(GltfOrmTexId))
                dst.SetTexture(GltfOrmTexId, ormTex);

            Texture occTex = FirstTexture(src, GltfOccTexId, OcclusionMapId, 0);
            if (occTex != null)
            {
                if (dst.HasProperty(GltfOccTexId)) dst.SetTexture(GltfOccTexId, occTex);
                if (dst.HasProperty(OcclusionMapId)) dst.SetTexture(OcclusionMapId, occTex);
            }

            Texture emiTex = FirstTexture(src, GltfEmissiveTexId, EmissionMapId, 0);
            Color emiCol = FirstColor(src, GltfEmissiveColorId, EmissionColorId, 0, Color.black);
            if (emiTex != null)
            {
                if (dst.HasProperty(GltfEmissiveTexId)) dst.SetTexture(GltfEmissiveTexId, emiTex);
                if (dst.HasProperty(EmissionMapId)) dst.SetTexture(EmissionMapId, emiTex);
            }
            if (emiCol.maxColorComponent > 0.001f)
            {
                if (dst.HasProperty(GltfEmissiveColorId)) dst.SetColor(GltfEmissiveColorId, emiCol);
                if (dst.HasProperty(EmissionColorId)) dst.SetColor(EmissionColorId, emiCol);
            }

            if (dst.HasProperty(CullId))
            {
                float cull = src.HasProperty(CullId)
                    ? src.GetFloat(CullId)
                    : (src.HasProperty(CullModeId) ? src.GetFloat(CullModeId) : 0f);
                dst.SetFloat(CullId, cull);
            }
            if (dst.HasProperty(SmoothnessId) && !dst.HasProperty(GltfBaseTexId))
                dst.SetFloat(SmoothnessId, 0.14f);
            if (dst.HasProperty(MetallicId) && !dst.HasProperty(GltfBaseTexId))
                dst.SetFloat(MetallicId, 0f);
        }

        static Vector4 ResolveBaseUvSt(Material src)
        {
            if (src.HasProperty(GltfBaseTexStId))
            {
                Vector4 st = src.GetVector(GltfBaseTexStId);
                if (Mathf.Abs(st.x) > 0.0001f || Mathf.Abs(st.y) > 0.0001f) return st;
            }
            if (src.HasProperty(GltfBaseTexId))
            {
                Vector2 sc = src.GetTextureScale(GltfBaseTexId);
                Vector2 of = src.GetTextureOffset(GltfBaseTexId);
                if (Mathf.Abs(sc.x) > 0.0001f || Mathf.Abs(sc.y) > 0.0001f)
                    return new Vector4(sc.x, sc.y, of.x, of.y);
            }
            bool fromGltfOrBroken = src.shader == null
                || src.shader.name.StartsWith("glTF/", StringComparison.Ordinal)
                || src.shader.name.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
            return fromGltfOrBroken ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
        }

        static Texture FirstTexture(Material m, int a, int b, int c)
        {
            if (a != 0 && m.HasProperty(a)) { Texture t = m.GetTexture(a); if (t != null) return t; }
            if (b != 0 && m.HasProperty(b)) { Texture t = m.GetTexture(b); if (t != null) return t; }
            if (c != 0 && m.HasProperty(c)) { Texture t = m.GetTexture(c); if (t != null) return t; }

            int[] savedIds = m.GetTexturePropertyNameIDs();
            for (int i = 0; i < savedIds.Length; i++)
            {
                int id = savedIds[i];
                if (id != a && id != b && id != c) continue;
                Texture t = m.GetTexture(id);
                if (t != null) return t;
            }
            return null;
        }

        static Color FirstColor(Material m, int a, int b, int c, Color fallback)
        {
            if (a != 0 && m.HasProperty(a)) return m.GetColor(a);
            if (b != 0 && m.HasProperty(b)) return m.GetColor(b);
            if (c != 0 && m.HasProperty(c)) return m.GetColor(c);
            return fallback;
        }
    }
}
