using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Zero-dependency pure-C# ISO/IEC 18004 Byte-mode QR Matrix encoder, lightweight finder/payload scanner,
    /// and UGUI QR Share + Live <see cref="WebCamTexture"/> Scanner modal with manual <c>ws://</c> / Room Code paste fallback
    /// (port of <c>scripts/ui/qr_code.gd</c>, <c>qr_field.gd</c>, <c>qr_reader.gd</c>, and <c>qr_scan_sheet.gd</c>).
    /// </summary>
    [ExecuteAlways]
    public sealed class QrCodeView : MonoBehaviour
    {
        public enum EccLevel
        {
            L = 0,
            M = 1,
            Q = 2,
            H = 3,
        }

        public const int MaxSupportedVersion = 10;
        public const float ScanIntervalSeconds = 0.30f;

        static readonly Color CardBg = new Color(0.07f, 0.11f, 0.22f, 0.98f);
        static readonly Color CardBorder = new Color(0.98f, 0.78f, 0.24f, 1f);
        static readonly Color SurfaceDark = new Color(0.04f, 0.06f, 0.13f, 1f);
        static readonly Color ButtonOrange = new Color(0.96f, 0.46f, 0.14f, 1f);
        static readonly Color ButtonBlue = new Color(0.18f, 0.52f, 0.92f, 1f);
        static readonly Color ButtonSlate = new Color(0.20f, 0.27f, 0.40f, 1f);
        static readonly Color MutedCream = new Color(0.84f, 0.88f, 0.96f, 1f);
        static readonly Color ErrorCoral = new Color(1.0f, 0.45f, 0.42f, 1f);

        /// <summary>Delegate matching <c>Func&lt;string, Sprite&gt;</c> for screen constructors (<c>scale = 6, quietZone = 2</c>).</summary>
        public static Func<string, Sprite> DefaultSpriteProvider { get; } = payload => EncodeSprite(payload, 6, 2);

        // =============================================================================================================================
        // Public Static QR Encoder & Decoder API
        // =============================================================================================================================

        public sealed class QrMatrix
        {
            public readonly int Version;
            public readonly int Size;
            public readonly EccLevel Ecc;
            public readonly int Mask;
            public readonly byte[] Modules;

            internal QrMatrix(int version, int size, EccLevel ecc, int mask, byte[] modules)
            {
                Version = version;
                Size = size;
                Ecc = ecc;
                Mask = mask;
                Modules = modules;
            }

            public bool IsDark(int x, int y) =>
                x >= 0 && y >= 0 && x < Size && y < Size && Modules[y * Size + x] != 0;
        }

        /// <summary>Encodes <paramref name="payload"/> into an ISO/IEC 18004 Byte-mode QR matrix (Versions 1..10), or null if too long.</summary>
        public static QrMatrix EncodeMatrix(string payload, EccLevel ecc = EccLevel.M)
        {
            if (payload == null) return null;
            byte[] utf8 = Encoding.UTF8.GetBytes(payload);
            QrMatrix matrix = QrIsoCodec.EncodeByteMode(utf8, ecc);
            if (matrix == null && ecc != EccLevel.L)
                matrix = QrIsoCodec.EncodeByteMode(utf8, EccLevel.L);
            return matrix;
        }

        /// <summary>
        /// Encodes <paramref name="payload"/> into a crisp <see cref="FilterMode.Point"/> <see cref="Texture2D"/>
        /// using ISO/IEC 18004 Byte-mode QR encoding (<c>scale = 6, quietZone = 2</c>).
        /// </summary>
        public static Texture2D EncodeTexture(string payload) =>
            EncodeTexture(payload, 6, 2);

        /// <summary>
        /// Encodes <paramref name="payload"/> into a crisp <see cref="FilterMode.Point"/> <see cref="Texture2D"/>
        /// using ISO/IEC 18004 Byte-mode QR encoding.
        /// </summary>
        public static Texture2D EncodeTexture(string payload, int scale, int quietZone = 2) =>
            EncodeTexture(payload, new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255), scale, quietZone);

        public static Texture2D EncodeTexture(
            string payload,
            Color32 darkColor,
            Color32 lightColor,
            int scale = 6,
            int quietZone = 2)
        {
            QrMatrix qr = EncodeMatrix(payload ?? "");
            if (qr == null) return null;

            int pxPerModule = Mathf.Clamp(scale, 1, 32);
            int border = Mathf.Clamp(quietZone, 0, 16);
            int totalModules = qr.Size + border * 2;
            int texSide = totalModules * pxPerModule;

            var pixels = new Color32[texSide * texSide];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = lightColor;

            // Unity Texture2D y=0 is bottom-left; map QR top row (my=0) to top of texture so finders sit upright.
            for (int my = 0; my < qr.Size; my++)
            {
                int flippedModuleY = totalModules - 1 - (my + border);
                int pyStart = flippedModuleY * pxPerModule;
                for (int mx = 0; mx < qr.Size; mx++)
                {
                    if (!qr.IsDark(mx, my)) continue;
                    int pxStart = (mx + border) * pxPerModule;
                    for (int dy = 0; dy < pxPerModule; dy++)
                    {
                        int rowOffset = (pyStart + dy) * texSide + pxStart;
                        for (int dx = 0; dx < pxPerModule; dx++)
                            pixels[rowOffset + dx] = darkColor;
                    }
                }
            }

            var tex = new Texture2D(texSide, texSide, TextureFormat.RGBA32, false)
            {
                name = "KalyeahQrTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Encodes <paramref name="payload"/> into a crisp <see cref="FilterMode.Point"/> UGUI <see cref="Sprite"/> (<c>scale = 6, quietZone = 2</c>; binds directly to <c>Func&lt;string, Sprite&gt;</c>).</summary>
        public static Sprite EncodeSprite(string payload) =>
            EncodeSprite(payload, 6, 2);

        /// <summary>Encodes <paramref name="payload"/> into a crisp <see cref="FilterMode.Point"/> UGUI <see cref="Sprite"/>.</summary>
        public static Sprite EncodeSprite(string payload, int scale, int quietZone = 2)
        {
            Texture2D tex = EncodeTexture(payload, scale, quietZone);
            if (tex == null) return null;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "KalyeahQrSprite";
            return sprite;
        }

        /// <summary>Builds an <c>Action&lt;Action&lt;string&gt;&gt;</c> scan-modal requester delegate for <c>HotspotFlowView</c> / <c>LobbyScreenView</c>.</summary>
        public static Action<Action<string>> MakeScanRequester(
            Transform modalParent,
            string title = "Scan QR or Enter Code",
            Func<string, bool> payloadValidator = null)
        {
            return onAccepted => ShowScanModal(modalParent, onAccepted, payloadValidator, title);
        }

        /// <summary>Decodes a QR payload from a readable <see cref="Texture2D"/> (such as an encoded QR or camera snapshot).</summary>
        public static bool TryDecodeTexture(Texture2D texture, out string payload)
        {
            payload = "";
            if (texture == null) return false;
            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch
            {
                return false;
            }
            var scanner = new QrScannerBuffer();
            return scanner.TryDecode(pixels, texture.width, texture.height, verticallyFlipped: true, out payload);
        }

        /// <summary>Decodes a QR payload from raw <see cref="Color32"/> pixels.</summary>
        public static bool TryDecodePixels(Color32[] pixels, int width, int height, out string payload, bool verticallyFlipped = true)
        {
            var scanner = new QrScannerBuffer();
            return scanner.TryDecode(pixels, width, height, verticallyFlipped, out payload);
        }

        // =============================================================================================================================
        // UGUI QR Share + Live WebCamTexture Scanner Modal Widget
        // =============================================================================================================================

        public RectTransform Root { get; private set; }
        public InputField ManualInput { get; private set; }
        public Text StatusLabel { get; private set; }
        public string LastScannedPayload { get; private set; } = "";
        public bool IsCameraRunning => _webCam != null && _webCam.isPlaying;

        Action<string> _onAccepted;
        Func<string, bool> _validator;
        Action _onClosed;

        Canvas _canvas;
        bool _hasShare;
        bool _includeScanner;
        bool _built;
        bool _inLayout;
        bool _subscribedViewport;
        string _sharePayloadText = "";

        RectTransform _borderRect;
        RectTransform _cardBody;
        RectTransform _headerRow;
        RectTransform _headerTitleRect;
        RectTransform _closeBtnRect;
        RectTransform _bodyRect;
        RectTransform _footerRow;

        RectTransform _shareCol;
        RectTransform _qrFrame;
        RectTransform _shareCaptionRect;
        RectTransform _shareStatusRect;
        Text _shareStatusLabel;
        RectTransform _copyBtnRect;

        RectTransform _camCol;
        RectTransform _camFrame;
        RawImage _cameraRawImage;
        RectTransform _cameraPreviewRect;
        Text _cameraHintLabel;
        RectTransform _cameraHintRect;
        RectTransform _startCamBtnRect;

        RectTransform _manualCol;
        RectTransform _manualTitleRect;
        RectTransform _manualHelpRect;
        RectTransform _manualInputRect;
        RectTransform _scanStatusRect;
        RectTransform _pasteBtnRect;
        RectTransform _connectBtnRect;

        Image _shareQrImage;
        Texture2D _ownedQrTexture;
        Sprite _ownedQrSprite;
        WebCamTexture _webCam;
        Color32[] _framePixels;
        readonly QrScannerBuffer _scanner = new QrScannerBuffer();

        float _scanCooldown;
        bool _wantedCameraBeforePause;
        bool _waitingForAndroidPermission;

        sealed class ModalScrimDismiss : MonoBehaviour, IPointerClickHandler
        {
            public Action OnDismiss;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData != null &&
                    eventData.pointerCurrentRaycast.gameObject != null &&
                    eventData.pointerCurrentRaycast.gameObject != gameObject)
                {
                    return;
                }
                OnDismiss?.Invoke();
            }
        }

        /// <summary>Opens a modal displaying a crisp scannable QR code plus a one-tap Copy button.</summary>
        public static QrCodeView ShowShareModal(
            Transform parent,
            string title,
            string payload,
            string subtitle = null,
            Action onClosed = null)
        {
            return CreateModal(parent, title, payload, null, null, onClosed, subtitle, "");
        }

        /// <summary>
        /// Opens a modal with a live <see cref="WebCamTexture"/> QR scanner AND a manual <c>ws://</c> / Room Code paste fallback.
        /// </summary>
        public static QrCodeView ShowScanModal(
            Transform parent,
            Action<string> onPayloadAccepted,
            Func<string, bool> payloadValidator = null,
            string title = "Scan QR or Enter Code",
            string initialManualText = "",
            Action onClosed = null)
        {
            return CreateModal(parent, title, null, onPayloadAccepted, payloadValidator, onClosed, null, initialManualText);
        }

        /// <summary>Creates a full UGUI QR modal supporting Share QR, Live Camera Scan, or both side-by-side.</summary>
        public static QrCodeView CreateModal(
            Transform parent,
            string title,
            string sharePayload,
            Action<string> onPayloadAccepted,
            Func<string, bool> payloadValidator = null,
            Action onClosed = null,
            string shareSubtitle = null,
            string initialManualText = "")
        {
            Ui.EnsureEventSystem();
            RectTransform backdrop = Ui.Fill("QrCodeModal", parent);

            RectTransform scrim = Ui.Fill("Scrim", backdrop);
            Image dim = scrim.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.06f, 0.84f);
            dim.raycastTarget = true;
            var scrimDismiss = scrim.gameObject.AddComponent<ModalScrimDismiss>();

            RectTransform safe = Ui.Fill("Safe", backdrop);
            safe.gameObject.AddComponent<SafeArea>();

            var view = backdrop.gameObject.AddComponent<QrCodeView>();
            view.Root = backdrop;
            view._canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
            view._onAccepted = onPayloadAccepted;
            view._validator = payloadValidator;
            view._onClosed = onClosed;
            scrimDismiss.OnDismiss = view.Close;

            view.BuildContents(safe, title, sharePayload, shareSubtitle, onPayloadAccepted != null, initialManualText);
            view.SubscribeViewport();
            view.RefreshLayout();
            return view;
        }

        void BuildContents(
            RectTransform safeParent,
            string title,
            string sharePayload,
            string shareSubtitle,
            bool includeScanner,
            string initialManualText)
        {
            _hasShare = !string.IsNullOrEmpty(sharePayload);
            _includeScanner = includeScanner;
            _sharePayloadText = sharePayload ?? "";

            _borderRect = Ui.Panel(
                "CardBorder",
                safeParent,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(960f, 620f));
            Image borderImg = _borderRect.gameObject.AddComponent<Image>();
            ApplySlicedPlate(borderImg, CardBorder);
            borderImg.raycastTarget = true;

            _cardBody = Ui.Panel(
                "CardBody",
                _borderRect,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-8f, -8f));
            Image cardImg = _cardBody.gameObject.AddComponent<Image>();
            ApplySlicedPlate(cardImg, CardBg);
            cardImg.raycastTarget = true;

            // Band 1: Dedicated Header Row
            _headerRow = Ui.Panel(
                "HeaderRow",
                _cardBody,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 112f));

            Image hdrRule = Ui.MakeImage("HeaderDivider", _headerRow, new Color(1f, 1f, 1f, 0.10f));
            hdrRule.raycastTarget = false;
            RectTransform hdrRuleRt = hdrRule.rectTransform;
            hdrRuleRt.anchorMin = new Vector2(0f, 0f);
            hdrRuleRt.anchorMax = new Vector2(1f, 0f);
            hdrRuleRt.pivot = new Vector2(0.5f, 0f);
            hdrRuleRt.offsetMin = new Vector2(16f, 0f);
            hdrRuleRt.offsetMax = new Vector2(-16f, 2f);

            Text headerLabel = Ui.MakeText(
                "Title",
                _headerRow,
                string.IsNullOrEmpty(title) ? "QR Room Link" : title,
                26,
                Ui.Cream,
                TextAnchor.MiddleLeft,
                KalyeahResources.DisplayFont());
            headerLabel.resizeTextForBestFit = true;
            headerLabel.resizeTextMinSize = 18;
            headerLabel.resizeTextMaxSize = 28;
            headerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            headerLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(headerLabel, Ui.Cream);
            _headerTitleRect = headerLabel.rectTransform;

            Button closeBtn = MakeButton(
                "CloseButton",
                _headerRow,
                "CLOSE",
                ButtonSlate,
                Ui.Cream,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(132f, 96f),
                Close);
            _closeBtnRect = closeBtn.GetComponent<RectTransform>();

            // Band 2: Middle Body
            _bodyRect = Ui.Panel(
                "Body",
                _cardBody,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            // Band 3: Dedicated Action Footer Row
            _footerRow = Ui.Panel(
                "FooterRow",
                _cardBody,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(0f, 116f));

            Image ftrRule = Ui.MakeImage("FooterDivider", _footerRow, new Color(1f, 1f, 1f, 0.10f));
            ftrRule.raycastTarget = false;
            RectTransform ftrRuleRt = ftrRule.rectTransform;
            ftrRuleRt.anchorMin = new Vector2(0f, 1f);
            ftrRuleRt.anchorMax = new Vector2(1f, 1f);
            ftrRuleRt.pivot = new Vector2(0.5f, 1f);
            ftrRuleRt.offsetMin = new Vector2(16f, -2f);
            ftrRuleRt.offsetMax = new Vector2(-16f, 0f);

            if (_hasShare)
                BuildShareColumn(_bodyRect, _footerRow, sharePayload, shareSubtitle);

            if (_includeScanner)
                BuildScannerAndManualColumns(_bodyRect, _footerRow, initialManualText);

            _built = true;
        }

        void BuildShareColumn(Transform bodyParent, Transform footerParent, string payload, string subtitle)
        {
            _shareCol = Ui.Panel("ShareColumn", bodyParent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            _qrFrame = Ui.Panel(
                "QrFrame",
                _shareCol,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(240f, 240f));
            Image qrBg = _qrFrame.gameObject.AddComponent<Image>();
            ApplySlicedPlate(qrBg, Color.white);
            qrBg.raycastTarget = false;

            _shareQrImage = Ui.MakeImage("QrMatrix", _qrFrame, Color.white);
            _shareQrImage.raycastTarget = false;
            RectTransform qrImgRect = _shareQrImage.rectTransform;
            qrImgRect.anchorMin = Vector2.zero;
            qrImgRect.anchorMax = Vector2.one;
            qrImgRect.offsetMin = new Vector2(10f, 10f);
            qrImgRect.offsetMax = new Vector2(-10f, -10f);
            _shareQrImage.preserveAspect = true;
            SetSharePayload(payload);

            string caption = !string.IsNullOrEmpty(subtitle) ? subtitle : payload;
            Text codeText = Ui.MakeText("ShareCaption", _shareCol, caption, 19, MutedCream, TextAnchor.MiddleCenter);
            codeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            codeText.verticalOverflow = VerticalWrapMode.Truncate;
            codeText.resizeTextForBestFit = true;
            codeText.resizeTextMinSize = 13;
            codeText.resizeTextMaxSize = 20;
            SyncOutlineForColor(codeText, MutedCream);
            _shareCaptionRect = codeText.rectTransform;

            _shareStatusLabel = Ui.MakeText("ShareStatus", _shareCol, "", 18, CardBorder, TextAnchor.MiddleCenter);
            _shareStatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _shareStatusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _shareStatusLabel.resizeTextForBestFit = true;
            _shareStatusLabel.resizeTextMinSize = 13;
            _shareStatusLabel.resizeTextMaxSize = 18;
            SyncOutlineForColor(_shareStatusLabel, CardBorder);
            _shareStatusRect = _shareStatusLabel.rectTransform;
            if (!_includeScanner)
                StatusLabel = _shareStatusLabel;

            string copyLabel = _includeScanner ? "Copy Link" : "Copy Link / Code";
            Button copyBtn = MakeButton(
                "CopyPayloadButton",
                footerParent,
                copyLabel,
                ButtonBlue,
                Ui.Cream,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(240f, 96f),
                () =>
                {
                    GUIUtility.systemCopyBuffer = _sharePayloadText ?? "";
                    Haptics.LightTap();
                    if (_shareStatusLabel != null)
                        _shareStatusLabel.text = "Copied to clipboard!";
                });
            _copyBtnRect = copyBtn.GetComponent<RectTransform>();
        }

        void BuildScannerAndManualColumns(Transform bodyParent, Transform footerParent, string initialManualText)
        {
            // Camera column
            _camCol = Ui.Panel("CameraColumn", bodyParent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            _camFrame = Ui.Panel(
                "CameraFrame",
                _camCol,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -4f),
                new Vector2(240f, 240f));
            Image camFrameBg = _camFrame.gameObject.AddComponent<Image>();
            ApplySlicedPlate(camFrameBg, SurfaceDark);
            camFrameBg.raycastTarget = false;

            var rawGo = new GameObject("CameraPreview", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(_camFrame, false);
            _cameraPreviewRect = rawGo.GetComponent<RectTransform>();
            _cameraPreviewRect.anchorMin = Vector2.zero;
            _cameraPreviewRect.anchorMax = Vector2.one;
            _cameraPreviewRect.offsetMin = new Vector2(6f, 6f);
            _cameraPreviewRect.offsetMax = new Vector2(-6f, -6f);
            _cameraRawImage = rawGo.GetComponent<RawImage>();
            _cameraRawImage.color = new Color(0.14f, 0.18f, 0.28f, 1f);
            _cameraRawImage.raycastTarget = false;

            _cameraHintLabel = Ui.MakeText(
                "CameraHint",
                _camCol,
                "Point camera at host QR. Nothing is saved.",
                18,
                MutedCream,
                TextAnchor.UpperCenter);
            _cameraHintLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cameraHintLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _cameraHintLabel.resizeTextForBestFit = true;
            _cameraHintLabel.resizeTextMinSize = 13;
            _cameraHintLabel.resizeTextMaxSize = 18;
            SyncOutlineForColor(_cameraHintLabel, MutedCream);
            _cameraHintRect = _cameraHintLabel.rectTransform;

            string camBtnLabel = _hasShare ? "Start Camera" : "Start / Retry Camera";
            Button camBtn = MakeButton(
                "StartCameraButton",
                footerParent,
                camBtnLabel,
                ButtonBlue,
                Ui.Cream,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(240f, 96f),
                StartCamera);
            _startCamBtnRect = camBtn.GetComponent<RectTransform>();

            // Manual column
            _manualCol = Ui.Panel("ManualColumn", bodyParent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            Text manualTitle = Ui.MakeText(
                "ManualHeader",
                _manualCol,
                "Or Paste Link / Room Code",
                21,
                Ui.Cream,
                TextAnchor.MiddleLeft,
                KalyeahResources.DisplayFont());
            manualTitle.resizeTextForBestFit = true;
            manualTitle.resizeTextMinSize = 14;
            manualTitle.resizeTextMaxSize = 22;
            manualTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            manualTitle.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(manualTitle, Ui.Cream);
            _manualTitleRect = manualTitle.rectTransform;

            Text manualDesc = Ui.MakeText(
                "ManualHelp",
                _manualCol,
                "No camera needed: paste ws://192.168.43.1:7777, an 8-letter Room Code, or WIFI: text.",
                18,
                MutedCream,
                TextAnchor.UpperLeft);
            manualDesc.resizeTextForBestFit = true;
            manualDesc.resizeTextMinSize = 13;
            manualDesc.resizeTextMaxSize = 18;
            manualDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            manualDesc.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(manualDesc, MutedCream);
            _manualHelpRect = manualDesc.rectTransform;

            ManualInput = MakeInputField(
                "ManualCodeInput",
                _manualCol,
                initialManualText ?? "",
                "ws://192.168.43.1:7777 or ABCD2345",
                _canvas);
            _manualInputRect = ManualInput.GetComponent<RectTransform>();

            StatusLabel = Ui.MakeText("ScanStatus", _manualCol, "", 18, CardBorder, TextAnchor.UpperLeft);
            StatusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            StatusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            StatusLabel.resizeTextForBestFit = true;
            StatusLabel.resizeTextMinSize = 13;
            StatusLabel.resizeTextMaxSize = 18;
            SyncOutlineForColor(StatusLabel, CardBorder);
            _scanStatusRect = StatusLabel.rectTransform;

            Button pasteBtn = MakeButton(
                "PasteClipboardButton",
                footerParent,
                "Paste",
                ButtonSlate,
                Ui.Cream,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(180f, 96f),
                PasteFromClipboard);
            _pasteBtnRect = pasteBtn.GetComponent<RectTransform>();

            Button connectBtn = MakeButton(
                "UseManualCodeButton",
                footerParent,
                "Connect",
                ButtonOrange,
                Ui.Cream,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(180f, 96f),
                SubmitManualInput);
            _connectBtnRect = connectBtn.GetComponent<RectTransform>();

            if (Application.isPlaying)
                StartCamera();
            else
                SetCameraMessage("Camera preview runs on device. Manual input is ready.");
        }

        /// <summary>Reflows modal card, columns, QR/camera frames, and >= 48 dp action footer across 21:9, 16:9, notched, and 4:3 viewports.</summary>
        public void RefreshLayout()
        {
            if (!_built || _inLayout || _borderRect == null) return;
            _inLayout = true;
            try
            {
                if (_canvas == null && Root != null)
                    _canvas = Root.GetComponentInParent<Canvas>();

                Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
                float minTouch = ScreenFitMetrics.ClampTouchSize(48f, ScreenFitMetrics.MinTouchDp, _canvas);
                float gutter = Mathf.Max(14f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));

                float targetMaxW = (_hasShare && _includeScanner)
                    ? 1160f
                    : (_includeScanner ? 920f : 560f);
                float cardBorderW = Mathf.Clamp(safe.x - 24f, Mathf.Min(safe.x - 12f, 480f), targetMaxW);
                float cardBorderH = Mathf.Clamp(safe.y - 20f, Mathf.Min(safe.y - 12f, 500f), 656f);
                _borderRect.sizeDelta = new Vector2(cardBorderW, cardBorderH);

                float cardW = Mathf.Max(320f, cardBorderW - 8f);
                float cardH = Mathf.Max(320f, cardBorderH - 8f);
                float padX = 18f;
                float innerW = Mathf.Max(260f, cardW - padX * 2f);

                float headerH = Mathf.Max(104f, minTouch + 16f);
                float footerH = Mathf.Max(108f, minTouch + 20f);
                _headerRow.sizeDelta = new Vector2(0f, headerH);
                _footerRow.sizeDelta = new Vector2(0f, footerH);

                float closeW = Mathf.Max(minTouch, 132f);
                if (_closeBtnRect != null)
                {
                    _closeBtnRect.anchorMin = new Vector2(1f, 0.5f);
                    _closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
                    _closeBtnRect.pivot = new Vector2(1f, 0.5f);
                    _closeBtnRect.anchoredPosition = new Vector2(-padX, 0f);
                    _closeBtnRect.sizeDelta = new Vector2(closeW, minTouch);
                }
                if (_headerTitleRect != null)
                {
                    _headerTitleRect.anchorMin = new Vector2(0f, 0f);
                    _headerTitleRect.anchorMax = new Vector2(1f, 1f);
                    _headerTitleRect.pivot = new Vector2(0.5f, 0.5f);
                    _headerTitleRect.offsetMin = new Vector2(padX + 2f, 8f);
                    _headerTitleRect.offsetMax = new Vector2(-(padX + closeW + gutter), -8f);
                }

                _bodyRect.anchorMin = Vector2.zero;
                _bodyRect.anchorMax = Vector2.one;
                _bodyRect.offsetMin = new Vector2(padX, footerH + 8f);
                _bodyRect.offsetMax = new Vector2(-padX, -(headerH + 8f));
                float bodyH = Mathf.Max(180f, cardH - headerH - footerH - 16f);

                if (_hasShare && !_includeScanner)
                {
                    LayoutShareColumn(0f, innerW, bodyH, padX, minTouch);
                }
                else if (!_hasShare && _includeScanner)
                {
                    float avail = Mathf.Max(200f, innerW - gutter);
                    float manW = Mathf.Max(minTouch * 2f + gutter, avail * 0.54f);
                    float camW = Mathf.Max(minTouch, avail - manW);
                    manW = Mathf.Max(minTouch * 2f + gutter, avail - camW);
                    LayoutCameraColumn(0f, camW, bodyH, padX, minTouch);
                    LayoutManualColumn(camW + gutter, manW, bodyH, padX, minTouch, gutter);
                }
                else if (_hasShare && _includeScanner)
                {
                    float avail = Mathf.Max(260f, innerW - gutter * 2f);
                    float minManW = minTouch * 2f + gutter + 16f;
                    float manW = Mathf.Max(minManW, avail * 0.38f);
                    float rem = Mathf.Max(minTouch * 2f, avail - manW);
                    float shareW = rem * 0.5f;
                    float camW = rem - shareW;
                    LayoutShareColumn(0f, shareW, bodyH, padX, minTouch);
                    LayoutCameraColumn(shareW + gutter, camW, bodyH, padX, minTouch);
                    LayoutManualColumn(shareW + gutter + camW + gutter, manW, bodyH, padX, minTouch, gutter);
                }
            }
            finally
            {
                _inLayout = false;
            }
        }

        void LayoutShareColumn(float colX, float colW, float bodyH, float footerPadX, float minTouch)
        {
            if (_shareCol == null) return;
            _shareCol.anchorMin = new Vector2(0f, 0f);
            _shareCol.anchorMax = new Vector2(0f, 1f);
            _shareCol.pivot = new Vector2(0f, 0.5f);
            _shareCol.offsetMin = new Vector2(colX, 0f);
            _shareCol.offsetMax = new Vector2(colX + colW, 0f);

            float qrSize = Mathf.Clamp(Mathf.Min(colW - 8f, bodyH - 116f), 136f, 252f);
            if (_qrFrame != null)
            {
                _qrFrame.anchorMin = new Vector2(0.5f, 1f);
                _qrFrame.anchorMax = new Vector2(0.5f, 1f);
                _qrFrame.pivot = new Vector2(0.5f, 1f);
                _qrFrame.anchoredPosition = new Vector2(0f, -4f);
                _qrFrame.sizeDelta = new Vector2(qrSize, qrSize);
            }

            float capTop = qrSize + 8f;
            float capH = Mathf.Clamp(bodyH - capTop - 36f, 68f, 92f);
            if (_shareCaptionRect != null)
            {
                _shareCaptionRect.anchorMin = new Vector2(0f, 1f);
                _shareCaptionRect.anchorMax = new Vector2(1f, 1f);
                _shareCaptionRect.pivot = new Vector2(0.5f, 1f);
                _shareCaptionRect.offsetMin = new Vector2(4f, -(capTop + capH));
                _shareCaptionRect.offsetMax = new Vector2(-4f, -capTop);
            }
            if (_shareStatusRect != null)
            {
                float stTop = capTop + capH + 4f;
                float stH = Mathf.Max(26f, bodyH - stTop - 2f);
                _shareStatusRect.anchorMin = new Vector2(0f, 1f);
                _shareStatusRect.anchorMax = new Vector2(1f, 1f);
                _shareStatusRect.pivot = new Vector2(0.5f, 1f);
                _shareStatusRect.offsetMin = new Vector2(4f, -(stTop + stH));
                _shareStatusRect.offsetMax = new Vector2(-4f, -stTop);
            }
            if (_copyBtnRect != null)
            {
                float btnW = Mathf.Clamp(colW, minTouch, 310f);
                float btnX = footerPadX + colX + (colW - btnW) * 0.5f;
                _copyBtnRect.anchorMin = new Vector2(0f, 0.5f);
                _copyBtnRect.anchorMax = new Vector2(0f, 0.5f);
                _copyBtnRect.pivot = new Vector2(0f, 0.5f);
                _copyBtnRect.anchoredPosition = new Vector2(btnX, 0f);
                _copyBtnRect.sizeDelta = new Vector2(btnW, minTouch);
            }
        }

        void LayoutCameraColumn(float colX, float colW, float bodyH, float footerPadX, float minTouch)
        {
            if (_camCol == null) return;
            _camCol.anchorMin = new Vector2(0f, 0f);
            _camCol.anchorMax = new Vector2(0f, 1f);
            _camCol.pivot = new Vector2(0f, 0.5f);
            _camCol.offsetMin = new Vector2(colX, 0f);
            _camCol.offsetMax = new Vector2(colX + colW, 0f);

            float camSize = Mathf.Clamp(Mathf.Min(colW - 8f, bodyH - 96f), 136f, 256f);
            if (_camFrame != null)
            {
                _camFrame.anchorMin = new Vector2(0.5f, 1f);
                _camFrame.anchorMax = new Vector2(0.5f, 1f);
                _camFrame.pivot = new Vector2(0.5f, 1f);
                _camFrame.anchoredPosition = new Vector2(0f, -4f);
                _camFrame.sizeDelta = new Vector2(camSize, camSize);
            }
            if (_cameraHintRect != null)
            {
                float hintTop = camSize + 10f;
                _cameraHintRect.anchorMin = new Vector2(0f, 0f);
                _cameraHintRect.anchorMax = new Vector2(1f, 1f);
                _cameraHintRect.pivot = new Vector2(0.5f, 1f);
                _cameraHintRect.offsetMin = new Vector2(4f, 4f);
                _cameraHintRect.offsetMax = new Vector2(-4f, -hintTop);
            }
            if (_startCamBtnRect != null)
            {
                float btnW = Mathf.Clamp(colW, minTouch, 310f);
                float btnX = footerPadX + colX + (colW - btnW) * 0.5f;
                _startCamBtnRect.anchorMin = new Vector2(0f, 0.5f);
                _startCamBtnRect.anchorMax = new Vector2(0f, 0.5f);
                _startCamBtnRect.pivot = new Vector2(0f, 0.5f);
                _startCamBtnRect.anchoredPosition = new Vector2(btnX, 0f);
                _startCamBtnRect.sizeDelta = new Vector2(btnW, minTouch);
            }
        }

        void LayoutManualColumn(float colX, float colW, float bodyH, float footerPadX, float minTouch, float gutter)
        {
            if (_manualCol == null) return;
            _manualCol.anchorMin = new Vector2(0f, 0f);
            _manualCol.anchorMax = new Vector2(0f, 1f);
            _manualCol.pivot = new Vector2(0f, 0.5f);
            _manualCol.offsetMin = new Vector2(colX, 0f);
            _manualCol.offsetMax = new Vector2(colX + colW, 0f);

            float y = 4f;
            if (_manualTitleRect != null)
            {
                _manualTitleRect.anchorMin = new Vector2(0f, 1f);
                _manualTitleRect.anchorMax = new Vector2(1f, 1f);
                _manualTitleRect.pivot = new Vector2(0.5f, 1f);
                _manualTitleRect.offsetMin = new Vector2(0f, -(y + 34f));
                _manualTitleRect.offsetMax = new Vector2(0f, -y);
                y += 38f;
            }
            if (_manualHelpRect != null)
            {
                float helpH = Mathf.Clamp(bodyH * 0.22f, 76f, 92f);
                _manualHelpRect.anchorMin = new Vector2(0f, 1f);
                _manualHelpRect.anchorMax = new Vector2(1f, 1f);
                _manualHelpRect.pivot = new Vector2(0.5f, 1f);
                _manualHelpRect.offsetMin = new Vector2(0f, -(y + helpH));
                _manualHelpRect.offsetMax = new Vector2(0f, -y);
                y += helpH + 8f;
            }
            if (_manualInputRect != null)
            {
                _manualInputRect.anchorMin = new Vector2(0f, 1f);
                _manualInputRect.anchorMax = new Vector2(1f, 1f);
                _manualInputRect.pivot = new Vector2(0.5f, 1f);
                _manualInputRect.offsetMin = new Vector2(0f, -(y + minTouch));
                _manualInputRect.offsetMax = new Vector2(0f, -y);
                y += minTouch + 10f;
            }
            if (_scanStatusRect != null)
            {
                _scanStatusRect.anchorMin = new Vector2(0f, 0f);
                _scanStatusRect.anchorMax = new Vector2(1f, 1f);
                _scanStatusRect.pivot = new Vector2(0.5f, 1f);
                _scanStatusRect.offsetMin = new Vector2(0f, 4f);
                _scanStatusRect.offsetMax = new Vector2(0f, -y);
            }

            float pairW = Mathf.Max(minTouch, (colW - gutter) * 0.5f);
            float baseFooterX = footerPadX + colX;
            if (_pasteBtnRect != null)
            {
                _pasteBtnRect.anchorMin = new Vector2(0f, 0.5f);
                _pasteBtnRect.anchorMax = new Vector2(0f, 0.5f);
                _pasteBtnRect.pivot = new Vector2(0f, 0.5f);
                _pasteBtnRect.anchoredPosition = new Vector2(baseFooterX, 0f);
                _pasteBtnRect.sizeDelta = new Vector2(pairW, minTouch);
            }
            if (_connectBtnRect != null)
            {
                _connectBtnRect.anchorMin = new Vector2(0f, 0.5f);
                _connectBtnRect.anchorMax = new Vector2(0f, 0.5f);
                _connectBtnRect.pivot = new Vector2(0f, 0.5f);
                _connectBtnRect.anchoredPosition = new Vector2(baseFooterX + pairW + gutter, 0f);
                _connectBtnRect.sizeDelta = new Vector2(pairW, minTouch);
            }
        }

        /// <summary>Updates the displayed QR sprite in Share mode without leaking previous textures.</summary>
        public void SetSharePayload(string payload, int scale = 6, int quietZone = 2)
        {
            _sharePayloadText = payload ?? "";
            ReleaseOwnedQrAssets();
            _ownedQrTexture = EncodeTexture(_sharePayloadText, scale, quietZone);
            if (_ownedQrTexture != null)
            {
                _ownedQrSprite = Sprite.Create(
                    _ownedQrTexture,
                    new Rect(0f, 0f, _ownedQrTexture.width, _ownedQrTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            if (_shareQrImage != null)
                _shareQrImage.sprite = _ownedQrSprite;
        }

        /// <summary>Starts the live back-preferred <see cref="WebCamTexture"/> preview or surfaces a clear manual fallback message.</summary>
        public void StartCamera()
        {
            StopCamera();
            if (_cameraRawImage == null) return;

            if (!AndroidLocalLink.HasCameraPermission())
            {
                _waitingForAndroidPermission = true;
                SetCameraMessage("Allow camera access to scan QR codes, or paste the code on the right.");
                AndroidLocalLink.RequestCameraPermission();
                return;
            }

            _waitingForAndroidPermission = false;
            WebCamDevice[] devices;
            try
            {
                devices = WebCamTexture.devices;
            }
            catch
            {
                devices = Array.Empty<WebCamDevice>();
            }

            if (devices == null || devices.Length == 0)
            {
                SetCameraMessage("No camera detected on this device. Paste ws:// or Room Code on the right.");
                return;
            }

            string chosenDevice = devices[0].name;
            for (int i = 0; i < devices.Length; i++)
            {
                if (!devices[i].isFrontFacing)
                {
                    chosenDevice = devices[i].name;
                    break;
                }
            }

            try
            {
                _webCam = new WebCamTexture(chosenDevice, 640, 480, 15);
                _webCam.Play();
                _cameraRawImage.texture = _webCam;
                _cameraRawImage.color = Color.white;
                _scanCooldown = ScanIntervalSeconds;
                SetCameraMessage("Fit the host's QR code inside the square.");
            }
            catch
            {
                StopCamera();
                SetCameraMessage("Camera unavailable right now. Paste ws:// or Room Code on the right.");
            }
        }

        public void StopCamera()
        {
            if (_webCam != null)
            {
                try
                {
                    if (_webCam.isPlaying) _webCam.Stop();
                }
                catch
                {
                    // Ignore native camera teardown errors on backgrounding.
                }
                DestroySafe(_webCam);
                _webCam = null;
            }
            if (_cameraRawImage != null)
            {
                _cameraRawImage.texture = null;
                _cameraRawImage.color = new Color(0.14f, 0.18f, 0.28f, 1f);
            }
            if (_cameraPreviewRect != null)
            {
                _cameraPreviewRect.localEulerAngles = Vector3.zero;
                _cameraPreviewRect.localScale = Vector3.one;
            }
        }

        public void PasteFromClipboard()
        {
            string clip = GUIUtility.systemCopyBuffer ?? "";
            if (ManualInput != null)
                ManualInput.text = clip.Trim();
            Haptics.LightTap();
        }

        public void SubmitManualInput()
        {
            string raw = ManualInput != null ? ManualInput.text : "";
            TryAcceptCandidate(raw, fromManualEntry: true);
        }

        public bool TryAcceptCandidate(string candidate, bool fromManualEntry = false)
        {
            string clean = (candidate ?? "").Trim();
            if (clean.Length == 0)
            {
                SetStatus("Enter or paste a ws:// URL, 8-letter Room Code, or WIFI: QR text first.", isError: true);
                Haptics.CancelTick();
                return false;
            }

            bool valid = _validator != null
                ? _validator(clean)
                : AndroidLocalLink.TryClassifyJoinPayload(clean, out _, out _, out _);

            if (!valid)
            {
                SetStatus(
                    fromManualEntry
                        ? "Unrecognized code or URL. Use an 8-letter Room Code or ws://<ip>:8080."
                        : "Scanned QR isn't a Kalyeah room or hotspot.",
                    isError: true);
                if (fromManualEntry) Haptics.CancelTick();
                return false;
            }

            LastScannedPayload = clean;
            Haptics.LightTap();
            Action<string> callback = _onAccepted;
            Close();
            callback?.Invoke(clean);
            return true;
        }

        public void Close()
        {
            _waitingForAndroidPermission = false;
            _wantedCameraBeforePause = false;
            UnsubscribeViewport();
            StopCamera();
            ReleaseOwnedQrAssets();
            Action closed = _onClosed;
            _onClosed = null;
            if (Root != null)
            {
                GameObject go = Root.gameObject;
                Root = null;
                DestroySafe(go);
            }
            closed?.Invoke();
        }

        void OnEnable()
        {
            SubscribeViewport();
            RefreshLayout();
        }

        void OnRectTransformDimensionsChange()
        {
            RefreshLayout();
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (_waitingForAndroidPermission && AndroidLocalLink.HasCameraPermission())
            {
                _waitingForAndroidPermission = false;
                StartCamera();
                return;
            }

            if (_webCam == null || !_webCam.isPlaying) return;

            if (_cameraPreviewRect != null)
            {
                float angle = -_webCam.videoRotationAngle;
                _cameraPreviewRect.localEulerAngles = new Vector3(0f, 0f, angle);
                float scaleY = _webCam.videoVerticallyMirrored ? -1f : 1f;
                _cameraPreviewRect.localScale = new Vector3(1f, scaleY, 1f);
            }

            _scanCooldown -= Time.unscaledDeltaTime;
            if (_scanCooldown > 0f) return;
            _scanCooldown = ScanIntervalSeconds;

            int w = _webCam.width;
            int h = _webCam.height;
            if (w < 32 || h < 32 || !_webCam.didUpdateThisFrame) return;

            int total = w * h;
            if (_framePixels == null || _framePixels.Length != total)
                _framePixels = new Color32[total];

            _webCam.GetPixels32(_framePixels);
            if (_scanner.TryDecode(_framePixels, w, h, verticallyFlipped: !_webCam.videoVerticallyMirrored, out string decoded) &&
                !string.IsNullOrEmpty(decoded))
            {
                TryAcceptCandidate(decoded, fromManualEntry: false);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _wantedCameraBeforePause = IsCameraRunning;
                if (_wantedCameraBeforePause)
                    StopCamera();
            }
            else if (_wantedCameraBeforePause && _cameraRawImage != null)
            {
                _wantedCameraBeforePause = false;
                StartCamera();
            }
        }

        void OnDisable()
        {
            UnsubscribeViewport();
            StopCamera();
        }

        void OnDestroy()
        {
            UnsubscribeViewport();
            StopCamera();
            ReleaseOwnedQrAssets();
        }

        void SubscribeViewport()
        {
            if (_subscribedViewport) return;
            ScreenFitMetrics.SimulatedViewportChanged += OnSimulatedViewportChanged;
            _subscribedViewport = true;
        }

        void UnsubscribeViewport()
        {
            if (!_subscribedViewport) return;
            ScreenFitMetrics.SimulatedViewportChanged -= OnSimulatedViewportChanged;
            _subscribedViewport = false;
        }

        void OnSimulatedViewportChanged() => RefreshLayout();

        void SetCameraMessage(string text)
        {
            if (_cameraHintLabel != null)
                _cameraHintLabel.text = text ?? "";
        }

        void SetStatus(string text, bool isError)
        {
            if (StatusLabel == null) return;
            Color c = isError ? ErrorCoral : CardBorder;
            StatusLabel.color = c;
            SyncOutlineForColor(StatusLabel, c);
            StatusLabel.text = text ?? "";
        }

        void ReleaseOwnedQrAssets()
        {
            if (_shareQrImage != null)
                _shareQrImage.sprite = null;
            if (_ownedQrSprite != null)
            {
                DestroySafe(_ownedQrSprite);
                _ownedQrSprite = null;
            }
            if (_ownedQrTexture != null)
            {
                DestroySafe(_ownedQrTexture);
                _ownedQrTexture = null;
            }
        }

        static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        static void ApplySlicedPlate(Image img, Color color)
        {
            if (img == null) return;
            Sprite rounded = MenuStack.RoundedSprite();
            if (rounded != null)
            {
                img.sprite = rounded;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
        }

        static void SyncOutlineForColor(Text label, Color c)
        {
            if (label == null) return;
            Outline ol = label.GetComponent<Outline>();
            if (ol != null)
                ol.enabled = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) >= 0.25f;
        }

        Button MakeButton(
            string name,
            Transform parent,
            string label,
            Color bgColor,
            Color textColor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            Action onClick)
        {
            Vector2 safeSize = anchorMin.x == anchorMax.x
                ? ScreenFitMetrics.ClampTouchSize(size, ScreenFitMetrics.MinTouchDp, _canvas)
                : new Vector2(size.x, ScreenFitMetrics.ClampTouchSize(size.y, ScreenFitMetrics.MinTouchDp, _canvas));

            Vector2 pivot = new Vector2(
                anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f,
                anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);

            RectTransform rect = Ui.Panel(name, parent, anchorMin, anchorMax, pivot, anchoredPos, safeSize);
            Image bg = rect.gameObject.AddComponent<Image>();
            ApplySlicedPlate(bg, bgColor);
            bg.raycastTarget = true;

            Button btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.55f);
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Text txt = Ui.MakeText("Label", rect, label, 20, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.raycastTarget = false;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 14;
            txt.resizeTextMaxSize = 21;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(txt, textColor);
            RectTransform tr = txt.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12f, 4f);
            tr.offsetMax = new Vector2(-12f, -4f);
            return btn;
        }

        static InputField MakeInputField(
            string name,
            Transform parent,
            string initialValue,
            string placeholderText,
            Canvas canvas)
        {
            float touchH = ScreenFitMetrics.ClampTouchSize(56f, ScreenFitMetrics.MinTouchDp, canvas);
            RectTransform rect = Ui.Panel(
                name,
                parent,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, touchH));
            Image bg = rect.gameObject.AddComponent<Image>();
            ApplySlicedPlate(bg, SurfaceDark);
            bg.raycastTarget = true;

            Text placeholder = Ui.MakeText("Placeholder", rect, placeholderText, 18, new Color(0.64f, 0.72f, 0.86f, 0.88f), TextAnchor.MiddleLeft);
            placeholder.raycastTarget = false;
            placeholder.resizeTextForBestFit = true;
            placeholder.resizeTextMinSize = 13;
            placeholder.resizeTextMaxSize = 18;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(placeholder, placeholder.color);
            RectTransform phRect = placeholder.rectTransform;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(14f, 6f);
            phRect.offsetMax = new Vector2(-14f, -6f);

            Text valueText = Ui.MakeText("Text", rect, initialValue ?? "", 20, Ui.Cream, TextAnchor.MiddleLeft);
            valueText.raycastTarget = false;
            valueText.supportRichText = false;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            SyncOutlineForColor(valueText, Ui.Cream);
            RectTransform vtRect = valueText.rectTransform;
            vtRect.anchorMin = Vector2.zero;
            vtRect.anchorMax = Vector2.one;
            vtRect.offsetMin = new Vector2(14f, 6f);
            vtRect.offsetMax = new Vector2(-14f, -6f);

            InputField field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = valueText;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.text = initialValue ?? "";
            return field;
        }

        // =============================================================================================================================
        // ISO/IEC 18004 Pure-C# Byte-Mode Encoder & GF(256) Reed-Solomon Tables (Versions 1..10)
        // =============================================================================================================================

        static class QrIsoCodec
        {
            public const int ModeNumeric = 1;
            public const int ModeAlphanumeric = 2;
            public const int ModeByte = 4;
            public const string AlphanumericTable = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

            static readonly int[] EccFormatBits = { 1, 0, 3, 2 }; // L, M, Q, H

            // Per version (1..10) and ECC level (L, M, Q, H): (eccCodewordsPerBlock, dataCodewordsPerBlock[])
            public static readonly (int EccPerBlock, int[] Blocks)[][] VersionBlocks =
            {
                null,
                // v1 (21x21)
                new[] { (7, new[] { 19 }), (10, new[] { 16 }), (13, new[] { 13 }), (17, new[] { 9 }) },
                // v2 (25x25)
                new[] { (10, new[] { 34 }), (16, new[] { 28 }), (22, new[] { 22 }), (28, new[] { 16 }) },
                // v3 (29x29)
                new[] { (15, new[] { 55 }), (26, new[] { 44 }), (18, new[] { 17, 17 }), (22, new[] { 13, 13 }) },
                // v4 (33x33)
                new[] { (20, new[] { 80 }), (18, new[] { 32, 32 }), (26, new[] { 24, 24 }), (16, new[] { 9, 9, 9, 9 }) },
                // v5 (37x37)
                new[] { (26, new[] { 108 }), (24, new[] { 43, 43 }), (18, new[] { 15, 15, 16, 16 }), (22, new[] { 11, 11, 12, 12 }) },
                // v6 (41x41)
                new[] { (18, new[] { 68, 68 }), (16, new[] { 27, 27, 27, 27 }), (24, new[] { 19, 19, 19, 19 }), (28, new[] { 15, 15, 15, 15 }) },
                // v7 (45x45)
                new[] { (20, new[] { 78, 78 }), (18, new[] { 31, 31, 31, 31 }), (18, new[] { 14, 14, 15, 15, 15, 15 }), (26, new[] { 13, 13, 13, 13, 14 }) },
                // v8 (49x49)
                new[] { (24, new[] { 97, 97 }), (22, new[] { 38, 38, 39, 39 }), (22, new[] { 18, 18, 18, 18, 19, 19 }), (26, new[] { 14, 14, 14, 14, 15, 15 }) },
                // v9 (53x53)
                new[] { (30, new[] { 116, 116 }), (22, new[] { 36, 36, 36, 37, 37 }), (20, new[] { 16, 16, 16, 16, 17, 17, 17, 17 }), (24, new[] { 12, 12, 12, 12, 13, 13, 13, 13 }) },
                // v10 (57x57)
                new[] { (18, new[] { 68, 68, 69, 69 }), (26, new[] { 43, 43, 43, 43, 44 }), (24, new[] { 19, 19, 19, 19, 19, 19, 20, 20 }), (28, new[] { 15, 15, 15, 15, 15, 15, 16, 16 }) },
            };

            static readonly int[][] AlignmentTracks =
            {
                Array.Empty<int>(),
                Array.Empty<int>(),
                new[] { 6, 18 },
                new[] { 6, 22 },
                new[] { 6, 26 },
                new[] { 6, 30 },
                new[] { 6, 34 },
                new[] { 6, 22, 38 },
                new[] { 6, 24, 42 },
                new[] { 6, 26, 46 },
                new[] { 6, 28, 50 },
            };

            static readonly byte[] GfExp = new byte[512];
            static readonly byte[] GfLog = new byte[256];

            static QrIsoCodec()
            {
                int v = 1;
                for (int i = 0; i < 255; i++)
                {
                    GfExp[i] = (byte)v;
                    GfLog[v] = (byte)i;
                    v <<= 1;
                    if ((v & 0x100) != 0) v ^= 0x11D;
                }
                for (int i = 255; i < 512; i++)
                    GfExp[i] = GfExp[i - 255];
            }

            public static int Side(int version) => 17 + version * 4;

            public static int CountBits(int mode, int version)
            {
                if (version <= 9)
                {
                    if (mode == ModeNumeric) return 10;
                    if (mode == ModeAlphanumeric) return 9;
                    return 8;
                }
                if (mode == ModeNumeric) return 12;
                if (mode == ModeAlphanumeric) return 11;
                return 16;
            }

            public static QrMatrix EncodeByteMode(byte[] utf8, EccLevel level)
            {
                int eccIndex = (int)level;
                for (int v = 1; v <= MaxSupportedVersion; v++)
                {
                    int dataBytesCap = TotalDataCodewords(v, eccIndex);
                    int capacityBits = dataBytesCap * 8;
                    int countLen = CountBits(ModeByte, v);
                    int neededBits = 4 + countLen + utf8.Length * 8;
                    if (neededBits > capacityBits) continue;

                    var data = new byte[dataBytesCap];
                    int bitPos = 0;
                    PutBits(data, ref bitPos, ModeByte, 4);
                    PutBits(data, ref bitPos, utf8.Length, countLen);
                    for (int i = 0; i < utf8.Length; i++)
                        PutBits(data, ref bitPos, utf8[i], 8);

                    int terminator = Math.Min(4, capacityBits - bitPos);
                    PutBits(data, ref bitPos, 0, terminator);
                    if ((bitPos & 7) != 0)
                        bitPos = (bitPos + 7) & ~7;

                    int byteCursor = bitPos >> 3;
                    for (int pad = 0; byteCursor < dataBytesCap; byteCursor++, pad++)
                        data[byteCursor] = (pad & 1) == 0 ? (byte)0xEC : (byte)0x11;

                    return BuildMatrix(v, level, data);
                }
                return null;
            }

            static int TotalDataCodewords(int v, int eccIdx)
            {
                int[] counts = VersionBlocks[v][eccIdx].Blocks;
                int sum = 0;
                for (int i = 0; i < counts.Length; i++) sum += counts[i];
                return sum;
            }

            static void PutBits(byte[] buffer, ref int bitPos, int value, int length)
            {
                for (int shift = length - 1; shift >= 0; shift--)
                {
                    if (((value >> shift) & 1) != 0)
                        buffer[bitPos >> 3] |= (byte)(1 << (7 - (bitPos & 7)));
                    bitPos++;
                }
            }

            static QrMatrix BuildMatrix(int v, EccLevel level, byte[] data)
            {
                int n = Side(v);
                byte[] codewords = InterleaveBlocks(v, (int)level, data);
                byte[] fnMap = FunctionMap(v);
                var baseGrid = new byte[n * n];
                DrawFunctionPatterns(baseGrid, v);

                int[] order = DataOrder(v, fnMap);
                int totalBits = codewords.Length * 8;
                for (int i = 0; i < order.Length && i < totalBits; i++)
                    baseGrid[order[i]] = (byte)((codewords[i >> 3] >> (7 - (i & 7))) & 1);

                int bestMask = 0;
                int bestPenalty = int.MaxValue;
                byte[] bestModules = null;

                for (int mask = 0; mask < 8; mask++)
                {
                    var trial = new byte[baseGrid.Length];
                    Buffer.BlockCopy(baseGrid, 0, trial, 0, baseGrid.Length);
                    ApplyMask(trial, fnMap, n, mask);
                    DrawFormatBits(trial, n, (int)level, mask);
                    int penalty = ScorePenalty(trial, n);
                    if (penalty < bestPenalty)
                    {
                        bestPenalty = penalty;
                        bestMask = mask;
                        bestModules = trial;
                    }
                }

                return new QrMatrix(v, n, level, bestMask, bestModules);
            }

            static byte[] InterleaveBlocks(int v, int eccIdx, byte[] data)
            {
                (int eccCount, int[] counts) = VersionBlocks[v][eccIdx];
                var blocks = new byte[counts.Length][];
                var eccs = new byte[counts.Length][];
                int offset = 0;
                int longest = 0;
                int totalOut = 0;

                for (int b = 0; b < counts.Length; b++)
                {
                    int len = counts[b];
                    if (len > longest) longest = len;
                    var slice = new byte[len];
                    Buffer.BlockCopy(data, offset, slice, 0, len);
                    offset += len;
                    blocks[b] = slice;
                    eccs[b] = RsRemainder(slice, eccCount);
                    totalOut += len + eccCount;
                }

                var output = new byte[totalOut];
                int cursor = 0;
                for (int i = 0; i < longest; i++)
                    for (int b = 0; b < blocks.Length; b++)
                        if (i < blocks[b].Length) output[cursor++] = blocks[b][i];

                for (int i = 0; i < eccCount; i++)
                    for (int b = 0; b < eccs.Length; b++)
                        output[cursor++] = eccs[b][i];

                return output;
            }

            public static byte[] FunctionMap(int v)
            {
                int n = Side(v);
                var map = new byte[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        bool used = x == 6 || y == 6 ||
                                    (x < 9 && y < 9) ||
                                    (x >= n - 8 && y < 9) ||
                                    (x < 9 && y >= n - 8);
                        if (v >= 7 && ((x < 6 && y >= n - 11 && y < n - 8) || (y < 6 && x >= n - 11 && x < n - 8)))
                            used = true;
                        if (used) map[y * n + x] = 1;
                    }
                }

                int[] tracks = AlignmentTracks[v];
                for (int iy = 0; iy < tracks.Length; iy++)
                {
                    for (int ix = 0; ix < tracks.Length; ix++)
                    {
                        if (OverlapsFinder(ix, iy, tracks.Length)) continue;
                        int cx = tracks[ix];
                        int cy = tracks[iy];
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                                map[(cy + dy) * n + cx + dx] = 1;
                    }
                }
                return map;
            }

            static bool OverlapsFinder(int ix, int iy, int trackLen) =>
                (ix == 0 && iy == 0) ||
                (ix == 0 && iy == trackLen - 1) ||
                (ix == trackLen - 1 && iy == 0);

            static void DrawFunctionPatterns(byte[] grid, int v)
            {
                int n = Side(v);
                for (int i = 0; i < n; i++)
                {
                    byte bit = (byte)((i & 1) == 0 ? 1 : 0);
                    grid[6 * n + i] = bit;
                    grid[i * n + 6] = bit;
                }

                DrawFinder(grid, n, 3, 3);
                DrawFinder(grid, n, n - 4, 3);
                DrawFinder(grid, n, 3, n - 4);

                int[] tracks = AlignmentTracks[v];
                for (int iy = 0; iy < tracks.Length; iy++)
                {
                    for (int ix = 0; ix < tracks.Length; ix++)
                    {
                        if (OverlapsFinder(ix, iy, tracks.Length)) continue;
                        int cx = tracks[ix];
                        int cy = tracks[iy];
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                                grid[(cy + dy) * n + cx + dx] = (byte)(Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1 ? 1 : 0);
                    }
                }

                if (v >= 7)
                {
                    int verBits = VersionInfoBits(v);
                    for (int i = 0; i < 18; i++)
                    {
                        byte bit = (byte)((verBits >> i) & 1);
                        int a = n - 11 + (i % 3);
                        int b = i / 3;
                        grid[b * n + a] = bit;
                        grid[a * n + b] = bit;
                    }
                }
            }

            static void DrawFinder(byte[] grid, int n, int cx, int cy)
            {
                for (int dy = -4; dy <= 4; dy++)
                {
                    int y = cy + dy;
                    if (y < 0 || y >= n) continue;
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        int x = cx + dx;
                        if (x < 0 || x >= n) continue;
                        int ring = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        grid[y * n + x] = (byte)(ring != 2 && ring != 4 ? 1 : 0);
                    }
                }
            }

            public static int[] DataOrder(int v, byte[] fnMap)
            {
                int n = Side(v);
                var list = new List<int>(n * n);
                int right = n - 1;
                while (right >= 1)
                {
                    if (right == 6) right = 5;
                    bool upward = ((right + 1) & 2) == 0;
                    for (int vert = 0; vert < n; vert++)
                    {
                        int y = upward ? (n - 1 - vert) : vert;
                        for (int j = 0; j < 2; j++)
                        {
                            int x = right - j;
                            if (fnMap[y * n + x] == 0)
                                list.Add(y * n + x);
                        }
                    }
                    right -= 2;
                }
                return list.ToArray();
            }

            public static bool MaskBit(int which, int x, int y)
            {
                switch (which)
                {
                    case 0: return ((x + y) & 1) == 0;
                    case 1: return (y & 1) == 0;
                    case 2: return x % 3 == 0;
                    case 3: return (x + y) % 3 == 0;
                    case 4: return (((y / 2) + (x / 3)) & 1) == 0;
                    case 5: return ((x * y) & 1) + ((x * y) % 3) == 0;
                    case 6: return ((((x * y) & 1) + ((x * y) % 3)) & 1) == 0;
                    default: return ((((x + y) & 1) + ((x * y) % 3)) & 1) == 0;
                }
            }

            public static void ApplyMask(byte[] grid, byte[] fnMap, int n, int which)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        if (fnMap[y * n + x] == 0 && MaskBit(which, x, y))
                            grid[y * n + x] ^= 1;
            }

            public static int FormatBits(int eccIdx, int mask)
            {
                int data = (EccFormatBits[eccIdx] << 3) | mask;
                int rem = data;
                for (int i = 0; i < 10; i++)
                    rem = (rem << 1) ^ ((rem >> 9) * 0x537);
                return ((data << 10) | (rem & 0x3FF)) ^ 0x5412;
            }

            static int VersionInfoBits(int version)
            {
                int rem = version;
                for (int i = 0; i < 12; i++)
                    rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                return (version << 12) | (rem & 0xFFF);
            }

            public static void GetFormatPositions(int n, Vector2Int[] copyA, Vector2Int[] copyB)
            {
                for (int i = 0; i < 6; i++) copyA[i] = new Vector2Int(8, i);
                copyA[6] = new Vector2Int(8, 7);
                copyA[7] = new Vector2Int(8, 8);
                copyA[8] = new Vector2Int(7, 8);
                for (int i = 9; i < 15; i++) copyA[i] = new Vector2Int(14 - i, 8);

                for (int i = 0; i < 8; i++) copyB[i] = new Vector2Int(n - 1 - i, 8);
                for (int i = 8; i < 15; i++) copyB[i] = new Vector2Int(8, n - 15 + i);
            }

            static void DrawFormatBits(byte[] grid, int n, int eccIdx, int mask)
            {
                int bits = FormatBits(eccIdx, mask);
                var a = new Vector2Int[15];
                var b = new Vector2Int[15];
                GetFormatPositions(n, a, b);
                for (int i = 0; i < 15; i++)
                {
                    byte bit = (byte)((bits >> i) & 1);
                    grid[a[i].y * n + a[i].x] = bit;
                    grid[b[i].y * n + b[i].x] = bit;
                }
                grid[(n - 8) * n + 8] = 1;
            }

            static int ScorePenalty(byte[] grid, int n)
            {
                int score = 0;
                for (int horiz = 0; horiz < 2; horiz++)
                {
                    for (int a = 0; a < n; a++)
                    {
                        int run = 0;
                        int last = -1;
                        int window = 0;
                        for (int b = 0; b < n; b++)
                        {
                            int val = horiz == 1 ? grid[a * n + b] : grid[b * n + a];
                            if (val == last)
                            {
                                run++;
                                if (run == 5) score += 3;
                                else if (run > 5) score += 1;
                            }
                            else
                            {
                                run = 1;
                                last = val;
                            }
                            window = ((window << 1) | val) & 0x7FF;
                            if (b >= 10 && (window == 0x5D0 || window == 0x05D))
                                score += 40;
                        }
                    }
                }
                for (int y = 0; y < n - 1; y++)
                {
                    for (int x = 0; x < n - 1; x++)
                    {
                        byte v = grid[y * n + x];
                        if (v == grid[y * n + x + 1] && v == grid[(y + 1) * n + x] && v == grid[(y + 1) * n + x + 1])
                            score += 3;
                    }
                }
                int dark = 0;
                for (int i = 0; i < grid.Length; i++) dark += grid[i];
                int percent = (dark * 100) / (n * n);
                score += (Math.Abs(percent - 50) / 5) * 10;
                return score;
            }

            // ---- GF(256) Arithmetic & Reed-Solomon Error Correction ----

            public static byte GfMul(int a, int b) =>
                (a == 0 || b == 0) ? (byte)0 : GfExp[GfLog[a] + GfLog[b]];

            public static byte GfDiv(int a, int b) =>
                a == 0 ? (byte)0 : GfExp[(GfLog[a] + 255 - GfLog[b]) % 255];

            public static byte GfPow2(int n)
            {
                int m = n % 255;
                if (m < 0) m += 255;
                return GfExp[m];
            }

            public static byte GfPow(int a, int n)
            {
                int res = 1;
                for (int i = 0; i < n; i++) res = GfMul(res, a);
                return (byte)res;
            }

            public static byte[] RsRemainder(byte[] data, int degree)
            {
                var divisor = new byte[degree];
                divisor[degree - 1] = 1;
                int root = 1;
                for (int i = 0; i < degree; i++)
                {
                    for (int j = 0; j < degree; j++)
                    {
                        divisor[j] = GfMul(divisor[j], root);
                        if (j + 1 < degree) divisor[j] ^= divisor[j + 1];
                    }
                    root = GfMul(root, 2);
                }

                var result = new byte[degree];
                for (int i = 0; i < data.Length; i++)
                {
                    int factor = data[i] ^ result[0];
                    for (int j = 0; j < degree - 1; j++)
                        result[j] = result[j + 1];
                    result[degree - 1] = 0;
                    for (int j = 0; j < degree; j++)
                        result[j] ^= GfMul(divisor[j], factor);
                }
                return result;
            }

            public static bool RsCorrect(byte[] block, int eccCount)
            {
                int n = block.Length;
                var syndromes = new byte[eccCount];
                bool clean = true;
                for (int i = 0; i < eccCount; i++)
                {
                    int x = GfPow2(i);
                    int val = 0;
                    for (int j = 0; j < n; j++)
                        val = GfMul(val, x) ^ block[j];
                    syndromes[i] = (byte)val;
                    if (val != 0) clean = false;
                }
                if (clean) return true;

                var locator = new byte[] { 1 };
                var previous = new byte[] { 1 };
                int length = 0;
                int shift = 1;
                int last = 1;

                for (int step = 0; step < eccCount; step++)
                {
                    int delta = syndromes[step];
                    for (int i = 1; i <= length; i++)
                        if (i < locator.Length)
                            delta ^= GfMul(locator[i], syndromes[step - i]);

                    if (delta == 0)
                    {
                        shift++;
                        continue;
                    }

                    int scale = GfDiv(delta, last);
                    int nextLen = Math.Max(locator.Length, previous.Length + shift);
                    var next = new byte[nextLen];
                    Buffer.BlockCopy(locator, 0, next, 0, locator.Length);
                    for (int i = 0; i < previous.Length; i++)
                        next[i + shift] ^= GfMul(scale, previous[i]);

                    if (2 * length <= step)
                    {
                        previous = locator;
                        length = step + 1 - length;
                        last = delta;
                        shift = 1;
                    }
                    else
                    {
                        shift++;
                    }
                    locator = next;
                }

                if (length * 2 > eccCount) return false;

                var evaluator = new byte[eccCount];
                for (int i = 0; i < eccCount; i++)
                    for (int j = 0; j < locator.Length; j++)
                        if (i + j < eccCount)
                            evaluator[i + j] ^= GfMul(syndromes[i], locator[j]);

                int found = 0;
                for (int idx = 0; idx < n; idx++)
                {
                    int place = GfPow2(n - 1 - idx);
                    int placeInv = GfDiv(1, place);
                    if (EvaluateLow(locator, placeInv) != 0) continue;

                    int deriv = 0;
                    for (int i = 1; i < locator.Length; i += 2)
                        deriv ^= GfMul(locator[i], GfPow(placeInv, i - 1));
                    if (deriv == 0) return false;

                    block[idx] ^= GfMul(place, GfDiv(EvaluateLow(evaluator, placeInv), deriv));
                    found++;
                }
                return found == length;
            }

            static byte EvaluateLow(byte[] poly, int x)
            {
                int v = 0;
                for (int i = poly.Length - 1; i >= 0; i--)
                    v = GfMul(v, x) ^ poly[i];
                return (byte)v;
            }
        }

        // =============================================================================================================================
        // Lightweight Finder (1:1:3:1:1) & Perspective QR Scanner (Port of scripts/ui/qr_reader.gd, Zero Per-Frame Alloc)
        // =============================================================================================================================

        sealed class QrScannerBuffer
        {
            const int MaxSide = 320;
            const int BlockDivisor = 5;
            const int DarkMargin = 6;
            const int SearchModules = 2;
            const double BudgetSeconds = 0.045;

            static readonly Vector2[] FiveSamples =
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.3f, 0.3f),
                new Vector2(0.7f, 0.3f),
                new Vector2(0.3f, 0.7f),
                new Vector2(0.7f, 0.7f),
            };

            static readonly Vector2[] CenterSample = { new Vector2(0.5f, 0.5f) };
            static readonly Vector2[] SearchOffsets = BuildSearchOffsets();

            byte[] _luma;
            byte[] _bits;
            int[] _integral;
            int _width;
            int _height;
            long _deadlineTicks;

            readonly List<Vector3> _finderCentres = new List<Vector3>(16);
            readonly List<int> _finderCounts = new List<int>(16);
            readonly int[] _runs = new int[5];
            readonly Vector2Int[] _fmtA = new Vector2Int[15];
            readonly Vector2Int[] _fmtB = new Vector2Int[15];

            static Vector2[] BuildSearchOffsets()
            {
                var list = new List<Vector2>();
                for (int dy = -SearchModules * 2; dy <= SearchModules * 2; dy++)
                    for (int dx = -SearchModules * 2; dx <= SearchModules * 2; dx++)
                        list.Add(new Vector2(dx * 0.5f, dy * 0.5f));
                list.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
                return list.ToArray();
            }

            public bool TryDecode(Color32[] pixels, int srcWidth, int srcHeight, bool verticallyFlipped, out string payload)
            {
                payload = "";
                if (pixels == null || srcWidth < 21 || srcHeight < 21 || pixels.Length < srcWidth * srcHeight)
                    return false;

                Binarize(pixels, srcWidth, srcHeight, verticallyFlipped);
                _deadlineTicks = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * BudgetSeconds);

                FindFinderCandidates();
                if (_finderCentres.Count < 3) return false;

                _finderCentres.Sort((a, b) => b.z.CompareTo(a.z));
                int take = Math.Min(_finderCentres.Count, 6);
                for (int i = 0; i < take; i++)
                {
                    for (int j = i + 1; j < take; j++)
                    {
                        for (int k = j + 1; k < take; k++)
                        {
                            string decoded = TryReadThree(_finderCentres[i], _finderCentres[j], _finderCentres[k]);
                            if (!string.IsNullOrEmpty(decoded))
                            {
                                payload = decoded;
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            void Binarize(Color32[] src, int srcW, int srcH, bool flipY)
            {
                int longest = Math.Max(srcW, srcH);
                float scale = longest > MaxSide ? (float)MaxSide / longest : 1f;
                _width = Math.Max(21, Mathf.RoundToInt(srcW * scale));
                _height = Math.Max(21, Mathf.RoundToInt(srcH * scale));
                int count = _width * _height;

                if (_luma == null || _luma.Length < count) _luma = new byte[count];
                if (_bits == null || _bits.Length < count) _bits = new byte[count];
                int intLen = (_width + 1) * (_height + 1);
                if (_integral == null || _integral.Length < intLen) _integral = new int[intLen];

                for (int y = 0; y < _height; y++)
                {
                    int sy = Mathf.Clamp((y * srcH) / _height, 0, srcH - 1);
                    if (flipY) sy = srcH - 1 - sy;
                    int srcRow = sy * srcW;
                    int dstRow = y * _width;
                    int intRow = (y + 1) * (_width + 1);
                    int intPrevRow = y * (_width + 1);
                    int rowSum = 0;

                    for (int x = 0; x < _width; x++)
                    {
                        int sx = Mathf.Clamp((x * srcW) / _width, 0, srcW - 1);
                        Color32 c = src[srcRow + sx];
                        int lum = (c.r * 77 + c.g * 150 + c.b * 29) >> 8;
                        _luma[dstRow + x] = (byte)lum;
                        rowSum += lum;
                        _integral[intRow + x + 1] = _integral[intPrevRow + x + 1] + rowSum;
                    }
                }

                int halfWindow = Math.Max(4, Math.Min(_width, _height) / (BlockDivisor * 2));
                int stride = _width + 1;
                for (int y = 0; y < _height; y++)
                {
                    int y0 = Math.Max(0, y - halfWindow);
                    int y1 = Math.Min(_height, y + halfWindow + 1);
                    int rowOff = y * _width;
                    for (int x = 0; x < _width; x++)
                    {
                        int x0 = Math.Max(0, x - halfWindow);
                        int x1 = Math.Min(_width, x + halfWindow + 1);
                        int area = (x1 - x0) * (y1 - y0);
                        int sum = _integral[y1 * stride + x1] - _integral[y0 * stride + x1] - _integral[y1 * stride + x0] + _integral[y0 * stride + x0];
                        int avg = sum / area;
                        _bits[rowOff + x] = (byte)((_luma[rowOff + x] + DarkMargin < avg) ? 1 : 0);
                    }
                }
            }

            bool IsDark(int x, int y) =>
                x >= 0 && y >= 0 && x < _width && y < _height && _bits[y * _width + x] != 0;

            bool Inside(int x, int y) =>
                x >= 0 && y >= 0 && x < _width && y < _height;

            void FindFinderCandidates()
            {
                _finderCentres.Clear();
                _finderCounts.Clear();
                int step = _height < 200 ? 1 : 2;

                for (int y = 0; y < _height; y += step)
                {
                    Array.Clear(_runs, 0, 5);
                    int state = 0;
                    int rowBase = y * _width;

                    for (int x = 0; x <= _width; x++)
                    {
                        bool dark = x < _width && _bits[rowBase + x] != 0;
                        if (dark == ((state & 1) == 0))
                        {
                            _runs[state]++;
                            continue;
                        }
                        if (state < 4)
                        {
                            if (state == 0 && _runs[0] == 0) continue;
                            state++;
                            _runs[state] = 1;
                            continue;
                        }

                        if (RatioOk(_runs))
                        {
                            float cx = x - _runs[4] - _runs[3] - _runs[2] * 0.5f;
                            Vector3 confirmed = ConfirmFinder(cx, y + 0.5f, _runs);
                            if (confirmed.z > 0f)
                                AddCandidate(confirmed);
                        }

                        _runs[0] = _runs[2];
                        _runs[1] = _runs[3];
                        _runs[2] = _runs[4];
                        _runs[3] = 1;
                        _runs[4] = 0;
                        state = 3;
                    }
                }

                for (int i = 0; i < _finderCentres.Count; i++)
                {
                    Vector3 c = _finderCentres[i];
                    _finderCentres[i] = new Vector3(c.x, c.y, _finderCounts[i] * 1000f + c.z);
                }
            }

            static bool RatioOk(int[] runs)
            {
                int total = 0;
                for (int i = 0; i < 5; i++)
                {
                    if (runs[i] == 0) return false;
                    total += runs[i];
                }
                if (total < 7) return false;
                float mod = total / 7f;
                float slack = mod / 1.5f;
                return Mathf.Abs(mod - runs[0]) < slack &&
                       Mathf.Abs(mod - runs[1]) < slack &&
                       Mathf.Abs(3f * mod - runs[2]) < 3f * slack &&
                       Mathf.Abs(mod - runs[3]) < slack &&
                       Mathf.Abs(mod - runs[4]) < slack;
            }

            Vector3 ConfirmFinder(float cx, float row, int[] rowRuns)
            {
                int total = rowRuns[0] + rowRuns[1] + rowRuns[2] + rowRuns[3] + rowRuns[4];
                float y = CrossCheck(Mathf.FloorToInt(cx), Mathf.FloorToInt(row), 0, 1, total);
                if (y < 0f) return Vector3.zero;
                float x = CrossCheck(Mathf.FloorToInt(cx), Mathf.FloorToInt(y), 1, 0, total);
                if (x < 0f) return Vector3.zero;
                float y2 = CrossCheck(Mathf.FloorToInt(x), Mathf.FloorToInt(y), 0, 1, total);
                if (y2 < 0f) return Vector3.zero;
                return new Vector3(x, y2, total / 7f);
            }

            float CrossCheck(int x, int y, int ax, int ay, int expected)
            {
                if (!IsDark(x, y)) return -1f;
                int limit = expected * 2;
                int backMid = RunPixels(x, y, -ax, -ay, true, limit);
                int atX = x - ax * backMid;
                int atY = y - ay * backMid;
                int backLight = RunPixels(atX, atY, -ax, -ay, false, limit);
                atX -= ax * backLight;
                atY -= ay * backLight;
                int backDark = RunPixels(atX, atY, -ax, -ay, true, limit);

                int fwdMid = RunPixels(x + ax, y + ay, ax, ay, true, limit);
                atX = x + ax * (1 + fwdMid);
                atY = y + ay * (1 + fwdMid);
                int fwdLight = RunPixels(atX, atY, ax, ay, false, limit);
                atX += ax * fwdLight;
                atY += ay * fwdLight;
                int fwdDark = RunPixels(atX, atY, ax, ay, true, limit);

                int[] r = { backDark, backLight, backMid + fwdMid, fwdLight, fwdDark };
                int total = r[0] + r[1] + r[2] + r[3] + r[4];
                if (Math.Abs(total - expected) * 5 >= expected * 2 || !RatioOk(r))
                    return -1f;
                int along = ax != 0 ? x : y;
                return along + 1f + (fwdMid - backMid) * 0.5f;
            }

            int RunPixels(int x, int y, int dx, int dy, bool wantDark, int limit)
            {
                int count = 0;
                while (count < limit && Inside(x, y) && (_bits[y * _width + x] != 0) == wantDark)
                {
                    count++;
                    x += dx;
                    y += dy;
                }
                return count;
            }

            void AddCandidate(Vector3 centre)
            {
                for (int i = 0; i < _finderCentres.Count; i++)
                {
                    Vector3 known = _finderCentres[i];
                    if (Mathf.Abs(known.x - centre.x) <= centre.z * 2f &&
                        Mathf.Abs(known.y - centre.y) <= centre.z * 2f &&
                        Mathf.Abs(known.z - centre.z) <= Mathf.Max(1f, known.z * 0.5f))
                    {
                        float n = _finderCounts[i];
                        _finderCentres[i] = new Vector3(
                            (known.x * n + centre.x) / (n + 1f),
                            (known.y * n + centre.y) / (n + 1f),
                            (known.z * n + centre.z) / (n + 1f));
                        _finderCounts[i] = (int)n + 1;
                        return;
                    }
                }
                _finderCentres.Add(centre);
                _finderCounts.Add(1);
            }

            string TryReadThree(Vector3 a, Vector3 b, Vector3 c)
            {
                float ma = a.z % 1000f;
                float mb = b.z % 1000f;
                float mc = c.z % 1000f;
                float smallest = Mathf.Min(ma, Mathf.Min(mb, mc));
                if (Mathf.Max(ma, Mathf.Max(mb, mc)) > smallest * 2f) return "";

                var pa = new Vector2(a.x, a.y);
                var pb = new Vector2(b.x, b.y);
                var pc = new Vector2(c.x, c.y);

                float ab = Vector2.Distance(pa, pb);
                float bc = Vector2.Distance(pb, pc);
                float ac = Vector2.Distance(pa, pc);

                Vector2 topLeft = pc, one = pa, two = pb;
                if (bc >= ab && bc >= ac) { topLeft = pa; one = pb; two = pc; }
                else if (ac >= ab && ac >= bc) { topLeft = pb; one = pa; two = pc; }

                Vector2 leg1 = one - topLeft;
                Vector2 leg2 = two - topLeft;
                if (leg1.magnitude < 1f || leg2.magnitude < 1f) return "";
                float ratio = leg1.magnitude / leg2.magnitude;
                if (ratio < 0.6f || ratio > 1.67f || Mathf.Abs(Vector2.Dot(leg1.normalized, leg2.normalized)) > 0.5f)
                    return "";

                Vector2 topRight = one;
                Vector2 bottomLeft = two;
                if ((leg1.x * leg2.y - leg1.y * leg2.x) < 0f)
                {
                    topRight = two;
                    bottomLeft = one;
                }

                float mod = (ModuleAlong(topLeft, topRight) + ModuleAlong(topRight, topLeft) +
                             ModuleAlong(topLeft, bottomLeft) + ModuleAlong(bottomLeft, topLeft)) * 0.25f;
                if (mod <= 0f) return "";

                float across = (Vector2.Distance(topLeft, topRight) + Vector2.Distance(topLeft, bottomLeft)) * 0.5f / mod;
                int side = Mathf.RoundToInt(across) + 7;
                switch (side & 3)
                {
                    case 0: side += 1; break;
                    case 2: side -= 1; break;
                    case 3: side -= 2; break;
                }
                int version = (side - 17) / 4;
                if (version < 1 || version > MaxSupportedVersion) return "";

                for (int flip = 0; flip < 2; flip++)
                {
                    Vector2 right = flip == 0 ? topRight : bottomLeft;
                    Vector2 down = flip == 0 ? bottomLeft : topRight;
                    string text = ReadFramed(topLeft, right, down, version, mod);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
                return "";
            }

            float ModuleAlong(Vector2 from, Vector2 to)
            {
                Vector2 dir = (to - from).normalized;
                float limit = Vector2.Distance(from, to) * 0.5f;
                float total = 0f;
                for (int s = 0; s < 2; s++)
                {
                    Vector2 step = s == 0 ? dir : -dir;
                    Vector2 at = from;
                    float travelled = 0f;
                    bool[] pattern = { true, false, true };
                    for (int p = 0; p < 3; p++)
                    {
                        bool want = pattern[p];
                        while (travelled < limit &&
                               (IsDark(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)) == want ||
                                IsDark(Mathf.FloorToInt(at.x + step.x), Mathf.FloorToInt(at.y + step.y)) == want))
                        {
                            at += step;
                            travelled += 1f;
                        }
                    }
                    if (travelled >= limit) return 0f;
                    total += travelled - 0.5f;
                }
                return total / 7f;
            }

            string ReadFramed(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, int v, float module)
            {
                int n = QrIsoCodec.Side(v);
                float far = n - 3.5f;
                float cornerAt = v == 1 ? far : (n - 6.5f);
                Vector2 guess = topLeft + (topRight - topLeft + bottomLeft - topLeft) * ((cornerAt - 3.5f) / (far - 3.5f));

                if (v > 1)
                {
                    Vector2 align = FindAlignment(guess, module);
                    if (align != guess)
                    {
                        string res = DecodeFramed(BuildHomography(topLeft, topRight, bottomLeft, align, cornerAt, v), v, FiveSamples);
                        if (!string.IsNullOrEmpty(res)) return res;
                    }
                }

                Vector2 across = (topRight - topLeft).normalized * module;
                Vector2 down = (bottomLeft - topLeft).normalized * module;
                for (int i = 0; i < SearchOffsets.Length; i++)
                {
                    Vector2 corner = guess + across * SearchOffsets[i].x + down * SearchOffsets[i].y;
                    string res = DecodeFramed(BuildHomography(topLeft, topRight, bottomLeft, corner, cornerAt, v), v, CenterSample);
                    if (!string.IsNullOrEmpty(res)) return res;
                }
                return "";
            }

            Vector2 FindAlignment(Vector2 guess, float module)
            {
                int reach = Mathf.CeilToInt(module * 4f);
                int step = Math.Max(1, Mathf.FloorToInt(module * 0.5f));
                Vector2 best = guess;
                float bestDist = float.PositiveInfinity;

                for (int dy = -reach; dy <= reach; dy += step)
                {
                    for (int dx = -reach; dx <= reach; dx += step)
                    {
                        int x = Mathf.RoundToInt(guess.x) + dx;
                        int y = Mathf.RoundToInt(guess.y) + dy;
                        if (!IsDark(x, y)) continue;
                        float cx = SmallCross(x, y, 1, 0, module);
                        if (cx < 0f) continue;
                        float cy = SmallCross(Mathf.RoundToInt(cx), y, 0, 1, module);
                        if (cy < 0f) continue;
                        var pt = new Vector2(cx, cy);
                        float d = (pt - guess).sqrMagnitude;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = pt;
                        }
                    }
                }
                return best;
            }

            float SmallCross(int x, int y, int ax, int ay, float module)
            {
                int limit = Mathf.CeilToInt(module * 2f);
                int lowX = x, lowY = y, low = 0;
                while (IsDark(lowX - ax, lowY - ay) && low < limit) { lowX -= ax; lowY -= ay; low++; }
                int highX = x, highY = y, count = low;
                while (IsDark(highX + ax, highY + ay) && count < limit) { highX += ax; highY += ay; count++; }
                int darkRun = count + 1;
                if (Mathf.Abs(darkRun - module) > module * 0.6f) return -1f;

                for (int dir = -1; dir <= 1; dir += 2)
                {
                    int ex = (dir < 0 ? lowX : highX) + ax * dir;
                    int ey = (dir < 0 ? lowY : highY) + ay * dir;
                    int light = 0;
                    while (!IsDark(ex, ey) && Inside(ex, ey) && light < limit) { ex += ax * dir; ey += ay * dir; light++; }
                    if (Mathf.Abs(light - module) > module * 0.6f || !IsDark(ex, ey)) return -1f;
                }
                int a = ax != 0 ? lowX : lowY;
                int b = ax != 0 ? highX : highY;
                return (a + b) * 0.5f + 0.5f;
            }

            static double[] BuildHomography(Vector2 tl, Vector2 tr, Vector2 bl, Vector2 corner, float cornerAt, int v)
            {
                float far = QrIsoCodec.Side(v) - 3.5f;
                Vector2[] from = { new Vector2(3.5f, 3.5f), new Vector2(far, 3.5f), new Vector2(cornerAt, cornerAt), new Vector2(3.5f, far) };
                Vector2[] to = { tl, tr, corner, bl };
                return MatMul(SquareToQuad(to), Adjoint(SquareToQuad(from)));
            }

            static double[] SquareToQuad(Vector2[] q)
            {
                double dx3 = q[0].x - q[1].x + q[2].x - q[3].x;
                double dy3 = q[0].y - q[1].y + q[2].y - q[3].y;
                double dx1 = q[1].x - q[2].x;
                double dx2 = q[3].x - q[2].x;
                double dy1 = q[1].y - q[2].y;
                double dy2 = q[3].y - q[2].y;
                double denom = dx1 * dy2 - dx2 * dy1;
                if (Math.Abs(denom) < 1e-9) denom = 1e-9;
                double g = (dx3 * dy2 - dx2 * dy3) / denom;
                double h = (dx1 * dy3 - dx3 * dy1) / denom;
                return new[]
                {
                    q[1].x - q[0].x + g * q[1].x, q[3].x - q[0].x + h * q[3].x, q[0].x,
                    q[1].y - q[0].y + g * q[1].y, q[3].y - q[0].y + h * q[3].y, q[0].y,
                    g, h, 1.0,
                };
            }

            static double[] Adjoint(double[] m) => new[]
            {
                m[4] * m[8] - m[5] * m[7], m[2] * m[7] - m[1] * m[8], m[1] * m[5] - m[2] * m[4],
                m[5] * m[6] - m[3] * m[8], m[0] * m[8] - m[2] * m[6], m[2] * m[3] - m[0] * m[5],
                m[3] * m[7] - m[4] * m[6], m[1] * m[6] - m[0] * m[7], m[0] * m[4] - m[1] * m[3],
            };

            static double[] MatMul(double[] a, double[] b)
            {
                var o = new double[9];
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        o[r * 3 + c] = a[r * 3] * b[c] + a[r * 3 + 1] * b[3 + c] + a[r * 3 + 2] * b[6 + c];
                return o;
            }

            static Vector2 ApplyHomography(double[] m, float px, float py)
            {
                double w = m[6] * px + m[7] * py + m[8];
                if (Math.Abs(w) < 1e-9) w = 1e-9;
                return new Vector2((float)((m[0] * px + m[1] * py + m[2]) / w), (float)((m[3] * px + m[4] * py + m[5]) / w));
            }

            bool SampleModule(double[] m, int x, int y, Vector2[] samples)
            {
                int votes = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    Vector2 p = ApplyHomography(m, x + samples[i].x, y + samples[i].y);
                    if (IsDark(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)))
                        votes++;
                }
                return votes * 2 > samples.Length;
            }

            string DecodeFramed(double[] transform, int v, Vector2[] samples)
            {
                if (Stopwatch.GetTimestamp() > _deadlineTicks) return "";
                int n = QrIsoCodec.Side(v);
                var grid = new byte[n * n];
                QrIsoCodec.GetFormatPositions(n, _fmtA, _fmtB);
                for (int i = 0; i < 15; i++)
                {
                    grid[_fmtA[i].y * n + _fmtA[i].x] = (byte)(SampleModule(transform, _fmtA[i].x, _fmtA[i].y, samples) ? 1 : 0);
                    grid[_fmtB[i].y * n + _fmtB[i].x] = (byte)(SampleModule(transform, _fmtB[i].x, _fmtB[i].y, samples) ? 1 : 0);
                }

                Vector2Int fmt = ReadFormat(grid);
                if (fmt.x < 0) return "";

                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        grid[y * n + x] = (byte)(SampleModule(transform, x, y, samples) ? 1 : 0);

                return DecodeSampledGrid(grid, v, fmt.x, fmt.y);
            }

            Vector2Int ReadFormat(byte[] grid)
            {
                int n = (int)Math.Sqrt(grid.Length);
                var best = new Vector2Int(-1, -1);
                int bestDist = 4;

                for (int copy = 0; copy < 2; copy++)
                {
                    Vector2Int[] pts = copy == 0 ? _fmtA : _fmtB;
                    int bits = 0;
                    for (int i = 0; i < 15; i++)
                        bits |= grid[pts[i].y * n + pts[i].x] << i;

                    for (int level = 0; level < 4; level++)
                    {
                        for (int mask = 0; mask < 8; mask++)
                        {
                            int dist = PopCount(bits ^ QrIsoCodec.FormatBits(level, mask));
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                best = new Vector2Int(level, mask);
                            }
                        }
                    }
                }
                return best;
            }

            static int PopCount(int v)
            {
                int c = 0;
                while (v != 0) { c += v & 1; v >>= 1; }
                return c;
            }

            static string DecodeSampledGrid(byte[] grid, int v, int eccIdx, int mask)
            {
                int n = QrIsoCodec.Side(v);
                byte[] fnMap = QrIsoCodec.FunctionMap(v);
                QrIsoCodec.ApplyMask(grid, fnMap, n, mask);
                int[] order = QrIsoCodec.DataOrder(v, fnMap);
                (int eccCount, int[] counts) = QrIsoCodec.VersionBlocks[v][eccIdx];

                int totalCodewords = 0;
                int longest = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    totalCodewords += counts[i] + eccCount;
                    if (counts[i] > longest) longest = counts[i];
                }

                var codewords = new byte[totalCodewords];
                for (int bit = 0; bit < totalCodewords * 8 && bit < order.Length; bit++)
                    codewords[bit >> 3] |= (byte)(grid[order[bit]] << (7 - (bit & 7)));

                var blocks = new byte[counts.Length][];
                int totalData = 0;
                for (int b = 0; b < counts.Length; b++)
                {
                    blocks[b] = new byte[counts[b] + eccCount];
                    totalData += counts[b];
                }

                int cursor = 0;
                for (int i = 0; i < longest; i++)
                    for (int b = 0; b < blocks.Length; b++)
                        if (i < counts[b]) blocks[b][i] = codewords[cursor++];

                for (int i = 0; i < eccCount; i++)
                    for (int b = 0; b < blocks.Length; b++)
                        blocks[b][counts[b] + i] = codewords[cursor++];

                var data = new byte[totalData];
                int dataOut = 0;
                for (int b = 0; b < blocks.Length; b++)
                {
                    if (!QrIsoCodec.RsCorrect(blocks[b], eccCount))
                        return "";
                    Buffer.BlockCopy(blocks[b], 0, data, dataOut, counts[b]);
                    dataOut += counts[b];
                }
                return ParseSegments(data, v);
            }

            static string ParseSegments(byte[] data, int v)
            {
                int bitPos = 0;
                int totalBits = data.Length * 8;
                var sb = new StringBuilder();
                var byteBuf = new List<byte>();

                while (totalBits - bitPos >= 4)
                {
                    int mode = TakeBits(data, ref bitPos, 4);
                    if (mode == 0) break;
                    if (mode == 7)
                    {
                        if (totalBits - bitPos < 8) return "";
                        TakeBits(data, ref bitPos, 8);
                        continue;
                    }
                    if (mode != QrIsoCodec.ModeNumeric && mode != QrIsoCodec.ModeAlphanumeric && mode != QrIsoCodec.ModeByte)
                        return "";

                    int lenBits = QrIsoCodec.CountBits(mode, v);
                    if (totalBits - bitPos < lenBits) return "";
                    int count = TakeBits(data, ref bitPos, lenBits);

                    if (mode == QrIsoCodec.ModeByte)
                    {
                        if (totalBits - bitPos < count * 8) return "";
                        for (int i = 0; i < count; i++)
                            byteBuf.Add((byte)TakeBits(data, ref bitPos, 8));
                        continue;
                    }

                    FlushUtf8(byteBuf, sb);
                    if (mode == QrIsoCodec.ModeAlphanumeric)
                    {
                        int left = count;
                        while (left >= 2)
                        {
                            if (totalBits - bitPos < 11) return "";
                            int pair = TakeBits(data, ref bitPos, 11);
                            if (pair >= 45 * 45) return "";
                            sb.Append(QrIsoCodec.AlphanumericTable[pair / 45]);
                            sb.Append(QrIsoCodec.AlphanumericTable[pair % 45]);
                            left -= 2;
                        }
                        if (left == 1)
                        {
                            if (totalBits - bitPos < 6) return "";
                            int single = TakeBits(data, ref bitPos, 6);
                            if (single >= 45) return "";
                            sb.Append(QrIsoCodec.AlphanumericTable[single]);
                        }
                    }
                    else
                    {
                        int left = count;
                        while (left > 0)
                        {
                            int digits = Math.Min(left, 3);
                            int width = digits == 3 ? 10 : (digits == 2 ? 7 : 4);
                            if (totalBits - bitPos < width) return "";
                            int val = TakeBits(data, ref bitPos, width);
                            sb.Append(val.ToString("D" + digits, System.Globalization.CultureInfo.InvariantCulture));
                            left -= digits;
                        }
                    }
                }

                FlushUtf8(byteBuf, sb);
                return sb.ToString();
            }

            static void FlushUtf8(List<byte> bytes, StringBuilder sb)
            {
                if (bytes.Count == 0) return;
                sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));
                bytes.Clear();
            }

            static int TakeBits(byte[] data, ref int bitPos, int count)
            {
                int val = 0;
                for (int i = 0; i < count; i++)
                {
                    val = (val << 1) | ((data[bitPos >> 3] >> (7 - (bitPos & 7))) & 1);
                    bitPos++;
                }
                return val;
            }
        }
    }
}
