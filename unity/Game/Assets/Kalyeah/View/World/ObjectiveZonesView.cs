using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Ground-painted objective zones with zero floating horizon clutter:
    /// 1) Street-chalk Base circles with radial capture progress ring & contested flash,
    /// 2) Ground Rescue area zones with flat world-space countdown digits (0.50 white while counting -> 0.55 team tint when ready),
    /// 3) 3 Merienda stands ((-34,0), (0,0), (+34,0), extendable up to 4) with spinning/bobbing treat visual & radial respawn ring.
    /// </summary>
    public sealed class ObjectiveZonesView : IDisposable
    {
        public const int KindPalamig = 1;
        public const int KindTeks = 2;
        public const int KindTuron = 3;

        const float RescueCountingAlpha = 0.50f;
        const float RescueReadyAlpha = 0.55f;
        const int ArcSegments = 48;
        const int MeriendaArcSegments = 32;
        const int MaxMeriendaStands = 4;

        static readonly Color OwnTeamColor = new Color(0.20f, 0.50f, 1.00f, 1f);
        static readonly Color EnemyTeamColor = new Color(1.00f, 0.28f, 0.28f, 1f);
        static readonly Color ContestedFlashColor = new Color(1.00f, 0.90f, 0.36f, 0.96f);

        static readonly SVector2[] DefaultMeriendaSpots =
        {
            new SVector2(-34f, 0f),
            new SVector2(0f, 0f),
            new SVector2(34f, 0f),
        };

        static readonly string[] CountdownStrings = BuildCountdownStrings(99);

        static readonly Color[] MeriendaPalette =
        {
            new Color(0.18f, 0.80f, 0.92f), // 0: Palamig icy cyan cup
            new Color(0.95f, 0.96f, 0.98f), // 1: dome lid / card trim
            new Color(0.92f, 0.24f, 0.22f), // 2: red straw / Teks crimson card
            new Color(0.28f, 0.18f, 0.14f), // 3: sago pearls
            new Color(0.96f, 0.76f, 0.18f), // 4: Teks gold card / Turon glaze
            new Color(0.22f, 0.52f, 0.92f), // 5: Teks cobalt card
            new Color(0.86f, 0.56f, 0.16f), // 6: fried Turon wrapper
            new Color(0.28f, 0.68f, 0.32f), // 7: banana leaf band
        };

        sealed class RadialArcMesh
        {
            public readonly GameObject Go;
            public readonly MeshRenderer Renderer;
            readonly Mesh _mesh;
            readonly Vector3[] _verts;
            readonly float _innerR, _outerR;
            readonly int _segments;
            float _shownProgress = -1f;
            Color _shownColor;

            public RadialArcMesh(
                Transform parent,
                string name,
                Vector3 worldPos,
                float innerR,
                float outerR,
                int segments,
                Material template,
                ProceduralAssetCleaner cleaner)
            {
                _innerR = innerR;
                _outerR = outerR;
                _segments = segments;
                _verts = new Vector3[(segments + 1) * 2];
                var uvs = new Vector2[_verts.Length];
                var normals = new Vector3[_verts.Length];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

                var tris = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                {
                    int v = i * 2;
                    int t = i * 6;
                    // Clockwise from +Y in Unity (left-handed): inner0 -> outer0 -> outer1, inner0 -> outer1 -> inner1
                    tris[t] = v;
                    tris[t + 1] = v + 1;
                    tris[t + 2] = v + 3;
                    tris[t + 3] = v;
                    tris[t + 4] = v + 3;
                    tris[t + 5] = v + 2;
                }

                _mesh = new Mesh { name = name + "_Mesh" };
                _mesh.MarkDynamic();
                _mesh.vertices = _verts;
                _mesh.normals = normals;
                _mesh.uv = uvs;
                _mesh.triangles = tris;
                _mesh.bounds = new Bounds(Vector3.zero, new Vector3(outerR * 2.2f, 0.5f, outerR * 2.2f));
                cleaner?.Track(_mesh);

                Go = new GameObject(name);
                Go.transform.SetParent(parent, false);
                Go.transform.position = worldPos;
                Go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                Renderer = Go.AddComponent<MeshRenderer>();
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                if (template != null)
                {
                    var mat = new Material(template) { name = name + "_Mat" };
                    cleaner?.Track(mat);
                    Renderer.sharedMaterial = mat;
                }

                SetArc(0f, Color.white);
            }

            public void SetArc(float progress01, Color color)
            {
                float clamped = Mathf.Clamp01(progress01);
                bool visible = clamped > 0.002f && color.a > 0.005f;
                if (Renderer.enabled != visible) Renderer.enabled = visible;
                if (!visible)
                {
                    _shownProgress = 0f;
                    return;
                }

                if (Mathf.Abs(clamped - _shownProgress) > 0.002f)
                {
                    _shownProgress = clamped;
                    float sweep = clamped * Mathf.PI * 2f;
                    for (int i = 0; i <= _segments; i++)
                    {
                        float t = (float)i / _segments;
                        // Start at 12 o'clock (+Z) and sweep clockwise (sin, cos)
                        float ang = t * sweep;
                        float s = Mathf.Sin(ang);
                        float c = Mathf.Cos(ang);
                        _verts[i * 2] = new Vector3(s * _innerR, 0f, c * _innerR);
                        _verts[i * 2 + 1] = new Vector3(s * _outerR, 0f, c * _outerR);
                    }
                    _mesh.vertices = _verts;
                }

                if (_shownColor != color && Renderer.sharedMaterial != null)
                {
                    _shownColor = color;
                    SetMaterialColor(Renderer.sharedMaterial, color);
                }
            }
        }

        sealed class BaseZoneNode
        {
            public int Side;
            public Material ChalkMat;
            public Material FillMat;
            public RadialArcMesh CaptureArc;
            public float Capture01;
            public bool Contested;
        }

        sealed class RescueZoneNode
        {
            public int Side;
            public Material ChalkMat;
            public Material FillMat;
            public Transform LabelTransform;
            public Text CountText;
            public int LastShownSeconds = -1;
            public int ReadyState = -1; // -1 unset, 0 counting, 1 ready
        }

        sealed class MeriendaStandNode
        {
            public GameObject RootGo;
            public RadialArcMesh RespawnRing;
            public GameObject[] KindVisuals; // 0=Palamig, 1=Teks, 2=Turon
            public Transform FloatPivot;
            public int Kind;
            public bool Active;
            public float Respawn01;
            public bool Configured;
        }

        public readonly Transform Root;
        readonly ProceduralAssetCleaner _cleaner;
        readonly MapLayoutData _layout;
        readonly Material _transparentTemplate;
        readonly Material _meriendaMat;
        readonly BaseZoneNode _ownBase;
        readonly BaseZoneNode _enemyBase;
        readonly RescueZoneNode _ownRescue;
        readonly RescueZoneNode _enemyRescue;
        readonly MeriendaStandNode[] _stands = new MeriendaStandNode[MaxMeriendaStands];
        int _ownSide;
        float _animTime;
        bool _disposed;

        public static ObjectiveZonesView Build(Transform parent, MapLayoutData layout, int ownSide = MapLayoutData.SideOwn)
            => new ObjectiveZonesView(parent, layout, ownSide);

        public ObjectiveZonesView(Transform parent, MapLayoutData layout, int ownSide = MapLayoutData.SideOwn)
        {
            Root = new GameObject("ObjectiveZones").transform;
            if (parent != null) Root.SetParent(parent, false);
            _cleaner = Root.gameObject.AddComponent<ProceduralAssetCleaner>();
            _layout = layout;
            _ownSide = ownSide >= 0 ? MapLayoutData.SideOwn : MapLayoutData.SideEnemy;
            _transparentTemplate = CreateTransparentUnlitTemplate();
            _meriendaMat = WorldMeshBatch.CreatePaletteMaterial("MeriendaTreatPalette", MeriendaPalette, true, 0.30f, _cleaner);

            if (_layout != null)
            {
                _ownBase = BuildBaseZone(MapLayoutData.SideOwn);
                _enemyBase = BuildBaseZone(MapLayoutData.SideEnemy);
                _ownRescue = BuildRescueZone(MapLayoutData.SideOwn);
                _enemyRescue = BuildRescueZone(MapLayoutData.SideEnemy);
            }

            for (int i = 0; i < MaxMeriendaStands; i++)
            {
                bool defaultActive = i < DefaultMeriendaSpots.Length;
                SVector2 spot = defaultActive ? DefaultMeriendaSpots[i] : SVector2.Zero;
                int defaultKind = (i % 3) + 1;
                _stands[i] = BuildMeriendaStand(i, spot, defaultKind, defaultActive);
            }

            ApplySideTints();
            SyncRescue(0f, 0f, false);
        }

        /// <summary>One-line primitive sync covering bases, rescue timers, and Merienda stand animations.</summary>
        public void Sync(
            float ownBaseCapture01,
            bool ownBaseContested,
            float enemyBaseCapture01,
            bool enemyBaseContested,
            float ownRescueCooldownSec,
            float enemyRescueCooldownSec,
            float dt,
            bool reduceMotion = false,
            bool hideRescueCounts = false)
        {
            SyncBases(ownBaseCapture01, ownBaseContested, enemyBaseCapture01, enemyBaseContested);
            SyncRescue(ownRescueCooldownSec, enemyRescueCooldownSec, hideRescueCounts);
            Tick(dt, reduceMotion);
        }

        /// <summary>Updates painted Base chalk capture progress rings (0..1) and contested state.</summary>
        public void SyncBases(float ownBaseCapture01, bool ownBaseContested, float enemyBaseCapture01, bool enemyBaseContested)
        {
            if (_disposed) return;
            if (_ownBase != null)
            {
                _ownBase.Capture01 = Mathf.Clamp01(ownBaseCapture01);
                _ownBase.Contested = ownBaseContested;
            }
            if (_enemyBase != null)
            {
                _enemyBase.Capture01 = Mathf.Clamp01(enemyBaseCapture01);
                _enemyBase.Contested = enemyBaseContested;
            }
            RefreshBaseVisuals();
        }

        /// <summary>Direct adapter for MatchSim's single capture_team (-1 none, localTeam -> enemy base, other -> own base).</summary>
        public void SyncCapture(int capturingTeam, float progress01, bool contested, int localTeam)
        {
            bool hittingEnemyBase = capturingTeam >= 0 && capturingTeam == localTeam;
            bool hittingOwnBase = capturingTeam >= 0 && capturingTeam != localTeam;
            SyncBases(
                hittingOwnBase ? progress01 : 0f,
                hittingOwnBase && contested,
                hittingEnemyBase ? progress01 : 0f,
                hittingEnemyBase && contested);
        }

        /// <summary>Updates ground Rescue zones: 0.50 white + flat countdown while counting -> 0.55 team tint when ready.</summary>
        public void SyncRescue(float ownCooldownSec, float enemyCooldownSec, bool hideCounts = false)
        {
            if (_disposed) return;
            UpdateRescueNode(_ownRescue, ownCooldownSec, hideCounts);
            UpdateRescueNode(_enemyRescue, enemyCooldownSec, hideCounts);
        }

        /// <summary>Updates one Merienda stand's active floating treat, respawn ring (0..1), and optional treat kind (1..3).</summary>
        public void SyncMerienda(int index, bool active, float respawnProgress01, int kind = 0)
        {
            if (_disposed || index < 0 || index >= MaxMeriendaStands) return;
            MeriendaStandNode stand = _stands[index];
            stand.Configured = true;
            if (!stand.RootGo.activeSelf) stand.RootGo.SetActive(true);
            if (kind >= KindPalamig && kind <= KindTuron && kind != stand.Kind)
            {
                stand.Kind = kind;
                for (int k = 0; k < stand.KindVisuals.Length; k++)
                    stand.KindVisuals[k].SetActive(k == kind - 1);
            }
            stand.Active = active;
            stand.Respawn01 = Mathf.Clamp01(respawnProgress01);
            if (stand.FloatPivot.gameObject.activeSelf != active)
                stand.FloatPivot.gameObject.SetActive(active);

            Color ringCol = active ? MeriendaRingColor(stand.Kind, 0.72f) : new Color(1f, 0.92f, 0.68f, 0.58f);
            stand.RespawnRing.SetArc(active ? 1f : stand.Respawn01, ringCol);
        }

        /// <summary>Moves and updates a Merienda stand when syncing custom MapLayout/MatchSim powerup coordinates.</summary>
        public void SyncMeriendaAt(int index, SVector2 groundPos, bool active, float respawnProgress01, int kind = 0)
        {
            if (_disposed || index < 0 || index >= MaxMeriendaStands) return;
            _stands[index].RootGo.transform.position = KalyeahResources.World(groundPos, 0f);
            SyncMerienda(index, active, respawnProgress01, kind);
        }

        /// <summary>Advances contested base flash and floating Merienda treat spin/bob with zero allocations.</summary>
        public void Tick(float dt, bool reduceMotion = false)
        {
            if (_disposed) return;
            if (!reduceMotion && dt > 0f) _animTime += dt;

            if ((_ownBase != null && _ownBase.Contested) || (_enemyBase != null && _enemyBase.Contested))
                RefreshBaseVisuals();

            for (int i = 0; i < MaxMeriendaStands; i++)
            {
                MeriendaStandNode stand = _stands[i];
                if (!stand.Configured || !stand.Active) continue;
                float bob = reduceMotion ? 0f : 0.075f * Mathf.Sin(_animTime * 2.8f + i * 1.6f);
                float spinDeg = reduceMotion ? (i * 35f) : (_animTime * 54f + i * 60f);
                stand.FloatPivot.localPosition = new Vector3(0f, 0.78f + bob, 0f);
                stand.FloatPivot.localRotation = Quaternion.Euler(0f, spinDeg, 0f);
            }
        }

        public void SetOwnSide(int ownSide)
        {
            if (_disposed) return;
            int normalized = ownSide >= 0 ? MapLayoutData.SideOwn : MapLayoutData.SideEnemy;
            if (_ownSide == normalized) return;
            _ownSide = normalized;
            if (_ownRescue != null) { _ownRescue.ReadyState = -1; OrientRescueLabel(_ownRescue); }
            if (_enemyRescue != null) { _enemyRescue.ReadyState = -1; OrientRescueLabel(_enemyRescue); }
            ApplySideTints();
            SyncRescue(0f, 0f, false);
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            if (Root != null && Root.gameObject.activeSelf != visible)
                Root.gameObject.SetActive(visible);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cleaner?.Dispose();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        Color TintForSide(int side) => side == _ownSide ? OwnTeamColor : EnemyTeamColor;

        void ApplySideTints()
        {
            RefreshBaseVisuals();
        }

        void RefreshBaseVisuals()
        {
            UpdateBaseNode(_ownBase, EnemyTeamColor);   // Enemy captures Own base
            UpdateBaseNode(_enemyBase, OwnTeamColor);   // Own team captures Enemy base
        }

        void UpdateBaseNode(BaseZoneNode node, Color attackerColor)
        {
            if (node == null) return;
            Color ownerColor = TintForSide(node.Side);
            float pulse = node.Contested ? (0.5f + 0.5f * Mathf.Sin(_animTime * 12f)) : 0f;

            Color chalkCol = node.Contested
                ? Color.Lerp(ownerColor, ContestedFlashColor, pulse)
                : new Color(ownerColor.r, ownerColor.g, ownerColor.b, 0.88f);
            Color fillCol = new Color(ownerColor.r, ownerColor.g, ownerColor.b, node.Contested ? Mathf.Lerp(0.10f, 0.24f, pulse) : 0.12f);

            if (node.ChalkMat != null) SetMaterialColor(node.ChalkMat, chalkCol);
            if (node.FillMat != null) SetMaterialColor(node.FillMat, fillCol);

            float arcValue = node.Contested && node.Capture01 < 0.08f ? 1f : node.Capture01;
            Color arcCol = node.Contested
                ? Color.Lerp(attackerColor, ContestedFlashColor, pulse)
                : new Color(attackerColor.r, attackerColor.g, attackerColor.b, 0.92f);
            node.CaptureArc.SetArc(arcValue, arcCol);
        }

        void UpdateRescueNode(RescueZoneNode node, float cooldownSec, bool hideCounts)
        {
            if (node == null) return;
            bool ready = cooldownSec <= 0.001f;
            int targetState = ready ? 1 : 0;

            if (node.ReadyState != targetState)
            {
                node.ReadyState = targetState;
                Color sideTint = TintForSide(node.Side);
                // Binding rule: 0.50 white while counting -> 0.55 team tint when ready
                Color chalkCol = ready
                    ? new Color(sideTint.r, sideTint.g, sideTint.b, RescueReadyAlpha)
                    : new Color(1f, 1f, 1f, RescueCountingAlpha);
                Color fillCol = ready
                    ? new Color(sideTint.r, sideTint.g, sideTint.b, 0.14f)
                    : new Color(1f, 1f, 1f, 0.07f);
                if (node.ChalkMat != null) SetMaterialColor(node.ChalkMat, chalkCol);
                if (node.FillMat != null) SetMaterialColor(node.FillMat, fillCol);
            }

            bool showNumber = !ready && !hideCounts;
            if (node.CountText.enabled != showNumber) node.CountText.enabled = showNumber;
            if (showNumber)
            {
                int secs = Mathf.Clamp(Mathf.CeilToInt(cooldownSec), 1, CountdownStrings.Length - 1);
                if (node.LastShownSeconds != secs)
                {
                    node.LastShownSeconds = secs;
                    node.CountText.text = CountdownStrings[secs];
                }
            }
        }

        BaseZoneNode BuildBaseZone(int side)
        {
            float r = Mathf.Max(_layout.BaseRadius, 3f);
            Vector3 center = KalyeahResources.World(_layout.BaseCenter(side), 0f);
            var group = new GameObject(side == MapLayoutData.SideOwn ? "BaseZone_Own" : "BaseZone_Enemy").transform;
            group.SetParent(Root, false);
            group.position = center;

            Material fillMat = CloneTransparent("BaseFill_" + side, new Color(1f, 1f, 1f, 0.12f));
            AttachZoneMesh(group, "FillDisc", BuildDiscMesh("BaseDisc", r - 0.24f, 40, 0.015f), fillMat);

            Material chalkMat = CloneTransparent("BaseChalk_" + side, Color.white);
            AttachZoneMesh(group, "ChalkRing", BuildDashedChalkCircleMesh("BaseChalk", r - 0.16f, 0.32f, 44, 0.025f), chalkMat);

            var arc = new RadialArcMesh(group, "CaptureProgressArc", center + Vector3.up * 0.033f, r - 0.62f, r - 0.20f, ArcSegments, _transparentTemplate, _cleaner);
            return new BaseZoneNode { Side = side, ChalkMat = chalkMat, FillMat = fillMat, CaptureArc = arc };
        }

        RescueZoneNode BuildRescueZone(int side)
        {
            GroundRect rect = _layout.RescueRect(side);
            Vector3 center = KalyeahResources.World(rect.Center, 0f);
            var group = new GameObject(side == MapLayoutData.SideOwn ? "RescueZone_Own" : "RescueZone_Enemy").transform;
            group.SetParent(Root, false);

            Material fillMat = CloneTransparent("RescueFill_" + side, new Color(1f, 1f, 1f, 0.08f));
            AttachZoneMesh(group, "RescuePad", BuildFlatRectMesh("RescuePadMesh", rect, 0.16f, 0.015f), fillMat);

            Material chalkMat = CloneTransparent("RescueChalk_" + side, new Color(1f, 1f, 1f, RescueCountingAlpha));
            AttachZoneMesh(group, "RescueChalkLoop", BuildRoundedRectChalkMesh("RescueChalkMesh", rect, 1.2f, 0.32f, 0.026f), chalkMat);

            // Flat ground-lying WorldSpace Canvas: crisp Komiks numeral at 0.50 white alpha, zero floating billboard clutter
            var canvasGo = new GameObject("RescueCountdownGround");
            canvasGo.transform.SetParent(group, false);
            canvasGo.transform.position = center + Vector3.up * 0.036f;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -10;
            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 220f);
            const float pxPerMeter = 96f / 2.2f;
            canvasGo.transform.localScale = new Vector3(1f / pxPerMeter, 1.38f / pxPerMeter, 1f);

            var textGo = new GameObject("Seconds");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.font = KalyeahResources.DisplayFont();
            text.fontSize = 96;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = new Color(1f, 1f, 1f, RescueCountingAlpha);
            text.enabled = false;

            var node = new RescueZoneNode
            {
                Side = side,
                ChalkMat = chalkMat,
                FillMat = fillMat,
                LabelTransform = canvasGo.transform,
                CountText = text,
            };
            OrientRescueLabel(node);
            return node;
        }

        void OrientRescueLabel(RescueZoneNode node)
        {
            float baseYaw = _ownSide == MapLayoutData.SideOwn ? 0f : 180f;
            float turn = -node.Side * _ownSide * 90f;
            node.LabelTransform.localRotation = Quaternion.Euler(90f, baseYaw + turn, 0f);
        }

        MeriendaStandNode BuildMeriendaStand(int index, SVector2 spot, int defaultKind, bool activeByDefault)
        {
            var standGo = new GameObject("MeriendaStand_" + index);
            standGo.transform.SetParent(Root, false);
            standGo.transform.position = KalyeahResources.World(spot, 0f);
            standGo.SetActive(activeByDefault);

            // Ground chalk pickup circle + radial respawn timer ring
            Material padMat = CloneTransparent("MeriendaPad_" + index, new Color(1f, 0.94f, 0.76f, 0.22f));
            var padGo = AttachZoneMesh(standGo.transform, "StandPad", BuildDiscMesh("MeriendaDisc", 0.96f, 24, 0.032f), padMat);
            padGo.transform.localPosition = Vector3.zero;

            var ring = new RadialArcMesh(standGo.transform, "RespawnRing", standGo.transform.position + Vector3.up * 0.040f, 0.84f, 1.08f, MeriendaArcSegments, _transparentTemplate, _cleaner);
            ring.Go.transform.localPosition = new Vector3(0f, 0.040f, 0f);

            var pivot = new GameObject("FloatingTreat").transform;
            pivot.SetParent(standGo.transform, false);
            pivot.localPosition = new Vector3(0f, 0.78f, 0f);

            var visuals = new GameObject[3];
            visuals[0] = BuildTreatVisual(pivot, "Palamig", KindPalamig);
            visuals[1] = BuildTreatVisual(pivot, "Teks", KindTeks);
            visuals[2] = BuildTreatVisual(pivot, "Turon", KindTuron);
            for (int k = 0; k < 3; k++) visuals[k].SetActive(k == defaultKind - 1);

            ring.SetArc(1f, MeriendaRingColor(defaultKind, 0.72f));
            return new MeriendaStandNode
            {
                RootGo = standGo,
                RespawnRing = ring,
                KindVisuals = visuals,
                FloatPivot = pivot,
                Kind = defaultKind,
                Active = activeByDefault,
                Respawn01 = 1f,
                Configured = activeByDefault,
            };
        }

        GameObject BuildTreatVisual(Transform parent, string name, int kind)
        {
            var batch = new WorldMeshBatch(MeriendaPalette.Length);
            if (kind == KindPalamig)
            {
                batch.AddVerticalCylinder(new Vector3(0f, -0.24f, 0f), 0.14f, 0.19f, 0.44f, 10, 0, 0);
                batch.AddVerticalCylinder(new Vector3(0f, -0.23f, 0f), 0.145f, 0.155f, 0.10f, 10, 3, 3);
                batch.AddVerticalCylinder(new Vector3(0f, 0.20f, 0f), 0.20f, 0.06f, 0.11f, 10, 1, 1);
                batch.AddOrientedBox(new Vector3(0.05f, 0.30f, 0f), new Vector3(0.04f, 0.34f, 0.04f), Quaternion.Euler(0f, 0f, -18f), 2, 2, true);
            }
            else if (kind == KindTeks)
            {
                batch.AddOrientedBox(new Vector3(-0.08f, 0f, -0.02f), new Vector3(0.28f, 0.40f, 0.035f), Quaternion.Euler(8f, -14f, 14f), 2, 1, true);
                batch.AddOrientedBox(new Vector3(0.08f, 0f, 0.02f), new Vector3(0.28f, 0.40f, 0.035f), Quaternion.Euler(-6f, 14f, -14f), 5, 1, true);
                batch.AddOrientedBox(new Vector3(0f, 0.03f, 0f), new Vector3(0.30f, 0.42f, 0.040f), Quaternion.identity, 4, 1, true);
            }
            else
            {
                Quaternion tilt = Quaternion.Euler(0f, 0f, 28f);
                batch.AddOrientedBox(Vector3.zero, new Vector3(0.20f, 0.54f, 0.18f), tilt, 6, 4, true);
                batch.AddOrientedBox(Vector3.zero, new Vector3(0.215f, 0.18f, 0.195f), tilt, 7, 7, true);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Mesh mesh = _cleaner.Track(batch.ToMesh("Treat_" + name));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (_meriendaMat != null) mr.sharedMaterial = _meriendaMat;
            return go;
        }

        static Color MeriendaRingColor(int kind, float alpha)
        {
            if (kind == KindPalamig) return new Color(0.22f, 0.86f, 0.98f, alpha);
            if (kind == KindTeks) return new Color(0.98f, 0.78f, 0.22f, alpha);
            return new Color(0.96f, 0.56f, 0.20f, alpha);
        }

        Material CreateTransparentUnlitTemplate()
        {
            Material src = Resources.Load<Material>("KalyeahMaterials/TransparentUnlit")
                        ?? Resources.Load<Material>("KalyeahMaterials/Unlit");
            Shader fallback = src == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (src == null && fallback == null) return null;
            Material mat = src != null
                ? new Material(src) { name = "ObjectiveTransparentUnlit" }
                : new Material(fallback) { name = "ObjectiveTransparentUnlit" };
            ConfigureTransparentUnlit(mat);
            return _cleaner.Track(mat);
        }

        Material CloneTransparent(string name, Color color)
        {
            if (_transparentTemplate == null) return null;
            var mat = new Material(_transparentTemplate) { name = name };
            SetMaterialColor(mat, color);
            return _cleaner.Track(mat);
        }

        static void ConfigureTransparentUnlit(Material mat)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        static void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        }

        GameObject AttachZoneMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            _cleaner.Track(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (material != null) mr.sharedMaterial = material;
            return go;
        }

        static Mesh BuildDiscMesh(string name, float radius, int segments, float y)
        {
            var batch = new WorldMeshBatch(1);
            batch.AddVerticalCylinder(new Vector3(0f, y - 0.004f, 0f), radius, radius, 0.004f, segments, 0, 0, true);
            return batch.ToMesh(name);
        }

        static Mesh BuildDashedChalkCircleMesh(string name, float radius, float width, int dashes, float y)
        {
            var batch = new WorldMeshBatch(1);
            float half = width * 0.5f;
            float step = Mathf.PI * 2f / dashes;
            for (int i = 0; i < dashes; i++)
            {
                // Slight hand-drawn street-chalk gaps every 4th stroke
                float fill = (i % 4 == 3) ? 0.62f : 0.88f;
                float a0 = i * step;
                float a1 = a0 + step * fill;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                Vector3 d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                batch.AddQuad(
                    d0 * (radius - half) + Vector3.up * y,
                    d0 * (radius + half) + Vector3.up * y,
                    d1 * (radius + half) + Vector3.up * y,
                    d1 * (radius - half) + Vector3.up * y,
                    Vector3.up);
            }
            return batch.ToMesh(name);
        }

        static Mesh BuildFlatRectMesh(string name, GroundRect rect, float inset, float y)
        {
            var batch = new WorldMeshBatch(1);
            Vector3 a = KalyeahResources.World(new SVector2(rect.X + inset, rect.Y + inset), y);
            Vector3 b = KalyeahResources.World(new SVector2(rect.End.X - inset, rect.Y + inset), y);
            Vector3 c = KalyeahResources.World(new SVector2(rect.End.X - inset, rect.End.Y - inset), y);
            Vector3 d = KalyeahResources.World(new SVector2(rect.X + inset, rect.End.Y - inset), y);
            batch.AddQuad(d, a, b, c, Vector3.up);
            return batch.ToMesh(name);
        }

        static Mesh BuildRoundedRectChalkMesh(string name, GroundRect rect, float cornerRadius, float width, float y)
        {
            var batch = new WorldMeshBatch(1);
            float half = width * 0.5f;
            var shrunk = new GroundRect(rect.X + half, rect.Y + half, Mathf.Max(rect.W - width, 0.5f), Mathf.Max(rect.H - width, 0.5f));
            float r = Mathf.Min(Mathf.Max(cornerRadius - half, 0.1f), Mathf.Min(shrunk.W, shrunk.H) * 0.45f);
            const int cornerSteps = 5;
            var pts = new SVector2[4 * (cornerSteps + 1)];
            SVector2[] centers =
            {
                new SVector2(shrunk.End.X - r, shrunk.Y + r),
                new SVector2(shrunk.End.X - r, shrunk.End.Y - r),
                new SVector2(shrunk.X + r, shrunk.End.Y - r),
                new SVector2(shrunk.X + r, shrunk.Y + r),
            };
            int cursor = 0;
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSteps; s++)
                {
                    float ang = -Mathf.PI * 0.5f + (c + (float)s / cornerSteps) * (Mathf.PI * 0.5f);
                    pts[cursor++] = centers[c] + new SVector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                }
            }

            int count = pts.Length;
            for (int i = 0; i < count; i++)
            {
                if (i % 6 == 5) continue; // Subtle chalk break
                int j = (i + 1) % count;
                Vector3 p0 = KalyeahResources.World(pts[i], y);
                Vector3 p1 = KalyeahResources.World(pts[j], y);
                Vector3 seg = p1 - p0;
                if (seg.sqrMagnitude < 0.0001f) continue;
                Vector3 perp = new Vector3(-seg.z, 0f, seg.x).normalized * half;
                batch.AddQuad(p0 - perp, p0 + perp, p1 + perp, p1 - perp, Vector3.up);
            }
            return batch.ToMesh(name);
        }

        static string[] BuildCountdownStrings(int max)
        {
            var table = new string[max + 1];
            for (int i = 0; i <= max; i++) table[i] = i.ToString();
            return table;
        }
    }
}
