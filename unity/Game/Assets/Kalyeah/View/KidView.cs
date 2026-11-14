using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// One kid in the street: its skinned GLB (assets/models/characters/&lt;id&gt;.glb, imported by glTFast with legacy animation clips),
    /// scaled to KID_HEIGHT x kid_view_scale like scripts/view/art/kid_model.gd, turned to its facing, idle or run. A team disc under its
    /// feet says whose it is (the Godot game uses an outline; not ported yet). A kid without a model is a capsule, like the Godot fallback
    /// to "junjun" when a model is missing.
    /// </summary>
    public sealed class KidView
    {
        /// <summary>kid_model.gd KID_HEIGHT: how tall a kid stands, before the camera's kid_view_scale.</summary>
        public const float KidHeight = 1.85f;
        /// <summary>kid_model.gd FALLBACK_SET.</summary>
        const string FallbackSet = "junjun";
        /// <summary>kid_model.gd BLEND: seconds of cross-fade between clips.</summary>
        const float Blend = 0.14f;
        /// <summary>Above this share of a full run the kid plays Run.</summary>
        const float RunShare = 0.08f;
        static readonly string[] IdleNames = { "Idle", "idle" };
        static readonly string[] RunNames = { "Run", "run" };

        public readonly int Id;
        public readonly GameObject Root;
        readonly GameObject _model;
        readonly Animation _animation;
        readonly string _idle, _run;
        string _playing;
        float _clock;

        public KidView(Transform parent, KidSnapshot kid, float viewScale, Color team, Material disc)
        {
            Id = kid.Id;
            Root = new GameObject($"Kid_{kid.Id}_{kid.CharacterId}");
            Root.transform.SetParent(parent, false);

            GameObject prefab = KalyeahResources.Model("characters", kid.CharacterId);
            if (prefab == null) prefab = KalyeahResources.Model("characters", FallbackSet);
            if (prefab != null)
            {
                _model = Object.Instantiate(prefab, Root.transform, false);
                _model.name = "Model";
                _model.transform.localRotation = Quaternion.identity;
                float natural = Mathf.Max(KalyeahResources.RenderBounds(_model).size.y, 0.1f);
                _model.transform.localScale = Vector3.one * (KidHeight * viewScale / natural);
                _animation = _model.GetComponentInChildren<Animation>(true);
                if (_animation != null)
                {
                    _idle = Clip(IdleNames);
                    _run = Clip(RunNames);
                    foreach (AnimationState state in _animation) state.wrapMode = WrapMode.Loop;
                    _clock = kid.Id * 0.37f; // the kids do not breathe in step
                }
            }
            else
            {
                _model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _model.name = "Model (no GLB: run tools/unity_export/sync_assets.sh)";
                KalyeahResources.Kill(_model.GetComponent<Collider>());
                _model.transform.SetParent(Root.transform, false);
                float tall = KidHeight * viewScale;
                _model.transform.localScale = new Vector3(tall * 0.3f, tall * 0.5f, tall * 0.3f);
                _model.transform.localPosition = Vector3.up * tall * 0.5f;
            }

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TeamDisc";
            KalyeahResources.Kill(ring.GetComponent<Collider>());
            ring.transform.SetParent(Root.transform, false);
            ring.transform.localScale = new Vector3(0.9f, 0.01f, 0.9f);
            ring.transform.localPosition = Vector3.up * 0.02f;
            var renderer = ring.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (disc != null) renderer.sharedMaterial = disc;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", team);
            renderer.SetPropertyBlock(block);
            Place(kid.Position, kid.Facing);
        }

        string Clip(string[] names)
        {
            foreach (string name in names)
                if (_animation.GetClip(name) != null) return name;
            foreach (AnimationState state in _animation)
                foreach (string name in names)
                    if (string.Equals(state.name, name, System.StringComparison.OrdinalIgnoreCase)) return state.name;
            return null;
        }

        /// <summary>Feet at `ground` (sim XZ), facing `facing` (sim XZ), the way kid_model.gd turns it.</summary>
        public void Place(SVector2 ground, SVector2 facing)
        {
            Root.transform.position = KalyeahResources.World(ground);
            if (facing.LengthSquared() > 0.0001f)
                Root.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(GodotSpace.KidYawRad(facing)), 0f);
        }

        /// <summary>Idle or run by speed. In play the Animation component plays it; in edit mode (screenshots) the clip is sampled.</summary>
        public void Animate(float speed01, float dt)
        {
            if (_animation == null) return;
            string want = speed01 > RunShare && _run != null ? _run : _idle;
            if (want == null) return;
            _clock = want == _playing || _playing == null ? _clock + dt : 0f;
            if (Application.isPlaying)
            {
                if (want != _playing) _animation.CrossFade(want, Blend);
            }
            else
            {
                AnimationClip clip = _animation.GetClip(want);
                if (clip != null) clip.SampleAnimation(_animation.gameObject, clip.length > 0f ? _clock % clip.length : 0f);
            }
            _playing = want;
        }
    }
}
