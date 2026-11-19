using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class InputI18nAndAccessibilityTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Texture2D> _textures = new List<Texture2D>();
        string _savedSettingsJson;

        [SetUp]
        public void SetUp()
        {
            _savedSettingsJson = SettingsStore.Current.ToJson();
            TiltInput.TestGravity = null;
            AndroidLocalLink.InterfaceProvider = null;
        }

        [TearDown]
        public void TearDown()
        {
            TiltInput.TestGravity = null;
            AndroidLocalLink.InterfaceProvider = null;
            ScreenFitMetrics.ClearSimulatedViewport();

            for (int i = _textures.Count - 1; i >= 0; i--)
            {
                if (_textures[i] != null)
                    UnityEngine.Object.DestroyImmediate(_textures[i]);
            }
            _textures.Clear();

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();

            SettingsStore.Current.ResetToDefaults();
            if (!string.IsNullOrEmpty(_savedSettingsJson))
                SettingsStore.Current.LoadJson(_savedSettingsJson);
        }

        static List<string[]> ParseCsvRows(string csvText)
        {
            var rows = new List<string[]>(400);
            if (string.IsNullOrEmpty(csvText)) return rows;

            var currentRow = new List<string>(4);
            var cell = new StringBuilder(64);
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csvText.Length && csvText[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                    }
                    else if (c == '\r')
                    {
                        continue;
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                        if (currentRow.Count > 1 || (currentRow.Count == 1 && currentRow[0].Length > 0))
                            rows.Add(currentRow.ToArray());
                        currentRow.Clear();
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
            }

            if (cell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(cell.ToString());
                if (currentRow.Count > 1 || (currentRow.Count == 1 && currentRow[0].Length > 0))
                    rows.Add(currentRow.ToArray());
            }

            return rows;
        }

        static int CountToken(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token)) return 0;
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }

        [Test]
        public void I18n_StringTableAndRuntimeTranslator_CoverWeaponsStickersPlaceholdersAndLocales()
        {
            TextAsset asset = Resources.Load<TextAsset>("Data/i18n__strings");
            string csv = asset != null ? asset.text : null;
            if (string.IsNullOrEmpty(csv))
            {
                string diskPath = Path.Combine(Application.dataPath, "Imported", "Resources", "Data", "i18n__strings.csv");
                if (File.Exists(diskPath))
                    csv = File.ReadAllText(diskPath);
            }
            Assert.IsFalse(string.IsNullOrEmpty(csv), "Data/i18n__strings.csv must be readable");
            StringAssert.StartsWith("keys,en", csv, "CSV header must begin with keys,en");

            List<string[]> rows = ParseCsvRows(csv);
            Assert.Greater(rows.Count, 200, "i18n__strings.csv must contain > 200 rows");

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                string[] cols = rows[r];
                if (cols.Length == 0 || string.IsNullOrEmpty(cols[0])) continue;
                string key = cols[0];
                keys.Add(key);

                int expD = CountToken(key, "%d");
                int expS = CountToken(key, "%s");
                for (int c = 1; c < cols.Length; c++)
                {
                    if (string.IsNullOrEmpty(cols[c])) continue;
                    Assert.AreEqual(expD, CountToken(cols[c], "%d"), $"%d placeholder mismatch in key '{key}' col {c}");
                    Assert.AreEqual(expS, CountToken(cols[c], "%s"), $"%s placeholder mismatch in key '{key}' col {c}");
                }
            }

            string[] requiredUiKeys = { "Play", "Create room", "Join room", "Quit game", "Race to %d", "Stunned" };
            for (int i = 0; i < requiredUiKeys.Length; i++)
                Assert.IsTrue(keys.Contains(requiredUiKeys[i]), $"Missing UI key '{requiredUiKeys[i]}' in i18n__strings.csv");

            for (int i = 0; i < MatchOverlaysView.StickerPhrases.Length; i++)
                Assert.IsTrue(keys.Contains(MatchOverlaysView.StickerPhrases[i]), $"Missing quick-chat phrase '{MatchOverlaysView.StickerPhrases[i]}' in i18n__strings.csv");

            DataSource data = KalyeahResources.Data();
            var sim = new SimBridge(data, new MatchSetup { Seed = 1L, TrainingMode = true });
            for (int i = 0; i < sim.AllWeapons.Count; i++)
            {
                string displayName = sim.AllWeapons[i].DisplayName;
                Assert.IsTrue(keys.Contains(displayName), $"Missing weapon display name '{displayName}' in i18n__strings.csv");
            }

            // Exercise engine-free Kalyeah.Game.I18n translator directly
            const string syntheticCsv =
                "keys,en,tl\n" +
                "Stunned,Stunned,Nakatulala\n" +
                "First to %d,First to %d,Una sa %d\n" +
                "Haha!,Haha!,Haha!\n";

            try
            {
                Assert.AreEqual(3, I18n.LoadCsv(syntheticCsv));
                Assert.AreEqual(3, I18n.KeyCount);
                Assert.IsTrue(I18n.HasKey("Stunned"));
                Assert.IsFalse(I18n.HasKey("Never translated"));
                CollectionAssert.AreEqual(new[] { "en" }, I18n.AvailableLanguages, "Fewer than 30 translated rows keeps locale list to en");

                Assert.AreEqual("Stunned", I18n.Tr("Stunned", "en"));
                Assert.AreEqual("Nakatulala", I18n.Tr("Stunned", "tl"));
                Assert.AreEqual("Nakatulala", I18n.Tr("Stunned", "fil"));
                Assert.AreEqual("Una sa %d", I18n.Tr("First to %d", "tl"));
                Assert.AreEqual("Never translated", I18n.Tr("Never translated", "tl"));
            }
            finally
            {
                I18n.LoadCsv(csv);
                Assert.Greater(I18n.KeyCount, 200);
            }
        }

        static float Rec709Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [Test]
        public void ColorblindPalettes_EnforceLuminanceContrastAndEliminateGreenBarsAcrossModes()
        {
            // Mode 0 preserves sunlit standard blue/red/green bars
            Color stdOwn = KomiksUi.TeamOwnColor(0);
            Color stdEnemy = KomiksUi.TeamEnemyColor(0);
            Color stdSelf = KomiksUi.SelfHealthColor(0);
            Assert.Greater(stdOwn.b, stdOwn.r + 0.5f);
            Assert.Greater(stdEnemy.r, stdEnemy.b + 0.5f);
            Assert.Greater(stdSelf.g, stdSelf.r + 0.4f);

            // Modes 1..3 (Protanopia, Deuteranopia, Tritanopia): zero green health bars + luminance separation
            for (int mode = 1; mode <= 3; mode++)
            {
                Color own = KomiksUi.TeamOwnColor(mode);
                Color ally = KomiksUi.AllyHealthColor(mode);
                Color enemy = KomiksUi.TeamEnemyColor(mode);
                Color self = KomiksUi.SelfHealthColor(mode);

                Color[] bars = { self, ally, enemy };
                for (int i = 0; i < bars.Length; i++)
                {
                    Color b = bars[i];
                    bool looksGreen = b.g > b.r + 0.2f && b.g > b.b + 0.2f;
                    Assert.IsFalse(looksGreen, $"Colorblind mode {mode} bar {i} ({b}) must not rely on green");
                }

                if (mode == 1 || mode == 2)
                {
                    Assert.Greater(own.b, own.r + 0.4f, $"Mode {mode} own team must read distinctly blue");
                    Assert.Greater(enemy.r, enemy.b + 0.5f, $"Mode {mode} enemy team must read distinctly orange");
                }

                float ownLuma = Rec709Luminance(own);
                float allyLuma = Rec709Luminance(ally);
                float enemyLuma = Rec709Luminance(enemy);
                float selfLuma = Rec709Luminance(self);

                Assert.Greater(
                    Mathf.Abs(ownLuma - enemyLuma),
                    0.08f,
                    $"Mode {mode} own ({ownLuma:F3}) vs enemy ({enemyLuma:F3}) must differ in luminance by > 0.08");
                Assert.Greater(
                    selfLuma - enemyLuma,
                    0.08f,
                    $"Mode {mode} local self bar ({selfLuma:F3}) must be brighter than enemy bar ({enemyLuma:F3})");
                Assert.Greater(
                    selfLuma - allyLuma,
                    0.08f,
                    $"Mode {mode} local self bar ({selfLuma:F3}) must be brighter than ally bar ({allyLuma:F3})");
            }
        }

        [Test]
        public void TiltInput_CalibratesRestingPostureEnforces8DegDeadzoneAndFlips180AcrossLandscapeSides()
        {
            var tilt = new TiltInput(enabled: true);

            // Missing or near-zero gravity safely returns Vector2.zero
            TiltInput.TestGravity = Vector3.zero;
            Assert.IsFalse(TiltInput.IsSensorAvailable);
            Assert.AreEqual(Vector2.zero, tilt.SampleDirection());
            Assert.AreEqual(Vector2.zero, tilt.Direction(new Vector3(0f, 0f, 0.1f)));

            // Arbitrary resting phone posture (35 deg pitch) calibrates Level on first sample and emits zero movement
            Vector3 resting = (Quaternion.Euler(35f, 0f, 0f) * new Vector3(0f, 0f, -1f)).normalized;
            Assert.AreEqual(Vector2.zero, tilt.Direction(resting), "Initial sample calibrates resting posture");
            Assert.AreEqual(1f, tilt.Level.magnitude, 0.001f);

            // < 8 degree tilt stays strictly inside deadzone
            Vector3 insideDeadzone = (Quaternion.Euler(0f, 5f, 0f) * resting).normalized;
            Assert.AreEqual(Vector2.zero, tilt.Direction(insideDeadzone), "5 deg tilt (< 8 deg DeadZoneDegrees) must stay zero");

            // >= 8 degree tilt emits unit run direction
            Vector3 pastDeadzone = (Quaternion.Euler(0f, 14f, 0f) * resting).normalized;
            Vector2 dir = tilt.Direction(pastDeadzone);
            Assert.AreEqual(1f, dir.magnitude, 0.001f, "14 deg tilt (>= 8 deg) must emit unit direction");

            // Sensor-to-screen mapping flips 180 deg cleanly between LandscapeLeft and LandscapeRight
            Vector3 rawSensor = new Vector3(0.35f, -0.62f, -0.70f);
            Vector3 left = TiltInput.AlignSensorToScreen(rawSensor, ScreenOrientation.LandscapeLeft);
            Vector3 right = TiltInput.AlignSensorToScreen(rawSensor, ScreenOrientation.LandscapeRight);
            Assert.AreEqual(new Vector3(0.62f, 0.35f, -0.70f), left);
            Assert.AreEqual(new Vector3(-0.62f, -0.35f, -0.70f), right);
            Assert.AreEqual(-left.x, right.x, 0.0001f);
            Assert.AreEqual(-left.y, right.y, 0.0001f);
            Assert.AreEqual(left.z, right.z, 0.0001f);
        }

        [Test]
        public void QrCodeView_AndAndroidLocalLink_RoundtripIsoQrAndClassifyJoinPayloads()
        {
            string wifiQr = AndroidLocalLink.BuildWifiQr("KalyeahHotspot", "tsinelas2026", "WPA");
            Assert.IsFalse(string.IsNullOrEmpty(wifiQr));

            string[] payloads =
            {
                "ws://192.168.43.1:8080",
                "KALYEAH:ABCD2345",
                wifiQr,
            };

            for (int i = 0; i < payloads.Length; i++)
            {
                Texture2D qrTex = QrCodeView.EncodeTexture(payloads[i]);
                Assert.IsNotNull(qrTex, $"QrCodeView.EncodeTexture returned null for '{payloads[i]}'");
                _textures.Add(qrTex);
                Assert.AreEqual(FilterMode.Point, qrTex.filterMode, "QR texture must stay crisp FilterMode.Point");

                bool ok = QrCodeView.TryDecodeTexture(qrTex, out string decoded);
                Assert.IsTrue(ok, $"QrCodeView.TryDecodeTexture failed on '{payloads[i]}'");
                Assert.AreEqual(payloads[i], decoded);
            }

            Assert.IsTrue(AndroidLocalLink.TryClassifyJoinPayload("192.168.43.1", out string wsUrl, out string roomCode, out var wifi));
            Assert.AreEqual("ws://192.168.43.1:8080", wsUrl);
            Assert.IsEmpty(roomCode);
            Assert.IsNull(wifi);

            Assert.IsTrue(AndroidLocalLink.TryClassifyJoinPayload("KALYEAH:ABCD2345", out wsUrl, out roomCode, out wifi));
            Assert.AreEqual("ABCD2345", roomCode);
            Assert.IsEmpty(wsUrl);
            Assert.IsNull(wifi);

            Assert.IsTrue(AndroidLocalLink.TryClassifyJoinPayload(wifiQr, out wsUrl, out roomCode, out wifi));
            Assert.IsNotNull(wifi);
            Assert.AreEqual("KalyeahHotspot", wifi.Ssid);
            Assert.AreEqual("tsinelas2026", wifi.Password);
        }

        [Test]
        public void AbilityArcView_TouchCancelPauseAndCancelZone_AbortHeldCastWithoutFiring()
        {
            DataSource data = KalyeahResources.Data();
            var sim = new SimBridge(data, new MatchSetup
            {
                Seed = 303L,
                LocalCharacter = "narda",
                SuperpowerLoadout = new[] { "tsinelas_light", "bola", "papel_shield" },
                CardCycleOverride = false,
                TrainingMode = true,
            });
            sim.SkipIntro();

            var hostGo = new GameObject("AbilityArcCancelHost");
            _spawned.Add(hostGo);
            Canvas canvas = Ui.MakeCanvas("ArcCanvas", hostGo.transform, null);
            AbilityArcView arc = AbilityArcView.Build(canvas.transform);
            arc.Sync(sim, 0f);

            var eventSysGo = new GameObject("TestEventSystem", typeof(EventSystem));
            _spawned.Add(eventSysGo);
            EventSystem eventSys = eventSysGo.GetComponent<EventSystem>();

            // 1. OS / UI pointer cancellation (ICancelHandler.OnCancel) drops held superpower with AimCancel and zero cooldown
            RectTransform slot0 = arc.GetSuperpowerButtonRect(0);
            var down0 = new PointerEventData(eventSys) { pointerId = 11, position = new Vector2(1080f, 120f) };
            slot0.GetComponent<IPointerDownHandler>().OnPointerDown(down0);
            Assert.IsTrue(arc.IsAiming);
            Assert.AreEqual(0, arc.HeldSlot);
            Assert.IsTrue(arc.CancelZoneRect.gameObject.activeSelf);

            AbilityInputState heldTick0 = arc.SampleInput(sim);
            sim.SetLocalInput(heldTick0.ToInputFrame(SVector2.Zero));
            sim.Step();
            Assert.AreEqual(0f, sim.LocalKid.Value.SlotCooldowns[0], 0.001f);

            slot0.GetComponent<ICancelHandler>().OnCancel(down0);
            Assert.IsFalse(arc.IsAiming);
            Assert.AreEqual(-1, arc.HeldSlot);

            AbilityInputState cancelTick0 = arc.SampleInput(sim);
            Assert.AreNotEqual(0, cancelTick0.Buttons & InputButtons.AimCancel, "Touch cancel must emit InputButtons.AimCancel");
            sim.SetLocalInput(cancelTick0.ToInputFrame(SVector2.Zero));
            sim.Step();
            Assert.AreEqual(0f, sim.LocalKid.Value.SlotCooldowns[0], 0.001f, "Cancelled superpower must never fire or spend cooldown");

            // 2. App pause / focus loss (CancelAll) aborts held cast cleanly without firing
            arc.Sync(sim, 0f);
            RectTransform slot1 = arc.GetSuperpowerButtonRect(1);
            var down1 = new PointerEventData(eventSys) { pointerId = 12, position = new Vector2(1040f, 220f) };
            slot1.GetComponent<IPointerDownHandler>().OnPointerDown(down1);
            Assert.AreEqual(1, arc.HeldSlot);

            sim.SetLocalInput(arc.SampleInput(sim).ToInputFrame(SVector2.Zero));
            sim.Step();

            arc.CancelAll();
            AbilityInputState pauseCancel = arc.SampleInput(sim);
            Assert.AreNotEqual(0, pauseCancel.Buttons & InputButtons.AimCancel);
            sim.SetLocalInput(pauseCancel.ToInputFrame(SVector2.Zero));
            sim.Step();
            Assert.AreEqual(0f, sim.LocalKid.Value.SlotCooldowns[1], 0.001f, "Pause/focus-loss CancelAll must never auto-fire");

            // 3. Releasing over the top-right red CancelZoneRect cancels cast instead of firing
            arc.Sync(sim, 0f);
            RectTransform slot2 = arc.GetSuperpowerButtonRect(2);
            var down2 = new PointerEventData(eventSys) { pointerId = 13, position = new Vector2(1180f, 260f) };
            slot2.GetComponent<IPointerDownHandler>().OnPointerDown(down2);
            Assert.AreEqual(2, arc.HeldSlot);

            sim.SetLocalInput(arc.SampleInput(sim).ToInputFrame(SVector2.Zero));
            sim.Step();

            Vector3 cancelWorldCenter = arc.CancelZoneRect.TransformPoint(arc.CancelZoneRect.rect.center);
            Vector2 cancelScreenPos = RectTransformUtility.WorldToScreenPoint(null, cancelWorldCenter);
            down2.position = cancelScreenPos;
            slot2.GetComponent<IDragHandler>().OnDrag(down2);
            Assert.IsTrue(arc.IsCancelHovered, "Dragging onto CancelAimZone highlights cancel hover");
            slot2.GetComponent<IPointerUpHandler>().OnPointerUp(down2);

            AbilityInputState zoneCancel = arc.SampleInput(sim);
            Assert.AreNotEqual(0, zoneCancel.Buttons & InputButtons.AimCancel, "Release on CancelAimZone must assert AimCancel");
            sim.SetLocalInput(zoneCancel.ToInputFrame(SVector2.Zero));
            sim.Step();
            Assert.AreEqual(0f, sim.LocalKid.Value.SlotCooldowns[2], 0.001f, "Release on CancelAimZone must put superpower back");
        }

        [Test]
        public void AimSolver_SkywardRayAndArenaPerimeter_ClampFiniteAimAheadOfKidInside84mBarangay()
        {
            MapLayoutData layout = MapLayoutData.Load(KalyeahResources.Data());
            Assert.AreEqual(84f, layout.ArenaRadius, 0.01f, "Circular barangay arena radius must be 84 m");

            SVector2 feetMid = new SVector2(0f, 20f);
            SVector2 facingEnemy = new SVector2(0f, -1f); // Sim -Z = Unity +Z
            var skywardRay = new Ray(
                new Vector3(0.68f, 1.42f, -22.55f),
                new Vector3(0f, 0.45f, 0.89f).normalized); // Pitched up into the sky (y > 0 -> never hits y=0 ground)

            AimSolution midSol = AimSolver.SolveRay(skywardRay, feetMid, facingEnemy, maxRange: 18f, layout: layout);
            Assert.IsFalse(float.IsNaN(midSol.SimAimPoint.X) || float.IsInfinity(midSol.SimAimPoint.X));
            Assert.IsFalse(float.IsNaN(midSol.SimAimPoint.Y) || float.IsInfinity(midSol.SimAimPoint.Y));
            Assert.LessOrEqual(midSol.SimAimPoint.Length(), layout.ArenaRadius + 0.01f, "Skyward aim stays inside 84 m barangay");
            Assert.LessOrEqual((midSol.SimAimPoint - feetMid).Length(), 18f + 0.01f, "Skyward aim clamps to weapon maxRange");
            Assert.Greater(
                SVector2.Dot(midSol.SimAimPoint - feetMid, facingEnemy),
                0f,
                "Skyward aim lands ahead of the kid along the camera's forward look direction");

            // Near north rim (z = -78 in sim XZ), aiming 20 m further north clamps strictly inside the 84 m arena perimeter
            SVector2 feetRim = new SVector2(0f, -78f);
            var rimSkyRay = new Ray(
                new Vector3(0.68f, 1.42f, 75.45f),
                new Vector3(0f, 0.35f, 0.93f).normalized);
            AimSolution rimSol = AimSolver.SolveRay(rimSkyRay, feetRim, facingEnemy, maxRange: 20f, layout: layout);
            Assert.LessOrEqual(rimSol.SimAimPoint.Length(), layout.ArenaRadius + 0.01f, "Arena rim clamps aim inside 84 m circle");
            Assert.Greater(SVector2.Dot(rimSol.SimAimPoint - feetRim, facingEnemy), 0f);
        }
    }
}
