using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class ScreenFitBoundsTests
    {
        const float AbilityButtonMinClearanceDp = 6f;

        readonly List<IDisposable> _disposables = new List<IDisposable>();
        readonly List<UnityObject> _cleanup = new List<UnityObject>();
        readonly List<RenderTexture> _targets = new List<RenderTexture>();
        MatchHud _hud;
        StreetHudView _streetHud;

        [SetUp]
        public void SetUp()
        {
            if (SettingsStore.Current.LeftHanded)
                SettingsStore.Current.LeftHanded = false;
            ScreenFitMetrics.ClearSimulatedViewport();
        }

        [TearDown]
        public void TearDown()
        {
            if (SettingsStore.Current.LeftHanded)
                SettingsStore.Current.LeftHanded = false;

            _hud?.Dispose();
            _hud = null;

            _streetHud?.Dispose();
            _streetHud = null;

            for (int i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i]?.Dispose();
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
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            return cam;
        }

        (MatchHud hud, SafeArea safe, Camera cam, SimBridge sim) BuildHudAtViewport(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation)
        {
            Camera cam = BuildViewportCamera("HudRoot", width, height, rawSafePx, orientation, out GameObject root);

            var sim = new SimBridge(LoadData(), 7L, "junjun");
            sim.SkipIntro();
            object simObj = sim;
            if (simObj is IDisposable dispSim)
                _disposables.Add(dispSim);

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
            Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 p2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float xMin = Mathf.Min(p0.x, p2.x);
            float xMax = Mathf.Max(p0.x, p2.x);
            float yMin = Mathf.Min(p0.y, p2.y);
            float yMax = Mathf.Max(p0.y, p2.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static Rect Shrink(Rect r, float px)
        {
            float dx = Mathf.Min(px, r.width * 0.25f);
            float dy = Mathf.Min(px, r.height * 0.25f);
            return Rect.MinMaxRect(r.xMin + dx, r.yMin + dy, r.xMax - dx, r.yMax - dy);
        }

        static void AssertContainedInSafeRect(Rect safePx, Rect childPx, string label, float tolerancePx = 1.5f)
        {
            Assert.GreaterOrEqual(childPx.xMin, safePx.xMin - tolerancePx, $"{label} left ({childPx.xMin:F1}) outside safe rect ({safePx.xMin:F1})");
            Assert.GreaterOrEqual(childPx.yMin, safePx.yMin - tolerancePx, $"{label} bottom ({childPx.yMin:F1}) outside safe rect ({safePx.yMin:F1})");
            Assert.LessOrEqual(childPx.xMax, safePx.xMax + tolerancePx, $"{label} right ({childPx.xMax:F1}) outside safe rect ({safePx.xMax:F1})");
            Assert.LessOrEqual(childPx.yMax, safePx.yMax + tolerancePx, $"{label} top ({childPx.yMax:F1}) outside safe rect ({safePx.yMax:F1})");
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

            // 3. Zero pairwise AABB overlap across ALL active HUD controls (Joystick.Base, Header, HeroVitalsChip, Pause, QuickChat, 8 AbilityArc buttons)
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
            Rect sightboxPx = new Rect(
                expectedSafePx.xMin + expectedSafePx.width * 0.275f,
                expectedSafePx.yMin + expectedSafePx.height * 0.25f,
                expectedSafePx.width * 0.45f,
                expectedSafePx.height * 0.50f);

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

        static StreetBridge CreateThrowerStreetBridge(DataSource data)
        {
            for (long seed = 1L; seed <= 32L; seed++)
            {
                var candidate = new StreetBridge(data, StreetGameKind.TumbangPreso, seed, "junjun");
                StreetKidSnapshot? me = candidate.LocalKid;
                if (me.HasValue && me.Value.Role != StreetRoleKind.Taya)
                    return candidate;
            }
            return new StreetBridge(data, StreetGameKind.TumbangPreso, 1L, "junjun");
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

            Rect sightboxPx = new Rect(
                expectedSafePx.xMin + expectedSafePx.width * 0.275f,
                expectedSafePx.yMin + expectedSafePx.height * 0.25f,
                expectedSafePx.width * 0.45f,
                expectedSafePx.height * 0.50f);

            for (int i = 0; i < controls.Count; i++)
            {
                Assert.IsFalse(
                    Shrink(controls[i], 1.0f).Overlaps(Shrink(sightboxPx, 1.0f)),
                    $"{profileDesc}: Street {names[i]} ({controls[i]}) intrudes into 45%x50% center sightbox ({sightboxPx})");
            }
        }

        [Test]
        public void StreetHudView_Across1280x720And844x390Notched_HasSafeContainmentZeroOverlapAnd48Dp()
        {
            DataSource data = LoadData();
            StreetBridge bridge = CreateThrowerStreetBridge(data);

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

        [Test]
        public void ScreenFitMetrics_ClampsBogusZeroAndExtremeDpiReports()
        {
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(0f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(-99f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.FallbackDpi, ScreenFitMetrics.ClampDpi(float.NaN), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.MinDpi, ScreenFitMetrics.ClampDpi(72f), 0.001f);
            Assert.AreEqual(ScreenFitMetrics.MaxDpi, ScreenFitMetrics.ClampDpi(1200f), 0.001f);
        }
    }
}
