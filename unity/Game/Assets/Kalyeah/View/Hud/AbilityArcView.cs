using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Kalyeah.Game
{
    /// <summary>
    /// Shared per-frame / per-tick combat HUD output consumed by MatchHud / MatchBoot (for SimBridge.SetLocalInput)
    /// and AimIndicatorView / CrosshairView (for world aim telegraphs and reticle lock-on).
    /// </summary>
    public struct AbilityInputState
    {
        public int Buttons;
        public SVector2 Aim;
        public SVector2 AimPoint;
        public bool HasAimPoint;
        public string PreviewWeaponId;
        public bool IsDraggingAim;
        public bool IsCancelHovered;

        public int HeldSlot;
        public Vector2 ScreenDragWay;
        public Vector2 AimScreenPosition;

        public LocalInputFrame ToInputFrame(SVector2 move, SVector2 moveTarget = default, bool hasMoveTarget = false)
        {
            int bits = Buttons;
            if (hasMoveTarget) bits |= InputButtons.MoveTarget;
            if (HasAimPoint) bits |= InputButtons.AimPoint;
            return new LocalInputFrame
            {
                Move = move,
                Aim = Aim,
                Buttons = bits,
                MoveTarget = moveTarget,
                AimPoint = AimPoint,
                HasMoveTarget = hasMoveTarget,
                HasAimPoint = HasAimPoint,
            };
        }
    }

    internal static class HudIconCache
    {
        sealed class ReferenceTextureComparer : IEqualityComparer<Texture2D>
        {
            public static readonly ReferenceTextureComparer Instance = new ReferenceTextureComparer();
            public bool Equals(Texture2D x, Texture2D y) => ReferenceEquals(x, y);
            public int GetHashCode(Texture2D obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        static readonly Dictionary<string, Sprite> HudIcons = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);
        static readonly Dictionary<string, Sprite> CardIcons = new Dictionary<string, Sprite>(32, StringComparer.Ordinal);
        static readonly Dictionary<string, Sprite> DirectSprites = new Dictionary<string, Sprite>(16, StringComparer.Ordinal);
        static readonly Dictionary<Texture2D, Sprite> SpritesByTexture = new Dictionary<Texture2D, Sprite>(64, ReferenceTextureComparer.Instance);
        static readonly Dictionary<string, string> ShortLabels = new Dictionary<string, string>(32, StringComparer.Ordinal);

        static readonly string[] TenthsTable = BuildTenthsTable();
        static readonly string[] WholeTable = BuildWholeTable();
        static readonly string[] PercentTable = BuildPercentTable();
        static readonly string[] AmmoSlashTable = BuildAmmoSlashTable();

        static Sprite _dropSprite;
        static Sprite _stonePipSprite;

        public static string Tenths(float seconds)
        {
            if (seconds <= 0.04f) return "";
            if (seconds >= 20f)
            {
                int whole = Mathf.Clamp(Mathf.CeilToInt(seconds), 0, WholeTable.Length - 1);
                return WholeTable[whole];
            }
            int idx = Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 1, TenthsTable.Length - 1);
            return TenthsTable[idx];
        }

        public static string Whole(int value)
        {
            int clamped = Mathf.Clamp(value, 0, WholeTable.Length - 1);
            return WholeTable[clamped];
        }

        public static string Percent(float value0To100)
        {
            int pct = Mathf.Clamp(Mathf.FloorToInt(value0To100 + 0.01f), 0, PercentTable.Length - 1);
            return PercentTable[pct];
        }

        public static string AmmoFraction(int ammo, int maxAmmo)
        {
            int m = Mathf.Clamp(maxAmmo, 1, 9);
            int a = Mathf.Clamp(ammo, 0, m);
            if (m == 7 && a < AmmoSlashTable.Length)
                return AmmoSlashTable[a];
            return Whole(a);
        }

        public static void SetTextIfChanged(Text label, string value)
        {
            if (label == null) return;
            string safe = value ?? "";
            if (!string.Equals(label.text, safe, StringComparison.Ordinal))
                label.text = safe;
        }

        public static string ShortLabel(string weaponId, string shortName, string displayName)
        {
            if (string.IsNullOrEmpty(weaponId)) return "";
            if (ShortLabels.TryGetValue(weaponId, out string cached)) return cached;

            string raw = !string.IsNullOrWhiteSpace(shortName)
                ? shortName
                : (!string.IsNullOrWhiteSpace(displayName) ? displayName : weaponId);
            raw = raw.Trim();
            if (raw.Length > 7) raw = raw.Substring(0, 7);
            string upper = raw.ToUpperInvariant();
            ShortLabels[weaponId] = upper;
            return upper;
        }

        public static Sprite LoadHudSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (DirectSprites.TryGetValue(resourcePath, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = KalyeahResources.Texture(resourcePath);
            Sprite sp = MakeSprite(tex);
            if (sp != null)
                DirectSprites[resourcePath] = sp;
            return sp;
        }

        public static Sprite LoadWeaponSprite(string weaponId, string iconPath = null, bool preferCardArt = false)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            Dictionary<string, Sprite> map = preferCardArt ? CardIcons : HudIcons;
            if (map.TryGetValue(weaponId, out Sprite cached) && cached != null)
                return cached;

            Texture2D tex = null;
            if (preferCardArt)
            {
                tex = KalyeahResources.Texture("ui/skill_cards/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("icons/art/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("icons/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("ui/hud_icons/" + weaponId);
            }
            else
            {
                tex = KalyeahResources.Texture("ui/hud_icons/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("icons/art/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("icons/" + weaponId);
                if (tex == null) tex = KalyeahResources.Texture("ui/skill_cards/" + weaponId);
            }

            if (tex == null && !string.IsNullOrEmpty(iconPath))
                tex = KalyeahResources.Texture(iconPath);

            Sprite sp = MakeSprite(tex);
            if (sp == null)
                sp = MenuArt.SkillEmblem(weaponId);
            if (sp != null)
                map[weaponId] = sp;
            return sp;
        }

        public static Sprite StonePipSprite()
        {
            if (_stonePipSprite != null) return _stonePipSprite;
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahStonePip",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            float outerR = size * 0.44f;
            float coreR = size * 0.31f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float outerA = Mathf.Clamp01(outerR + 0.7f - dist);
                    if (outerA <= 0.001f)
                    {
                        px[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    float coreT = Mathf.Clamp01((coreR + 0.8f - dist) * 0.9f);
                    // Subtle obsidian contrast ring around luminous pure-white core so stone dots stay crisp on bright asphalt
                    Color c = Color.Lerp(new Color(0.18f, 0.22f, 0.28f, 0.92f), Color.white, coreT);
                    c.a *= outerA;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _stonePipSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _stonePipSprite.name = "KalyeahStonePip";
            return _stonePipSprite;
        }

        public static Sprite RingSprite() => KomiksUi.RingSprite(128, 3);

        public static Sprite RoundedPlateSprite() => KomiksUi.RoundedSprite(14, 64);

        public static Sprite DropSprite()
        {
            Sprite imported = LoadHudSprite("ui/hand/ice_water");
            if (imported != null) return imported;
            if (_dropSprite != null) return _dropSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahIceDrop",
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size; // 0 bottom .. 1 top
                float halfW = ny < 0.45f
                    ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((ny - 0.42f) / 0.38f, 2f))) * 0.36f
                    : Mathf.Max(0f, (0.94f - ny) / 0.49f) * 0.36f;
                for (int x = 0; x < size; x++)
                {
                    float nx = Mathf.Abs((x + 0.5f) / size - 0.5f);
                    float a = ny >= 0.06f && ny <= 0.94f ? Mathf.Clamp01((halfW - nx) * size * 0.8f) : 0f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _dropSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _dropSprite.name = "KalyeahIceDrop";
            return _dropSprite;
        }

        public static Sprite ReloadSprite() => MenuArt.SkillEmblem("reload");

        public static Sprite MakeSprite(Texture2D tex)
        {
            if (tex == null) return null;
            if (SpritesByTexture.TryGetValue(tex, out Sprite existing) && existing != null)
                return existing;
            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = tex.name;
            SpritesByTexture[tex] = sp;
            return sp;
        }

        static string[] BuildTenthsTable()
        {
            var arr = new string[200];
            arr[0] = "";
            for (int i = 1; i < arr.Length; i++)
                arr[i] = (i * 0.1f).ToString("0.0", CultureInfo.InvariantCulture);
            return arr;
        }

        static string[] BuildWholeTable()
        {
            var arr = new string[101];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = i.ToString(CultureInfo.InvariantCulture);
            return arr;
        }

        static string[] BuildPercentTable()
        {
            var arr = new string[101];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = i.ToString(CultureInfo.InvariantCulture) + "%";
            return arr;
        }

        static string[] BuildAmmoSlashTable()
        {
            return new[] { "0/7", "1/7", "2/7", "3/7", "4/7", "5/7", "6/7", "7/7" };
        }
    }

    /// <summary>
    /// Wild Rift × Call of Duty: Mobile right-thumb radial combat arc (.claude/skills/apex-mobile-hud/SKILL.md):
    /// - >= 72 dp Tirador primary anchor at resting right-thumb corner
    /// - 3 Superpowers on a 115–138 dp ergonomic thumb-pivot arc
    /// - Luksong-Tinik dash/cleanse, Bagsak ultimate charge, Jump, and smart Crouch/Slide satellites
    /// - Persistent pointerId per button, quick-tap (<= 0.30 s) press-locked auto-aim vs hold/drag ground aim
    /// - Unmistakable top-right red "Release here to cancel" zone (InputButtons.AimCancel + Haptics.CancelTick())
    /// - Safe cancel on OnApplicationPause / OnApplicationFocus / OnDisable / Tagged (never auto-fires!)
    /// </summary>
    [ExecuteAlways]
    public sealed class AbilityArcView : MonoBehaviour
    {
        public const float QuickTapMaxSeconds = 0.30f;
        public const float SuperpowerArcDp = 124f;
        public const float AimDragRadiusDp = 68f;
        public const float AimDeadZoneDp = 12f;
        public const float MinButtonGutterDp = 8.8f;

        const int SuperpowerCount = 3;
        const int TotalButtonCount = 8;
        const int NoPointer = int.MinValue;
        const float DenyFlashDuration = 0.28f;
        const float PressFlashDuration = 0.18f;

        // Ergonomic offsets in dp inward from the active bottom corner (right corner by default; left corner when LeftHanded).
        // Superpower slots sit at 134.0 dp, 117.6 dp, and 116.1 dp (all strictly inside 115–138 dp) with >= 8.8 dp edge-to-edge
        // clearance after ClampTouchSize, 0 px intrusion into the sacred 45%x50% center sightbox across 21:9, 19.5:9, 16:9, and
        // 4:3 foldables, and full clearance below HeroVitalsChip / QuickChatButton in both right- and left-handed layouts.
        static readonly Vector2 PrimaryDp = new Vector2(44f, 44f);
        static readonly Vector2[] SuperpowerDp =
        {
            new Vector2(178f, 44f),
            new Vector2(112f, 140f),
            new Vector2(40f, 160f),
        };
        static readonly Vector2 DashDp = new Vector2(116f, 34f);
        static readonly Vector2 UltDp = new Vector2(112f, 204f);
        static readonly Vector2 JumpDp = new Vector2(38f, 218f);
        static readonly Vector2 CrouchDp = new Vector2(240f, 34f);

        // Locked G7 A2 Frosted Smoked-Glass + Brushed-Titanium Combat Cluster palette
        static readonly Color DiscBgReady = new Color(0.068f, 0.098f, 0.142f, 0.76f);
        static readonly Color DiscBgPrimary = new Color(0.086f, 0.128f, 0.190f, 0.84f);
        static readonly Color DiscBgLocked = new Color(0.048f, 0.062f, 0.088f, 0.62f);
        static readonly Color DiscBgUltReady = new Color(0.075f, 0.145f, 0.205f, 0.86f);
        static readonly Color DiscBgRecast = new Color(0.200f, 0.145f, 0.055f, 0.86f);
        static readonly Color RimReady = new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.90f);
        static readonly Color RimPrimaryReady = new Color(MenuArt.PaperWhite.r, MenuArt.PaperWhite.g, MenuArt.PaperWhite.b, 0.96f);
        static readonly Color RimHeld = MenuArt.CoralGold;
        static readonly Color RimUltReady = MenuArt.WaterCyan;
        static readonly Color RimLocked = new Color(0.46f, 0.52f, 0.60f, 0.42f);
        static readonly Color SweepShade = new Color(0.03f, 0.05f, 0.09f, 0.78f);
        static readonly Color PrimaryCadenceShade = new Color(MenuArt.CoralGold.r, MenuArt.CoralGold.g, MenuArt.CoralGold.b, 0.22f);
        static readonly Color DenyTint = new Color(MenuArt.SignalVermilionDeep.r, MenuArt.SignalVermilionDeep.g, MenuArt.SignalVermilionDeep.b, 0.86f);
        static readonly Color WaterBadgeReady = MenuArt.WaterCyan;
        static readonly Color WaterBadgeLow = MenuArt.SignalVermilion;
        static readonly Color WaterLowRimA = new Color(0.24f, 0.62f, 0.90f, 0.56f);
        static readonly Color WaterLowRimB = MenuArt.SignalVermilion;
        static readonly Color ChargePipLit = MenuArt.GoldLight;
        static readonly Color ChargePipSpent = new Color(0.14f, 0.18f, 0.26f, 0.72f);
        static readonly Color AmmoPipLit = new Color(1.00f, 0.99f, 0.96f, 1.00f);
        static readonly Color AmmoPipEmpowered = MenuArt.WaterCyan;
        static readonly Color AmmoPipSpent = new Color(0.14f, 0.18f, 0.24f, 0.44f);
        static readonly Color RecastRimGold = MenuArt.Gold;
        static readonly Color CancelIdlePlate = new Color(MenuArt.SignalVermilionDeep.r, MenuArt.SignalVermilionDeep.g, MenuArt.SignalVermilionDeep.b, 0.92f);
        static readonly Color CancelHotPlate = new Color(MenuArt.SignalVermilion.r, MenuArt.SignalVermilion.g, MenuArt.SignalVermilion.b, 0.98f);

        const int MaxAmmoPips = 7;
        const int MaxChargePips = 3;

        enum ButtonRole
        {
            Primary = 0,
            Slot0 = 1,
            Slot1 = 2,
            Slot2 = 3,
            Dash = 4,
            Ultimate = 5,
            Jump = 6,
            CrouchSlide = 7,
        }

        sealed class ArcButtonHit : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICancelHandler
        {
            public AbilityArcView Owner;
            public ButtonRole Role;

            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointerDown(Role, eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandleDrag(Role, eventData);
            public void OnPointerUp(PointerEventData eventData) => Owner?.HandlePointerUp(Role, eventData);
            public void OnCancel(BaseEventData eventData) => Owner?.HandlePointerCancel(Role, eventData as PointerEventData);
        }

        sealed class ButtonWidget
        {
            public ButtonRole Role;
            public RectTransform Rect;
            public Image Bg;
            public Material DiscMat;
            public bool HasDiscShader;
            public UiPressSpring PressSpring;
            public Image Rim;
            public Image Halo;
            public bool HasSoftGlowHalo;
            public Image Icon;
            public Image CooldownSweep;
            public Text CenterText;
            public Text CooldownText;
            public Text BadgeText;
            public Image[] ChargePips;
            public string LastWeaponId = "";
            public bool Ready;
            public bool InsufficientWater;
            public float DenyTimer;
            public float FlashTimer;
            public Vector2 RestPos;
        }

        readonly ButtonWidget[] _buttons = new ButtonWidget[TotalButtonCount];
        readonly RectTransform[] _buttonRects = new RectTransform[TotalButtonCount];
        readonly List<RectTransform> _activeControlRects = new List<RectTransform>(TotalButtonCount);
        readonly Vector2[] _layoutCenters = new Vector2[TotalButtonCount];
        readonly float[] _layoutRadii = new float[TotalButtonCount];
        readonly string[] _slotWeaponIds = { "", "", "" };
        readonly float[] _slotMaxRanges = { 16f, 16f, 16f };
        readonly AimCameraKind[] _slotAimCameras = { AimCameraKind.Normal, AimCameraKind.Normal, AimCameraKind.Normal };
        readonly Image[] _ammoPips = new Image[MaxAmmoPips];
        RectTransform _ammoPipGroup;

        RectTransform _root;
        RectTransform _clusterRoot;
        RectTransform _cancelZone;
        Image _cancelPlate;
        Image _cancelRim;
        Text _cancelLabel;
        RectTransform _statusBanner;
        Text _statusBannerText;

        bool _leftHanded;
        float _controlScale = SettingsStore.ScaleNormal;
        bool _reduceMotion;
        bool _updatingLayout;
        bool _subscribed;

        // Multi-touch pointerId locks (strictly one pointer per role; never steals active drags)
        int _primaryPointer = NoPointer;
        int _crouchPointer = NoPointer;
        int _slotPointer = NoPointer;
        int _heldSlot = -1;
        float _holdElapsed;
        Vector2 _pressScreenPos;
        Vector2 _currentScreenPos;
        Vector2 _screenDragWay;
        bool _hasDraggedPastDeadZone;
        bool _isCancelHovered;

        // Tick-safe pulse & release latches so <33ms quick-taps and mid-tick cancels never drop or misfire
        bool _primaryTapPending;
        int _primaryTapTick = int.MinValue;
        bool _slotPressObservedBySim;
        int _pendingReleaseSlot = -1;
        bool _pendingReleaseCancel;
        bool _pendingReleaseUseDragAim;
        bool _pendingReleaseUseCrosshairAim;
        Vector2 _pendingReleaseDragWay;
        float _pendingReleaseMaxRange;
        int _latchedPulseButtons;
        bool _latchedAimCancel;
        int _lastSampledSimTick = int.MinValue;

        // Cached readiness flags updated from SimBridge snapshot
        bool _matchInteractive = true;
        bool _kidTagged;
        bool _castingBlocked;
        bool _canMove = true;
        bool _canCast = true;
        bool _canSlideNow;
        bool _usingSpecKit;
        bool _canManualReload;
        float _ultPulsePhase;
        float _waterPulsePhase;

        public AudioHub Audio { get; set; }
        public AimCameraKind ActiveAimCamera { get; private set; } = AimCameraKind.Normal;
        public RectTransform Root => _root;
        public RectTransform PrimaryButtonRect => _buttons[(int)ButtonRole.Primary]?.Rect;
        public RectTransform DashButtonRect => _buttons[(int)ButtonRole.Dash]?.Rect;
        public RectTransform UltimateButtonRect => _buttons[(int)ButtonRole.Ultimate]?.Rect;
        public RectTransform JumpButtonRect => _buttons[(int)ButtonRole.Jump]?.Rect;
        public RectTransform CrouchSlideButtonRect => _buttons[(int)ButtonRole.CrouchSlide]?.Rect;
        public RectTransform CancelZoneRect => _cancelZone;
        public IReadOnlyList<RectTransform> InteractiveButtonRects => _buttonRects;

        /// <summary>Returns all currently active interactive button RectTransforms (Primary, Superpower 0..2, Dash, Ultimate, Jump, Crouch/Slide).</summary>
        public IReadOnlyList<RectTransform> AllActiveControlRects
        {
            get
            {
                _activeControlRects.Clear();
                if (_root != null && _root.gameObject.activeInHierarchy && (_clusterRoot == null || _clusterRoot.gameObject.activeSelf))
                {
                    for (int i = 0; i < _buttonRects.Length; i++)
                    {
                        RectTransform rt = _buttonRects[i];
                        if (rt != null && rt.gameObject.activeSelf)
                            _activeControlRects.Add(rt);
                    }
                }
                return _activeControlRects;
            }
        }

        public RectTransform GetSuperpowerButtonRect(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SuperpowerCount) return null;
            return _buttons[(int)ButtonRole.Slot0 + slotIndex]?.Rect;
        }

        public bool LeftHanded => _leftHanded;
        public float ControlScale => _controlScale;
        public int HeldSlot => _heldSlot;
        public bool IsAiming => _heldSlot >= 0;
        public bool IsDraggingAim => _heldSlot >= 0;
        public bool IsPrimaryHeld => _primaryPointer != NoPointer || _primaryTapPending;
        public bool IsCrouchHeld => _crouchPointer != NoPointer;
        public bool IsCancelHovered => _isCancelHovered;
        public AbilityInputState CurrentInput { get; private set; }

        public static AbilityArcView Build(Transform parent, bool leftHanded = false)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();

            RectTransform root = Ui.Fill("AbilityArc", parent);
            var view = root.gameObject.AddComponent<AbilityArcView>();
            view._root = root;
            view._leftHanded = leftHanded;
            view._controlScale = SettingsStore.Current.ControlScale;
            view._reduceMotion = SettingsStore.Current.ReduceMotion;
            view.BuildHierarchy();
            view.RefreshLayout();
            return view;
        }

        void BuildHierarchy()
        {
            _clusterRoot = Ui.Fill("Cluster", _root);

            CreateButton(ButtonRole.Primary, "PrimaryTirador", DiscBgPrimary, includeHalo: false);
            CreateButton(ButtonRole.Slot0, "Superpower1", DiscBgReady, includeHalo: false);
            CreateButton(ButtonRole.Slot1, "Superpower2", DiscBgReady, includeHalo: false);
            CreateButton(ButtonRole.Slot2, "Superpower3", DiscBgReady, includeHalo: false);
            CreateButton(ButtonRole.Dash, "VaultDash", DiscBgReady, includeHalo: false);
            CreateButton(ButtonRole.Ultimate, "UltimateBagsak", DiscBgReady, includeHalo: true);
            CreateButton(ButtonRole.Jump, "JumpButton", DiscBgReady, includeHalo: false);
            CreateButton(ButtonRole.CrouchSlide, "CrouchSlideButton", DiscBgReady, includeHalo: false);

            AssignStaticIcon(ButtonRole.Primary, "tirador");
            AssignStaticIcon(ButtonRole.Slot0, "luksong_tinik");
            AssignStaticIcon(ButtonRole.Slot1, "tira_holens");
            AssignStaticIcon(ButtonRole.Slot2, "reload");
            AssignStaticIcon(ButtonRole.Dash, "vault");
            AssignStaticIcon(ButtonRole.Ultimate, "ult_bagsak");
            AssignStaticIcon(ButtonRole.Jump, "jump");
            AssignStaticIcon(ButtonRole.CrouchSlide, "slide");

            // Unmistakable top-band red cancel target (shown only while holding/dragging a superpower)
            _cancelZone = Ui.Panel("CancelAimZone", _root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-132f, -12f), new Vector2(224f, 96f));
            _cancelPlate = Ui.MakeImage("CancelPlate", _cancelZone, CancelIdlePlate, round: false, raycast: false);
            _cancelPlate.sprite = HudIconCache.RoundedPlateSprite();
            _cancelPlate.type = Image.Type.Sliced;
            FillRect(_cancelPlate.rectTransform, Vector2.zero);

            Image cancelBorder = Ui.MakeImage("CancelBorder", _cancelZone, new Color(MenuArt.PaperWhite.r, MenuArt.PaperWhite.g, MenuArt.PaperWhite.b, 0.72f), round: false, raycast: false);
            cancelBorder.sprite = HudIconCache.RoundedPlateSprite();
            cancelBorder.type = Image.Type.Sliced;
            cancelBorder.fillCenter = false;
            FillRect(cancelBorder.rectTransform, Vector2.zero);

            _cancelRim = Ui.MakeImage("CancelIconDisc", _cancelZone, new Color(1f, 1f, 1f, 0.96f), round: false, raycast: false);
            _cancelRim.sprite = HudIconCache.RingSprite();
            _cancelRim.rectTransform.anchorMin = _cancelRim.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _cancelRim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _cancelRim.rectTransform.anchoredPosition = new Vector2(28f, 0f);
            _cancelRim.rectTransform.sizeDelta = new Vector2(36f, 36f);

            Text xGlyph = Ui.MakeText("CancelX", _cancelRim.transform, "X", 20, MenuArt.PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(xGlyph.rectTransform, Vector2.zero);

            _cancelLabel = Ui.MakeText("CancelLabel", _cancelZone, "Release to\ncancel", 18, MenuArt.PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _cancelLabel.lineSpacing = 0.95f;
            _cancelLabel.resizeTextForBestFit = true;
            _cancelLabel.resizeTextMinSize = 14;
            _cancelLabel.resizeTextMaxSize = 18;
            _cancelLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _cancelLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _cancelLabel.rectTransform.offsetMin = new Vector2(54f, 8f);
            _cancelLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
            _cancelZone.gameObject.SetActive(false);

            // Compact lockout pill right above primary cluster when in rescue zone / enemy base
            _statusBanner = Ui.Panel("CastLockoutPill", _clusterRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 10f), new Vector2(168f, 28f));
            Image lockBg = Ui.MakeImage("Bg", _statusBanner, Color.white, round: false, raycast: false);
            Material lockMat = UiShaders.CreateTacticalGlass(
                MenuArt.ObsidianSolid,
                new Color(MenuArt.TitaniumRim.r, MenuArt.TitaniumRim.g, MenuArt.TitaniumRim.b, 0.42f),
                MenuArt.CoralGold,
                cornerCut: 4f,
                accentW: 3f,
                topHighlight: 0.14f);
            if (IsCustomUiMaterial(lockMat))
            {
                lockBg.material = lockMat;
                lockBg.sprite = null;
                lockBg.type = Image.Type.Simple;
                lockBg.color = Color.white;
            }
            else
            {
                lockBg.sprite = HudIconCache.RoundedPlateSprite();
                lockBg.type = Image.Type.Sliced;
                lockBg.color = MenuArt.ObsidianSolid;
            }
            FillRect(lockBg.rectTransform, Vector2.zero);
            _statusBannerText = Ui.MakeText("Label", _statusBanner, "NO CAST ZONE", 14, MenuArt.CoralGold, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            FillRect(_statusBannerText.rectTransform, Vector2.zero);
            _statusBanner.gameObject.SetActive(false);
        }

        static bool IsCustomUiMaterial(Material mat)
        {
            return mat != null
                && mat != Graphic.defaultGraphicMaterial
                && mat != Canvas.GetDefaultCanvasMaterial();
        }

        void CreateButton(ButtonRole role, string name, Color bgColor, bool includeHalo)
        {
            int idx = (int)role;
            RectTransform rect = Ui.Panel(name, _clusterRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));
            Image bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = Ui.Circle();
            bg.type = Image.Type.Simple;
            bg.raycastTarget = true;

            float innerRing = (role == ButtonRole.Primary || role == ButtonRole.Ultimate) ? 0.79f : 0.74f;
            Color initRim = role == ButtonRole.Primary ? RimPrimaryReady : RimReady;
            Material discMat = UiShaders.CreateFrostedTitaniumDisc(bgColor, initRim, innerRing);
            bool hasDiscShader = IsCustomUiMaterial(discMat) && discMat.HasProperty(UiShaders.PropCooldown01);
            if (hasDiscShader)
            {
                bg.material = discMat;
                bg.color = Color.white;
            }
            else
            {
                discMat = null;
                bg.color = bgColor;
            }

            // Base Selectable (not Button/Toggle/InputField) pairs with UiPressSpring for PlayMode press squash + lip dip
            Selectable springSelectable = rect.gameObject.AddComponent<Selectable>();
            springSelectable.transition = Selectable.Transition.None;
            springSelectable.navigation = new Navigation { mode = Navigation.Mode.None };
            springSelectable.targetGraphic = bg;
            UiPressSpring pressSpring = UiShaders.AttachPressSpring(springSelectable, 0.94f, 2.5f);

            var hit = rect.gameObject.AddComponent<ArcButtonHit>();
            hit.Owner = this;
            hit.Role = role;

            Image halo = null;
            bool hasSoftGlowHalo = false;
            if (includeHalo)
            {
                halo = Ui.MakeImage("ReadyHalo", rect, new Color(MenuArt.WaterCyan.r, MenuArt.WaterCyan.g, MenuArt.WaterCyan.b, 0f), round: true, raycast: false);
                Material glowMat = UiShaders.CreateSoftGlow(MenuArt.WaterCyan);
                if (IsCustomUiMaterial(glowMat))
                {
                    halo.material = glowMat;
                    halo.sprite = null;
                    halo.type = Image.Type.Simple;
                    halo.color = new Color(1f, 1f, 1f, 0f);
                    hasSoftGlowHalo = true;
                }
                FillRect(halo.rectTransform, new Vector2(-14f, -14f));
            }

            Image icon = Ui.MakeImage("Icon", rect, MenuArt.PaperWhite, round: false, raycast: false);
            icon.preserveAspect = true;
            bool hasBottomBadge = role == ButtonRole.Slot0 || role == ButtonRole.Slot1 || role == ButtonRole.Slot2 || role == ButtonRole.Ultimate;
            icon.rectTransform.anchorMin = hasBottomBadge ? new Vector2(0.20f, 0.27f) : new Vector2(0.18f, 0.18f);
            icon.rectTransform.anchorMax = hasBottomBadge ? new Vector2(0.80f, 0.85f) : new Vector2(0.82f, 0.82f);
            icon.rectTransform.offsetMin = Vector2.zero;
            icon.rectTransform.offsetMax = Vector2.zero;

            Text centerText = Ui.MakeText("FallbackLabel", rect, "", 16, MenuArt.PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            centerText.resizeTextForBestFit = true;
            centerText.resizeTextMinSize = 10;
            centerText.resizeTextMaxSize = 16;
            FillRect(centerText.rectTransform, new Vector2(6f, 6f));

            Image sweep = Ui.MakeImage("CooldownSweep", rect, SweepShade, round: true, raycast: false);
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;
            sweep.fillClockwise = true;
            sweep.fillAmount = 0f;
            sweep.enabled = !hasDiscShader;
            FillRect(sweep.rectTransform, Vector2.zero);

            Image rim = Ui.MakeImage("Rim", rect, initRim, round: false, raycast: false);
            rim.sprite = HudIconCache.RingSprite();
            rim.enabled = !hasDiscShader;
            FillRect(rim.rectTransform, new Vector2(-2f, -2f));

            Text cdText = Ui.MakeText("CooldownText", rect, "", 20, MenuArt.PaperWhite, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            cdText.resizeTextForBestFit = true;
            cdText.resizeTextMinSize = 11;
            cdText.resizeTextMaxSize = 20;
            FillRect(cdText.rectTransform, Vector2.zero);

            Text badgeText = Ui.MakeText("BadgeText", rect, "", 13, WaterBadgeReady, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            badgeText.resizeTextForBestFit = true;
            badgeText.resizeTextMinSize = 10;
            badgeText.resizeTextMaxSize = 13;
            badgeText.rectTransform.anchorMin = new Vector2(0f, 0f);
            badgeText.rectTransform.anchorMax = new Vector2(1f, 0.34f);
            badgeText.rectTransform.offsetMin = new Vector2(4f, 4f);
            badgeText.rectTransform.offsetMax = new Vector2(-4f, 0f);

            Image[] chargePips = null;
            if (role == ButtonRole.Slot0 || role == ButtonRole.Slot1)
            {
                chargePips = new Image[MaxChargePips];
                for (int p = 0; p < MaxChargePips; p++)
                {
                    Image pip = Ui.MakeImage("ChargePip_" + p, rect, ChargePipLit, round: true, raycast: false);
                    pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    pip.rectTransform.sizeDelta = new Vector2(8f, 8f);
                    pip.rectTransform.anchoredPosition = new Vector2((p - 1) * 11f, -10f);
                    pip.gameObject.SetActive(false);
                    chargePips[p] = pip;
                }
            }

            if (role == ButtonRole.Primary)
            {
                // Exact 7 glowing white stone dots curving cleanly along Primary's outer upper-left arc (repositioned in CommitButtonPlacements)
                _ammoPipGroup = Ui.Panel("AmmoPips", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Sprite pipSprite = HudIconCache.StonePipSprite();
                for (int p = 0; p < MaxAmmoPips; p++)
                {
                    Image pip = Ui.MakeImage("StonePip_" + p, _ammoPipGroup, AmmoPipLit, round: false, raycast: false);
                    pip.sprite = pipSprite;
                    pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    pip.rectTransform.sizeDelta = new Vector2(7f, 7f);
                    _ammoPips[p] = pip;
                }
                _ammoPipGroup.gameObject.SetActive(true);
            }

            _buttons[idx] = new ButtonWidget
            {
                Role = role,
                Rect = rect,
                Bg = bg,
                DiscMat = discMat,
                HasDiscShader = hasDiscShader,
                PressSpring = pressSpring,
                Rim = rim,
                Halo = halo,
                HasSoftGlowHalo = hasSoftGlowHalo,
                Icon = icon,
                CooldownSweep = sweep,
                CenterText = centerText,
                CooldownText = cdText,
                BadgeText = badgeText,
                ChargePips = chargePips,
                Ready = true,
            };
            _buttonRects[idx] = rect;
        }

        void AssignStaticIcon(ButtonRole role, string skillEmblemId)
        {
            ButtonWidget w = _buttons[(int)role];
            if (w == null || w.Icon == null) return;
            if (string.Equals(w.LastWeaponId, skillEmblemId, StringComparison.Ordinal)) return;
            w.LastWeaponId = skillEmblemId;

            w.Icon.sprite = MenuArt.SkillEmblem(skillEmblemId);
            w.Icon.enabled = true;
            HudIconCache.SetTextIfChanged(w.CenterText, "");
        }

        static void FillRect(RectTransform rt, Vector2 inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = inset;
            rt.offsetMax = -inset;
        }

        void OnEnable()
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            EnsureSubscribed();
            _reduceMotion = SettingsStore.Current.ReduceMotion;
            Haptics.Enabled = SettingsStore.Current.Haptics;
            RefreshLayout();
        }

        void OnDisable()
        {
            Unsubscribe();
            CancelAll();
        }

        void OnDestroy()
        {
            Unsubscribe();
            for (int i = 0; i < _buttons.Length; i++)
            {
                ButtonWidget w = _buttons[i];
                if (w == null) continue;
                if (w.Bg != null && w.DiscMat != null && ReferenceEquals(w.Bg.material, w.DiscMat))
                    w.Bg.material = null;
                if (w.DiscMat != null && IsCustomUiMaterial(w.DiscMat))
                {
                    KalyeahResources.Kill(w.DiscMat);
                    w.DiscMat = null;
                }
            }
        }

        void EnsureSubscribed()
        {
            if (_subscribed) return;
            ScreenFitMetrics.SimulatedViewportChanged += HandleSimulatedViewportChanged;
            SettingsStore.Current.Changed += OnSettingsChanged;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            ScreenFitMetrics.SimulatedViewportChanged -= HandleSimulatedViewportChanged;
            SettingsStore.Current.Changed -= OnSettingsChanged;
            _subscribed = false;
        }

        void HandleSimulatedViewportChanged()
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }
            if (!isActiveAndEnabled) return;
            RefreshLayout();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelAll();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) CancelAll();
        }

        void OnRectTransformDimensionsChange()
        {
            if (!_updatingLayout && _root != null)
                RefreshLayout();
        }

        void OnSettingsChanged()
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }
            ApplySettings(SettingsStore.Current);
        }

        public void ApplySettings(SettingsStore settings = null)
        {
            SettingsStore s = settings ?? SettingsStore.Current;
            _reduceMotion = s.ReduceMotion;
            Haptics.Enabled = s.Haptics;
            bool layoutDirty = _leftHanded != s.LeftHanded || Mathf.Abs(_controlScale - s.ControlScale) > 0.001f;
            _leftHanded = s.LeftHanded;
            _controlScale = s.ControlScale;
            if (layoutDirty)
            {
                CancelAll();
                RefreshLayout();
            }
        }

        public void SetLeftHanded(bool leftHanded)
        {
            if (_leftHanded == leftHanded) return;
            _leftHanded = leftHanded;
            CancelAll();
            RefreshLayout();
        }

        public void SetControlScale(float scale)
        {
            float snapped = SettingsStore.SnapControlScale(scale);
            if (Mathf.Abs(_controlScale - snapped) <= 0.001f) return;
            _controlScale = snapped;
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            if (_root == null || _clusterRoot == null || _updatingLayout) return;
            if (isActiveAndEnabled) EnsureSubscribed();
            _updatingLayout = true;
            try
            {
                Canvas canvas = _root.GetComponentInParent<Canvas>();
                float dp = ScreenFitMetrics.DpToCanvasPx(1f, canvas);
                Vector2 safeCanvas = ScreenFitMetrics.SafeCanvasSize(canvas);
                float minCornerBtnCanvas = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, canvas);

                // Keep upper-arc buttons strictly outside the central 45%x50% sightbox across 21:9..4:3 foldables
                // and strictly below top-left HeroVitalsChip / top-right QuickChatButton in both handedness modes.
                float sightboxXLimitCanvas = Mathf.Max(120f * dp, safeCanvas.x * 0.275f - 2.5f * dp);
                float sightboxYLimitCanvas = safeCanvas.y * 0.25f - 2.0f * dp;
                float heroBottomCanvas = safeCanvas.y - (minCornerBtnCanvas + 22f + 74f);
                float chatBottomCanvas = safeCanvas.y - (12f + minCornerBtnCanvas);
                float maxClusterTopCanvas = Mathf.Max(210f * dp, Mathf.Min(heroBottomCanvas, chatBottomCanvas) - 3.0f * dp);

                // Keep opposite-corner Joystick.Base clear even on 4:3 inner foldables
                float minStickRadius = ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinTouchDp, canvas);
                float maxFoldStickRadius = Mathf.Max(72f, (safeCanvas.y * ScreenFitMetrics.MaxFoldableClusterHeightShare - 44f) * 0.5f);
                float effStickRadius = Mathf.Clamp(Mathf.Max(VirtualJoystick.Radius, minStickRadius), 72f, Mathf.Max(minStickRadius, maxFoldStickRadius));
                float stickOuterEdgeFromOpposite = ScreenFitMetrics.ClampThumbSpreadOffset(44f + effStickRadius, 160f, canvas) + effStickRadius;
                float maxHorizontalReachCanvas = Mathf.Max(220f * dp, safeCanvas.x - stickOuterEdgeFromOpposite - MinButtonGutterDp * dp);

                float capX = (sightboxXLimitCanvas / Mathf.Max(0.01f, dp)) / (UltDp.x + 25f);
                float capY = (maxClusterTopCanvas / Mathf.Max(0.01f, dp)) / (JumpDp.y + 24f);
                float maxSpreadScale = Mathf.Max(1f, Mathf.Min(1.2f, Mathf.Min(capX, capY)));

                // Never compress center offsets below 1.0x when ClampTouchSize holds diameters at >= 48 dp / >= 72 dp
                float spreadScale = Mathf.Clamp(_controlScale, 1f, maxSpreadScale);
                float sizeScale = Mathf.Clamp(_controlScale, 0.85f, spreadScale);

                float primaryPx = ScreenFitMetrics.ClampTouchSize(74f * dp * sizeScale, ScreenFitMetrics.PrimaryFireMinDp, canvas);
                float slotPx = ScreenFitMetrics.ClampTouchSize(49f * dp * sizeScale, ScreenFitMetrics.MinTouchDp, canvas);
                float ultPx = ScreenFitMetrics.ClampTouchSize(50f * dp * sizeScale, ScreenFitMetrics.MinTouchDp, canvas);
                float satPx = ScreenFitMetrics.ClampTouchSize(48f * dp * sizeScale, ScreenFitMetrics.MinTouchDp, canvas);

                InitButtonLayout(ButtonRole.Primary, PrimaryDp, dp, spreadScale, primaryPx);
                InitButtonLayout(ButtonRole.Slot0, SuperpowerDp[0], dp, spreadScale, slotPx);
                InitButtonLayout(ButtonRole.Slot1, SuperpowerDp[1], dp, spreadScale, slotPx);
                InitButtonLayout(ButtonRole.Slot2, SuperpowerDp[2], dp, spreadScale, slotPx);
                InitButtonLayout(ButtonRole.Dash, DashDp, dp, spreadScale, satPx);
                InitButtonLayout(ButtonRole.Ultimate, UltDp, dp, spreadScale, ultPx);
                InitButtonLayout(ButtonRole.Jump, JumpDp, dp, spreadScale, satPx);
                InitButtonLayout(ButtonRole.CrouchSlide, CrouchDp, dp, spreadScale, satPx);

                // Post-ClampTouchSize iterative solver guaranteeing >= 8.8 dp edge-to-edge clearance across every button pair
                float minGutterCanvas = MinButtonGutterDp * dp;
                float outerMarginCanvas = 6.0f * dp;
                SolveButtonClearances(minGutterCanvas, outerMarginCanvas, maxHorizontalReachCanvas, maxClusterTopCanvas, sightboxXLimitCanvas, sightboxYLimitCanvas);
                CommitButtonPlacements();

                if (_cancelZone != null)
                {
                    Vector2 cancelAnchor = _leftHanded ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                    _cancelZone.anchorMin = _cancelZone.anchorMax = cancelAnchor;
                    _cancelZone.pivot = cancelAnchor;
                    float cancelH = ScreenFitMetrics.ClampTouchSize(48f * dp, ScreenFitMetrics.MinTouchDp, canvas);
                    float cornerInset = Mathf.Max(24f, 12f * dp);
                    float innerOffset = cornerInset + minCornerBtnCanvas + 12f;
                    float openWingGap = safeCanvas.x * 0.5f - 344f - innerOffset;
                    if (openWingGap >= 160f)
                    {
                        float cancelW = Mathf.Clamp(openWingGap, 164f, 224f);
                        _cancelZone.sizeDelta = new Vector2(cancelW, cancelH);
                        _cancelZone.anchoredPosition = new Vector2(_leftHanded ? innerOffset : -innerOffset, -12f);
                    }
                    else
                    {
                        float cancelX = _leftHanded ? 232f : -innerOffset;
                        float cancelW = _leftHanded
                            ? Mathf.Max(cancelH, Mathf.Min(Mathf.Max(124f, cancelH * 1.35f), safeCanvas.x * 0.5f - 124f - cancelX))
                            : Mathf.Max(188f, cancelH * 2.1f);
                        _cancelZone.sizeDelta = new Vector2(cancelW, cancelH);
                        _cancelZone.anchoredPosition = new Vector2(cancelX, -68f);
                    }
                }

                if (_statusBanner != null)
                {
                    Vector2 corner = _leftHanded ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                    _statusBanner.anchorMin = _statusBanner.anchorMax = corner;
                    _statusBanner.pivot = corner;
                    float primCenterY = _layoutCenters[(int)ButtonRole.Primary].y;
                    _statusBanner.anchoredPosition = new Vector2(_leftHanded ? 16f : -16f, primCenterY + primaryPx * 0.56f);
                }
            }
            finally
            {
                _updatingLayout = false;
            }
        }

        void InitButtonLayout(ButtonRole role, Vector2 cornerDp, float dpToCanvas, float spreadScale, float sizeCanvas)
        {
            int idx = (int)role;
            float r = sizeCanvas * 0.5f;
            float outerMargin = 6.0f * dpToCanvas;
            float dx = Mathf.Max(r + outerMargin, cornerDp.x * dpToCanvas * spreadScale);
            float dy = Mathf.Max(r + outerMargin, cornerDp.y * dpToCanvas * spreadScale);
            _layoutRadii[idx] = r;
            _layoutCenters[idx] = new Vector2(dx, dy);
        }

        void SolveButtonClearances(
            float minGutterCanvas,
            float outerMarginCanvas,
            float maxHorizontalReachCanvas,
            float maxClusterTopCanvas,
            float sightboxXLimitCanvas,
            float sightboxYLimitCanvas)
        {
            for (int iter = 0; iter < 16; iter++)
            {
                bool moved = false;
                for (int i = 0; i < TotalButtonCount; i++)
                {
                    if (_buttons[i]?.Rect == null) continue;
                    for (int j = i + 1; j < TotalButtonCount; j++)
                    {
                        if (_buttons[j]?.Rect == null) continue;
                        Vector2 pi = _layoutCenters[i];
                        Vector2 pj = _layoutCenters[j];
                        Vector2 delta = pj - pi;
                        float dist = delta.magnitude;
                        float required = _layoutRadii[i] + _layoutRadii[j] + minGutterCanvas;
                        if (dist < required - 0.01f)
                        {
                            Vector2 dir = dist > 0.001f ? (delta / dist) : Vector2.up;
                            float push = (required - dist) * 0.52f;
                            if (i == (int)ButtonRole.Primary)
                            {
                                _layoutCenters[j] = pj + dir * (push * 2f);
                            }
                            else
                            {
                                _layoutCenters[i] = pi - dir * push;
                                _layoutCenters[j] = pj + dir * push;
                            }
                            moved = true;
                        }
                    }
                }

                for (int i = 0; i < TotalButtonCount; i++)
                {
                    if (_buttons[i]?.Rect == null) continue;
                    float r = _layoutRadii[i];
                    Vector2 p = _layoutCenters[i];
                    p.x = Mathf.Clamp(p.x, r + outerMarginCanvas, Mathf.Max(r + outerMarginCanvas, maxHorizontalReachCanvas - r));
                    p.y = Mathf.Clamp(p.y, r + outerMarginCanvas, Mathf.Max(r + outerMarginCanvas, maxClusterTopCanvas - r));

                    if (p.y + r > sightboxYLimitCanvas && p.x + r > sightboxXLimitCanvas)
                        p.x = Mathf.Max(r + outerMarginCanvas, sightboxXLimitCanvas - r);

                    _layoutCenters[i] = p;
                }

                if (!moved)
                    break;
            }
        }

        void CommitButtonPlacements()
        {
            Vector2 cornerAnchor = _leftHanded ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            for (int i = 0; i < TotalButtonCount; i++)
            {
                ButtonWidget w = _buttons[i];
                if (w == null || w.Rect == null) continue;
                w.PressSpring?.SnapToRest();
                float sizeCanvas = _layoutRadii[i] * 2f;
                Vector2 c = _layoutCenters[i];

                w.Rect.anchorMin = w.Rect.anchorMax = cornerAnchor;
                w.Rect.pivot = new Vector2(0.5f, 0.5f);
                w.Rect.sizeDelta = new Vector2(sizeCanvas, sizeCanvas);
                w.RestPos = new Vector2(_leftHanded ? c.x : -c.x, c.y);
                w.Rect.anchoredPosition = w.RestPos;
            }

            // Position the exact 7 glowing white stone dots curving cleanly along Primary's outer upper-left arc
            // (mirrored to upper-right when LeftHanded), strictly inside the >= 8.8 dp button gutter
            if (_ammoPipGroup != null)
            {
                float primaryRadius = Mathf.Max(36f, _layoutRadii[(int)ButtonRole.Primary]);
                float arcRadius = primaryRadius + 6.0f;
                for (int p = 0; p < MaxAmmoPips; p++)
                {
                    Image pip = _ammoPips[p];
                    if (pip == null) continue;
                    float deg = 192f - p * 18f; // 192 -> 84 deg (upper-left arc of Primary fire button)
                    if (_leftHanded) deg = 180f - deg;
                    float rad = deg * Mathf.Deg2Rad;
                    pip.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad) * arcRadius, Mathf.Sin(rad) * arcRadius);
                }
            }
        }

        /// <summary>Aborts any active hold or drag cleanly with InputButtons.AimCancel (spends zero cooldown or elixir, never auto-fires).</summary>
        public void CancelAll()
        {
            bool wasHoldingAbility = _heldSlot >= 0 || _pendingReleaseSlot >= 0;
            _primaryPointer = NoPointer;
            _primaryTapPending = false;
            _primaryTapTick = int.MinValue;
            _crouchPointer = NoPointer;
            _slotPointer = NoPointer;
            _heldSlot = -1;
            _holdElapsed = 0f;
            _screenDragWay = Vector2.zero;
            _hasDraggedPastDeadZone = false;
            _isCancelHovered = false;
            _slotPressObservedBySim = false;
            _pendingReleaseSlot = -1;
            _pendingReleaseCancel = false;
            _pendingReleaseUseDragAim = false;
            _pendingReleaseUseCrosshairAim = false;
            _latchedPulseButtons = 0;
            ActiveAimCamera = AimCameraKind.Normal;

            if (wasHoldingAbility)
                _latchedAimCancel = true;

            if (_cancelZone != null && _cancelZone.gameObject.activeSelf)
                _cancelZone.gameObject.SetActive(false);

            CurrentInput = new AbilityInputState
            {
                Buttons = wasHoldingAbility ? InputButtons.AimCancel : 0,
                HeldSlot = -1,
                PreviewWeaponId = "",
            };
        }

        /// <summary>Updates button icons, 0.1s cooldown sweeps, lockouts, and computes CurrentInput.</summary>
        public void Sync(SimBridge sim, float dt, ShoulderCamera shoulder = null, Camera worldCamera = null, MapLayoutData layout = null)
        {
            StepVisualTimers(dt);

            if (sim == null)
            {
                CurrentInput = default;
                return;
            }

            KidSnapshot? localOpt = sim.LocalKid;
            if (!localOpt.HasValue)
            {
                CancelAll();
                return;
            }

            KidSnapshot kid = localOpt.Value;
            bool playing = sim.Phase == MatchPhaseKind.Playing;
            _kidTagged = kid.Tagged;

            // Hide combat cluster cleanly while Tagged on the sidewalk so rescue countdown + D13 swap take over
            if (_clusterRoot != null && _clusterRoot.gameObject.activeSelf == _kidTagged)
                _clusterRoot.gameObject.SetActive(!_kidTagged);

            bool statusCanCast = kid.StatusKind != StatusKind.Stun &&
                                 kid.StatusKind != StatusKind.Polymorph &&
                                 kid.StatusKind != StatusKind.Knockout &&
                                 kid.StatusKind != StatusKind.Tripped;
            // docs/KITS.md: Root blocks movement only, NOT shooting or skills
            bool statusCanMove = statusCanCast && kid.StatusKind != StatusKind.Airborne && kid.StatusKind != StatusKind.Root;

            _usingSpecKit = kid.UsingSpecKit;
            _matchInteractive = playing && !_kidTagged;
            _castingBlocked = kid.CastingBlocked || kid.InRescueZone;
            _canCast = _matchInteractive && statusCanCast && !_castingBlocked;
            _canMove = _matchInteractive && statusCanMove;

            if (!_reduceMotion)
                _waterPulsePhase = Mathf.Repeat(_waterPulsePhase + Mathf.Max(0f, dt) * 4.4f, Mathf.PI * 2f);

            if ((!_canCast && _heldSlot >= 0) || (!_matchInteractive && (_primaryPointer != NoPointer || _primaryTapPending || _crouchPointer != NoPointer)))
                CancelAll();
            else if (!_canCast && _primaryTapPending)
            {
                _primaryTapPending = false;
                _primaryTapTick = int.MinValue;
            }

            if (_statusBanner != null)
            {
                bool showZoneBlock = _matchInteractive && _castingBlocked;
                bool showSkyCast = _matchInteractive && !showZoneBlock && _heldSlot >= 0 && ActiveAimCamera == AimCameraKind.MapCast;
                bool showBanner = showZoneBlock || showSkyCast;
                if (_statusBanner.gameObject.activeSelf != showBanner)
                    _statusBanner.gameObject.SetActive(showBanner);
                if (showZoneBlock)
                {
                    HudIconCache.SetTextIfChanged(
                        _statusBannerText,
                        kid.InRescueZone ? "NO CAST IN RESCUE" : "NO CAST IN BASE");
                }
                else if (showSkyCast)
                {
                    HudIconCache.SetTextIfChanged(_statusBannerText, "SKY CAST - AIM");
                }
            }

            // 1. Primary (Tirador) — 7-pip stone magazine, reload sweep, and hold-to-charge rim glow in Spec Kit mode
            float primCd = Mathf.Max(0f, kid.PrimaryCooldown);
            float primMax = Mathf.Max(0.1f, kid.PrimaryMaxCooldown > 0.01f
                ? kid.PrimaryMaxCooldown
                : (sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.Cooldown : 0.48f));
            UpdatePrimaryVisual(
                _buttons[(int)ButtonRole.Primary],
                _canCast,
                primCd,
                primMax,
                _primaryPointer != NoPointer || _primaryTapPending,
                in kid);

            // 2. Skill Slots (S1, S2, and Reload in Spec Kit mode; or 3 Superpowers in legacy mode)
            int handCount = kid.HandWeaponIds != null ? kid.HandWeaponIds.Length : 0;
            for (int s = 0; s < SuperpowerCount; s++)
            {
                string wid = s < handCount ? (kid.HandWeaponIds[s] ?? "") : "";
                _slotWeaponIds[s] = wid;

                WeaponInfo? wInfo = !string.IsNullOrEmpty(wid) ? sim.GetWeaponInfo(wid) : null;
                _slotMaxRanges[s] = wInfo.HasValue && wInfo.Value.MaxRange > 0.1f ? wInfo.Value.MaxRange : 16f;
                _slotAimCameras[s] = (kid.SlotAimCameras != null && s < kid.SlotAimCameras.Length)
                    ? kid.SlotAimCameras[s]
                    : (wInfo.HasValue ? wInfo.Value.AimCamera : AimCameraKind.Normal);

                if (_usingSpecKit && s == 2)
                {
                    // Repurpose Slot2 into crisp Tirador Reload button (keeps all 8 InteractiveButtonRects active for ScreenFitBoundsTests!)
                    ButtonWidget rldBtn = _buttons[(int)ButtonRole.Slot2];
                    if (!rldBtn.Rect.gameObject.activeSelf) rldBtn.Rect.gameObject.SetActive(true);
                    UpdateReloadVisual(rldBtn, in kid);
                    continue;
                }

                ButtonWidget w = _buttons[(int)ButtonRole.Slot0 + s];
                if (string.IsNullOrEmpty(wid))
                {
                    if (w.Rect.gameObject.activeSelf) w.Rect.gameObject.SetActive(false);
                    continue;
                }
                if (!w.Rect.gameObject.activeSelf) w.Rect.gameObject.SetActive(true);

                BindDynamicWeaponIcon(w, wid, wInfo);

                float cd = (kid.SlotCooldowns != null && s < kid.SlotCooldowns.Length) ? Mathf.Max(0f, kid.SlotCooldowns[s]) : 0f;
                float maxCd = (kid.SlotMaxCooldowns != null && s < kid.SlotMaxCooldowns.Length && kid.SlotMaxCooldowns[s] > 0.01f)
                    ? kid.SlotMaxCooldowns[s]
                    : (wInfo.HasValue ? Mathf.Max(0.1f, wInfo.Value.Cooldown) : 1f);

                if (_usingSpecKit)
                {
                    int waterCost = (kid.SlotWaterCosts != null && s < kid.SlotWaterCosts.Length)
                        ? kid.SlotWaterCosts[s]
                        : (wInfo.HasValue ? wInfo.Value.WaterCost : 0);
                    int maxCharges = (kid.SlotMaxCharges != null && s < kid.SlotMaxCharges.Length && kid.SlotMaxCharges[s] > 0)
                        ? kid.SlotMaxCharges[s]
                        : (wInfo.HasValue && wInfo.Value.MaxCharges > 0 ? wInfo.Value.MaxCharges : 1);
                    int charges = (kid.SlotCharges != null && s < kid.SlotCharges.Length)
                        ? Mathf.Clamp(kid.SlotCharges[s], 0, maxCharges)
                        : (cd <= 0.01f ? maxCharges : 0);

                    bool recastReady = s == 0 && kid.SyatoRecastReady;
                    bool hasCharge = recastReady || (maxCharges > 1 ? charges > 0 : cd <= 0.01f);
                    bool affordable = recastReady || waterCost <= 0 || kid.Water + 0.001f >= waterCost;
                    bool slotReady = _canCast && hasCharge && affordable;
                    w.InsufficientWater = !recastReady && waterCost > 0 && !affordable;

                    string badge = recastReady
                        ? "DASH"
                        : (waterCost > 0 ? HudIconCache.Whole(waterCost) : "");
                    float sweepRatio = (!hasCharge && cd > 0.01f)
                        ? Mathf.Clamp01(cd / maxCd)
                        : (w.InsufficientWater && waterCost > 0 ? Mathf.Clamp01(1f - kid.Water / waterCost) : 0f);

                    UpdateSpecSlotVisual(
                        w,
                        slotReady,
                        hasCharge ? 0f : cd,
                        sweepRatio,
                        _heldSlot == s,
                        badge,
                        w.InsufficientWater,
                        recastReady,
                        charges,
                        maxCharges);
                }
                else
                {
                    w.InsufficientWater = false;
                    HideChargePips(w);
                    int cost = (kid.SlotElixirCosts != null && s < kid.SlotElixirCosts.Length) ? kid.SlotElixirCosts[s] : 0;
                    bool affordable = !kid.CardsMode || kid.Elixir + 0.001f >= cost;
                    bool slotReady = _canCast && cd <= 0.01f && affordable;

                    string badge = kid.CardsMode && cost > 0 ? HudIconCache.Whole(cost) : "";
                    float sweepRatio = cd > 0.01f
                        ? Mathf.Clamp01(cd / maxCd)
                        : (!affordable && cost > 0 ? Mathf.Clamp01(1f - kid.Elixir / cost) : 0f);

                    UpdateSlotVisual(w, slotReady, cd, sweepRatio, _heldSlot == s, badge);
                }
            }

            // 3. Dash (Luksong-Tinik)
            float dashCd = Mathf.Max(0f, kid.DashCooldown);
            float dashMax = Mathf.Max(0.1f, kid.DashMaxCooldown > 0.01f ? kid.DashMaxCooldown : 4.2f);
            bool dashReady = _canMove && dashCd <= 0.01f && !kid.Dashing;
            UpdateButtonState(_buttons[(int)ButtonRole.Dash], dashReady, dashCd, dashMax, kid.Dashing, "");

            // 4. Ultimate (Spec ULT on slot 2 with 0 water & drag-aim, or legacy Bagsak ng Kalye 0..100% charge)
            if (_usingSpecKit)
            {
                ButtonWidget ultBtn = _buttons[(int)ButtonRole.Ultimate];
                string ultWid = _slotWeaponIds[2];
                WeaponInfo? ultInfo = !string.IsNullOrEmpty(ultWid) ? sim.GetWeaponInfo(ultWid) : null;
                if (!string.IsNullOrEmpty(ultWid))
                    BindDynamicWeaponIcon(ultBtn, ultWid, ultInfo);
                float ultCd = (kid.SlotCooldowns != null && kid.SlotCooldowns.Length > 2) ? Mathf.Max(0f, kid.SlotCooldowns[2]) : 0f;
                float ultMax = (kid.SlotMaxCooldowns != null && kid.SlotMaxCooldowns.Length > 2 && kid.SlotMaxCooldowns[2] > 0.01f)
                    ? kid.SlotMaxCooldowns[2]
                    : (ultInfo.HasValue && ultInfo.Value.Cooldown > 0.1f ? ultInfo.Value.Cooldown : 85f);
                bool ultReady = _canCast && ultCd <= 0.01f && !string.IsNullOrEmpty(ultWid);
                UpdateSpecUltimateVisual(ultBtn, ultReady, ultCd, ultMax, _heldSlot == 2, dt);
            }
            else
            {
                float ultCharge = Mathf.Clamp(kid.UltCharge, 0f, 100f);
                bool ultReady = _canCast && ultCharge >= 99.99f && sim.UltimateWeaponInfo.HasValue;
                UpdateUltimateVisual(_buttons[(int)ButtonRole.Ultimate], ultReady, ultCharge, dt);
            }

            // 5. Jump
            float jumpCd = Mathf.Max(0f, kid.JumpCooldown);
            bool inAir = kid.AirY > 0.02f;
            bool jumpReady = _canMove && !inAir && !kid.Dashing && !kid.Sliding && jumpCd <= 0.01f;
            UpdateButtonState(_buttons[(int)ButtonRole.Jump], jumpReady, jumpCd, 0.35f, false, "");

            // 6. Smart Crouch / Slide
            float slideCd = Mathf.Max(0f, kid.SlideCooldown);
            bool runningFast = _canMove && !kid.Walking && !kid.Crouching && !kid.Overclocked && (kid.Sprinting || kid.Speed01 >= VirtualJoystick.WalkRunThreshold);
            _canSlideNow = runningFast && !inAir && !kid.Sliding && slideCd <= 0.01f;
            ButtonWidget crouchBtn = _buttons[(int)ButtonRole.CrouchSlide];
            AssignStaticIcon(ButtonRole.CrouchSlide, "slide");
            UpdateButtonState(crouchBtn, _canMove, _canSlideNow ? 0f : slideCd, 3.2f, _crouchPointer != NoPointer || kid.Crouching || kid.Sliding, "");

            // 7. Cancel Zone highlight
            bool showCancel = _heldSlot >= 0;
            if (_cancelZone != null)
            {
                if (_cancelZone.gameObject.activeSelf != showCancel)
                    _cancelZone.gameObject.SetActive(showCancel);
                if (showCancel)
                {
                    _cancelPlate.color = _isCancelHovered ? CancelHotPlate : CancelIdlePlate;
                    float scaleBoost = (_isCancelHovered && !_reduceMotion) ? 1.06f : 1f;
                    _cancelZone.localScale = new Vector3(scaleBoost, scaleBoost, 1f);
                }
            }

            if (_heldSlot >= 0)
            {
                _holdElapsed += Mathf.Max(0f, dt);
                if (_holdElapsed > QuickTapMaxSeconds && !_isCancelHovered)
                    _hasDraggedPastDeadZone = _hasDraggedPastDeadZone || false; // hold-past-tap activates crosshair aim preview
                ActiveAimCamera = _heldSlot < _slotAimCameras.Length ? _slotAimCameras[_heldSlot] : AimCameraKind.Normal;
            }
            else
            {
                ActiveAimCamera = AimCameraKind.Normal;
            }

            CurrentInput = SampleInput(sim, shoulder, worldCamera, layout);
        }

        /// <summary>Builds the authoritative AbilityInputState for the current frame/tick (with tick-safe quick-tap &amp; cancel latches).</summary>
        public AbilityInputState SampleInput(SimBridge sim, ShoulderCamera shoulder = null, Camera worldCamera = null, MapLayoutData layout = null)
        {
            int simTick = sim != null ? sim.Tick : (_lastSampledSimTick + 1);
            bool tickAdvanced = simTick != _lastSampledSimTick;
            if (tickAdvanced)
                _lastSampledSimTick = simTick;

            KidSnapshot? localOpt = sim?.LocalKid;
            SVector2 kidFeet = localOpt.HasValue ? localOpt.Value.Position : SVector2.Zero;
            SVector2 kidFacing = localOpt.HasValue ? localOpt.Value.Facing : new SVector2(0f, -1f);
            float originH = (localOpt.HasValue && (localOpt.Value.Crouching || localOpt.Value.Sliding))
                ? AimSolver.CrouchedOriginHeight
                : AimSolver.DefaultOriginHeight;

            int buttons = 0;
            SVector2 aim = SVector2.Zero;
            SVector2 aimPoint = SVector2.Zero;
            bool hasAimPoint = false;
            string previewWid = "";
            bool isDraggingAim = false;

            if (_primaryTapPending && _primaryTapTick != int.MinValue && simTick != _primaryTapTick && _primaryPointer == NoPointer)
            {
                _primaryTapPending = false;
                _primaryTapTick = int.MinValue;
            }

            if ((_primaryPointer != NoPointer || _primaryTapPending) && _canCast && _heldSlot < 0 && _pendingReleaseSlot < 0)
            {
                if (_primaryTapPending && _primaryTapTick == int.MinValue)
                    _primaryTapTick = simTick;
                buttons |= InputButtons.Primary;
                if (worldCamera != null || shoulder != null)
                {
                    float pRange = localOpt.HasValue && localOpt.Value.TiradorCurrentRange > 0.5f
                        ? localOpt.Value.TiradorCurrentRange
                        : (sim != null && sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.MaxRange : 18f);
                    AimSolution pSol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, pRange, layout, originH);
                    aim = pSol.SimAimDirection;
                    aimPoint = pSol.SimAimPoint;
                    hasAimPoint = true;
                }
            }

            if (_crouchPointer != NoPointer && _canMove)
                buttons |= InputButtons.Crouch;

            // Consume one-tick pulses (Dash, Ultimate, Jump, Slide) cleanly across 30 Hz sim ticks
            if (_latchedPulseButtons != 0)
            {
                buttons |= _latchedPulseButtons;
                if ((_latchedPulseButtons & InputButtons.Ultimate) != 0 && sim != null && sim.UltimateWeaponInfo.HasValue)
                {
                    AimSolution uSol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, sim.UltimateWeaponInfo.Value.MaxRange, layout, originH);
                    aim = uSol.SimAimDirection;
                    aimPoint = uSol.SimAimPoint;
                    hasAimPoint = true;
                }
                if (tickAdvanced)
                    _latchedPulseButtons = 0;
            }

            if (_heldSlot >= 0)
            {
                buttons |= InputButtons.CardSlotBit(_heldSlot);
                _slotPressObservedBySim = true;

                previewWid = _slotWeaponIds[_heldSlot];
                float range = _slotMaxRanges[_heldSlot];
                isDraggingAim = _hasDraggedPastDeadZone || _holdElapsed > QuickTapMaxSeconds;

                if (_hasDraggedPastDeadZone)
                {
                    AimSolution sol = AimSolver.SolveArcDrag(shoulder, kidFeet, kidFacing, _screenDragWay, range, layout, originH);
                    aim = sol.SimAimDirection;
                    aimPoint = sol.SimAimPoint;
                    hasAimPoint = true;
                }
                else if (_holdElapsed > QuickTapMaxSeconds)
                {
                    AimSolution sol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, range, layout, originH);
                    aim = sol.SimAimDirection;
                    aimPoint = sol.SimAimPoint;
                    hasAimPoint = true;
                }
            }
            else if (_pendingReleaseSlot >= 0)
            {
                // Sub-tick tap guard: ensure StartFreshAim sees CardSlotBit asserted for 1 tick before the release tick fires
                if (!_slotPressObservedBySim)
                {
                    buttons |= InputButtons.CardSlotBit(_pendingReleaseSlot);
                    if (tickAdvanced)
                        _slotPressObservedBySim = true;
                }
                else
                {
                    if (_pendingReleaseCancel)
                    {
                        buttons |= InputButtons.AimCancel;
                    }
                    else if (_pendingReleaseUseDragAim)
                    {
                        AimSolution sol = AimSolver.SolveArcDrag(shoulder, kidFeet, kidFacing, _pendingReleaseDragWay, _pendingReleaseMaxRange, layout, originH);
                        aim = sol.SimAimDirection;
                        aimPoint = sol.SimAimPoint;
                        hasAimPoint = true;
                    }
                    else if (_pendingReleaseUseCrosshairAim)
                    {
                        AimSolution sol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, _pendingReleaseMaxRange, layout, originH);
                        aim = sol.SimAimDirection;
                        aimPoint = sol.SimAimPoint;
                        hasAimPoint = true;
                    }
                    // Quick-tap (!UseDragAim && !UseCrosshairAim): HasAimPoint=false & Aim=Zero -> fires press-locked auto-aim!

                    if (tickAdvanced)
                    {
                        _pendingReleaseSlot = -1;
                        _slotPressObservedBySim = false;
                    }
                }
            }

            if (_latchedAimCancel)
            {
                buttons |= InputButtons.AimCancel;
                if (tickAdvanced)
                    _latchedAimCancel = false;
            }

            var state = new AbilityInputState
            {
                Buttons = buttons,
                Aim = aim,
                AimPoint = aimPoint,
                HasAimPoint = hasAimPoint,
                PreviewWeaponId = previewWid ?? "",
                IsDraggingAim = isDraggingAim,
                IsCancelHovered = _heldSlot >= 0 && _isCancelHovered,
                HeldSlot = _heldSlot,
                ScreenDragWay = _screenDragWay,
                AimScreenPosition = _currentScreenPos,
            };
            CurrentInput = state;
            return state;
        }

        void HandlePointerDown(ButtonRole role, PointerEventData eventData)
        {
            if (!isActiveAndEnabled) return;
            ButtonWidget w = _buttons[(int)role];
            if (w == null) return;

            switch (role)
            {
                case ButtonRole.Primary:
                    if (_primaryPointer != NoPointer) return;
                    if (!_canCast)
                    {
                        TriggerDeny(w);
                        return;
                    }
                    _primaryPointer = eventData.pointerId;
                    _primaryTapPending = true;
                    _primaryTapTick = int.MinValue;
                    w.FlashTimer = PressFlashDuration;
                    Haptics.LightTap();
                    break;

                case ButtonRole.Slot0:
                case ButtonRole.Slot1:
                case ButtonRole.Slot2:
                {
                    if (_usingSpecKit && role == ButtonRole.Slot2)
                    {
                        if (!_canCast || !_canManualReload)
                        {
                            TriggerDeny(w);
                            return;
                        }
                        _latchedPulseButtons |= InputButtons.Reload;
                        w.FlashTimer = PressFlashDuration;
                        Haptics.LightTap();
                        return;
                    }

                    int slot = (int)role - (int)ButtonRole.Slot0;
                    BeginSlotAim(w, slot, eventData);
                    break;
                }

                case ButtonRole.Dash:
                    if (!w.Ready || !_canMove) { TriggerDeny(w); return; }
                    _latchedPulseButtons |= InputButtons.Dash;
                    w.FlashTimer = PressFlashDuration;
                    Haptics.LightTap();
                    break;

                case ButtonRole.Ultimate:
                    if (_usingSpecKit)
                    {
                        BeginSlotAim(w, 2, eventData);
                        break;
                    }
                    if (!w.Ready || !_canCast) { TriggerDeny(w); return; }
                    _latchedPulseButtons |= InputButtons.Ultimate;
                    w.FlashTimer = PressFlashDuration * 1.4f;
                    Haptics.HeavyImpact();
                    break;

                case ButtonRole.Jump:
                    if (!w.Ready || !_canMove) { TriggerDeny(w); return; }
                    _latchedPulseButtons |= InputButtons.Jump;
                    w.FlashTimer = PressFlashDuration;
                    Haptics.LightTap();
                    break;

                case ButtonRole.CrouchSlide:
                    if (_crouchPointer != NoPointer) return;
                    if (!_canMove) { TriggerDeny(w); return; }
                    if (_canSlideNow)
                    {
                        _latchedPulseButtons |= InputButtons.Slide;
                        w.FlashTimer = PressFlashDuration;
                        Haptics.LightTap();
                    }
                    else
                    {
                        _crouchPointer = eventData.pointerId;
                        w.FlashTimer = PressFlashDuration;
                        Haptics.LightTap();
                    }
                    break;
            }
        }

        void BeginSlotAim(ButtonWidget w, int slot, PointerEventData eventData)
        {
            if (_slotPointer != NoPointer || _heldSlot >= 0) return;
            if (!w.Ready || !_canCast || slot < 0 || slot >= _slotWeaponIds.Length || string.IsNullOrEmpty(_slotWeaponIds[slot]))
            {
                TriggerDeny(w);
                if (w.InsufficientWater)
                    Audio?.PlayWaterEmpty();
                return;
            }
            _slotPointer = eventData.pointerId;
            _heldSlot = slot;
            _holdElapsed = 0f;
            _pressScreenPos = eventData.position;
            _currentScreenPos = eventData.position;
            _screenDragWay = Vector2.zero;
            _hasDraggedPastDeadZone = false;
            _isCancelHovered = false;
            _slotPressObservedBySim = false;
            _pendingReleaseSlot = -1;
            ActiveAimCamera = slot < _slotAimCameras.Length ? _slotAimCameras[slot] : AimCameraKind.Normal;
            w.FlashTimer = slot == 2 ? PressFlashDuration * 1.35f : PressFlashDuration;
            if (_cancelZone != null) _cancelZone.gameObject.SetActive(true);
            if (slot == 2) Haptics.HeavyImpact();
            else Haptics.LightTap();
        }

        bool IsAimableSlotRole(ButtonRole role)
        {
            if (role == ButtonRole.Slot0 || role == ButtonRole.Slot1) return true;
            if (role == ButtonRole.Slot2) return !_usingSpecKit;
            if (role == ButtonRole.Ultimate) return _usingSpecKit;
            return false;
        }

        void HandleDrag(ButtonRole role, PointerEventData eventData)
        {
            if (!IsAimableSlotRole(role)) return;
            if (eventData.pointerId != _slotPointer || _heldSlot < 0) return;

            _currentScreenPos = eventData.position;
            Canvas canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            float deadZonePx = Mathf.Max(14f, ScreenFitMetrics.DpToScreenPx(AimDeadZoneDp));
            float dragRadiusPx = Mathf.Max(36f, ScreenFitMetrics.DpToScreenPx(AimDragRadiusDp));

            Vector2 deltaScreen = eventData.position - _pressScreenPos;
            if (deltaScreen.magnitude > deadZonePx)
                _hasDraggedPastDeadZone = true;

            _screenDragWay = _hasDraggedPastDeadZone
                ? Vector2.ClampMagnitude(deltaScreen / dragRadiusPx, 1f)
                : Vector2.zero;

            bool overCancel = IsOverCancelZone(eventData.position, eventData.pressEventCamera ?? (canvas != null ? canvas.worldCamera : null));
            if (overCancel && !_isCancelHovered)
                Haptics.CancelTick();
            _isCancelHovered = overCancel;
        }

        void HandlePointerUp(ButtonRole role, PointerEventData eventData)
        {
            if (role == ButtonRole.Primary && eventData.pointerId == _primaryPointer)
            {
                _primaryPointer = NoPointer;
                return;
            }
            if (role == ButtonRole.CrouchSlide && eventData.pointerId == _crouchPointer)
            {
                _crouchPointer = NoPointer;
                return;
            }
            if (IsAimableSlotRole(role) && eventData.pointerId == _slotPointer && _heldSlot >= 0)
            {
                Canvas canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
                bool overCancel = _isCancelHovered || IsOverCancelZone(eventData.position, eventData.pressEventCamera ?? (canvas != null ? canvas.worldCamera : null));
                if (overCancel)
                {
                    Haptics.CancelTick();
                    _latchedAimCancel = true;
                    _pendingReleaseSlot = -1;
                    _pendingReleaseCancel = false;
                    _pendingReleaseUseDragAim = false;
                    _pendingReleaseUseCrosshairAim = false;
                    _slotPressObservedBySim = false;
                }
                else
                {
                    _pendingReleaseSlot = _heldSlot;
                    _pendingReleaseCancel = false;
                    _pendingReleaseUseDragAim = _hasDraggedPastDeadZone;
                    _pendingReleaseUseCrosshairAim = !_hasDraggedPastDeadZone && _holdElapsed > QuickTapMaxSeconds;
                    _pendingReleaseDragWay = _screenDragWay;
                    _pendingReleaseMaxRange = _slotMaxRanges[_heldSlot];
                }

                _slotPointer = NoPointer;
                _heldSlot = -1;
                _holdElapsed = 0f;
                _screenDragWay = Vector2.zero;
                _hasDraggedPastDeadZone = false;
                _isCancelHovered = false;
                ActiveAimCamera = AimCameraKind.Normal;
                if (_cancelZone != null) _cancelZone.gameObject.SetActive(false);
            }
        }

        void HandlePointerCancel(ButtonRole role, PointerEventData eventData)
        {
            int pid = eventData != null ? eventData.pointerId : NoPointer;
            if (role == ButtonRole.Primary && (eventData == null || pid == _primaryPointer))
            {
                _primaryPointer = NoPointer;
                _primaryTapPending = false;
                _primaryTapTick = int.MinValue;
            }
            else if (role == ButtonRole.CrouchSlide && (eventData == null || pid == _crouchPointer))
                _crouchPointer = NoPointer;
            else if (IsAimableSlotRole(role) && (eventData == null || pid == _slotPointer))
                CancelAll();
        }

        void PollOsTouchCancel()
        {
            if ((_slotPointer == NoPointer && _primaryPointer == NoPointer && _crouchPointer == NoPointer) || !EnhancedTouchSupport.enabled)
                return;
            var touches = ETouch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                ETouch t = touches[i];
                if (t.phase != ETouchPhase.Canceled) continue;
                if (t.touchId == _slotPointer) { CancelAll(); return; }
                if (t.touchId == _primaryPointer)
                {
                    _primaryPointer = NoPointer;
                    _primaryTapPending = false;
                    _primaryTapTick = int.MinValue;
                }
                if (t.touchId == _crouchPointer) _crouchPointer = NoPointer;
            }
        }

        bool IsOverCancelZone(Vector2 screenPos, Camera eventCamera)
        {
            if (_cancelZone == null || !_cancelZone.gameObject.activeInHierarchy) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_cancelZone, screenPos, eventCamera, out Vector2 local))
                return false;
            Rect r = _cancelZone.rect;
            const float generousPad = 14f;
            return local.x >= r.xMin - generousPad && local.x <= r.xMax + generousPad &&
                   local.y >= r.yMin - generousPad && local.y <= r.yMax + generousPad;
        }

        void TriggerDeny(ButtonWidget w)
        {
            w.DenyTimer = DenyFlashDuration;
            Haptics.CancelTick();
        }

        void StepVisualTimers(float dt)
        {
            PollOsTouchCancel();
            if (dt <= 0f) return;
            for (int i = 0; i < _buttons.Length; i++)
            {
                ButtonWidget w = _buttons[i];
                if (w == null) continue;
                if (w.DenyTimer > 0f)
                {
                    w.DenyTimer = Mathf.Max(0f, w.DenyTimer - dt);
                    float shake = (!_reduceMotion && w.DenyTimer > 0f)
                        ? Mathf.Sin((1f - w.DenyTimer / DenyFlashDuration) * Mathf.PI * 6f) * 5f
                        : 0f;
                    w.Rect.anchoredPosition = new Vector2(w.RestPos.x + shake, w.RestPos.y);
                }
                if (w.FlashTimer > 0f)
                    w.FlashTimer = Mathf.Max(0f, w.FlashTimer - dt);
            }
        }

        static void BindDynamicWeaponIcon(ButtonWidget w, string weaponId, WeaponInfo? info)
        {
            if (string.Equals(w.LastWeaponId, weaponId, StringComparison.Ordinal)) return;
            w.LastWeaponId = weaponId;

            // Locked G7: always render crisp illustrated MenuArt.SkillEmblem vector icon (never raw text labels)
            w.Icon.sprite = MenuArt.SkillEmblem(weaponId);
            w.Icon.enabled = true;
            HudIconCache.SetTextIfChanged(w.CenterText, "");
        }

        void ApplyDiscVisual(
            ButtonWidget w,
            Color glassTint,
            Color rimColor,
            float rimGlow,
            float cooldown01,
            float arc01,
            float lockedDesat,
            float ultPulse,
            Color fallbackSweepColor,
            bool scaleBoostActive,
            float boostScale)
        {
            if (w.HasDiscShader && w.DiscMat != null)
            {
                w.Bg.color = Color.white;
                w.DiscMat.SetColor(UiShaders.PropGlassTint, glassTint);
                w.DiscMat.SetColor(UiShaders.PropRimColor, rimColor);
                w.DiscMat.SetFloat(UiShaders.PropRimGlow, rimGlow);
                w.DiscMat.SetFloat(UiShaders.PropCooldown01, Mathf.Clamp01(cooldown01));
                w.DiscMat.SetFloat(UiShaders.PropArc01, Mathf.Clamp01(arc01));
                w.DiscMat.SetFloat(UiShaders.PropLockedDesat, Mathf.Clamp01(lockedDesat));
                w.DiscMat.SetFloat(UiShaders.PropUltPulse, Mathf.Clamp01(ultPulse));
                w.DiscMat.SetFloat(UiShaders.PropReduceMotion, _reduceMotion ? 1f : 0f);
            }
            else
            {
                w.Bg.color = glassTint;
                w.Rim.color = rimColor;
                w.CooldownSweep.color = fallbackSweepColor;
                w.CooldownSweep.fillAmount = Mathf.Clamp01(cooldown01);
            }

            if (!Application.isPlaying)
            {
                w.Rect.localScale = Vector3.one;
            }
            else if (w.PressSpring == null)
            {
                float s = (!_reduceMotion && scaleBoostActive) ? boostScale : 1f;
                w.Rect.localScale = new Vector3(s, s, 1f);
            }
        }

        void UpdatePrimaryVisual(ButtonWidget w, bool canCast, float cd, float maxCd, bool isHeld, in KidSnapshot kid)
        {
            bool showMagazine = kid.UsingSpecKit && kid.TiradorMaxAmmo > 0;
            if (_ammoPipGroup != null && _ammoPipGroup.gameObject.activeSelf != showMagazine)
                _ammoPipGroup.gameObject.SetActive(showMagazine);

            if (showMagazine)
            {
                int maxAmmo = Mathf.Clamp(kid.TiradorMaxAmmo, 1, MaxAmmoPips);
                int ammo = Mathf.Clamp(kid.TiradorAmmo, 0, maxAmmo);
                Color litCol = kid.EmpoweredShotReady ? AmmoPipEmpowered : AmmoPipLit;
                for (int i = 0; i < MaxAmmoPips; i++)
                {
                    bool usePip = i < maxAmmo;
                    if (_ammoPips[i].gameObject.activeSelf != usePip)
                        _ammoPips[i].gameObject.SetActive(usePip);
                    if (usePip)
                    {
                        bool lit = i < ammo;
                        _ammoPips[i].color = lit ? litCol : AmmoPipSpent;
                        float pipSz = lit ? 7.2f : 5.2f;
                        _ammoPips[i].rectTransform.sizeDelta = new Vector2(pipSz, pipSz);
                    }
                }

                bool reloading = kid.TiradorReloadLeft > 0.01f;
                if (reloading)
                {
                    float total = Mathf.Max(0.1f, kid.TiradorReloadTotal);
                    float reloadFrac = Mathf.Clamp01(kid.TiradorReloadLeft / total);
                    w.Ready = false;
                    HudIconCache.SetTextIfChanged(w.CooldownText, HudIconCache.Tenths(kid.TiradorReloadLeft));
                    HudIconCache.SetTextIfChanged(w.BadgeText, "RLD");
                    w.BadgeText.color = RimHeld;
                    w.Icon.color = new Color(0.68f, 0.74f, 0.82f, 0.54f);
                    ApplyDiscVisual(
                        w,
                        DiscBgLocked,
                        RimHeld,
                        0.45f,
                        reloadFrac,
                        1f - reloadFrac,
                        0.60f,
                        0f,
                        SweepShade,
                        false,
                        1f);
                    return;
                }

                bool charging = kid.TiradorCharge01 >= 0f;
                w.Ready = canCast && cd <= 0.01f && ammo > 0;
                HudIconCache.SetTextIfChanged(w.CooldownText, "");
                HudIconCache.SetTextIfChanged(w.BadgeText, "");

                bool denyingSpec = w.DenyTimer > 0.001f;
                bool activeSpec = isHeld || charging || w.FlashTimer > 0f;
                Color bgCol = denyingSpec ? DenyTint : (canCast ? DiscBgPrimary : DiscBgLocked);
                Color rimCol = charging
                    ? Color.Lerp(RimHeld, AmmoPipEmpowered, Mathf.Clamp01(kid.TiradorCharge01))
                    : (activeSpec ? RimHeld : (kid.EmpoweredShotReady ? AmmoPipEmpowered : RimPrimaryReady));
                float rimGlow = activeSpec || kid.EmpoweredShotReady ? 0.82f : (canCast ? 0.42f : 0.08f);
                float chargeArc = charging ? Mathf.Clamp01(kid.TiradorCharge01) : (kid.EmpoweredShotReady ? 1f : 0f);
                float cadenceSweep = cd > 0.04f ? Mathf.Clamp01(cd / Mathf.Max(0.1f, maxCd)) : 0f;

                w.Icon.color = canCast ? MenuArt.PaperWhite : new Color(0.60f, 0.65f, 0.72f, 0.50f);
                ApplyDiscVisual(
                    w,
                    bgCol,
                    rimCol,
                    rimGlow,
                    w.HasDiscShader ? 0f : cadenceSweep,
                    chargeArc,
                    canCast ? 0f : 0.75f,
                    0f,
                    PrimaryCadenceShade,
                    activeSpec,
                    1.06f);
                return;
            }

            bool shortCadence = canCast && cd < 1.0f;
            if (!shortCadence)
            {
                UpdateButtonState(w, false, cd, maxCd, isHeld, "");
                return;
            }

            w.Ready = cd <= 0.01f;
            HudIconCache.SetTextIfChanged(w.CooldownText, "");
            HudIconCache.SetTextIfChanged(w.BadgeText, "");

            bool denying = w.DenyTimer > 0.001f;
            bool activePulse = isHeld || w.FlashTimer > 0f;
            float legSweep = cd > 0.04f ? Mathf.Clamp01(cd / Mathf.Max(0.1f, maxCd)) : 0f;
            w.Icon.color = MenuArt.PaperWhite;
            ApplyDiscVisual(
                w,
                denying ? DenyTint : DiscBgPrimary,
                activePulse ? RimHeld : RimPrimaryReady,
                activePulse ? 0.80f : 0.42f,
                w.HasDiscShader ? 0f : legSweep,
                0f,
                0f,
                0f,
                PrimaryCadenceShade,
                activePulse,
                1.06f);
        }

        void UpdateReloadVisual(ButtonWidget w, in KidSnapshot kid)
        {
            w.LastWeaponId = "reload";
            w.InsufficientWater = false;
            HideChargePips(w);

            w.Icon.sprite = MenuArt.SkillEmblem("reload");
            w.Icon.enabled = true;
            HudIconCache.SetTextIfChanged(w.CenterText, "");

            int maxAmmo = Mathf.Max(1, kid.TiradorMaxAmmo);
            int ammo = Mathf.Clamp(kid.TiradorAmmo, 0, maxAmmo);
            bool reloading = kid.TiradorReloadLeft > 0.01f;
            _canManualReload = !reloading && ammo < maxAmmo;
            w.Ready = _canCast && _canManualReload;

            float sweep = reloading && kid.TiradorReloadTotal > 0.01f
                ? Mathf.Clamp01(kid.TiradorReloadLeft / kid.TiradorReloadTotal)
                : 0f;
            HudIconCache.SetTextIfChanged(w.CooldownText, reloading ? HudIconCache.Tenths(kid.TiradorReloadLeft) : "");
            HudIconCache.SetTextIfChanged(w.BadgeText, HudIconCache.AmmoFraction(ammo, maxAmmo));
            w.BadgeText.color = reloading ? RimHeld : (ammo <= 2 ? WaterBadgeLow : WaterBadgeReady);

            bool denying = w.DenyTimer > 0.001f;
            bool lowAmmoAlert = !reloading && ammo <= 2 && _canCast;
            Color bgCol = denying ? DenyTint : (w.Ready || reloading ? DiscBgReady : DiscBgLocked);
            Color rimCol = reloading
                ? RimHeld
                : (lowAmmoAlert ? Color.Lerp(RimReady, RimHeld, 0.5f + 0.5f * Mathf.Sin(_waterPulsePhase)) : (w.Ready ? RimReady : RimLocked));
            w.Icon.color = reloading
                ? RimHeld
                : (w.Ready ? MenuArt.PaperWhite : new Color(0.62f, 0.67f, 0.74f, 0.56f));

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                reloading || lowAmmoAlert ? 0.65f : (w.Ready ? 0.28f : 0.08f),
                sweep,
                reloading ? 1f - sweep : (ammo / (float)maxAmmo),
                w.Ready || reloading ? 0f : 0.45f,
                0f,
                SweepShade,
                w.FlashTimer > 0f,
                1.07f);
        }

        void UpdateSpecSlotVisual(
            ButtonWidget w,
            bool ready,
            float cd,
            float sweepRatio,
            bool isHeld,
            string badge,
            bool insufficientWater,
            bool recastReady,
            int charges,
            int maxCharges)
        {
            w.Ready = ready;
            HudIconCache.SetTextIfChanged(w.CooldownText, cd > 0.04f ? HudIconCache.Tenths(cd) : "");
            HudIconCache.SetTextIfChanged(w.BadgeText, badge);
            w.BadgeText.color = recastReady
                ? RecastRimGold
                : (insufficientWater ? WaterBadgeLow : WaterBadgeReady);

            if (w.ChargePips != null)
            {
                bool showPips = maxCharges > 1;
                int visiblePips = showPips ? Mathf.Min(maxCharges, MaxChargePips) : 0;
                for (int i = 0; i < w.ChargePips.Length; i++)
                {
                    bool active = i < visiblePips;
                    if (w.ChargePips[i].gameObject.activeSelf != active)
                        w.ChargePips[i].gameObject.SetActive(active);
                    if (active)
                    {
                        float offset = (i - (visiblePips - 1) * 0.5f) * 11f;
                        w.ChargePips[i].rectTransform.anchoredPosition = new Vector2(offset, -9f);
                        w.ChargePips[i].color = i < charges ? ChargePipLit : ChargePipSpent;
                    }
                }
            }

            bool denying = w.DenyTimer > 0.001f;
            float pulse = _reduceMotion ? 0.5f : (0.5f + 0.5f * Mathf.Sin(_waterPulsePhase));
            Color bgCol = denying
                ? DenyTint
                : (recastReady ? DiscBgRecast : (ready ? DiscBgReady : DiscBgLocked));
            Color rimCol = isHeld
                ? RimHeld
                : (recastReady
                    ? RecastRimGold
                    : (insufficientWater
                        ? Color.Lerp(WaterLowRimA, WaterLowRimB, pulse)
                        : (ready ? RimReady : RimLocked)));
            float rimGlow = isHeld || recastReady ? 0.85f : (ready ? 0.28f : 0.08f);
            w.Icon.color = ready ? MenuArt.PaperWhite : new Color(0.56f, 0.62f, 0.70f, insufficientWater ? 0.44f : 0.52f);

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                rimGlow,
                sweepRatio,
                recastReady ? 1f : 0f,
                ready ? 0f : 0.75f,
                0f,
                SweepShade,
                isHeld || w.FlashTimer > 0f,
                1.08f);
        }

        void UpdateSpecUltimateVisual(ButtonWidget w, bool ready, float cd, float maxCd, bool isHeld, float dt)
        {
            w.Ready = ready;
            w.InsufficientWater = false;
            float sweep = (!ready && cd > 0.04f) ? Mathf.Clamp01(cd / Mathf.Max(0.1f, maxCd)) : 0f;
            HudIconCache.SetTextIfChanged(w.CooldownText, (!ready && cd > 0.04f) ? HudIconCache.Tenths(cd) : "");
            HudIconCache.SetTextIfChanged(w.BadgeText, ready ? "ULT" : "");
            w.BadgeText.color = RimUltReady;

            bool denying = w.DenyTimer > 0.001f;
            Color bgCol = denying ? DenyTint : (ready ? DiscBgUltReady : DiscBgLocked);
            Color rimCol = isHeld ? RimHeld : (ready ? RimUltReady : RimLocked);
            w.Icon.color = ready ? MenuArt.GoldLight : new Color(0.62f, 0.67f, 0.74f, 0.52f);

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                ready || isHeld ? 0.92f : 0.12f,
                sweep,
                1f - sweep,
                ready ? 0f : 0.72f,
                ready ? 1f : 0f,
                SweepShade,
                isHeld || w.FlashTimer > 0f,
                1.10f);

            SyncUltimateHalo(w, ready, dt);
        }

        void SyncUltimateHalo(ButtonWidget w, bool ready, float dt)
        {
            if (w.Halo == null) return;
            if (ready)
            {
                if (!_reduceMotion) _ultPulsePhase = Mathf.Repeat(_ultPulsePhase + dt * 3.2f, Mathf.PI * 2f);
                float alpha = _reduceMotion ? 0.62f : (0.48f + 0.28f * (0.5f + 0.5f * Mathf.Sin(_ultPulsePhase)));
                w.Halo.color = w.HasSoftGlowHalo
                    ? new Color(1f, 1f, 1f, alpha)
                    : new Color(MenuArt.WaterCyan.r, MenuArt.WaterCyan.g, MenuArt.WaterCyan.b, alpha * 0.55f);
            }
            else
            {
                w.Halo.color = w.HasSoftGlowHalo
                    ? new Color(1f, 1f, 1f, 0f)
                    : new Color(MenuArt.WaterCyan.r, MenuArt.WaterCyan.g, MenuArt.WaterCyan.b, 0f);
            }
        }

        static void HideChargePips(ButtonWidget w)
        {
            if (w?.ChargePips == null) return;
            for (int i = 0; i < w.ChargePips.Length; i++)
                if (w.ChargePips[i].gameObject.activeSelf)
                    w.ChargePips[i].gameObject.SetActive(false);
        }

        void UpdateButtonState(ButtonWidget w, bool ready, float cd, float maxCd, bool heldOrActive, string badge)
        {
            w.Ready = ready;
            float sweep = cd > 0.04f ? Mathf.Clamp01(cd / Mathf.Max(0.1f, maxCd)) : 0f;
            HudIconCache.SetTextIfChanged(w.CooldownText, cd > 0.04f ? HudIconCache.Tenths(cd) : "");
            HudIconCache.SetTextIfChanged(w.BadgeText, badge);

            bool denying = w.DenyTimer > 0.001f;
            Color bgCol = denying ? DenyTint : (ready ? (w.Role == ButtonRole.Primary ? DiscBgPrimary : DiscBgReady) : DiscBgLocked);
            Color rimCol = heldOrActive ? RimHeld : (ready ? RimReady : RimLocked);
            w.Icon.color = ready ? MenuArt.PaperWhite : new Color(0.60f, 0.65f, 0.72f, 0.52f);

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                heldOrActive ? 0.80f : (ready ? 0.26f : 0.08f),
                sweep,
                0f,
                ready ? 0f : 0.75f,
                0f,
                SweepShade,
                heldOrActive || w.FlashTimer > 0f,
                1.06f);
        }

        void UpdateSlotVisual(ButtonWidget w, bool ready, float cd, float sweepRatio, bool isHeld, string badge)
        {
            w.Ready = ready;
            HudIconCache.SetTextIfChanged(w.CooldownText, cd > 0.04f ? HudIconCache.Tenths(cd) : "");
            HudIconCache.SetTextIfChanged(w.BadgeText, badge);
            w.BadgeText.color = WaterBadgeReady;

            bool denying = w.DenyTimer > 0.001f;
            Color bgCol = denying ? DenyTint : (ready ? DiscBgReady : DiscBgLocked);
            Color rimCol = isHeld ? RimHeld : (ready ? RimReady : RimLocked);
            w.Icon.color = ready ? MenuArt.PaperWhite : new Color(0.60f, 0.65f, 0.72f, 0.50f);

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                isHeld ? 0.82f : (ready ? 0.28f : 0.08f),
                sweepRatio,
                0f,
                ready ? 0f : 0.75f,
                0f,
                SweepShade,
                isHeld || w.FlashTimer > 0f,
                1.08f);
        }

        void UpdateUltimateVisual(ButtonWidget w, bool ready, float charge0To100, float dt)
        {
            w.Ready = ready;
            float missing = ready ? 0f : Mathf.Clamp01(1f - charge0To100 / 100f);
            HudIconCache.SetTextIfChanged(w.CooldownText, "");
            HudIconCache.SetTextIfChanged(w.BadgeText, ready ? "ULT" : HudIconCache.Percent(charge0To100));
            w.BadgeText.color = ready ? RimUltReady : WaterBadgeReady;

            bool denying = w.DenyTimer > 0.001f;
            Color bgCol = denying ? DenyTint : (ready ? DiscBgUltReady : DiscBgLocked);
            Color rimCol = ready ? RimUltReady : RimLocked;
            w.Icon.color = ready ? MenuArt.GoldLight : new Color(0.62f, 0.67f, 0.74f, 0.55f);

            ApplyDiscVisual(
                w,
                bgCol,
                rimCol,
                ready ? 0.92f : 0.14f,
                missing,
                Mathf.Clamp01(charge0To100 / 100f),
                ready ? 0f : 0.70f,
                ready ? 1f : 0f,
                SweepShade,
                w.FlashTimer > 0f,
                1.10f);

            SyncUltimateHalo(w, ready, dt);
        }
    }
}
