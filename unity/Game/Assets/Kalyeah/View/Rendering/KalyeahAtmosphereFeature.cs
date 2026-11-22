using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Kalyeah.Game
{
    /// <summary>
    /// URP 17.3 Render Graph fullscreen aerial haze feature for Option A1 10:30 AM sunlit Manila depth separation.
    /// Separates distant barangay silhouettes (45-110 m) without washing out the 0-35 m combat lane,
    /// and automatically bypasses on Low/Medium tiers or non-3D/overlay/orthographic/UI cameras.
    /// </summary>
    public sealed class KalyeahAtmosphereFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "Kalyeah/Atmosphere";

        [SerializeField] Shader atmosphereShader;
        [SerializeField] Color hazeColor = new Color(0.96f, 0.94f, 0.87f, 1f);
        [SerializeField] Color sunGlowColor = new Color(1.00f, 0.96f, 0.86f, 1f);
        [SerializeField] float hazeStartDistance = 45f;
        [SerializeField] float hazeEndDistance = 110f;
        [SerializeField, Range(0f, 0.65f)] float maxOpacity = 0.28f;
        [SerializeField] float baseHeight = 0f;
        [SerializeField] float heightFalloff = 0.085f;
        [SerializeField, Range(0f, 0.35f)] float sunGlowStrength = 0.14f;

        Material _material;
        AtmosphereRenderPass _pass;

        public override void Create()
        {
            _pass = new AtmosphereRenderPass();
            EnsureMaterial();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;

            // High/Ultra only so Low and Medium mobile tiers pay zero fullscreen pass cost.
            if (SettingsStore.Current.GraphicsQuality < SettingsStore.GraphicsHigh)
                return;

            ref CameraData camData = ref renderingData.cameraData;
            if (camData.renderType != CameraRenderType.Base)
                return;
            if (camData.cameraType != CameraType.Game && camData.cameraType != CameraType.SceneView)
                return;

            Camera cam = camData.camera;
            if (cam == null || cam.orthographic || cam.clearFlags != CameraClearFlags.Skybox)
                return;

            if (!EnsureMaterial())
                return;

            float startDist = Mathf.Max(1f, hazeStartDistance);
            float endDist = Mathf.Max(startDist + 1f, hazeEndDistance);

            _pass.Setup(
                _material,
                new Vector4(hazeColor.r, hazeColor.g, hazeColor.b, 1f),
                new Vector4(sunGlowColor.r, sunGlowColor.g, sunGlowColor.b, 1f),
                new Vector4(startDist, endDist, Mathf.Clamp01(maxOpacity), Mathf.Max(0.001f, heightFalloff)),
                new Vector4(baseHeight, Mathf.Clamp01(sunGlowStrength), 0f, 0f));

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_material != null)
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
            _pass = null;
        }

        bool EnsureMaterial()
        {
            if (_material != null)
                return true;

            Shader resolved = atmosphereShader != null ? atmosphereShader : Shader.Find(ShaderName);
            if (resolved == null || !resolved.isSupported)
                return false;

            _material = CoreUtils.CreateEngineMaterial(resolved);
            return _material != null;
        }

        sealed class AtmosphereRenderPass : ScriptableRenderPass
        {
            const string PassName = "KalyeahAtmosphereHaze";

            static readonly int ColorTexId = Shader.PropertyToID("_AtmosphereColorTex");
            static readonly int DepthTexId = Shader.PropertyToID("_AtmosphereDepthTex");
            static readonly int ColorScaleBiasId = Shader.PropertyToID("_ColorScaleBias");
            static readonly int DepthScaleBiasId = Shader.PropertyToID("_DepthScaleBias");
            static readonly int HazeColorId = Shader.PropertyToID("_HazeColor");
            static readonly int SunGlowColorId = Shader.PropertyToID("_SunGlowColor");
            static readonly int HazeParamsId = Shader.PropertyToID("_HazeParams");
            static readonly int HeightAndSunParamsId = Shader.PropertyToID("_HeightAndSunParams");
            static readonly Vector4 FullscreenScaleBias = new Vector4(1f, 1f, 0f, 0f);
            static MaterialPropertyBlock s_propertyBlock;

            readonly ProfilingSampler _sampler;
            Material _material;
            Vector4 _hazeColor;
            Vector4 _sunGlowColor;
            Vector4 _hazeParams;
            Vector4 _heightAndSunParams;

            sealed class PassData
            {
                public TextureHandle SourceColor;
                public TextureHandle CameraDepth;
                public Material Material;
                public Vector4 HazeColor;
                public Vector4 SunGlowColor;
                public Vector4 HazeParams;
                public Vector4 HeightAndSunParams;
            }

            public AtmosphereRenderPass()
            {
                _sampler = new ProfilingSampler(PassName);
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(
                Material material,
                Vector4 hazeColor,
                Vector4 sunGlowColor,
                Vector4 hazeParams,
                Vector4 heightAndSunParams)
            {
                _material = material;
                _hazeColor = hazeColor;
                _sunGlowColor = sunGlowColor;
                _hazeParams = hazeParams;
                _heightAndSunParams = heightAndSunParams;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer)
                    return;

                Camera cam = cameraData.camera;
                if (cam == null || cam.orthographic || cameraData.renderType != CameraRenderType.Base)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureHandle depth = resourceData.cameraDepthTexture;
                if (!source.IsValid() || !depth.IsValid())
                    return;

                // Derive the destination target directly from the active graph color texture.
                TextureDesc destinationDesc = source.GetDescriptor(renderGraph);
                destinationDesc.name = "_KalyeahAtmosphereColor";
                destinationDesc.msaaSamples = MSAASamples.None;
                destinationDesc.clearBuffer = false;

                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out PassData passData, _sampler))
                {
                    passData.SourceColor = source;
                    passData.CameraDepth = depth;
                    passData.Material = _material;
                    passData.HazeColor = _hazeColor;
                    passData.SunGlowColor = _sunGlowColor;
                    passData.HazeParams = _hazeParams;
                    passData.HeightAndSunParams = _heightAndSunParams;

                    builder.UseTexture(passData.SourceColor, AccessFlags.Read);
                    builder.UseTexture(passData.CameraDepth, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }

                // Redirect URP's camera color target directly so no redundant copy-back pass is needed.
                resourceData.cameraColor = destination;
            }

            static void ExecutePass(PassData data, RasterGraphContext context)
            {
                if (data.Material == null)
                    return;

                RTHandle sourceRt = data.SourceColor;
                RTHandle depthRt = data.CameraDepth;
                if (sourceRt == null || depthRt == null)
                    return;

                Texture sourceTex = sourceRt;
                Texture depthTex = depthRt;
                if (sourceTex == null || depthTex == null)
                    return;

                Vector4 colorScaleBias = sourceRt.useScaling
                    ? new Vector4(sourceRt.rtHandleProperties.rtHandleScale.x, sourceRt.rtHandleProperties.rtHandleScale.y, 0f, 0f)
                    : FullscreenScaleBias;
                Vector4 depthScaleBias = depthRt.useScaling
                    ? new Vector4(depthRt.rtHandleProperties.rtHandleScale.x, depthRt.rtHandleProperties.rtHandleScale.y, 0f, 0f)
                    : FullscreenScaleBias;

                if (s_propertyBlock == null)
                    s_propertyBlock = new MaterialPropertyBlock();

                s_propertyBlock.Clear();
                s_propertyBlock.SetTexture(ColorTexId, sourceTex);
                s_propertyBlock.SetTexture(DepthTexId, depthTex);
                s_propertyBlock.SetVector(ColorScaleBiasId, colorScaleBias);
                s_propertyBlock.SetVector(DepthScaleBiasId, depthScaleBias);
                s_propertyBlock.SetVector(HazeColorId, data.HazeColor);
                s_propertyBlock.SetVector(SunGlowColorId, data.SunGlowColor);
                s_propertyBlock.SetVector(HazeParamsId, data.HazeParams);
                s_propertyBlock.SetVector(HeightAndSunParamsId, data.HeightAndSunParams);

                context.cmd.DrawProcedural(
                    Matrix4x4.identity,
                    data.Material,
                    0,
                    MeshTopology.Triangles,
                    3,
                    1,
                    s_propertyBlock);
            }
        }
    }
}
