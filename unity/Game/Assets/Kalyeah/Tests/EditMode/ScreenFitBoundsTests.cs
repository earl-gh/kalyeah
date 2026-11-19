using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Kalyeah.Net;
using Kalyeah.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>
    /// Multi-aspect &amp; cutout screen-fit bounds gate (.claude/skills/apex-screen-fit/SKILL.md):
    /// Sweeps MatchHud, StreetHudView, and all Menu/Lobby/Hotspot/QR/Overlay GUI surfaces across the 4 canonical
    /// landscape device profiles (844x390 notched 19.5:9, 1280x720 16:9, 934x400 21:9, 1024x768 4:3 foldable),
    /// verifying SafeArea containment, full-bleed backdrop/scrim coverage, zero control/header-body overlaps,
    /// &gt;= 48 dp physical touch floors, zero dark-ink + dark-outline smudge, 45%x50% center combat sightbox
    /// sanctity, 180 deg orientation flips, and DPI fallback clamps.
    /// </summary>
    [TestFixture]
    public sealed class ScreenFitBoundsTests
    {
        const float AbilityButtonMinClearanceDp = 6f;

        readonly struct ViewportProfile
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Rect RawSafeRect;
            public readonly ScreenOrientation Orientation;
            public readonly string Label;

            public ViewportProfile(int width, int height, Rect rawSafeRect, ScreenOrientation orientation, string label)
            {
                Width = width;
                Height = height;
                RawSafeRect = rawSafeRect;
                Orientation = orientation;
                Label = label;
            }
        }

        readonly List<IDisposable> _disposables = new List<IDisposable>();
        readonly List<UnityObject> _cleanup = new List<UnityObject>();
        readonly List<RenderTexture> _targets = new List<RenderTexture>();
        MatchHud _hud;
        StreetHudView _streetHud;
        string _savedSessionJson;
        string _savedSettingsJson;

        [SetUp]
        public void SetUp()
        {
            _savedSessionJson = AppSession.Current.ToJson();
            _savedSettingsJson = SettingsStore.Current.ToJson();
            AppSession.Current.StopAllNetwork();
            AndroidLocalLink.HandlePluginHotspotStopped();
            if (SettingsStore.Current.LeftHanded)
                SettingsStore.Current.LeftHanded = false;
            ScreenFitMetrics.ClearSimulatedViewport();
        }

        [TearDown]
        public void TearDown()
        {
            AppSession.Current.StopAllNetwork();
            AndroidLocalLink.HandlePluginHotspotStopped();

            if (SettingsStore.Current.LeftHanded)
                SettingsStore.Current.LeftHanded = false;

            _hud?.Dispose();
            _hud = null;

            _streetHud?.Dispose();
            _streetHud = null;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try { _disposables[i]?.Dispose(); }
                catch (Exception) { }
            }
            _disposables.Clear();

            ScreenFitMetrics.ClearSimulatedViewport();

            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null)
                    KalyeahResources.Kill(_cleanup[i]);
            }
            _cleanup.Clear();

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i] != null)
                {
                    _targets[i].Release();
                    KalyeahResources.Kill(_targets[i]);
                }
            }
            _targets.Clear();

            if (!string.IsNullOrEmpty(_savedSessionJson))
                AppSession.Current.LoadJson(_savedSessionJson);
            if (!string.IsNullOrEmpty(_savedSettingsJson))
                SettingsStore.Current.LoadJson(_savedSettingsJson);
        }

        static DataSource LoadData()
        {
            DataSource res = KalyeahResources.Data();
            return new DataSource(name =>
            {
                try
                {
                    return res.Text(name);
                }
                catch (KeyNotFoundException)
                {
                    string path = Path.Combine(Application.dataPath, "Imported", "Resources", "Data", name + ".json");
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
            });
        }

        public static IEnumerable<TestCaseData> CanonicalProfiles()
        {
            yield return new TestCaseData(
                844,
                390,
                ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: false),
                ScreenOrientation.LandscapeLeft,
                "19.5:9 Notched 844x390 (+44px cutout)").SetName("Profile_844x390_Notched19_5x9");

            yield return new TestCaseData(
                1280,
                720,
                new Rect(0f, 0f, 1280f, 720f),
                ScreenOrientation.LandscapeLeft,
                "16:9 Reference 1280x720").SetName("Profile_1280x720_Reference16x9");

            yield return new TestCaseData(
                934,
                400,
                new Rect(0f, 0f, 934f, 400f),
                ScreenOrientation.LandscapeLeft,
                "21:9 UltraWide 934x400").SetName("Profile_934x400_UltraWide21x9");

            yield return new TestCaseData(
                1024,
                768,
                new Rect(0f, 0f, 1024f, 768f),
                ScreenOrientation.LandscapeLeft,
                "4:3 Inner Foldable 1024x768").SetName("Profile_1024x768_Foldable4x3");
        }

        Camera BuildViewportCamera(string rootPrefix, int width, int height, Rect rawSafePx, ScreenOrientation orientation, out GameObject root)
        {
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(width, height), rawSafePx, null, orientation);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            _targets.Add(rt);

            root = new GameObject($"{rootPrefix}_{width}x{height}");
            _cleanup.Add(root);

            var camGo = new GameObject("HudCamera", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            return cam;
        }

        Canvas BuildMenuCanvasRig(string rootPrefix, ViewportProfile profile, out Camera cam)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            Ui.EnsureEventSystem();
            cam = BuildViewportCamera(rootPrefix, profile.Width, profile.Height, profile.RawSafeRect, profile.Orientation, out GameObject root);
            cam.orthographic = true;
            Canvas canvas = Ui.MakeCanvas(rootPrefix + "_Canvas", root.transform, cam);
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        (MatchHud hud, SafeArea safe, Camera cam, SimBridge sim) BuildHudAtViewport(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation)
        {
            Camera cam = BuildViewportCamera("HudRoot", width, height, rawSafePx, orientation, out GameObject root);

            var sim = new SimBridge(LoadData(), 7L, "narda");
            sim.SkipIntro();
            _disposables.Add(sim);

            _hud?.Dispose();
            var hud = new MatchHud(root.transform, sim, cam, interactive: true);
            _hud = hud;
            SafeArea safe = hud.Canvas.GetComponentInChildren<SafeArea>();

            RefreshAll(hud, safe, sim);
            return (hud, safe, cam, sim);
        }

        static void RefreshAll(MatchHud hud, SafeArea safe, SimBridge sim = null)
        {
            Canvas.ForceUpdateCanvases();
            safe.ForceApply();
            hud.Joystick.RefreshLayout();
            hud.Look.RefreshLayout();
            hud.AbilityArc?.RefreshLayout();
            hud.CardHand?.RefreshLayout();
            hud.Crosshair?.RefreshLayout();
            hud.Overlays?.RefreshLayout();
            if (sim != null)
                hud.Refresh(sim);
            Canvas.ForceUpdateCanvases();
        }

        static Rect GetScreenRect(RectTransform rt, Camera cam)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Canvas rootCanvas = rt.GetComponentInParent<Canvas>();
            Camera uiCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : cam)
                : null;

            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
                if (sp.x < xMin) xMin = sp.x;
                if (sp.y < yMin) yMin = sp.y;
                if (sp.x > xMax) xMax = sp.x;
                if (sp.y > yMax) yMax = sp.y;
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static Rect Shrink(Rect r, float px)
        {
            float dx = Mathf.Min(px, r.width * 0.25f);
            float dy = Mathf.Min(px, r.height * 0.25f);
            return Rect.MinMaxRect(r.xMin + dx, r.yMin + dy, r.xMax - dx, r.yMax - dy);
        }

        static Rect ComputeCenterSightbox(Rect safePx) =>
            new Rect(
                safePx.xMin + safePx.width * 0.275f,
                safePx.yMin + safePx.height * 0.25f,
                safePx.width * 0.45f,
                safePx.height * 0.50f);

        static void AssertContainedInSafeRect(Rect safePx, Rect childPx, string label, float tolerancePx = 1.5f)
        {
            Assert.GreaterOrEqual(childPx.xMin, safePx.xMin - tolerancePx, $"{label} left ({childPx.xMin:F1}) outside safe rect ({safePx.xMin:F1})");
            Assert.GreaterOrEqual(childPx.yMin, safePx.yMin - tolerancePx, $"{label} bottom ({childPx.yMin:F1}) outside safe rect ({safePx.yMin:F1})");
            Assert.LessOrEqual(childPx.xMax, safePx.xMax + tolerancePx, $"{label} right ({childPx.xMax:F1}) outside safe rect ({safePx.xMax:F1})");
            Assert.LessOrEqual(childPx.yMax, safePx.yMax + tolerancePx, $"{label} top ({childPx.yMax:F1}) outside safe rect ({safePx.yMax:F1})");
        }

        static void AssertTouchTarget48Dp(Rect rect, float minTouchPx, string label)
        {
            const float tol = 0.75f;
            Assert.GreaterOrEqual(rect.width, minTouchPx - tol, $"{label} width {rect.width:F1}px < 48dp floor ({minTouchPx:F1}px)");
            Assert.GreaterOrEqual(rect.height, minTouchPx - tol, $"{label} height {rect.height:F1}px < 48dp floor ({minTouchPx:F1}px)");
        }

        static void AssertHudBoundsValid(MatchHud hud, SafeArea safe, Camera cam, int width, int height, string profileDesc)
        {
            Rect expectedSafePx = safe.AppliedSafeRectPx;
            Rect actualSafePx = GetScreenRect((RectTransform)safe.transform, cam);
            Assert.AreEqual(expectedSafePx.xMin, actualSafePx.xMin, 1.5f, $"{profileDesc}: SafeArea xMin");
            Assert.AreEqual(expectedSafePx.yMin, actualSafePx.yMin, 1.5f, $"{profileDesc}: SafeArea yMin (includes 18dp gesture pill)");
            Assert.AreEqual(expectedSafePx.xMax, actualSafePx.xMax, 1.5f, $"{profileDesc}: SafeArea xMax");
            Assert.AreEqual(expectedSafePx.yMax, actualSafePx.yMax, 1.5f, $"{profileDesc}: SafeArea yMax");

            float pillFloorPx = Mathf.Min(ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.GesturePillDp), height * 0.15f);
            Assert.GreaterOrEqual(safe.InsetsPx.y, pillFloorPx - 0.5f, $"{profileDesc}: bottom gesture-pill clearance >= 18 dp");

            var moveZone = (RectTransform)hud.Joystick.transform;
            var lookZone = (RectTransform)hud.Look.transform;
            var stickBase = (RectTransform)moveZone.Find("Base");
            var stickThumb = (RectTransform)stickBase.Find("Thumb");
            var header = (RectTransform)safe.transform.Find("Header");

            Assert.IsNotNull(hud.Overlays, $"{profileDesc}: MatchOverlaysView present");
            var heroVitals = (RectTransform)hud.Overlays.Root.Find("HeroVitalsChip");
            var pauseBtn = (RectTransform)hud.Overlays.Root.Find("PauseButton");
            var quickChatBtn = (RectTransform)hud.Overlays.Root.Find("QuickChatButton");

            Assert.IsNotNull(stickBase, $"{profileDesc}: Joystick.Base");
            Assert.IsNotNull(stickThumb, $"{profileDesc}: Joystick.Thumb");
            Assert.IsNotNull(header, $"{profileDesc}: Header");
            Assert.IsNotNull(heroVitals, $"{profileDesc}: HeroVitalsChip");
            Assert.IsNotNull(pauseBtn, $"{profileDesc}: PauseButton");
            Assert.IsNotNull(quickChatBtn, $"{profileDesc}: QuickChatButton");
            Assert.IsNotNull(hud.AbilityArc, $"{profileDesc}: AbilityArcView");
            Assert.IsFalse(hud.UsingCardsTray, $"{profileDesc}: default 5v5 mode uses AbilityArcView");

            Rect moveZonePx = GetScreenRect(moveZone, cam);
            Rect lookZonePx = GetScreenRect(lookZone, cam);
            Rect stickBasePx = GetScreenRect(stickBase, cam);
            Rect stickThumbPx = GetScreenRect(stickThumb, cam);
            Rect pausePx = GetScreenRect(pauseBtn, cam);
            Rect quickChatPx = GetScreenRect(quickChatBtn, cam);

            var controls = new List<Rect>(16)
            {
                stickBasePx,
                GetScreenRect(header, cam),
                GetScreenRect(heroVitals, cam),
                pausePx,
                quickChatPx,
            };
            var names = new List<string>(16)
            {
                "Joystick.Base",
                "Header",
                "HeroVitalsChip",
                "PauseButton",
                "QuickChatButton",
            };

            // the ADS button sits under the minimap and is a full touch target (docs/SPEC.md §8)
            Assert.IsNotNull(hud.Ads, $"{profileDesc}: AdsButton");
            Rect adsPx = GetScreenRect(hud.Ads.Rect, cam);
            controls.Add(adsPx);
            names.Add("AdsButton");
            AssertTouchTarget48Dp(adsPx, ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp), $"{profileDesc} AdsButton");
            if (hud.Minimap.Root.gameObject.activeInHierarchy)
            {
                Rect mapPx = GetScreenRect(hud.Minimap.Root, cam);
                Assert.IsFalse(Shrink(adsPx, 0.75f).Overlaps(Shrink(mapPx, 0.75f)), $"{profileDesc}: AdsButton ({adsPx}) overlaps the minimap ({mapPx})");
                Assert.Less(adsPx.yMax, mapPx.yMin + 1f, $"{profileDesc}: AdsButton sits under the minimap");
            }

            var arcRectsPx = new List<Rect>(8);
            var arcNames = new List<string>(8);
            IReadOnlyList<RectTransform> arcButtons = hud.AbilityArc.InteractiveButtonRects;
            for (int i = 0; i < arcButtons.Count; i++)
            {
                RectTransform btnRt = arcButtons[i];
                if (btnRt == null || !btnRt.gameObject.activeInHierarchy)
                    continue;
                Rect btnPx = GetScreenRect(btnRt, cam);
                string btnName = "AbilityArc." + btnRt.name;
                arcRectsPx.Add(btnPx);
                arcNames.Add(btnName);
                controls.Add(btnPx);
                names.Add(btnName);
            }

            Assert.AreEqual(
                8,
                arcRectsPx.Count,
                $"{profileDesc}: all 8 AbilityArcView buttons (Primary, Superpower 1..3, Dash, Ultimate, Jump, CrouchSlide) must be active");

            // 1. 100% Safe-rect containment for thumb zones and EVERY active interactive/HUD control
            AssertContainedInSafeRect(expectedSafePx, moveZonePx, $"{profileDesc} MoveZone");
            AssertContainedInSafeRect(expectedSafePx, lookZonePx, $"{profileDesc} LookZone");
            for (int i = 0; i < controls.Count; i++)
                AssertContainedInSafeRect(expectedSafePx, controls[i], $"{profileDesc} {names[i]}");

            // 2. Zero sibling touch-zone interior AABB overlap (MoveZone vs LookZone share only their seam line)
            Assert.IsFalse(
                Shrink(moveZonePx, 0.75f).Overlaps(Shrink(lookZonePx, 0.75f)),
                $"{profileDesc}: MoveZone ({moveZonePx}) and LookZone ({lookZonePx}) must not overlap");

            // 3. Zero pairwise AABB overlap across ALL active HUD controls
            for (int i = 0; i < controls.Count; i++)
            {
                for (int j = i + 1; j < controls.Count; j++)
                {
                    Assert.IsFalse(
                        Shrink(controls[i], 0.75f).Overlaps(Shrink(controls[j], 0.75f)),
                        $"{profileDesc}: {names[i]} ({controls[i]}) overlaps {names[j]} ({controls[j]})");
                }
            }

            // 4. >= 6 dp edge-to-edge clearance between EVERY pair of active AbilityArcView buttons
            float minArcClearancePx = ScreenFitMetrics.DpToScreenPx(AbilityButtonMinClearanceDp) - 1.0f;
            for (int i = 0; i < arcRectsPx.Count; i++)
            {
                float rA = 0.5f * Mathf.Max(arcRectsPx[i].width, arcRectsPx[i].height);
                for (int j = i + 1; j < arcRectsPx.Count; j++)
                {
                    float rB = 0.5f * Mathf.Max(arcRectsPx[j].width, arcRectsPx[j].height);
                    float edgeClearancePx = Vector2.Distance(arcRectsPx[i].center, arcRectsPx[j].center) - (rA + rB);
                    Assert.GreaterOrEqual(
                        edgeClearancePx,
                        minArcClearancePx,
                        $"{profileDesc}: {arcNames[i]} and {arcNames[j]} edge-to-edge clearance ({edgeClearancePx:F1} px) < 6 dp ({minArcClearancePx:F1} px)");
                }
            }

            // 5. Unobstructed 45% x 50% center sightbox inside the safe viewport
            Rect sightboxPx = ComputeCenterSightbox(expectedSafePx);
            for (int i = 0; i < controls.Count; i++)
            {
                Assert.IsFalse(
                    Shrink(controls[i], 1.0f).Overlaps(Shrink(sightboxPx, 1.0f)),
                    $"{profileDesc}: {names[i]} ({controls[i]}) intrudes into 45%x50% center sightbox ({sightboxPx})");
            }

            // 6. Physical >= 48 dp touch size constancy (and >= 72 dp on Primary fire anchor)
            float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
            float minPrimaryPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.PrimaryFireMinDp);
            Assert.GreaterOrEqual(stickBasePx.width, minTouchPx - 0.75f, $"{profileDesc}: Joystick.Base width >= 48 dp");
            Assert.GreaterOrEqual(stickBasePx.height, minTouchPx - 0.75f, $"{profileDesc}: Joystick.Base height >= 48 dp");
            Assert.GreaterOrEqual(stickThumbPx.width, minTouchPx - 0.75f, $"{profileDesc}: Joystick.Thumb width >= 48 dp");
            Assert.GreaterOrEqual(stickThumbPx.height, minTouchPx - 0.75f, $"{profileDesc}: Joystick.Thumb height >= 48 dp");
            Assert.GreaterOrEqual(moveZonePx.width, minTouchPx - 0.75f, $"{profileDesc}: MoveZone width >= 48 dp");
            Assert.GreaterOrEqual(lookZonePx.width, minTouchPx - 0.75f, $"{profileDesc}: LookZone width >= 48 dp");
            Assert.GreaterOrEqual(pausePx.width, minTouchPx - 0.75f, $"{profileDesc}: PauseButton width >= 48 dp");
            Assert.GreaterOrEqual(pausePx.height, minTouchPx - 0.75f, $"{profileDesc}: PauseButton height >= 48 dp");
            Assert.GreaterOrEqual(quickChatPx.width, minTouchPx - 0.75f, $"{profileDesc}: QuickChatButton width >= 48 dp");
            Assert.GreaterOrEqual(quickChatPx.height, minTouchPx - 0.75f, $"{profileDesc}: QuickChatButton height >= 48 dp");

            for (int i = 0; i < arcRectsPx.Count; i++)
            {
                Assert.GreaterOrEqual(arcRectsPx[i].width, minTouchPx - 0.75f, $"{profileDesc}: {arcNames[i]} width >= 48 dp");
                Assert.GreaterOrEqual(arcRectsPx[i].height, minTouchPx - 0.75f, $"{profileDesc}: {arcNames[i]} height >= 48 dp");
            }

            if (hud.AbilityArc.PrimaryButtonRect != null)
            {
                Rect primaryPx = GetScreenRect(hud.AbilityArc.PrimaryButtonRect, cam);
                Assert.GreaterOrEqual(primaryPx.width, minPrimaryPx - 1.0f, $"{profileDesc}: PrimaryTirador width >= 72 dp");
                Assert.GreaterOrEqual(primaryPx.height, minPrimaryPx - 1.0f, $"{profileDesc}: PrimaryTirador height >= 72 dp");
            }

            // 7. 4:3 inner foldable cluster height cap (<= 42% safe height)
            if ((float)width / height <= 1.4f)
            {
                Assert.LessOrEqual(
                    stickBasePx.height,
                    expectedSafePx.height * ScreenFitMetrics.MaxFoldableClusterHeightShare + 1.5f,
                    $"{profileDesc}: Joystick cluster respects <= 42% foldable height cap");
            }

            // 8. Zero dark-ink + dark-outline text smudge & positive/unclipped text integrity
            AssertNoDarkInkDarkOutlineSmudge((Transform)safe.transform, profileDesc);
            AssertTextIntegrity((Transform)safe.transform, profileDesc);
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void MatchHud_AcrossCanonicalAspectMatrix_HasZeroOverlapSafeContainmentSightboxAnd48Dp(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var (hud, safe, cam, sim) = BuildHudAtViewport(width, height, rawSafePx, orientation);
            AssertHudBoundsValid(hud, safe, cam, width, height, label);

            // Mirror entire HUD via SettingsStore.Current.LeftHanded and re-verify every bound & clearance
            SettingsStore.Current.LeftHanded = true;
            RefreshAll(hud, safe, sim);
            AssertHudBoundsValid(hud, safe, cam, width, height, label + " [LeftHanded]");
        }

        [Test]
        public void SafeArea_AndControls_ReflowDynamicallyOn180DegreeLandscapeOrientationFlip()
        {
            Rect leftCutout = ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: false);
            var (hud, safe, cam, sim) = BuildHudAtViewport(844, 390, leftCutout, ScreenOrientation.LandscapeLeft);

            Assert.AreEqual(ScreenOrientation.LandscapeLeft, safe.AppliedOrientation);
            Assert.AreEqual(44f, safe.InsetsPx.x, 0.5f, "LandscapeLeft punch-hole cutout insets left edge by 44 px");
            Assert.AreEqual(0f, safe.InsetsPx.z, 0.5f, "LandscapeLeft right edge has 0 px cutout");
            AssertHudBoundsValid(hud, safe, cam, 844, 390, "844x390 LandscapeLeft");

            // Flip 180 degrees to LandscapeRight: punch-hole camera swaps to the right edge synchronously via SimulatedViewportChanged
            Rect rightCutout = ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: true);
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(844, 390), rightCutout, null, ScreenOrientation.LandscapeRight);
            hud.Refresh(sim);
            Canvas.ForceUpdateCanvases();

            Assert.AreEqual(ScreenOrientation.LandscapeRight, safe.AppliedOrientation);
            Assert.AreEqual(0f, safe.InsetsPx.x, 0.5f, "LandscapeRight left edge clears back to 0 px");
            Assert.AreEqual(44f, safe.InsetsPx.z, 0.5f, "LandscapeRight punch-hole cutout insets right edge by 44 px");
            AssertHudBoundsValid(hud, safe, cam, 844, 390, "844x390 LandscapeRight");

            // Flip back to LandscapeLeft
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(844, 390), leftCutout, null, ScreenOrientation.LandscapeLeft);
            hud.Refresh(sim);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(44f, safe.InsetsPx.x, 0.5f);
            Assert.AreEqual(0f, safe.InsetsPx.z, 0.5f);
            AssertHudBoundsValid(hud, safe, cam, 844, 390, "844x390 LandscapeLeft Restored");
        }

        static StreetBridge CreateActiveRoleStreetBridge(DataSource data, StreetGameKind kind)
        {
            for (long seed = 1L; seed <= 32L; seed++)
            {
                var candidate = new StreetBridge(data, kind, seed, "narda");
                candidate.SkipIntro();
                for (int w = 0; w < 150 && candidate.IsIntro; w++)
                    candidate.Step();
                StreetKidSnapshot? me = candidate.LocalKid;
                if (me.HasValue && me.Value.Role != StreetRoleKind.Taya && me.Value.Role != StreetRoleKind.Guard)
                    return candidate;
            }
            var fallback = new StreetBridge(data, kind, 1L, "narda");
            fallback.SkipIntro();
            for (int w = 0; w < 150 && fallback.IsIntro; w++)
                fallback.Step();
            return fallback;
        }

        static void AssertStreetHudBoundsValid(
            StreetHudView streetHud,
            StreetBridge bridge,
            Camera cam,
            string profileDesc)
        {
            SafeArea safe = streetHud.SafeAreaComponent;
            Assert.IsNotNull(safe, $"{profileDesc}: SafeAreaComponent");
            Assert.IsNotNull(streetHud.Joystick, $"{profileDesc}: StreetHudView.Joystick must always be built");

            Canvas.ForceUpdateCanvases();
            safe.ForceApply();
            streetHud.ApplyResponsiveLayout();
            streetHud.Joystick.RefreshLayout();
            streetHud.Look?.RefreshLayout();
            streetHud.Refresh(bridge, 0f);
            Canvas.ForceUpdateCanvases();

            Rect expectedSafePx = safe.AppliedSafeRectPx;
            Rect actualSafePx = GetScreenRect(streetHud.Safe, cam);
            Assert.AreEqual(expectedSafePx.xMin, actualSafePx.xMin, 1.5f, $"{profileDesc}: Street SafeArea xMin");
            Assert.AreEqual(expectedSafePx.yMin, actualSafePx.yMin, 1.5f, $"{profileDesc}: Street SafeArea yMin");
            Assert.AreEqual(expectedSafePx.xMax, actualSafePx.xMax, 1.5f, $"{profileDesc}: Street SafeArea xMax");
            Assert.AreEqual(expectedSafePx.yMax, actualSafePx.yMax, 1.5f, $"{profileDesc}: Street SafeArea yMax");

            var moveZone = (RectTransform)streetHud.Joystick.transform;
            var stickBase = (RectTransform)moveZone.Find("Base");
            var header = (RectTransform)streetHud.Safe.Find("Header");
            var rolePill = (RectTransform)streetHud.Safe.Find("RolePill");
            var actionCluster = (RectTransform)streetHud.Safe.Find("ActionCluster");
            Transform leaveTr = streetHud.Safe.Find("LeaveButton") ?? streetHud.Safe.Find("PauseButton");

            Assert.IsNotNull(stickBase, $"{profileDesc}: Street Joystick.Base");
            Assert.IsNotNull(header, $"{profileDesc}: Street Header");
            Assert.IsNotNull(rolePill, $"{profileDesc}: Street RolePill");
            Assert.IsNotNull(actionCluster, $"{profileDesc}: Street ActionCluster");

            var controls = new List<Rect>(8)
            {
                GetScreenRect(stickBase, cam),
                GetScreenRect(header, cam),
                GetScreenRect(rolePill, cam),
            };
            var names = new List<string>(8)
            {
                "Joystick.Base",
                "Header",
                "RolePill",
            };

            float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
            Assert.GreaterOrEqual(controls[0].width, minTouchPx - 0.75f, $"{profileDesc}: Street Joystick.Base width >= 48 dp");
            Assert.GreaterOrEqual(controls[0].height, minTouchPx - 0.75f, $"{profileDesc}: Street Joystick.Base height >= 48 dp");

            if (leaveTr is RectTransform leaveRt && leaveRt.gameObject.activeInHierarchy)
            {
                Rect leavePx = GetScreenRect(leaveRt, cam);
                controls.Add(leavePx);
                names.Add(leaveRt.name);
                Assert.GreaterOrEqual(leavePx.width, minTouchPx - 0.75f, $"{profileDesc}: {leaveRt.name} width >= 48 dp");
                Assert.GreaterOrEqual(leavePx.height, minTouchPx - 0.75f, $"{profileDesc}: {leaveRt.name} height >= 48 dp");
            }

            string[] actionNames = { "PrimaryAction", "SecondaryAction" };
            int activeActions = 0;
            for (int i = 0; i < actionNames.Length; i++)
            {
                if (actionCluster.Find(actionNames[i]) is RectTransform actRt && actRt.gameObject.activeInHierarchy)
                {
                    Rect actPx = GetScreenRect(actRt, cam);
                    controls.Add(actPx);
                    names.Add(actionNames[i]);
                    activeActions++;
                    Assert.GreaterOrEqual(actPx.width, minTouchPx - 0.75f, $"{profileDesc}: {actionNames[i]} width >= 48 dp");
                    Assert.GreaterOrEqual(actPx.height, minTouchPx - 0.75f, $"{profileDesc}: {actionNames[i]} height >= 48 dp");
                }
            }
            Assert.GreaterOrEqual(activeActions, 1, $"{profileDesc}: at least one street action button active");

            AssertContainedInSafeRect(expectedSafePx, GetScreenRect(moveZone, cam), $"{profileDesc} Street MoveZone");
            for (int i = 0; i < controls.Count; i++)
                AssertContainedInSafeRect(expectedSafePx, controls[i], $"{profileDesc} Street {names[i]}");

            for (int i = 0; i < controls.Count; i++)
            {
                for (int j = i + 1; j < controls.Count; j++)
                {
                    Assert.IsFalse(
                        Shrink(controls[i], 0.75f).Overlaps(Shrink(controls[j], 0.75f)),
                        $"{profileDesc}: Street {names[i]} ({controls[i]}) overlaps {names[j]} ({controls[j]})");
                }
            }

            Rect sightboxPx = ComputeCenterSightbox(expectedSafePx);
            for (int i = 0; i < controls.Count; i++)
            {
                Assert.IsFalse(
                    Shrink(controls[i], 1.0f).Overlaps(Shrink(sightboxPx, 1.0f)),
                    $"{profileDesc}: Street {names[i]} ({controls[i]}) intrudes into 45%x50% center sightbox ({sightboxPx})");
            }

            AssertNoDarkInkDarkOutlineSmudge(streetHud.Safe, profileDesc);
            AssertTextIntegrity(streetHud.Safe, profileDesc);
        }

        [Test]
        public void StreetHudView_Across1280x720And844x390Notched_HasSafeContainmentZeroOverlapAnd48Dp()
        {
            DataSource data = LoadData();
            StreetBridge bridge = CreateActiveRoleStreetBridge(data, StreetGameKind.TumbangPreso);

            var profiles = new (int w, int h, Rect safePx, ScreenOrientation orient, string label)[]
            {
                (1280, 720, new Rect(0f, 0f, 1280f, 720f), ScreenOrientation.LandscapeLeft, "Street 1280x720 16:9"),
                (844, 390, ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: false), ScreenOrientation.LandscapeLeft, "Street 844x390 Notched 19.5:9"),
            };

            for (int i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                SettingsStore.Current.LeftHanded = false;
                Camera cam = BuildViewportCamera("StreetHudRoot", p.w, p.h, p.safePx, p.orient, out GameObject root);

                _streetHud?.Dispose();
                _streetHud = new StreetHudView(root.transform, bridge, cam, interactive: true, onPlayAgain: () => { }, onHome: () => { });
                AssertStreetHudBoundsValid(_streetHud, bridge, cam, p.label);

                SettingsStore.Current.LeftHanded = true;
                AssertStreetHudBoundsValid(_streetHud, bridge, cam, p.label + " [LeftHanded]");

                _streetHud.Dispose();
                _streetHud = null;
            }
            SettingsStore.Current.LeftHanded = false;
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void StreetHudView_AcrossCanonicalAspectMatrix_AllStreetModesMeet48DpSafeContainmentSightboxZeroOverlapAndZeroSmudge(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            DataSource data = LoadData();
            StreetGameKind[] kinds =
            {
                StreetGameKind.Patintero,
                StreetGameKind.TumbangPreso,
                StreetGameKind.LangitLupa,
            };

            for (int k = 0; k < kinds.Length; k++)
            {
                StreetGameKind kind = kinds[k];
                StreetBridge bridge = CreateActiveRoleStreetBridge(data, kind);
                SettingsStore.Current.LeftHanded = false;
                Camera cam = BuildViewportCamera($"StreetMatrix_{kind}", width, height, rawSafePx, orientation, out GameObject root);

                using var hud = new StreetHudView(root.transform, bridge, cam, interactive: true, onPlayAgain: () => { }, onHome: () => { });
                AssertStreetHudBoundsValid(hud, bridge, cam, $"{kind} {label}");

                SettingsStore.Current.LeftHanded = true;
                AssertStreetHudBoundsValid(hud, bridge, cam, $"{kind} {label} [LeftHanded]");
            }
            SettingsStore.Current.LeftHanded = false;
        }

        [Test]
        public void ScreenFitMetrics_ClampsBogusZeroAndExtremeDpiReports()
        {
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(0f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(-99f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(float.NaN), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.MinDpi, ScreenFitMetrics.ClampDpi(72f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.MaxDpi, ScreenFitMetrics.ClampDpi(1200f), 0.001f);
        }

        // =================================================================================================================
        // Phase 3A Expanded GUI Surface Gates Across All 4 Canonical Landscape Profiles (#231 / #241)
        // =================================================================================================================

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void MenuScreens_AcrossCanonicalAspectMatrix_Meet48DpSafeContainmentFullBleedZeroOverlapAndZeroSmudge(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);
            DataSource data = LoadData();
            MatchBoot.ApplyBootResourcesAndGraphics(data);

            // Seed deterministic player session + match history rows so HistoryScreenView exercises live match log rows & ladder badges
            AppSession.Current.PlayerName = "Junjun";
            AppSession.Current.SelectedCharacter = "narda";
            AppSession.Current.SetSuperpowers(new[] { "bato_light", "tsinelas_light", "papel_shield" });
            if (AppSession.Current.History.Count == 0)
            {
                AppSession.Current.RecordMatchResult("practice", "narda", won: true, myScore: 3, theirScore: 1);
                AppSession.Current.RecordMatchResult("patintero", "jumong", won: false, myScore: 1, theirScore: 2);
            }

            // 1. LoadingScreenView.BuildPreview
            {
                Canvas canvas = BuildMenuCanvasRig($"Loading_{width}x{height}", profile, out Camera cam);
                LoadingScreenView loading = LoadingScreenView.BuildPreview(canvas.transform, data, "AGAWANG BASE · 5V5", 0.68f);
                _disposables.Add(loading);

                AuditGuiSurface(
                    canvas,
                    cam,
                    loading.Root,
                    loading.BackdropRect,
                    profile,
                    "LoadingScreenView",
                    expectSelectables: false,
                    disjointBands: new[]
                    {
                        loading.LogoFrameRect,
                        loading.ModeBadgeRect,
                        loading.TipCardRect,
                        loading.ProgressCardRect,
                    },
                    onReflow: loading.RefreshLayout);
                AssertFullBleedRect(loading.ScrimRect, cam, profile, "LoadingScreenView/Scrim");
            }

            // 2. HomeScreenView
            {
                Canvas canvas = BuildMenuCanvasRig($"Home_{width}x{height}", profile, out Camera cam);
                var home = new HomeScreenView(canvas.transform, data, _ => { }, _ => { });
                _disposables.Add(home);
                home.Refresh();

                RectTransform backdrop = home.Root.Find("Backdrop") as RectTransform;
                RectTransform topBar = home.SafeRoot.Find("TopBar") as RectTransform;
                RectTransform hubBody = home.SafeRoot.Find("HubBody") as RectTransform;

                AuditGuiSurface(
                    canvas,
                    cam,
                    home.Root,
                    backdrop,
                    profile,
                    "HomeScreenView",
                    expectSelectables: true,
                    disjointBands: new[] { topBar, hubBody },
                    onReflow: home.RefreshLayout);

                if (hubBody != null && hubBody.childCount >= 3)
                {
                    AssertBandsDisjointAndSafe(
                        cam,
                        ScreenFitMetrics.ResolveSafeRectPx(),
                        $"HomeScreenView/Columns[{profile.Label}]",
                        hubBody.GetChild(0) as RectTransform,
                        hubBody.GetChild(1) as RectTransform,
                        hubBody.GetChild(2) as RectTransform);
                }
            }

            // 3. PickScreenView
            {
                Canvas canvas = BuildMenuCanvasRig($"Pick_{width}x{height}", profile, out Camera cam);
                var pick = new PickScreenView(canvas.transform, data, () => { }, () => { });
                _disposables.Add(pick);
                pick.RefreshFromSession();

                RectTransform backdrop = pick.Root.Find("Backdrop") as RectTransform;
                Transform workspace = pick.SafeRoot.Find("Workspace");
                RectTransform headerBar = workspace != null ? workspace.Find("HeaderBar") as RectTransform : null;
                RectTransform rosterPanel = workspace != null ? workspace.Find("RosterPanel") as RectTransform : null;
                RectTransform rightPanel = workspace != null && workspace.childCount >= 3 ? workspace.GetChild(2) as RectTransform : null;

                AuditGuiSurface(
                    canvas,
                    cam,
                    pick.Root,
                    backdrop,
                    profile,
                    "PickScreenView",
                    expectSelectables: true,
                    disjointBands: new[] { headerBar, rosterPanel, rightPanel },
                    onReflow: pick.RefreshResponsiveLayout);
            }

            // 4. CodexScreenView (all 3 tabs: Kids, Weapons & Kit, Larong Kalye)
            {
                Canvas canvas = BuildMenuCanvasRig($"Codex_{width}x{height}", profile, out Camera cam);
                var codex = new CodexScreenView(canvas.transform, data, () => { });
                _disposables.Add(codex);
                codex.Show();

                RectTransform backdrop = codex.Root.Find("Backdrop") as RectTransform;
                Transform shell = codex.SafeRoot.Find("Shell");
                RectTransform header = shell != null ? shell.Find("Header") as RectTransform : null;

                for (int tab = 0; tab < 3; tab++)
                {
                    codex.SetActiveTab(tab);
                    RectTransform activePane = FindActiveSiblingAfter(shell, header);

                    AuditGuiSurface(
                        canvas,
                        cam,
                        codex.Root,
                        backdrop,
                        profile,
                        $"CodexScreenView(Tab={tab})",
                        expectSelectables: true,
                        disjointBands: new[] { header, activePane },
                        onReflow: codex.RefreshLayout);
                }
            }

            // 5. HistoryScreenView
            {
                Canvas canvas = BuildMenuCanvasRig($"History_{width}x{height}", profile, out Camera cam);
                var history = new HistoryScreenView(canvas.transform, data, () => { });
                _disposables.Add(history);
                history.Show();

                RectTransform backdrop = history.Root.Find("Backdrop") as RectTransform;
                Transform shell = history.SafeRoot.Find("Shell");
                RectTransform header = shell != null ? shell.Find("Header") as RectTransform : null;
                RectTransform body = shell != null ? shell.Find("Body") as RectTransform : null;

                AuditGuiSurface(
                    canvas,
                    cam,
                    history.Root,
                    backdrop,
                    profile,
                    "HistoryScreenView",
                    expectSelectables: true,
                    disjointBands: new[] { header, body },
                    onReflow: history.RefreshLayout);
            }

            // 6. SettingsScreenView (all 3 tabs: Audio, Controls, Graphics & Accessibility)
            {
                Canvas canvas = BuildMenuCanvasRig($"Settings_{width}x{height}", profile, out Camera cam);
                var settings = new SettingsScreenView(canvas.transform, data, () => { });
                _disposables.Add(settings);
                settings.Show();

                RectTransform backdrop = settings.Root.Find("Backdrop") as RectTransform;
                Transform shell = settings.SafeRoot.Find("Shell");
                RectTransform header = shell != null ? shell.Find("Header") as RectTransform : null;
                RectTransform tabBar = shell != null ? shell.Find("TabBar") as RectTransform : null;

                for (int tab = 0; tab < 3; tab++)
                {
                    settings.SetActiveTab(tab);
                    RectTransform activePane = FindActiveSiblingAfter(shell, tabBar);

                    AuditGuiSurface(
                        canvas,
                        cam,
                        settings.Root,
                        backdrop,
                        profile,
                        $"SettingsScreenView(Tab={tab})",
                        expectSelectables: true,
                        disjointBands: new[] { header, tabBar, activePane },
                        onReflow: settings.RefreshLayout);
                }
            }
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void MenuModalsAndNetworkFlows_AcrossCanonicalAspectMatrix_Meet48DpSafeContainmentFullBleedZeroOverlapAndZeroSmudge(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);
            DataSource data = LoadData();
            MatchBoot.ApplyBootResourcesAndGraphics(data);

            // 1. HomeSheetsView (Profile, IdCamera, GameOfMonth, Donate) + MenuStack QuitModal + QrCodeView Modal
            {
                Canvas canvas = BuildMenuCanvasRig($"HomeModals_{width}x{height}", profile, out Camera cam);
                var stack = new MenuStack(canvas.transform, onQuitConfirmed: () => { });
                _disposables.Add(stack);
                var home = new HomeScreenView(canvas.transform, data, _ => { }, _ => { });
                _disposables.Add(home);
                stack.Register(MenuStack.HomeId, home.Root, () =>
                {
                    home.Refresh();
                    home.RefreshLayout();
                });
                stack.Push(MenuStack.HomeId);

                RectTransform sheetsScrim = home.Sheets.Root.Find("Scrim") as RectTransform;
                Transform creamFill = home.Sheets.SafeRoot.Find("SheetCard/CardCreamFill");
                RectTransform sheetHeader = creamFill != null ? creamFill.Find("HeaderBand") as RectTransform : null;

                Action[] sheetOpeners =
                {
                    () => home.Sheets.OpenProfile(),
                    () => home.Sheets.OpenIdCamera(),
                    () => home.Sheets.OpenGameOfMonth(1),
                    () => home.Sheets.OpenDonate(),
                };
                string[] sheetNames = { "ProfileSheet", "IdCameraSheet", "GameOfMonthSheet", "DonateSheet" };

                for (int s = 0; s < sheetOpeners.Length; s++)
                {
                    sheetOpeners[s]();
                    Assert.IsTrue(home.Sheets.IsOpen, $"{sheetNames[s]} must open on {profile.Label}");

                    RectTransform activeBody = FindActiveSiblingAfter(creamFill, sheetHeader);
                    AuditGuiSurface(
                        canvas,
                        cam,
                        home.Sheets.Root,
                        sheetsScrim,
                        profile,
                        $"HomeSheetsView/{sheetNames[s]}",
                        expectSelectables: true,
                        disjointBands: new[] { sheetHeader, activeBody },
                        onReflow: home.Sheets.RefreshLayout);
                }
                home.Sheets.Close();

                // MenuStack QuitModal
                stack.ShowQuitModal();
                Assert.IsTrue(stack.IsQuitModalOpen, $"QuitModal must open on {profile.Label}");
                RectTransform quitScrim = stack.QuitModalRoot.Find("Scrim") as RectTransform;
                Transform quitBody = stack.QuitModalRoot.Find("SafeArea/QuitCardFrame/QuitCardBody") ??
                                     FindDescendantByName(stack.QuitModalRoot, "QuitCardBody");
                RectTransform quitHeader = quitBody != null ? quitBody.Find("HeaderBand") as RectTransform : null;

                AuditGuiSurface(
                    canvas,
                    cam,
                    stack.QuitModalRoot,
                    quitScrim,
                    profile,
                    "MenuStack/QuitModal",
                    expectSelectables: true,
                    disjointBands: new[]
                    {
                        quitHeader,
                        stack.QuitCancelButton != null ? stack.QuitCancelButton.GetComponent<RectTransform>() : null,
                        stack.QuitConfirmButton != null ? stack.QuitConfirmButton.GetComponent<RectTransform>() : null,
                    },
                    onReflow: stack.RefreshModalLayout);
                stack.HideQuitModal();

                // QrCodeView Modal (Share QR + Camera Scanner + Manual Input)
                QrCodeView qr = QrCodeView.CreateModal(
                    canvas.transform,
                    "ROOM CODE: KALY2026",
                    "ws://192.168.43.1:7777",
                    _ => { },
                    _ => true,
                    () => { },
                    "Scan from a nearby phone on the same Hotspot or enter code manually",
                    "KALY2026");

                RectTransform qrScrim = qr.Root.Find("Scrim") as RectTransform;
                Transform cardBody = qr.Root.Find("Safe/CardBorder/CardBody");
                RectTransform qrHeader = cardBody != null ? cardBody.Find("HeaderRow") as RectTransform : null;
                RectTransform qrBody = cardBody != null ? cardBody.Find("Body") as RectTransform : null;
                RectTransform qrFooter = cardBody != null ? cardBody.Find("FooterRow") as RectTransform : null;

                AuditGuiSurface(
                    canvas,
                    cam,
                    qr.Root,
                    qrScrim,
                    profile,
                    "QrCodeView/Modal",
                    expectSelectables: true,
                    disjointBands: new[] { qrHeader, qrBody, qrFooter },
                    onReflow: qr.RefreshLayout);
                qr.Close();
            }

            // 2. LobbyScreenView (Browser + Populated 5v5 Room + RoomQrModal via deterministic MemoryHub)
            {
                AppSession.Current.StopAllNetwork();
                AppSession.Current.Mode = GameMode.Online;
                AppSession.Current.Intent = LobbyIntent.Join;
                AppSession.Current.ServerUrl = "";

                SimData simData = AppSession.Current.EnsureSimData(data);
                NetRules netRules = NetRules.FromData(simData);
                var catalog = new StreetCatalog(simData);
                var memHub = new MemoryHub();
                var memServer = new GameServer(simData.GameRules, simData.MapLayout, netRules, 20261115L, catalog)
                {
                    AutoJoin = false,
                };
                memServer.Attach(memHub);

                var hostPeer = new GameClient(simData.GameRules, simData.MapLayout, netRules, catalog)
                {
                    PlayerName = "Ligaya",
                };
                hostPeer.Attach(memHub.Connect(1));
                GameClient localClient = AppSession.Current.EnsureClient(data);
                localClient.PlayerName = "Junjun";
                localClient.Attach(memHub.Connect(2));

                void PumpLobby(int ticks)
                {
                    const double dt = 1.0 / 30.0;
                    for (int i = 0; i < ticks; i++)
                    {
                        memHub.Poll();
                        memServer.Poll(dt);
                        hostPeer.Poll(dt);
                        localClient.Poll(dt);
                    }
                }

                try
                {
                    PumpLobby(3);
                    hostPeer.CreateRoom();
                    PumpLobby(3);
                    hostPeer.SetCharacter("jumong");
                    hostPeer.SetFillBots(true);
                    hostPeer.SetOpen(true);
                    hostPeer.SetReady(true);
                    PumpLobby(3);
                    string openCode = hostPeer.Room.GetStr("code", "");
                    localClient.ListRooms();
                    PumpLobby(3);

                    Canvas lobbyCanvas = BuildMenuCanvasRig($"Lobby_{width}x{height}", profile, out Camera lobbyCam);
                    using var lobby = new LobbyScreenView(
                        lobbyCanvas.transform,
                        data,
                        onBack: () => { },
                        onMatchStarted: () => { },
                        onPickKid: () => { },
                        qrSpriteProvider: QrCodeView.EncodeSprite,
                        lanIpProvider: () => "192.168.43.1",
                        qrScanRequester: QrCodeView.MakeScanRequester(lobbyCanvas.transform));
                    lobby.AutoPollNetwork = false;
                    lobby.SetFields("", "Junjun", openCode);
                    localClient.ListRooms();
                    PumpLobby(3);
                    lobby.OnShow();

                    RectTransform lobbyBackdrop = lobby.Root.Find("Backdrop") as RectTransform;
                    Transform lobbyFrame = lobby.Root.Find("SafeRoot/LobbyFrame");
                    RectTransform topBar = lobbyFrame != null ? lobbyFrame.Find("TopBar") as RectTransform : null;
                    RectTransform setupCard = lobbyFrame != null ? lobbyFrame.Find("OnlineConnectPanel/SetupCard") as RectTransform : null;
                    RectTransform openRoomsCard = lobbyFrame != null ? lobbyFrame.Find("OnlineConnectPanel/OpenRoomsCard") as RectTransform : null;

                    AuditGuiSurface(
                        lobbyCanvas,
                        lobbyCam,
                        lobby.Root,
                        lobbyBackdrop,
                        profile,
                        "LobbyScreenView/Browser",
                        expectSelectables: true,
                        disjointBands: new[] { topBar, setupCard, openRoomsCard },
                        onReflow: lobby.OnShow);

                    // Join populated 5v5 room
                    localClient.JoinRoom(openCode);
                    PumpLobby(4);
                    localClient.SetCharacter("narda");
                    localClient.SetReady(true);
                    PumpLobby(4);
                    lobby.OnShow();
                    Assert.IsTrue(lobby.InRoom, $"LobbyScreenView must enter populated RoomPanel on {profile.Label}");

                    RectTransform blueCol = lobbyFrame != null ? lobbyFrame.Find("RoomPanel/BlueTeamColumn") as RectTransform : null;
                    RectTransform redCol = lobbyFrame != null ? lobbyFrame.Find("RoomPanel/RedTeamColumn") as RectTransform : null;
                    RectTransform bottomControls = lobbyFrame != null ? lobbyFrame.Find("RoomPanel/BottomControls") as RectTransform : null;

                    AuditGuiSurface(
                        lobbyCanvas,
                        lobbyCam,
                        lobby.Root,
                        lobbyBackdrop,
                        profile,
                        "LobbyScreenView/RoomPanel",
                        expectSelectables: true,
                        disjointBands: new[] { topBar, blueCol, redCol, bottomControls },
                        onReflow: lobby.OnShow);

                    // Also exercise RoomQrModal when QrModalBtn is active
                    Transform qrModalBtnTr = topBar != null ? FindDescendantByName(topBar, "QrModalBtn") : null;
                    Button qrModalBtn = qrModalBtnTr != null ? qrModalBtnTr.GetComponent<Button>() : null;
                    if (qrModalBtn != null && qrModalBtn.gameObject.activeInHierarchy)
                    {
                        qrModalBtn.onClick.Invoke();
                        RectTransform roomQrModal = lobby.Root.Find("RoomQrModal") as RectTransform;
                        if (roomQrModal != null && roomQrModal.gameObject.activeInHierarchy)
                        {
                            RectTransform roomQrDim = roomQrModal.Find("Dim") as RectTransform;
                            AuditGuiSurface(
                                lobbyCanvas,
                                lobbyCam,
                                roomQrModal,
                                roomQrDim,
                                profile,
                                "LobbyScreenView/RoomQrModal",
                                expectSelectables: true,
                                onReflow: lobby.OnShow);
                            lobby.HandleBack();
                        }
                    }
                }
                finally
                {
                    hostPeer.Close();
                    memServer.CloseRooms();
                    AppSession.Current.StopAllNetwork();
                }
            }

            // 3. HotspotFlowView (Step 1 ChooseRole, Step 2A HostWizard, Step 2B GuestWizard)
            {
                AppSession.Current.StopAllNetwork();
                AppSession.Current.Mode = GameMode.HotspotHost;
                AppSession.Current.Intent = LobbyIntent.Create;

                try
                {
                    Canvas hsCanvas = BuildMenuCanvasRig($"Hotspot_{width}x{height}", profile, out Camera hsCam);
                    using var hotspot = new HotspotFlowView(
                        hsCanvas.transform,
                        data,
                        onBack: () => { },
                        onEnterLobby: () => { },
                        qrSpriteProvider: QrCodeView.EncodeSprite,
                        lanIpProvider: () => "192.168.43.1",
                        qrScanRequester: QrCodeView.MakeScanRequester(hsCanvas.transform));

                    RectTransform hsBackdrop = hotspot.Root.Find("Backdrop") as RectTransform;
                    Transform hsFrame = hotspot.Root.Find("SafeRoot/Frame");
                    RectTransform hsTopBar = hsFrame != null ? hsFrame.Find("TopBar") as RectTransform : null;

                    // Step 1: Choose Role
                    hotspot.ShowChooseRole();
                    RectTransform hostRoleCard = hsFrame != null ? hsFrame.Find("Step1ChooseRole/HostRoleCard") as RectTransform : null;
                    RectTransform guestRoleCard = hsFrame != null ? hsFrame.Find("Step1ChooseRole/GuestRoleCard") as RectTransform : null;

                    AuditGuiSurface(
                        hsCanvas,
                        hsCam,
                        hotspot.Root,
                        hsBackdrop,
                        profile,
                        "HotspotFlowView/ChooseRole",
                        expectSelectables: true,
                        disjointBands: new[] { hsTopBar, hostRoleCard, guestRoleCard },
                        onReflow: hotspot.OnShow);

                    // Step 2A: Host Wizard (ephemeral loopback port so zero OS port collisions)
                    int ephemeralPort = ReserveEphemeralTcpPort();
                    AndroidLocalLink.HandlePluginHotspotStarted("Kalyeah-4821", "larokalye", "WPA");
                    hotspot.StartHostWizard(port: ephemeralPort, seed: 42L, direct: true);
                    for (int i = 0; i < 4; i++)
                    {
                        AppSession.Current.PollNetwork(0.02);
                        hotspot.Tick(0.02f);
                    }

                    RectTransform hostInfoCard = hsFrame != null ? hsFrame.Find("Step2HostWizard/HostInfoCard") as RectTransform : null;
                    RectTransform qrCard = hsFrame != null ? hsFrame.Find("Step2HostWizard/QrCard") as RectTransform : null;

                    AuditGuiSurface(
                        hsCanvas,
                        hsCam,
                        hotspot.Root,
                        hsBackdrop,
                        profile,
                        "HotspotFlowView/HostWizard",
                        expectSelectables: true,
                        disjointBands: new[] { hsTopBar, hostInfoCard, qrCard },
                        onReflow: hotspot.OnShow);

                    // Step 2B: Guest Wizard
                    hotspot.StartGuestWizard(direct: true);
                    RectTransform guestListCard = hsFrame != null ? hsFrame.Find("Step2GuestWizard/DiscoveredHostsCard") as RectTransform : null;

                    AuditGuiSurface(
                        hsCanvas,
                        hsCam,
                        hotspot.Root,
                        hsBackdrop,
                        profile,
                        "HotspotFlowView/GuestWizard",
                        expectSelectables: true,
                        disjointBands: new[] { hsTopBar, guestListCard },
                        onReflow: hotspot.OnShow);
                }
                finally
                {
                    AppSession.Current.StopAllNetwork();
                    AndroidLocalLink.HandlePluginHotspotStopped();
                }
            }
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void MatchHudOverlaysAimCancelAndCardHand_AcrossCanonicalAspectMatrix_Meet48DpSafeContainmentFullBleedZeroOverlapAndZeroSmudge(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);
            DataSource data = LoadData();
            Ui.EnsureEventSystem();

            // 1. BriefingCard, CancelAimZone (Right-Handed + LeftHanded), CardHandView (Bottom + Sides, Right-Handed + LeftHanded),
            //    QuickChatModal, and PauseQuickSettingsSheet (+ LeaveConfirmRow)
            {
                AppSession.Current.Mode = GameMode.Practice;
                AppSession.Current.BriefingPending = true;
                SettingsStore.Current.CardsTrayMode = false;
                SettingsStore.Current.LeftHanded = false;

                Camera cam = BuildViewportCamera($"MatchModals_{width}x{height}", width, height, rawSafePx, orientation, out GameObject root);
                var setup = new MatchSetup
                {
                    Seed = 7L,
                    LocalCharacter = "narda",
                    SuperpowerLoadout = new[] { "bato_light", "tsinelas_light", "papel_shield" },
                    FirstTo = 3,
                };
                var sim = new SimBridge(data, setup);
                _disposables.Add(sim);
                sim.SkipIntro();
                sim.Step();

                var hud = new MatchHud(root.transform, sim, cam, interactive: true);
                _disposables.Add(hud);
                hud.RefreshLayout();
                hud.Refresh(sim, cam, null, null, 0f);

                // 1a. First-Match BriefingCard Modal
                Assert.IsTrue(hud.Overlays.IsBriefingVisible, $"BriefingCard must be visible when BriefingPending=true on {profile.Label}");
                RectTransform briefingRoot = hud.Overlays.Root.Find("BriefingCard") as RectTransform;
                RectTransform briefingBleed = briefingRoot != null ? briefingRoot.Find("Backdrop") as RectTransform : null;
                AuditGuiSurface(
                    hud.Canvas,
                    cam,
                    briefingRoot,
                    briefingBleed,
                    profile,
                    "MatchOverlays/BriefingCard",
                    expectSelectables: true);

                hud.Overlays.DismissBriefing();
                hud.Refresh(sim, cam, null, null, 0.05f);
                Assert.IsFalse(hud.Overlays.IsBriefingVisible);

                // 1b. AbilityArcView CancelAimZone (Right-Handed & LeftHanded)
                foreach (bool leftHanded in new[] { false, true })
                {
                    SettingsStore.Current.LeftHanded = leftHanded;
                    SettingsStore.Current.CardsTrayMode = false;
                    StageAbilityArcCancelHover(hud, sim, cam);

                    string ctx = $"AbilityArcCancel[{profile.Label},LH={leftHanded}]";
                    Assert.IsTrue(hud.AbilityArc.IsDraggingAim, $"{ctx} AbilityArc must be in active aim-drag state");
                    RectTransform cancelZone = hud.AbilityArc.CancelZoneRect;
                    Assert.IsNotNull(cancelZone, $"{ctx} CancelZoneRect");
                    Assert.IsTrue(cancelZone.gameObject.activeInHierarchy, $"{ctx} CancelZoneRect must be active while dragging ability");

                    Rect safePx = ScreenFitMetrics.ResolveSafeRectPx();
                    Rect sightboxPx = ComputeCenterSightbox(safePx);
                    float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
                    Rect cancelPx = GetScreenRect(cancelZone, cam);

                    AssertTouchTarget48Dp(cancelPx, minTouchPx, $"{ctx} CancelAimZone");
                    AssertContainedInSafeRect(safePx, cancelPx, $"{ctx} CancelAimZone");
                    Assert.IsFalse(sightboxPx.Overlaps(Shrink(cancelPx, 0.75f)),
                        $"{ctx}: CancelAimZone {cancelPx} intrudes into center sightbox {sightboxPx}");

                    var controls = new List<RectTransform>(16);
                    hud.CollectActiveControlRects(controls);
                    for (int i = 0; i < controls.Count; i++)
                    {
                        if (controls[i] == cancelZone) continue;
                        Rect other = GetScreenRect(controls[i], cam);
                        Assert.IsFalse(
                            Shrink(cancelPx, 1.0f).Overlaps(Shrink(other, 1.0f)),
                            $"{ctx}: CancelAimZone {cancelPx} overlaps '{controls[i].name}' {other}");
                    }
                    AssertNoDarkInkDarkOutlineSmudge(hud.SafeRoot, ctx);

                    hud.AbilityArc.CancelAll();
                    hud.Refresh(sim, cam, null, null, 0f);
                }

                // 1c. CardHandView (CardsTrayMode = true across "bottom" & "sides" placements, Right-Handed & LeftHanded)
                foreach (string placement in new[] { "bottom", "sides" })
                {
                    foreach (bool leftHanded in new[] { false, true })
                    {
                        SettingsStore.Current.LeftHanded = leftHanded;
                        SettingsStore.Current.CardsTrayPlacement = placement;
                        SettingsStore.Current.CardsTrayMode = true;
                        hud.RefreshLayout();
                        hud.Refresh(sim, cam, null, null, 0.05f);
                        Canvas.ForceUpdateCanvases();

                        string ctx = $"CardHandView[{profile.Label},Placement={placement},LH={leftHanded}]";
                        Assert.IsTrue(hud.UsingCardsTray, $"{ctx} MatchHud must switch to CardHandView");
                        Assert.IsTrue(hud.CardHand.Root.gameObject.activeInHierarchy, $"{ctx} CardHand.Root must be active");

                        Rect safePx = ScreenFitMetrics.ResolveSafeRectPx();
                        Rect sightboxPx = ComputeCenterSightbox(safePx);
                        float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);

                        IReadOnlyList<RectTransform> slots = hud.CardHand.CardSlotRects;
                        Assert.AreEqual(CardHandView.MaxSlots, slots.Count);

                        var trayItems = new List<(string Name, Rect Rect)>(slots.Count + 2);
                        for (int i = 0; i < slots.Count; i++)
                        {
                            Rect sr = GetScreenRect(slots[i], cam);
                            AssertTouchTarget48Dp(sr, minTouchPx, $"{ctx} CardSlot_{i}");
                            AssertContainedInSafeRect(safePx, sr, $"{ctx} CardSlot_{i}");
                            Assert.IsFalse(sightboxPx.Overlaps(Shrink(sr, 0.75f)),
                                $"{ctx}: CardSlot_{i} {sr} intrudes into center sightbox {sightboxPx}");
                            trayItems.Add(($"CardSlot_{i}", sr));
                        }

                        if (hud.CardHand.NextCardRect != null && hud.CardHand.NextCardRect.gameObject.activeInHierarchy)
                        {
                            Rect nextRect = GetScreenRect(hud.CardHand.NextCardRect, cam);
                            AssertContainedInSafeRect(safePx, nextRect, $"{ctx} NextCard");
                            Assert.IsFalse(sightboxPx.Overlaps(Shrink(nextRect, 0.75f)),
                                $"{ctx}: NextCard {nextRect} intrudes into center sightbox {sightboxPx}");
                            trayItems.Add(("NextCard", nextRect));
                        }

                        if (hud.CardHand.ElixirBarRect != null && hud.CardHand.ElixirBarRect.gameObject.activeInHierarchy)
                        {
                            Rect elixirRect = GetScreenRect(hud.CardHand.ElixirBarRect, cam);
                            AssertContainedInSafeRect(safePx, elixirRect, $"{ctx} ElixirBar");
                            trayItems.Add(("ElixirBar", elixirRect));
                        }

                        for (int i = 0; i < trayItems.Count; i++)
                        {
                            for (int j = i + 1; j < trayItems.Count; j++)
                            {
                                Assert.IsFalse(
                                    Shrink(trayItems[i].Rect, 1.0f).Overlaps(Shrink(trayItems[j].Rect, 1.0f)),
                                    $"{ctx}: '{trayItems[i].Name}' {trayItems[i].Rect} overlaps '{trayItems[j].Name}' {trayItems[j].Rect}");
                            }
                        }

                        Rect joyPx = GetScreenRect(hud.JoystickBaseRect, cam);
                        Rect headerPx = GetScreenRect(hud.HeaderRect, cam);
                        for (int i = 0; i < slots.Count; i++)
                        {
                            Rect sr = trayItems[i].Rect;
                            Assert.IsFalse(Shrink(sr, 1.0f).Overlaps(Shrink(joyPx, 1.0f)), $"{ctx}: CardSlot_{i} overlaps VirtualJoystick");
                            Assert.IsFalse(Shrink(sr, 1.0f).Overlaps(Shrink(headerPx, 1.0f)), $"{ctx}: CardSlot_{i} overlaps Header");
                        }

                        AssertNoDarkInkDarkOutlineSmudge(hud.CardHand.Root, ctx);
                    }
                }

                SettingsStore.Current.CardsTrayMode = false;
                SettingsStore.Current.LeftHanded = false;
                hud.RefreshLayout();
                hud.Refresh(sim, cam, null, null, 0f);

                // 1d. QuickChatModal
                hud.Overlays.SetQuickChatOpen(true);
                hud.RefreshLayout();
                hud.Refresh(sim, cam, null, null, 0f);
                RectTransform quickChatRoot = hud.Overlays.Root.Find("QuickChatModal") as RectTransform;
                RectTransform quickChatDim = quickChatRoot != null ? quickChatRoot.Find("DimBackdrop") as RectTransform : null;
                AuditGuiSurface(
                    hud.Canvas,
                    cam,
                    quickChatRoot,
                    quickChatDim,
                    profile,
                    "MatchOverlays/QuickChatModal",
                    expectSelectables: true);
                hud.Overlays.SetQuickChatOpen(false);

                // 1e. PauseQuickSettingsSheet (default footer + LeaveConfirmRow state)
                hud.Overlays.SetPaused(true);
                hud.RefreshLayout();
                hud.Refresh(sim, cam, null, null, 0f);
                RectTransform pauseRoot = hud.Overlays.Root.Find("PauseQuickSettingsSheet") as RectTransform;
                RectTransform pauseDim = pauseRoot != null ? pauseRoot.Find("DimBackdrop") as RectTransform : null;
                Transform pauseCard = pauseRoot != null ? pauseRoot.Find("SettingsCard") : null;
                RectTransform pauseFooter = pauseCard != null ? pauseCard.Find("FooterRow") as RectTransform : null;
                ScrollRect pauseScroll = pauseCard != null ? pauseCard.GetComponentInChildren<ScrollRect>(false) : null;
                RectTransform pauseViewport = pauseScroll != null ? pauseScroll.viewport : null;

                AuditGuiSurface(
                    hud.Canvas,
                    cam,
                    pauseRoot,
                    pauseDim,
                    profile,
                    "MatchOverlays/PauseSheet",
                    expectSelectables: true,
                    disjointBands: new[] { pauseViewport, pauseFooter },
                    onReflow: hud.RefreshLayout);

                Transform leaveBtnTr = pauseFooter != null ? pauseFooter.Find("LeaveMatchButton") : null;
                Button leaveBtn = leaveBtnTr != null ? leaveBtnTr.GetComponent<Button>() : null;
                Assert.IsNotNull(leaveBtn, "PauseQuickSettingsSheet LeaveMatchButton");
                leaveBtn.onClick.Invoke();
                Assert.IsTrue(hud.Overlays.IsLeaveConfirmOpen, $"Pause LeaveConfirmRow must open on {profile.Label}");

                RectTransform leaveConfirmRow = pauseCard != null ? pauseCard.Find("LeaveConfirmRow") as RectTransform : null;
                AuditGuiSurface(
                    hud.Canvas,
                    cam,
                    pauseRoot,
                    pauseDim,
                    profile,
                    "MatchOverlays/PauseSheet_LeaveConfirm",
                    expectSelectables: true,
                    disjointBands: new[] { pauseViewport, leaveConfirmRow },
                    onReflow: hud.RefreshLayout);
                hud.Overlays.SetPaused(false);
            }

            // 2. TaggedSidewalkOverlay + SwapLoadoutModal
            {
                AppSession.Current.Mode = GameMode.Practice;
                AppSession.Current.BriefingPending = false;
                DataSource tagData = new DataSource(name =>
                {
                    string json = data.Text(name);
                    if (name != "rules__game_rules") return json;
                    json = Regex.Replace(json, "\"player_max_hp\"\\s*:\\s*[^,\\r\\n}]+", "\"player_max_hp\": 1");
                    json = Regex.Replace(json, "\"practice_fighters\"\\s*:\\s*[^,\\r\\n}]+", "\"practice_fighters\": 2");
                    json = Regex.Replace(json, "\"dummy_stand_x\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_stand_x\": 0.0");
                    json = Regex.Replace(json, "\"dummy_patrol_x\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_patrol_x\": 2.0");
                    json = Regex.Replace(json, "\"dummy_z\"\\s*:\\s*[^,\\r\\n}]+", "\"dummy_z\": 50.0");
                    return json;
                });

                Camera tagCam = BuildViewportCamera($"Tagged_{width}x{height}", width, height, rawSafePx, orientation, out GameObject tagRoot);
                var tagSetup = new MatchSetup
                {
                    Seed = 123L,
                    LocalCharacter = "narda",
                    SuperpowerLoadout = new[] { "bato_light", "tsinelas_light", "papel_shield" },
                    TrainingMode = true,
                };
                var tagSim = new SimBridge(tagData, tagSetup);
                _disposables.Add(tagSim);
                tagSim.SkipIntro();
                tagSim.SetPlayerPositionForPreview(SimBridge.LocalId, new System.Numerics.Vector2(0f, 52.0f), -System.Numerics.Vector2.UnitY);
                tagSim.SetPlayerPositionForPreview(SimBridge.TrainingAllyRescuerId, new System.Numerics.Vector2(-18f, 0f), System.Numerics.Vector2.UnitY);

                for (int i = 0; i < 180 && (!tagSim.LocalKid.HasValue || !tagSim.LocalKid.Value.Tagged); i++)
                {
                    tagSim.SetLocalInput(new LocalInputFrame { Move = new System.Numerics.Vector2(0f, -1f) });
                    tagSim.Step();
                }
                Assert.IsTrue(tagSim.LocalKid.HasValue && tagSim.LocalKid.Value.Tagged, "Local kid must enter Tagged state");

                var tagHud = new MatchHud(tagRoot.transform, tagSim, tagCam, interactive: true);
                _disposables.Add(tagHud);
                tagHud.RefreshLayout();
                tagHud.Refresh(tagSim, tagCam, null, null, 0.05f);
                Assert.IsTrue(tagHud.Overlays.IsTaggedOverlayVisible, $"TaggedSidewalkOverlay must be visible on {profile.Label}");

                RectTransform taggedPanel = tagHud.Overlays.Root.Find("TaggedSidewalkOverlay") as RectTransform;
                AuditGuiSurface(
                    tagHud.Canvas,
                    tagCam,
                    taggedPanel,
                    fullBleedBackdrop: null,
                    profile,
                    "MatchOverlays/TaggedSidewalkOverlay",
                    expectSelectables: true,
                    disjointBands: new[] { taggedPanel, tagHud.HeaderRect, tagHud.PauseButtonRect, tagHud.QuickChatButtonRect },
                    onReflow: tagHud.RefreshLayout);

                // Open D13 SwapLoadoutModal
                tagHud.Overlays.SetSwapPickerOpen(true);
                tagHud.Refresh(tagSim, tagCam, null, null, 0f);
                Assert.IsTrue(tagHud.Overlays.IsSwapPickerOpen);

                RectTransform swapRoot = tagHud.Overlays.Root.Find("SwapLoadoutModal") as RectTransform;
                RectTransform swapDim = swapRoot != null ? swapRoot.Find("DimBackdrop") as RectTransform : null;
                Transform swapCard = swapRoot != null ? swapRoot.Find("SwapCard") : null;
                ScrollRect swapScroll = swapCard != null ? swapCard.GetComponentInChildren<ScrollRect>(false) : null;
                RectTransform swapViewport = swapScroll != null ? swapScroll.viewport : null;
                Transform confirmBtnTr = swapCard != null ? FindDescendantByName(swapCard, "ConfirmSwapBtn") ?? FindDescendantByName(swapCard, "ConfirmButton") : null;
                RectTransform swapConfirm = confirmBtnTr as RectTransform;

                AuditGuiSurface(
                    tagHud.Canvas,
                    tagCam,
                    swapRoot,
                    swapDim,
                    profile,
                    "MatchOverlays/SwapLoadoutModal",
                    expectSelectables: true,
                    disjointBands: swapConfirm != null ? new[] { swapViewport, swapConfirm } : new[] { swapViewport },
                    onReflow: tagHud.RefreshLayout);
                tagHud.Overlays.SetSwapPickerOpen(false);
            }

            // 3. MatchVictoryOverlay (MatchOver Victory + Scoreboard + Ranked LP)
            {
                AppSession.Current.Mode = GameMode.Practice;
                AppSession.Current.BriefingPending = false;
                AppSession.Current.ConsumeLastOutcome();
                DataSource winData = new DataSource(name =>
                {
                    string json = data.Text(name);
                    if (name != "rules__game_rules") return json;
                    json = Regex.Replace(json, "\"capture_solo_time\"\\s*:\\s*[^,\\r\\n}]+", "\"capture_solo_time\": 0.05");
                    json = Regex.Replace(json, "\"point_freeze_time\"\\s*:\\s*[^,\\r\\n}]+", "\"point_freeze_time\": 0.05");
                    json = Regex.Replace(json, "\"first_to\"\\s*:\\s*[^,\\r\\n}]+", "\"first_to\": 1");
                    return json;
                });

                Camera winCam = BuildViewportCamera($"Victory_{width}x{height}", width, height, rawSafePx, orientation, out GameObject winRoot);
                var winSetup = new MatchSetup
                {
                    Seed = 7L,
                    LocalCharacter = "narda",
                    SuperpowerLoadout = new[] { "bato_light", "tsinelas_light", "papel_shield" },
                    FirstTo = 1,
                };
                var winSim = new SimBridge(winData, winSetup);
                _disposables.Add(winSim);
                winSim.SkipIntro();

                System.Numerics.Vector2 enemyBasePos = new System.Numerics.Vector2(0f, -52f);
                for (int i = 0; i < winSim.Bases.Count; i++)
                {
                    if (winSim.Bases[i].Team != SimBridge.LocalTeam)
                    {
                        enemyBasePos = winSim.Bases[i].Center;
                        break;
                    }
                }
                foreach (int foeId in SimBridge.EnemyIds)
                    winSim.SetPlayerPositionForPreview(foeId, System.Numerics.Vector2.Zero, System.Numerics.Vector2.UnitY);

                for (int i = 0; i < 30 && winSim.Phase != MatchPhaseKind.MatchOver; i++)
                {
                    winSim.SetPlayerPositionForPreview(SimBridge.LocalId, enemyBasePos, -System.Numerics.Vector2.UnitY);
                    winSim.Step();
                }
                Assert.AreEqual(MatchPhaseKind.MatchOver, winSim.Phase, "Match must reach MatchOver");

                var winHud = new MatchHud(winRoot.transform, winSim, winCam, interactive: true);
                _disposables.Add(winHud);
                winHud.Refresh(winSim, winCam, null, null, 0.35f);

                RectTransform victoryRoot = winHud.Overlays.Root.Find("MatchVictoryOverlay") as RectTransform;
                RectTransform victoryDim = victoryRoot != null ? victoryRoot.Find("Backdrop") as RectTransform : null;
                Transform victoryCard = victoryRoot != null ? victoryRoot.Find("VictoryCard") : null;
                RectTransform scoreRow = victoryCard != null ? victoryCard.Find("ScoreRow") as RectTransform : null;
                RectTransform rankCard = victoryCard != null ? victoryCard.Find("RankCard") as RectTransform : null;

                AuditGuiSurface(
                    winHud.Canvas,
                    winCam,
                    victoryRoot,
                    victoryDim,
                    profile,
                    "MatchOverlays/MatchVictoryOverlay",
                    expectSelectables: true,
                    disjointBands: new[] { scoreRow, rankCard },
                    onReflow: winHud.RefreshLayout);
            }
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void StreetHudModalsAndRoulette_AcrossCanonicalAspectMatrix_Meet48DpSafeContainmentFullBleedZeroOverlapAndZeroSmudge(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);
            DataSource data = LoadData();
            Ui.EnsureEventSystem();

            // 1. TumbangPreso Roulette Banner (TumbangRoll) + Active Throw Hold CancelZone (Right-Handed & LeftHanded)
            {
                long throwerSeed = FindTumbangThrowerSeed(data);
                Camera tumbangCam = BuildViewportCamera($"TumbangModals_{width}x{height}", width, height, rawSafePx, orientation, out GameObject tumbangRoot);
                var tumbangBridge = new StreetBridge(data, StreetGameKind.TumbangPreso, throwerSeed, "narda");
                var tumbangHud = new StreetHudView(tumbangRoot.transform, tumbangBridge, tumbangCam, interactive: true, () => { }, () => { });
                _disposables.Add(tumbangHud);

                for (int i = 0; i < 15; i++)
                {
                    tumbangBridge.Step();
                    tumbangHud.Refresh(tumbangBridge, tumbangBridge.TickDt);
                }
                tumbangHud.ApplyResponsiveLayout();
                Canvas.ForceUpdateCanvases();

                Assert.IsTrue(tumbangHud.IsTumbangRollVisible, $"TumbangRoll must be visible during TumbangPreso intro on {profile.Label}");
                Rect safePx = ScreenFitMetrics.ResolveSafeRectPx();
                Rect sightboxPx = ComputeCenterSightbox(safePx);
                Rect rollPx = GetScreenRect(tumbangHud.TumbangRollRect, tumbangCam);

                AssertContainedInSafeRect(safePx, rollPx, $"TumbangRoll[{profile.Label}]");
                Assert.IsFalse(sightboxPx.Overlaps(Shrink(rollPx, 0.75f)),
                    $"TumbangRoll[{profile.Label}] {rollPx} must stay outside 45%x50% center sightbox {sightboxPx}");
                AssertBandsDisjointAndSafe(
                    tumbangCam,
                    safePx,
                    $"TumbangRollChrome[{profile.Label}]",
                    tumbangHud.HeaderRect,
                    tumbangHud.TimerPillRect,
                    tumbangHud.TumbangRollRect,
                    tumbangHud.LeaveButtonRect);
                AssertNoDarkInkDarkOutlineSmudge(tumbangHud.TumbangRollRect, $"TumbangRoll[{profile.Label}]");

                // Advance into active TumbangPreso gameplay and test CancelZoneRect in both handednesses
                tumbangBridge.SkipIntro();
                for (int w = 0; w < 150 && tumbangBridge.IsIntro; w++)
                    tumbangBridge.Step();
                for (int t = 0; t < 5; t++)
                    tumbangBridge.Step();

                foreach (bool leftHanded in new[] { false, true })
                {
                    SettingsStore.Current.LeftHanded = leftHanded;
                    tumbangHud.ApplyResponsiveLayout();
                    tumbangHud.Refresh(tumbangBridge, 0.05f);
                    Canvas.ForceUpdateCanvases();

                    if (tumbangHud.PrimaryButtonRect != null && tumbangHud.PrimaryButtonRect.gameObject.activeInHierarchy)
                    {
                        var downHandler = tumbangHud.PrimaryButtonRect.GetComponent<IPointerDownHandler>();
                        Vector2 btnCenter = RectTransformUtility.WorldToScreenPoint(
                            tumbangCam,
                            tumbangHud.PrimaryButtonRect.TransformPoint(tumbangHud.PrimaryButtonRect.rect.center));
                        var ptr = new PointerEventData(EventSystem.current) { pointerId = 77, position = btnCenter };
                        downHandler?.OnPointerDown(ptr);
                        tumbangHud.Refresh(tumbangBridge, 0.02f);
                        Canvas.ForceUpdateCanvases();

                        string ctx = $"StreetCancelZone[{profile.Label},LH={leftHanded}]";
                        Assert.IsTrue(tumbangHud.CancelZoneRect.gameObject.activeInHierarchy, $"{ctx} CancelZoneRect must activate while holding Throw");
                        Rect cancelPx = GetScreenRect(tumbangHud.CancelZoneRect, tumbangCam);
                        float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);

                        AssertTouchTarget48Dp(cancelPx, minTouchPx, ctx);
                        AssertContainedInSafeRect(safePx, cancelPx, ctx);
                        Assert.IsFalse(sightboxPx.Overlaps(Shrink(cancelPx, 0.75f)),
                            $"{ctx} {cancelPx} intrudes into center sightbox {sightboxPx}");

                        var activeRects = new List<RectTransform>(12);
                        tumbangHud.CollectActiveControlRects(activeRects);
                        for (int i = 0; i < activeRects.Count; i++)
                        {
                            if (activeRects[i] == tumbangHud.CancelZoneRect) continue;
                            Rect other = GetScreenRect(activeRects[i], tumbangCam);
                            Assert.IsFalse(Shrink(cancelPx, 1.0f).Overlaps(Shrink(other, 1.0f)),
                                $"{ctx}: CancelZone {cancelPx} overlaps '{activeRects[i].name}' {other}");
                        }

                        tumbangHud.CancelActiveTouches();
                        tumbangHud.Refresh(tumbangBridge, 0f);
                    }
                }
                SettingsStore.Current.LeftHanded = false;
            }

            // 2. Street LeaveConfirmOverlay (Pause/Leave Modal) + MatchOverOverlay (Summary + Ranked LP Modal)
            {
                DataSource fastPatData = new DataSource(name =>
                {
                    string json = data.Text(name);
                    if (name != "rules__patintero") return json;
                    json = Regex.Replace(json, "\"home_z\"\\s*:\\s*[^,\\r\\n}]+", "\"home_z\": 1.5");
                    json = Regex.Replace(json, "\"goal_z\"\\s*:\\s*[^,\\r\\n}]+", "\"goal_z\": -1.5");
                    json = Regex.Replace(json, "\"tag_circle_radius\"\\s*:\\s*[^,\\r\\n}]+", "\"tag_circle_radius\": 0.01");
                    return Regex.Replace(json, "\"match_seconds\"\\s*:\\s*[^,\\r\\n}]+", "\"match_seconds\": 1.2");
                });

                Camera patCam = BuildViewportCamera($"StreetModals_{width}x{height}", width, height, rawSafePx, orientation, out GameObject patRoot);
                var patBridge = new StreetBridge(fastPatData, StreetGameKind.Patintero, 11L, "narda");
                patBridge.SkipIntro();
                for (int w = 0; w < 150 && patBridge.IsIntro; w++)
                    patBridge.Step();

                var patHud = new StreetHudView(patRoot.transform, patBridge, patCam, interactive: true, () => { }, () => { });
                _disposables.Add(patHud);
                patHud.ApplyResponsiveLayout();
                patHud.Refresh(patBridge, 0.05f);

                // 2a. LeaveConfirmOverlay
                patHud.SetLeaveConfirmOpen(true);
                patHud.ApplyResponsiveLayout();
                patHud.Refresh(patBridge, 0f);
                Assert.IsTrue(patHud.IsLeaveConfirmVisible, $"Street LeaveConfirmOverlay must open on {profile.Label}");
                Assert.IsFalse(patHud.Joystick.gameObject.activeInHierarchy, "Gameplay Joystick must hide while LeaveConfirmOverlay is open");

                RectTransform leaveOverlay = patHud.Canvas.transform.Find("LeaveConfirmOverlay") as RectTransform;
                RectTransform leaveDimmer = leaveOverlay != null ? leaveOverlay.Find("Dimmer") as RectTransform : null;
                Transform leaveCard = leaveOverlay != null ? leaveOverlay.Find("ModalSafe/LeaveCard") : null;
                RectTransform leaveHeadline = leaveCard != null ? leaveCard.Find("Headline") as RectTransform : null;
                RectTransform leaveBody = leaveCard != null ? leaveCard.Find("Body") as RectTransform : null;
                RectTransform resumeBtn = leaveCard != null ? leaveCard.Find("ResumeButton") as RectTransform : null;
                RectTransform confirmLeaveBtn = leaveCard != null ? leaveCard.Find("ConfirmLeaveButton") as RectTransform : null;

                AuditGuiSurface(
                    patHud.Canvas,
                    patCam,
                    leaveOverlay,
                    leaveDimmer,
                    profile,
                    "StreetHudView/LeaveConfirmOverlay",
                    expectSelectables: true,
                    disjointBands: new[] { leaveHeadline, leaveBody, resumeBtn, confirmLeaveBtn },
                    onReflow: patHud.ApplyResponsiveLayout);
                patHud.SetLeaveConfirmOpen(false);

                // 2b. MatchOverOverlay
                for (int i = 0; i < 120 && !patBridge.IsMatchOver; i++)
                {
                    patBridge.Step();
                    patHud.Refresh(patBridge, patBridge.TickDt);
                }
                patHud.Refresh(patBridge, 0f);
                Assert.IsTrue(patHud.IsMatchOverVisible, $"Street MatchOverOverlay must appear on {profile.Label}");
                Assert.IsFalse(patHud.Joystick.gameObject.activeInHierarchy, "Gameplay Joystick must hide while MatchOverOverlay is open");

                RectTransform overOverlay = patHud.Canvas.transform.Find("MatchOverOverlay") as RectTransform;
                RectTransform overDimmer = overOverlay != null ? overOverlay.Find("Dimmer") as RectTransform : null;
                Transform summaryCard = overOverlay != null ? overOverlay.Find("ModalSafe/SummaryCard") : null;
                RectTransform overHeadline = summaryCard != null ? summaryCard.Find("Headline") as RectTransform : null;
                RectTransform overSubhead = summaryCard != null ? summaryCard.Find("Subhead") as RectTransform : null;
                RectTransform overLpBadge = summaryCard != null ? summaryCard.Find("RankedLpBadge") as RectTransform : null;
                RectTransform overPlayAgain = summaryCard != null ? summaryCard.Find("PlayAgainButton") as RectTransform : null;
                RectTransform overHome = summaryCard != null ? summaryCard.Find("HomeButton") as RectTransform : null;

                AuditGuiSurface(
                    patHud.Canvas,
                    patCam,
                    overOverlay,
                    overDimmer,
                    profile,
                    "StreetHudView/MatchOverOverlay",
                    expectSelectables: true,
                    disjointBands: new[] { overHeadline, overSubhead, overLpBadge, overPlayAgain, overHome },
                    onReflow: patHud.ApplyResponsiveLayout);
            }
        }

        // =================================================================================================================
        // Unified Surface Bounds, >= 48 dp, Full-Bleed Scrim, Overlap & Smudge Auditors
        // =================================================================================================================

        static void AuditGuiSurface(
            Canvas canvas,
            Camera cam,
            RectTransform surfaceRoot,
            RectTransform fullBleedBackdrop,
            ViewportProfile profile,
            string surfaceLabel,
            bool expectSelectables,
            RectTransform[] disjointBands = null,
            Action onReflow = null)
        {
            Assert.IsNotNull(canvas, $"{surfaceLabel}[{profile.Label}] Canvas");
            Assert.IsNotNull(surfaceRoot, $"{surfaceLabel}[{profile.Label}] surfaceRoot");
            Assert.IsTrue(surfaceRoot.gameObject.activeInHierarchy, $"{surfaceLabel}[{profile.Label}] surfaceRoot must be active");

            ForceFullCanvasAndSafeAreaUpdate(canvas, surfaceRoot, onReflow);

            Rect expectedSafePx = ScreenFitMetrics.ResolveSafeRectPx();
            float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
            string ctx = $"{surfaceLabel}[{profile.Label}]";

            // (c1) Full-bleed backdrop/scrim covers 100% of raw Canvas bounds (including notches & 18 dp gesture bar)
            if (fullBleedBackdrop != null)
                AssertFullBleedRect(fullBleedBackdrop, cam, profile, $"{ctx}/FullBleedBackdrop({fullBleedBackdrop.name})");

            // (c2) Every active ScrollRect viewport inside surfaceRoot must sit strictly inside SafeArea
            ScrollRect[] scrolls = surfaceRoot.GetComponentsInChildren<ScrollRect>(includeInactive: false);
            for (int i = 0; i < scrolls.Length; i++)
            {
                ScrollRect sr = scrolls[i];
                if (sr == null || !sr.isActiveAndEnabled || IsHiddenByCanvasGroup(sr.transform, surfaceRoot))
                    continue;
                RectTransform vp = sr.viewport != null ? sr.viewport : sr.GetComponent<RectTransform>();
                if (vp != null)
                    AssertContainedInSafeRect(expectedSafePx, GetScreenRect(vp, cam), $"{ctx} ScrollViewport '{GetRelativePath(surfaceRoot, vp)}'");
            }

            // Gather active interactive Selectables (Button, Toggle, InputField; excluding Slider/Scrollbar tracks & non-Selectable catchers)
            Selectable[] allSelectables = surfaceRoot.GetComponentsInChildren<Selectable>(includeInactive: false);
            var activeControls = new List<(Selectable Sel, RectTransform Rt, Rect UnclippedPx)>(allSelectables.Length);

            for (int i = 0; i < allSelectables.Length; i++)
            {
                Selectable sel = allSelectables[i];
                if (!IsAuditedInteractiveSelectable(sel, surfaceRoot))
                    continue;

                var rt = sel.transform as RectTransform;
                if (rt == null) continue;

                Rect rawRect = GetScreenRect(rt, cam);
                if (rawRect.width >= profile.Width - 4f && rawRect.height >= profile.Height - 4f)
                    continue;

                activeControls.Add((sel, rt, rawRect));
            }

            if (expectSelectables)
                Assert.Greater(activeControls.Count, 0, $"{ctx}: expected at least 1 active interactive Selectable control");

            var controlsByParent = new Dictionary<Transform, List<(string Path, Rect UnclippedPx)>>();

            for (int i = 0; i < activeControls.Count; i++)
            {
                var item = activeControls[i];
                string relPath = GetRelativePath(surfaceRoot, item.Rt);

                // (a) >= 48 dp physical touch floor on unclipped control rect
                AssertTouchTarget48Dp(item.UnclippedPx, minTouchPx, $"{ctx} Selectable '{relPath}'");

                // Zero nested interactive Selectables (e.g. Button inside Button or InputField inside Button)
                for (int j = 0; j < activeControls.Count; j++)
                {
                    if (i == j) continue;
                    Assert.IsFalse(
                        item.Rt.IsChildOf(activeControls[j].Rt),
                        $"{ctx}: Selectable '{relPath}' is nested inside ancestor Selectable '{GetRelativePath(surfaceRoot, activeControls[j].Rt)}'");
                }

                // (c3) SafeArea containment (clamped to ancestor RectMask2D / Mask clip rect for scrolled items)
                if (TryGetAncestorClipRect(item.Rt, surfaceRoot, cam, out Rect clipPx))
                {
                    if (TryIntersectRect(item.UnclippedPx, clipPx, out Rect visiblePx) && visiblePx.width > 1.5f && visiblePx.height > 1.5f)
                        AssertContainedInSafeRect(expectedSafePx, visiblePx, $"{ctx} clipped Selectable '{relPath}'");
                }
                else
                {
                    AssertContainedInSafeRect(expectedSafePx, item.UnclippedPx, $"{ctx} Selectable '{relPath}'");
                }

                Transform parent = item.Rt.parent;
                if (parent != null)
                {
                    if (!controlsByParent.TryGetValue(parent, out var list))
                    {
                        list = new List<(string, Rect)>(8);
                        controlsByParent[parent] = list;
                    }
                    list.Add((relPath, item.UnclippedPx));
                }
            }

            // (b1) Zero unintended sibling Selectable rect overlaps within every shared parent container
            foreach (KeyValuePair<Transform, List<(string Path, Rect UnclippedPx)>> kvp in controlsByParent)
            {
                List<(string Path, Rect UnclippedPx)> siblings = kvp.Value;
                for (int i = 0; i < siblings.Count; i++)
                {
                    for (int j = i + 1; j < siblings.Count; j++)
                    {
                        Rect a = Shrink(siblings[i].UnclippedPx, 1.0f);
                        Rect b = Shrink(siblings[j].UnclippedPx, 1.0f);
                        Assert.IsFalse(
                            a.Overlaps(b),
                            $"{ctx}: sibling Selectables '{siblings[i].Path}' {siblings[i].UnclippedPx} and '{siblings[j].Path}' {siblings[j].UnclippedPx} overlap");
                    }
                }
            }

            // (b2) Disjoint structural bands (Header vs TabBar vs Body/Content/Footer) + SafeArea containment
            if (disjointBands != null && disjointBands.Length > 0)
                AssertBandsDisjointAndSafe(cam, expectedSafePx, ctx, disjointBands);

            // (d) Zero dark-ink + dark-outline text smudge
            AssertNoDarkInkDarkOutlineSmudge(surfaceRoot, ctx);

            // (e) Text integrity: positive rect dimensions (>= 8x8) & zero VerticalWrapMode.Truncate cutoff
            AssertTextIntegrity(surfaceRoot, ctx);
        }

        static void AssertFullBleedRect(RectTransform bleedRect, Camera cam, ViewportProfile profile, string label)
        {
            Assert.IsNotNull(bleedRect, $"{label} RectTransform must exist");
            Assert.IsTrue(bleedRect.gameObject.activeInHierarchy, $"{label} must be active");
            Rect px = GetScreenRect(bleedRect, cam);
            Assert.LessOrEqual(px.xMin, 1.5f, $"{label} left edge must cover full raw screen (0), got {px}");
            Assert.LessOrEqual(px.yMin, 1.5f, $"{label} bottom edge must cover full raw screen (0), got {px}");
            Assert.GreaterOrEqual(px.xMax, profile.Width - 1.5f, $"{label} right edge must cover full raw width ({profile.Width}), got {px}");
            Assert.GreaterOrEqual(px.yMax, profile.Height - 1.5f, $"{label} top edge must cover full raw height ({profile.Height}), got {px}");
        }

        static void AssertBandsDisjointAndSafe(Camera cam, Rect safePx, string ctx, params RectTransform[] bands)
        {
            var activeBands = new List<(string Name, Rect Rect)>(bands.Length);
            for (int i = 0; i < bands.Length; i++)
            {
                RectTransform b = bands[i];
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                Rect r = GetScreenRect(b, cam);
                AssertContainedInSafeRect(safePx, r, $"{ctx} band '{b.name}'");
                activeBands.Add((b.name, r));
            }

            for (int i = 0; i < activeBands.Count; i++)
            {
                for (int j = i + 1; j < activeBands.Count; j++)
                {
                    Rect a = Shrink(activeBands[i].Rect, 1.0f);
                    Rect b = Shrink(activeBands[j].Rect, 1.0f);
                    Assert.IsFalse(
                        a.Overlaps(b),
                        $"{ctx}: band '{activeBands[i].Name}' {activeBands[i].Rect} overlaps band '{activeBands[j].Name}' {activeBands[j].Rect}");
                }
            }
        }

        static void AssertNoDarkInkDarkOutlineSmudge(Transform root, string context)
        {
            if (root == null) return;
            Text[] texts = root.GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text txt = texts[i];
                if (txt == null || !txt.enabled || !txt.gameObject.activeInHierarchy)
                    continue;
                if (string.IsNullOrWhiteSpace(txt.text) || txt.color.a <= 0.05f)
                    continue;
                if (IsHiddenByCanvasGroup(txt.transform, root))
                    continue;

                float inkLum = Rec709Luminance(txt.color);
                if (inkLum >= 0.25f)
                    continue;

                Outline[] outlines = txt.GetComponents<Outline>();
                for (int k = 0; k < outlines.Length; k++)
                {
                    Outline ol = outlines[k];
                    if (ol == null || !ol.enabled || ol.effectColor.a <= 0.05f)
                        continue;

                    float outlineLum = Rec709Luminance(ol.effectColor);
                    Assert.GreaterOrEqual(
                        outlineLum,
                        0.35f,
                        $"{context}: dark-ink Text '{GetRelativePath(root, txt.transform)}' (\"{txt.text}\", inkLum={inkLum:F3}) has smudged dark Outline (outlineLum={outlineLum:F3} < 0.35)");
                }
            }
        }

        static void AssertTextIntegrity(Transform root, string context)
        {
            if (root == null) return;
            Text[] texts = root.GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text txt = texts[i];
                if (txt == null || !txt.enabled || !txt.gameObject.activeInHierarchy)
                    continue;
                if (string.IsNullOrWhiteSpace(txt.text) || txt.color.a <= 0.05f)
                    continue;
                if (IsHiddenByCanvasGroup(txt.transform, root))
                    continue;

                string relPath = GetRelativePath(root, txt.transform);
                Rect rect = txt.rectTransform.rect;

                // (a) Positive rect dimensions (catches negative sizeDelta / point-anchor offset collapse like _playPracticeLabel)
                Assert.GreaterOrEqual(
                    rect.width,
                    8f,
                    $"{context}: Text '{relPath}' (\"{txt.text}\") collapsed horizontally to width={rect.width:F1} (rect={rect})");
                Assert.GreaterOrEqual(
                    rect.height,
                    8f,
                    $"{context}: Text '{relPath}' (\"{txt.text}\") collapsed vertically to height={rect.height:F1} (rect={rect})");

                // (b) Zero silent mid-word / mid-sentence cutoff on VerticalWrapMode.Truncate labels
                if (txt.verticalOverflow == VerticalWrapMode.Truncate)
                {
                    string trimmed = txt.text.Replace("\r\n", "\n").Trim();
                    if (trimmed.Length == 0)
                        continue;

                    TextGenerationSettings genSettings = txt.GetGenerationSettings(rect.size);
                    txt.cachedTextGenerator.Populate(trimmed, genSettings);
                    int generatedCount = txt.cachedTextGenerator.characterCount;
                    Assert.GreaterOrEqual(
                        generatedCount,
                        trimmed.Length,
                        $"{context}: Truncate Text '{relPath}' cut off ({generatedCount}/{trimmed.Length} chars in rect={rect.size}, fontSize={txt.fontSize}, bestFit={txt.resizeTextForBestFit}[{txt.resizeTextMinSize}..{txt.resizeTextMaxSize}]): \"{trimmed}\"");
                }
            }
        }

        static bool IsAuditedInteractiveSelectable(Selectable sel, Transform stopAtRoot)
        {
            if (sel == null || !sel.enabled || !sel.gameObject.activeInHierarchy)
                return false;
            if (!(sel is Button || sel is Toggle || sel is InputField))
                return false;
            if (IsHiddenByCanvasGroup(sel.transform, stopAtRoot))
                return false;
            return true;
        }

        static bool IsHiddenByCanvasGroup(Transform leaf, Transform stopAtRoot)
        {
            Transform cur = leaf;
            while (cur != null)
            {
                CanvasGroup cg = cur.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    if (cg.alpha <= 0.01f)
                        return true;
                    if (cg.ignoreParentGroups)
                        break;
                }
                if (cur == stopAtRoot)
                    break;
                cur = cur.parent;
            }
            return false;
        }

        static bool TryGetAncestorClipRect(RectTransform leaf, Transform stopAtRoot, Camera cam, out Rect clipPx)
        {
            clipPx = default;
            bool hasClip = false;
            Transform cur = leaf != null ? leaf.parent : null;

            while (cur != null)
            {
                bool masks = false;
                RectMask2D rm2d = cur.GetComponent<RectMask2D>();
                if (rm2d != null && rm2d.enabled)
                    masks = true;
                else
                {
                    Mask mask = cur.GetComponent<Mask>();
                    if (mask != null && mask.enabled)
                        masks = true;
                }

                if (masks && cur is RectTransform maskRt)
                {
                    Rect mr = GetScreenRect(maskRt, cam);
                    if (!hasClip)
                    {
                        clipPx = mr;
                        hasClip = true;
                    }
                    else if (TryIntersectRect(clipPx, mr, out Rect inter))
                    {
                        clipPx = inter;
                    }
                    else
                    {
                        clipPx = Rect.zero;
                    }
                }

                if (cur == stopAtRoot)
                    break;
                cur = cur.parent;
            }

            return hasClip;
        }

        static bool TryIntersectRect(Rect a, Rect b, out Rect intersection)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
            {
                intersection = Rect.zero;
                return false;
            }
            intersection = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        static void ForceFullCanvasAndSafeAreaUpdate(Canvas canvas, RectTransform surfaceRoot, Action onReflow = null)
        {
            Canvas.ForceUpdateCanvases();
            SafeArea[] safeAreas = canvas.GetComponentsInChildren<SafeArea>(includeInactive: true);
            for (int i = 0; i < safeAreas.Length; i++)
                safeAreas[i].ForceApply();
            onReflow?.Invoke();
            LayoutRebuilder.ForceRebuildLayoutImmediate(surfaceRoot);
            Canvas.ForceUpdateCanvases();
        }

        static void StageAbilityArcCancelHover(MatchHud hud, SimBridge sim, Camera cam)
        {
            AbilityArcView arc = hud.AbilityArc;
            arc.CancelAll();
            hud.SafeArea.ForceApply();
            hud.RefreshLayout();
            Canvas.ForceUpdateCanvases();
            arc.Sync(sim, 0f, null, cam, null);

            Camera uiCam = (hud.Canvas != null && hud.Canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? hud.Canvas.worldCamera : null;
            RectTransform slotRect = arc.GetSuperpowerButtonRect(0);
            Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(uiCam, slotRect.TransformPoint(slotRect.rect.center));

            Ui.EnsureEventSystem();
            var ptr = new PointerEventData(EventSystem.current) { pointerId = 41, position = slotScreen };
            slotRect.GetComponent<IPointerDownHandler>().OnPointerDown(ptr);
            arc.Sync(sim, 0.25f, null, cam, null);
            Canvas.ForceUpdateCanvases();

            Vector2 cancelScreen = RectTransformUtility.WorldToScreenPoint(uiCam, arc.CancelZoneRect.TransformPoint(arc.CancelZoneRect.rect.center));
            ptr.position = cancelScreen;
            slotRect.GetComponent<IDragHandler>().OnDrag(ptr);
            hud.Refresh(sim, cam, null, null, 0f);
            Canvas.ForceUpdateCanvases();
        }

        static long FindTumbangThrowerSeed(DataSource data)
        {
            for (long seed = 1L; seed <= 30L; seed++)
            {
                var probe = new StreetBridge(data, StreetGameKind.TumbangPreso, seed, "narda");
                probe.SkipIntro();
                for (int w = 0; w < 150 && probe.IsIntro; w++)
                    probe.Step();
                if (probe.LocalKid.HasValue && probe.LocalKid.Value.Role != StreetRoleKind.Taya)
                    return seed;
            }
            return 1L;
        }

        static int ReserveEphemeralTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        static RectTransform FindActiveSiblingAfter(Transform parent, RectTransform afterChild)
        {
            if (parent == null) return null;
            bool passed = afterChild == null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (!passed)
                {
                    if (child == afterChild) passed = true;
                    continue;
                }
                if (child.gameObject.activeSelf && child is RectTransform rt)
                    return rt;
            }
            return null;
        }

        static Transform FindDescendantByName(Transform root, string exactName)
        {
            if (root == null) return null;
            if (string.Equals(root.name, exactName, StringComparison.Ordinal))
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDescendantByName(root.GetChild(i), exactName);
                if (hit != null) return hit;
            }
            return null;
        }

        static string GetRelativePath(Transform root, Transform leaf)
        {
            if (leaf == null) return "<null>";
            if (root == null || leaf == root) return leaf.name;
            var parts = new List<string>(6);
            Transform cur = leaf;
            while (cur != null && cur != root)
            {
                parts.Add(cur.name);
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static float Rec709Luminance(Color c) =>
            0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
