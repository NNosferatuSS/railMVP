using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Core;
using RailSwitchMVP.Gameplay;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// HUD da camada de jogo (Combo/Fever/Zonas), construído em código num Canvas
    /// próprio — não mexe nos prefabs de HUD existentes.
    /// - Barra de Fever + corrente (topo).
    /// - Timer da zona ativa (fino, na cor da zona).
    /// - Popups das ações (CLUTCH!, RISCO!, RASPÃO!) saindo da posição no mundo.
    /// - Banner de zona / Fever.
    /// - Botão "SOLTAR FEVER" quando a barra enche (único elemento que bloqueia toque;
    ///   o TouchDirectionalInput ignora toques sobre UI, então não troca o conector).
    /// </summary>
    public class RunHUD : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 2340);
        [Tooltip("Distância do topo (px de referência) onde fica a barra de Fever.")]
        [SerializeField] private float barTopOffset = 360f;

        static readonly Color BarBack = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color BarFill = new Color(0.35f, 1f, 0.85f, 0.95f);
        static readonly Color BarReady = new Color(1f, 0.55f, 1f, 1f);
        static readonly Color BarFever = new Color(1f, 0.85f, 0.35f, 1f);

        RectTransform _canvasRt, _fill, _zoneFill, _zoneBar;
        Image _fillImg, _zoneImg;
        TextMeshProUGUI _chainText, _barLabel, _bannerTitle, _bannerSub, _feverText;
        CanvasGroup _bannerGroup, _buttonGroup;
        RectTransform _button;
        float _bannerT = -1f, _bannerHold;
        Camera _cam;

        struct Pop { public TextMeshProUGUI t; public RectTransform rt; public float age; public Vector2 start; public bool live; }
        readonly List<Pop> _pops = new List<Pop>();
        int _popNext;

        void Awake()
        {
            Build();
            ComboFeverManager.Popup += OnPopup;
            ComboFeverManager.FeverReady += OnFeverReady;
            ComboFeverManager.FeverStarted += OnFeverStarted;
            ComboFeverManager.FeverEnded += OnFeverEnded;
            ZoneDirector.ZoneStarted += OnZoneStarted;
            ZoneDirector.ZoneEnded += OnZoneEnded;
        }

        RailSwitchMVP.Player.PlayerRailRider _rider;

        void Start()
        {
            _rider = FindFirstObjectByType<RailSwitchMVP.Player.PlayerRailRider>();
            if (_rider != null) _rider.GhostFlightStarted += OnGhostFlight;
        }

        void OnGhostFlight()
        {
            if (_rider != null) OnPopup("GHOST!", 0, _rider.transform.position, new Color(0.85f, 0.6f, 1f));
        }

        void OnDestroy()
        {
            ComboFeverManager.Popup -= OnPopup;
            ComboFeverManager.FeverReady -= OnFeverReady;
            ComboFeverManager.FeverStarted -= OnFeverStarted;
            ComboFeverManager.FeverEnded -= OnFeverEnded;
            ZoneDirector.ZoneStarted -= OnZoneStarted;
            ZoneDirector.ZoneEnded -= OnZoneEnded;
            if (_rider != null) _rider.GhostFlightStarted -= OnGhostFlight;
        }

        // ------------------------------------------------------------ construção

        void Build()
        {
            var canvasGo = new GameObject("RunHUD_Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;   // acima do HUD (0), abaixo de Warmup (8), GameOver (10) e Pause (12)
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRt = (RectTransform)canvasGo.transform;

            // Barra de Fever
            var bar = Rect("FeverBar", _canvasRt, new Vector2(0.5f, 1f), new Vector2(0, -barTopOffset), new Vector2(560, 20));
            Img(bar, BarBack);
            _fill = Rect("Fill", bar, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            _fill.anchorMin = new Vector2(0, 0); _fill.anchorMax = new Vector2(0, 1); _fill.pivot = new Vector2(0, 0.5f);
            _fill.offsetMin = Vector2.zero; _fill.offsetMax = Vector2.zero;
            _fillImg = Img(_fill, BarFill);
            _barLabel = Text("Label", bar, new Vector2(0f, 0.5f), new Vector2(-14, 0), 30, TextAlignmentOptions.Right, new Vector2(200, 40));
            _barLabel.rectTransform.pivot = new Vector2(1, 0.5f);
            _barLabel.text = "FEVER";
            _barLabel.color = new Color(1, 1, 1, 0.75f);
            _chainText = Text("Chain", bar, new Vector2(1f, 0.5f), new Vector2(14, 0), 38, TextAlignmentOptions.Left, new Vector2(240, 50));
            _chainText.rectTransform.pivot = new Vector2(0, 0.5f);

            // Timer de zona
            _zoneBar = Rect("ZoneBar", _canvasRt, new Vector2(0.5f, 1f), new Vector2(0, -barTopOffset - 26), new Vector2(560, 6));
            Img(_zoneBar, new Color(1, 1, 1, 0.08f));
            _zoneFill = Rect("Fill", _zoneBar, Vector2.zero, Vector2.zero, Vector2.zero);
            _zoneFill.anchorMin = Vector2.zero; _zoneFill.anchorMax = new Vector2(1, 1); _zoneFill.pivot = new Vector2(0, 0.5f);
            _zoneFill.offsetMin = Vector2.zero; _zoneFill.offsetMax = Vector2.zero;
            _zoneImg = Img(_zoneFill, Color.white);
            _zoneBar.gameObject.SetActive(false);

            // Texto de Fever ativo
            _feverText = Text("FeverActive", _canvasRt, new Vector2(0.5f, 1f), new Vector2(0, -barTopOffset - 80), 64, TextAlignmentOptions.Center, new Vector2(900, 90));
            _feverText.text = "FEVER  ×2";
            _feverText.color = BarFever;
            _feverText.gameObject.SetActive(false);

            // Banner
            var banner = Rect("Banner", _canvasRt, new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(1000, 260));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerTitle = Text("Title", banner, new Vector2(0.5f, 0.62f), Vector2.zero, 110, TextAlignmentOptions.Center, new Vector2(1000, 130));
            _bannerSub = Text("Sub", banner, new Vector2(0.5f, 0.2f), Vector2.zero, 40, TextAlignmentOptions.Center, new Vector2(1000, 60));
            _bannerSub.color = new Color(1, 1, 1, 0.85f);

            // Botão de soltar Fever
            // Bem na borda de baixo: o player fica ~16% acima da base da tela e o trilho à frente dele não pode ser coberto.
            _button = Rect("FeverButton", _canvasRt, new Vector2(0.5f, 0f), new Vector2(0, 95), new Vector2(460, 110));
            var bImg = Img(_button, new Color(1f, 0.45f, 0.95f, 0.92f));
            bImg.raycastTarget = true;
            var btn = _button.gameObject.AddComponent<Button>();
            btn.targetGraphic = bImg;
            btn.onClick.AddListener(() => { if (ComboFeverManager.Instance != null) ComboFeverManager.Instance.TryActivateFever(); });
            var bt = Text("Label", _button, new Vector2(0.5f, 0.5f), Vector2.zero, 50, TextAlignmentOptions.Center, new Vector2(460, 110));
            bt.text = "RELEASE FEVER!";
            bt.color = Color.white;
            _buttonGroup = _button.gameObject.AddComponent<CanvasGroup>();
            _button.gameObject.SetActive(false);

            // Pool de popups
            for (int i = 0; i < 10; i++)
            {
                var t = Text($"Pop{i}", _canvasRt, new Vector2(0.5f, 0.5f), Vector2.zero, 54, TextAlignmentOptions.Center, new Vector2(700, 80));
                t.gameObject.SetActive(false);
                _pops.Add(new Pop { t = t, rt = t.rectTransform });
            }
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 pos, float size, TextAlignmentOptions align, Vector2 box)
        {
            var rt = Rect(name, parent, anchor, pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(4, 10, 30, 200);
            return t;
        }

        // ------------------------------------------------------------ eventos

        void OnPopup(string label, int points, Vector3 world, Color color)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var sp = _cam.WorldToScreenPoint(world + Vector3.up * 0.8f);
            if (sp.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, sp, null, out var local);
            var p = _pops[_popNext];
            _popNext = (_popNext + 1) % _pops.Count;
            p.t.text = label;
            p.t.color = color;
            p.start = local + new Vector2(0, 40);
            p.age = 0f;
            p.live = true;
            p.t.gameObject.SetActive(true);
            _pops[(_popNext - 1 + _pops.Count) % _pops.Count] = p;
        }

        void OnFeverReady() => ShowBanner("FEVER READY!", "tap the button or ↑ to release", BarReady, 1.2f);
        void OnFeverStarted() => ShowBanner("FEVER!", "coins ×2 · magnet", BarFever, 1.0f);
        void OnFeverEnded() { }

        void OnZoneStarted(ZoneKind k) => ShowBanner(ZoneDirector.Title(k), ZoneDirector.Subtitle(k), ZoneDirector.Tint(k), 1.8f);

        void OnZoneEnded(ZoneKind k, int bonus)
        {
            if (bonus > 0) ShowBanner("ZONE CLEAR", $"+{bonus} coins", ZoneDirector.Tint(k), 1.2f);
        }

        void ShowBanner(string title, string sub, Color c, float hold)
        {
            _bannerTitle.text = title;
            _bannerTitle.color = c;
            _bannerSub.text = sub;
            _bannerT = 0f;
            _bannerHold = hold;
        }

        // ------------------------------------------------------------ frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            var cf = ComboFeverManager.Instance;

            // Barra
            float m = cf != null ? cf.Meter01 : 0f;
            _fill.anchorMax = new Vector2(Mathf.Lerp(_fill.anchorMax.x, m, 1f - Mathf.Exp(-dt * 14f)), 1f);
            bool ready = cf != null && cf.IsReady;
            bool fever = cf != null && cf.IsFever;
            _fillImg.color = fever ? Color.Lerp(BarFever, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 14f))
                : ready ? Color.Lerp(BarReady, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 8f))
                : BarFill;
            int chain = cf != null ? cf.Chain : 0;
            _chainText.text = chain > 1 ? $"x{chain}" : "";
            _chainText.color = Color.Lerp(BarFill, BarFever, Mathf.Clamp01(chain / 12f));

            // Botão
            bool showBtn = ready && !fever;
            if (_button.gameObject.activeSelf != showBtn) _button.gameObject.SetActive(showBtn);
            if (showBtn) _button.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 9f));

            // Fever ativo
            if (_feverText.gameObject.activeSelf != fever) _feverText.gameObject.SetActive(fever);
            if (fever)
            {
                _feverText.text = $"FEVER  ×{RunModifiers.CoinMultiplier}";
                _feverText.rectTransform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 12f));
            }

            // Zona
            var zd = ZoneDirector.Instance;
            bool zone = zd != null && zd.Active != ZoneKind.None;
            if (_zoneBar.gameObject.activeSelf != zone) _zoneBar.gameObject.SetActive(zone);
            if (zone)
            {
                _zoneFill.anchorMax = new Vector2(zd.Remaining01, 1f);
                _zoneImg.color = ZoneDirector.Tint(zd.Active);
            }

            // Banner (in 0.22s · hold · out 0.35s)
            if (_bannerT >= 0f)
            {
                _bannerT += dt;
                float a, s;
                if (_bannerT < 0.22f) { float k = _bannerT / 0.22f; a = k; s = Mathf.Lerp(1.4f, 1f, EaseOut(k)); }
                else if (_bannerT < 0.22f + _bannerHold) { a = 1f; s = 1f; }
                else
                {
                    float k = (_bannerT - 0.22f - _bannerHold) / 0.35f;
                    a = 1f - k; s = 1f + 0.1f * k;
                    if (k >= 1f) { _bannerT = -1f; a = 0f; }
                }
                _bannerGroup.alpha = a;
                _bannerGroup.transform.localScale = Vector3.one * s;
            }

            // Popups
            for (int i = 0; i < _pops.Count; i++)
            {
                var p = _pops[i];
                if (!p.live) continue;
                p.age += dt;
                float k = p.age / 0.9f;
                if (k >= 1f) { p.live = false; p.t.gameObject.SetActive(false); _pops[i] = p; continue; }
                float pop = k < 0.12f ? Mathf.Lerp(0.4f, 1.25f, k / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.12f) / 0.2f));
                p.rt.anchoredPosition = p.start + new Vector2(0, 160f * EaseOut(k));
                p.rt.localScale = Vector3.one * pop;
                var c = p.t.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; p.t.color = c;
                _pops[i] = p;
            }
        }

        static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
    }
}
