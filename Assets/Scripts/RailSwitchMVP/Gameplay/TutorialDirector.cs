using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.InputSys;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Gameplay
{
    /// <summary>
    /// Tutorial contextual (V2) — ensina DENTRO da corrida, sem pausar.
    ///
    /// Primeira run (PlayerPrefs RailMVP.Tutorial.SeenV2 = 0):
    ///  1. "Toque na esquerda ou direita" (metades da tela pulsando) → qualquer troca.
    ///  2. "Toque direto no trilho que você quer" (moldura num trilho alcançável) → toque no trilho.
    ///  3. "Ou deslize ← →" → swipe (ou tempo).
    ///  Durante o tutorial (e 3 trilhos depois): velocidade ×0.7 e PROTEÇÃO TOTAL — o jogador
    ///  não perde: conector pro vazio é corrigido (rede de segurança aqui + RunModifiers.
    ///  TutorialProtection no PlayerRailRider), obstáculo não mata e não nascem hazards novos.
    /// Dicas avulsas (uma vez cada, em qualquer run): conector vermelho, Fever pronto, Ghost.
    ///
    /// Substitui o TutorialOverlayController V1 (painel que pausava o jogo).
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        public const string PrefsKey = "RailMVP.Tutorial.SeenV5";   // V5: toque à frente conecta pelo trilho anterior + proteção total
        const string TipRed = "RailMVP.Tip.Red";
        const string TipFever = "RailMVP.Tip.Fever";
        const string TipGhost = "RailMVP.Tip.Ghost";

        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Parâmetros do slow-motion e da velocidade do tutorial (Control Panel).")]
        [SerializeField] private GameplayLayerConfig config;
        [Tooltip("Progresso no trilho a partir do qual a rede de segurança corrige um conector apontando pro vazio.")]
        [Range(0.5f, 0.98f)] [SerializeField] private float safetyNetProgress = 0.86f;
        [SerializeField] private float targetStepTimeout = 12f;
        [SerializeField] private float swipeStepTimeout = 7f;

        GameplayLayerConfig _fallbackConfig;
        GameplayLayerConfig Cfg
        {
            get
            {
                if (config != null) return config;
                if (_fallbackConfig == null) _fallbackConfig = ScriptableObject.CreateInstance<GameplayLayerConfig>();
                return _fallbackConfig;
            }
        }

        // ---- slow-motion (bullet time) nas decisões do tutorial
        enum Slow { None, In, Hold, Out }
        Slow _slow = Slow.None;
        float _slowT, _slowFrom = 1f, _ownedScale = -1f;
        readonly HashSet<Step> _slowUsed = new HashSet<Step>();
        TrackTile _lastSlowTile;   // um slow-motion por trilho: entre passos o jogo volta ao ritmo

        enum Step { Off, WaitStart, TapHalves, TapTrack, TapAhead, Swipe, Done }
        Step _step = Step.Off;
        float _stepTime;

        PlayerRailRider _rider;
        Camera _cam;
        RectTransform _canvas, _left, _right, _frame;
        Image _leftImg, _rightImg;
        Image[] _frameEdges;
        TextMeshProUGUI _title, _sub;
        CanvasGroup _hintGroup;
        float _hintHold, _hintT = -1f;
        string _stepTitle, _stepSub;   // instrução do passo atual — volta depois de um aviso
        TrackTile _frameTarget;
        int _tilesSinceDone;
        const int ProtectionTilesAfterDone = 5;

        // Proteção vale do início do tutorial até alguns trilhos depois do "NICE!" final.
        bool Protecting => _step == Step.WaitStart || Active
                        || (_step == Step.Done && _ranTutorial && _tilesSinceDone < ProtectionTilesAfterDone);
        bool _ranTutorial;

        /// <summary>Apaga o progresso do tutorial (Control Panel / debug).</summary>
        public static void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.DeleteKey(TipRed);
            PlayerPrefs.DeleteKey(TipFever);
            PlayerPrefs.DeleteKey(TipGhost);
        }

        void Awake()
        {
            // Aposenta o tutorial V1 (painel modal que pausava) antes do Start dele.
            PlayerPrefs.SetInt("RailMVP.Tutorial.SeenV1", 1);
            BuildUI();
        }

        void Start()
        {
            _rider = FindFirstObjectByType<PlayerRailRider>();
            bool first = PlayerPrefs.GetInt(PrefsKey, 0) == 0;
            _step = first ? Step.WaitStart : Step.Done;
            _ranTutorial = first;

            SwitchController.AnyNudged += OnNudge;
            SmartTouchInput.CommandIssued += OnCommand;
            ComboFeverManager.FeverReady += OnFeverReady;
            if (_rider != null)
            {
                _rider.GhostFlightStarted += OnGhost;
                _rider.OnTileEntered += OnTileEntered;
                _rider.TutorialRescued += OnRescued;
            }
            PlayerRailRider.TapResolved += OnTapResolved;
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver += OnGameOver;
        }

        void OnDestroy()
        {
            SwitchController.AnyNudged -= OnNudge;
            SmartTouchInput.CommandIssued -= OnCommand;
            ComboFeverManager.FeverReady -= OnFeverReady;
            if (_rider != null)
            {
                _rider.GhostFlightStarted -= OnGhost;
                _rider.OnTileEntered -= OnTileEntered;
                _rider.TutorialRescued -= OnRescued;
            }
            PlayerRailRider.TapResolved -= OnTapResolved;
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= OnGameOver;
            if (_step != Step.Done && _step != Step.Off) RunModifiers.AssistSpeedMultiplier = 1f;
            RunModifiers.TutorialProtection = false;
            ReleaseTime();
        }

        bool Active => _step == Step.TapHalves || _step == Step.TapTrack || _step == Step.TapAhead || _step == Step.Swipe;

        // ------------------------------------------------------------ fluxo

        void Enter(Step s)
        {
            bool advancedDuringSlow = _slow == Slow.In || _slow == Slow.Hold;
            if (advancedDuringSlow) EndSlow();
            _step = s;
            _stepTime = Time.unscaledTime;
            switch (s)
            {
                case Step.TapHalves:
                    Hint("TAP LEFT OR RIGHT", "to point the connector to the next track", 999f);
                    break;
                case Step.TapTrack:
                    Hint("TAP THE TRACK YOU WANT", "the connector snaps to it instantly", 999f);
                    break;
                case Step.TapAhead:
                    Hint("TAP A TRACK FURTHER AHEAD", "the track before it connects to it", 999f);
                    break;
                case Step.Swipe:
                    Hint("OR SWIPE  ←  →", "all three ways work together", 999f);
                    break;
                case Step.Done:
                    _stepTitle = null;
                    Hint("NICE!", "switch at the last moment for a CLUTCH and fill the FEVER", 2.6f);
                    PlayerPrefs.SetInt(PrefsKey, 1);
                    PlayerPrefs.Save();
                    RunModifiers.AssistSpeedMultiplier = 1f;
                    break;
            }
            if (Active) RunModifiers.AssistSpeedMultiplier = Cfg.tutorialSpeedMultiplier;
            // Acertou no slow-motion: reforço rápido antes da próxima instrução.
            if (advancedDuringSlow && s != Step.Done) Hint("NICE!", "", 0.6f, keepStep: true);
        }

        void OnNudge(SwitchState s)
        {
            if (_step == Step.TapHalves) Enter(Step.TapTrack);
        }

        void OnCommand(SwitchCommandKind k)
        {
            if (_step == Step.TapTrack && k == SwitchCommandKind.TargetTile) Enter(Step.TapAhead);
            else if (_step == Step.Swipe && k == SwitchCommandKind.OverrideFromPress) Enter(Step.Done);
        }

        // Passo "mais à frente": conta quando um toque distante conectou de verdade.
        void OnTapResolved(TrackTile tapped, TrackTile changed, bool ok)
        {
            if (_step != Step.TapAhead || !ok || changed == null || _rider == null) return;
            if (changed != _rider.CurrentTile) Enter(Step.Swipe);
        }

        void OnTileEntered(TrackTile t)
        {
            if (_step == Step.Done) _tilesSinceDone++;
        }

        void OnRescued()
        {
            if (_step != Step.Done) Hint("WHOA! RED = FALL", "point the connector to a track (green)", 2.2f, keepStep: true);
        }

        void OnFeverReady()
        {
            if (PlayerPrefs.GetInt(TipFever, 0) == 1) return;
            PlayerPrefs.SetInt(TipFever, 1);
            Hint("FEVER READY!", "tap RELEASE FEVER: 8 s of coins ×2 + magnet", 3.2f);
        }

        void OnGhost()
        {
            if (PlayerPrefs.GetInt(TipGhost, 0) == 1) return;
            PlayerPrefs.SetInt(TipGhost, 1);
            Hint("GHOST!", "you float over the void while Ghost lasts", 2.6f);
        }

        void OnGameOver(GameOverReason r)
        {
            RunModifiers.TutorialProtection = false;
            if (Active) RunModifiers.AssistSpeedMultiplier = 1f;
            ReleaseTime();
            HideHint();
        }

        void Update()
        {
            // Vale desde o warmup: a 1ª decisão real (row 1 → 2) acontece antes do GO.
            bool playing = (GameManager.Instance == null || GameManager.Instance.IsActive) && _rider != null && _rider.CurrentTile != null;

            if (_step == Step.WaitStart && playing) Enter(Step.TapHalves);   // começa no GO
            if (_step == Step.TapTrack && Time.unscaledTime - _stepTime > targetStepTimeout) Enter(Step.TapAhead);
            if (_step == Step.TapAhead && Time.unscaledTime - _stepTime > targetStepTimeout) Enter(Step.Swipe);
            if (_step == Step.Swipe && Time.unscaledTime - _stepTime > swipeStepTimeout) Enter(Step.Done);

            RunModifiers.TutorialProtection = Protecting;
            if (Protecting && playing) SafetyNet();
            if (Active && playing) MaybeStartSlow();
            TickSlow();
            if (playing) RedConnectorTip();

            UpdateVisuals();
        }

        // ------------------------------------------------------------ slow-motion

        // Entra uma vez por passo, no trilho em que há decisão (2+ opções ou conector pro vazio).
        void MaybeStartSlow()
        {
            if (_slow != Slow.None || _slowUsed.Contains(_step)) return;
            var t = _rider.CurrentTile;
            if (t == null || t.StartPoint == null || t.EndPoint == null || t == _lastSlowTile) return;
            float p = Mathf.InverseLerp(t.StartPoint.position.z, t.EndPoint.position.z, _rider.transform.position.z);
            if (p < Cfg.tutorialSlowTriggerProgress || p > 0.9f) return;
            if (!HasChoiceAhead() && !TargetIsVoid(t)) return;
            _slowUsed.Add(_step);
            _lastSlowTile = t;
            Debug.Log($"[Tutorial] slow-motion IN (passo {_step}, trilho {t.Row}/{t.Lane}, p={p:0.00})");
            _slow = Slow.In;
            _slowT = 0f;
            _slowFrom = Time.timeScale;
        }

        bool TargetIsVoid(TrackTile t)
        {
            var rm = RailManager.Instance;
            var next = rm != null ? rm.GetRow(t.Row + 1) : null;
            return t.Switch != null && next != null && !next.HasTile(t.Switch.TargetLane);
        }

        void EndSlow()
        {
            if (_slow == Slow.None || _slow == Slow.Out) return;
            Debug.Log($"[Tutorial] slow-motion OUT (passo {_step}, após {_slowT:0.0}s)");
            _slow = Slow.Out;
            _slowT = 0f;
            _slowFrom = Time.timeScale;
        }

        void TickSlow()
        {
            if (_slow == Slow.None) return;
            float dt = Time.unscaledDeltaTime;
            _slowT += dt;
            var cfg = Cfg;
            float target;
            switch (_slow)
            {
                case Slow.In:
                    target = Mathf.Lerp(_slowFrom, cfg.tutorialSlowTimeScale, Smooth(_slowT / cfg.tutorialSlowRampIn));
                    if (_slowT >= cfg.tutorialSlowRampIn) { _slow = Slow.Hold; _slowT = 0f; }
                    break;
                case Slow.Hold:
                    target = cfg.tutorialSlowTimeScale;
                    if (_slowT >= cfg.tutorialSlowMaxHold) EndSlow();   // ninguém fica preso no tutorial
                    break;
                default: // Out
                    target = Mathf.Lerp(_slowFrom, 1f, Smooth(_slowT / cfg.tutorialSlowRampOut));
                    if (_slowT >= cfg.tutorialSlowRampOut) { target = 1f; _slow = Slow.None; }
                    break;
            }
            ClaimTime(target);
            if (_slow == Slow.None) ReleaseTime();
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        // Só mexe no Time.timeScale se ninguém mais estiver mexendo (pause = 0, TimeFreeze,
        // câmera de morte...): ou está em 1, ou no valor que o próprio tutorial escreveu.
        void ClaimTime(float scale)
        {
            float cur = Time.timeScale;
            bool ours = Mathf.Approximately(cur, 1f) || (_ownedScale >= 0f && Mathf.Abs(cur - _ownedScale) < 1e-4f);
            if (!ours) return;
            Time.timeScale = scale;
            _ownedScale = scale;
        }

        void ReleaseTime()
        {
            if (_ownedScale >= 0f && Mathf.Abs(Time.timeScale - _ownedScale) < 1e-4f) Time.timeScale = 1f;
            _ownedScale = -1f;
            _slow = Slow.None;
        }

        bool HasChoiceAhead()
        {
            var t = _rider != null ? _rider.CurrentTile : null;
            var rm = RailManager.Instance;
            if (t == null || rm == null) return false;
            var next = rm.GetRow(t.Row + 1);
            if (next == null) return false;
            int n = 0;
            for (int d = -1; d <= 1; d++) if (next.HasTile(t.Lane + d)) n++;
            return n >= 2;
        }

        // Rede de segurança: no fim do trilho, conector pro vazio → corrige pra uma lane válida.
        void SafetyNet()
        {
            var t = _rider.CurrentTile;
            var rm = RailManager.Instance;
            if (t == null || t.Switch == null || rm == null || t.StartPoint == null) return;
            float p = Mathf.InverseLerp(t.StartPoint.position.z, t.EndPoint.position.z, _rider.transform.position.z);
            if (p < safetyNetProgress) return;
            var next = rm.GetRow(t.Row + 1);
            if (next == null) return;
            if (next.HasTile(t.Switch.TargetLane)) return;
            foreach (int s in new[] { 0, -1, 1 })
            {
                if (!next.HasTile(t.Lane + s)) continue;
                t.Switch.SetState((SwitchState)s);
                Hint("WHOA! RED = FALL", "point the connector to a track (green)", 2.2f, keepStep: true);
                return;
            }
        }

        void RedConnectorTip()
        {
            if (PlayerPrefs.GetInt(TipRed, 0) == 1 || Active) return;
            var t = _rider != null ? _rider.CurrentTile : null;
            var rm = RailManager.Instance;
            if (t == null || t.Switch == null || rm == null) return;
            var next = rm.GetRow(t.Row + 1);
            if (next == null || next.HasTile(t.Switch.TargetLane)) return;
            PlayerPrefs.SetInt(TipRed, 1);
            Hint("RED CONNECTOR = FALL", "switch before the track ends", 2.4f);
        }

        // ------------------------------------------------------------ UI

        void BuildUI()
        {
            var go = new GameObject("Tutorial_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 7;   // acima do HUD de jogo, abaixo de Warmup (8), GameOver (10) e Pause (12)
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 2340);
            sc.matchWidthOrHeight = 0.5f;
            _canvas = (RectTransform)go.transform;

            _left = Stretch("Left", new Vector2(0, 0), new Vector2(0.5f, 1));
            _right = Stretch("Right", new Vector2(0.5f, 0), new Vector2(1, 1));
            _leftImg = Fill(_left, new Color(0.35f, 1f, 0.85f, 0f));
            _rightImg = Fill(_right, new Color(0.35f, 1f, 0.85f, 0f));

            _frame = Node("TargetFrame", _canvas);
            _frameEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var e = Node($"Edge{i}", _frame);
                _frameEdges[i] = Fill(e, new Color(1f, 0.9f, 0.4f, 0.95f));
            }
            _frame.gameObject.SetActive(false);

            var hint = Node("Hint", _canvas);
            hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.36f);
            hint.sizeDelta = new Vector2(1000, 240);
            _hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.alpha = 0;
            _hintGroup.blocksRaycasts = false;
            _title = Label(hint, 64, new Vector2(0.5f, 0.66f));
            _sub = Label(hint, 38, new Vector2(0.5f, 0.24f));
            _sub.color = new Color(1, 1, 1, 0.85f);
        }

        RectTransform Node(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        RectTransform Stretch(string n, Vector2 min, Vector2 max)
        {
            var rt = Node(n, _canvas);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image Fill(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI Label(RectTransform parent, float size, Vector2 anchor)
        {
            var rt = Node("Text", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(1000, size * 1.6f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(4, 10, 30, 220);
            return t;
        }

        void Hint(string title, string sub, float hold, bool keepStep = false)
        {
            if (hold >= 999f) { _stepTitle = title; _stepSub = sub; }   // instrução persistente do passo
            else if (!keepStep && !Active) { _stepTitle = null; _stepSub = null; }
            _title.text = title;
            _sub.text = sub;
            _hintT = 0f;
            _hintHold = hold;
        }

        void HideHint() => _hintT = -1f;

        void UpdateVisuals()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // Pausado (Pause / Revive / Game Over): o tutorial some da tela.
            bool hidden = Time.timeScale <= 0.001f;
            if (_canvas.gameObject.activeSelf == hidden) _canvas.gameObject.SetActive(!hidden);
            if (hidden) return;

            // Metades da tela pulsando (passo 1)
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            float halfA = _step == Step.TapHalves ? 0.05f + 0.07f * pulse : 0f;
            SetAlpha(_leftImg, halfA);
            SetAlpha(_rightImg, halfA);

            // Moldura no trilho-alvo (passo 2)
            bool showFrame = (_step == Step.TapTrack && UpdateFrameTarget())
                          || (_step == Step.TapAhead && UpdateAheadTarget());
            if (_frame.gameObject.activeSelf != showFrame) _frame.gameObject.SetActive(showFrame);
            if (showFrame) PlaceFrame(pulse);

            // Dica
            if (_hintT >= 0f)
            {
                _hintT += dt;
                float a = _hintT < 0.2f ? _hintT / 0.2f : (_hintT < 0.2f + _hintHold ? 1f : 1f - (_hintT - 0.2f - _hintHold) / 0.35f);
                if (a <= 0f && _hintT > 0.2f)
                {
                    _hintT = -1f; a = 0f;
                    // Aviso passou: a instrução do passo volta.
                    if (Active && _stepTitle != null) Hint(_stepTitle, _stepSub, 999f);
                }
                _hintGroup.alpha = Mathf.Clamp01(a);
                _hintGroup.transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 6f));
            }
            else _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, 0f, dt * 4f);
        }

        static void SetAlpha(Image i, float a) { var c = i.color; c.a = a; i.color = c; }

        bool UpdateFrameTarget()
        {
            var cur = _rider != null ? _rider.CurrentTile : null;
            var rm = RailManager.Instance;
            if (cur == null || cur.Switch == null || rm == null) return false;
            var next = rm.GetRow(cur.Row + 1);
            if (next == null || next.Tiles == null) return false;
            // Trilho alcançável DIFERENTE do que o conector já aponta (pra o toque mudar algo).
            _frameTarget = null;
            foreach (int d in new[] { -1, 1, 0 })
            {
                int lane = cur.Lane + d;
                if (lane == cur.Switch.TargetLane || !next.HasTile(lane)) continue;
                _frameTarget = next.Tiles[lane];
                break;
            }
            return _frameTarget != null;
        }

        // Passo "mais à frente": um trilho 2 linhas adiante que a rota ainda NÃO usa, mas que o
        // trilho da rota na linha anterior alcança (tocar nele muda aquele conector).
        bool UpdateAheadTarget()
        {
            var cur = _rider != null ? _rider.CurrentTile : null;
            var rm = RailManager.Instance;
            if (cur == null || cur.Switch == null || rm == null) return false;
            var r1 = rm.GetRow(cur.Row + 1);
            if (r1 == null || !r1.HasTile(cur.Switch.TargetLane)) return false;
            var t1 = r1.Tiles[cur.Switch.TargetLane];
            var r2 = rm.GetRow(cur.Row + 2);
            if (t1 == null || t1.Switch == null || r2 == null) return false;
            foreach (int d in new[] { -1, 1, 0 })
            {
                int lane = t1.Lane + d;
                if (lane == t1.Switch.TargetLane || !r2.HasTile(lane)) continue;
                _frameTarget = r2.Tiles[lane];
                return true;
            }
            return false;
        }

        void PlaceFrame(float pulse)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _frameTarget == null) return;
            var a = _cam.WorldToScreenPoint(_frameTarget.StartPoint.position);
            var b = _cam.WorldToScreenPoint(_frameTarget.EndPoint.position);
            if (a.z < 0 || b.z < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, a, null, out var la);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, b, null, out var lb);
            float w = 120f + 20f * pulse;
            var center = (la + lb) * 0.5f;
            float h = Mathf.Abs(lb.y - la.y) + 40f + 20f * pulse;
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.anchoredPosition = center;
            _frame.sizeDelta = new Vector2(w, h);
            const float th = 8f;
            SetEdge(0, new Vector2(0, h / 2), new Vector2(w, th));
            SetEdge(1, new Vector2(0, -h / 2), new Vector2(w, th));
            SetEdge(2, new Vector2(-w / 2, 0), new Vector2(th, h));
            SetEdge(3, new Vector2(w / 2, 0), new Vector2(th, h));
        }

        void SetEdge(int i, Vector2 pos, Vector2 size)
        {
            var rt = _frameEdges[i].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
