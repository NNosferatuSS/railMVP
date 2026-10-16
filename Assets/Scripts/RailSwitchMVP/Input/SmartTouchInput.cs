using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Input de toque com 3 formas simultâneas de trocar o conector (e mouse no Editor):
    ///
    /// 1. TOQUE NO TRILHO (prioridade): tocar num trilho à frente (ou na ponta dele)
    ///    aponta o conector direto pra ele. Trilho a 2+ linhas ou na mesma linha ao
    ///    lado = um passo na direção dele. Mira assistida: pega o trilho mais
    ///    próximo do dedo dentro de um raio generoso (fração da largura da tela).
    /// 2. TAP NA METADE DA TELA: se o toque não caiu perto de nenhum trilho.
    /// 3. SWIPE ESQ/DIR: se o dedo deslizar, o swipe SOBRESCREVE o que o toque fez
    ///    (estado = estado no momento do toque ± 1) — o jogador nunca "perde" o gesto.
    ///
    /// Toques sobre UI são ignorados (botões do HUD não trocam o conector).
    /// Produz SwitchCommand (ISwitchCommandInput); ConsumeDirection sempre 0.
    /// </summary>
    public class SmartTouchInput : MonoBehaviour, IDirectionalInput, ISwitchCommandInput
    {
        [Header("Toque no trilho")]
        [Tooltip("Raio de mira em fração da largura da tela. 0.09 ≈ 1/11 da largura.")]
        [Range(0.02f, 0.25f)] [SerializeField] private float pickRadius = 0.09f;
        [Tooltip("Quantas linhas à frente aceitam toque.")]
        [SerializeField] private int pickRowsAhead = 5;
        [Tooltip("Penalidade (fração de largura) por linha de distância — desempata a favor da próxima linha.")]
        [SerializeField] private float rowPenalty = 0.012f;

        [Header("Tap nas metades")]
        [Range(0.3f, 0.7f)] [SerializeField] private float zoneSplit = 0.5f;

        [Header("Swipe")]
        [Range(0.02f, 0.3f)] [SerializeField] private float minSwipeFraction = 0.06f;
        [SerializeField] private float minSwipePixels = 40f;
        [Range(0.2f, 2f)] [SerializeField] private float maxVerticalRatio = 1f;
        [SerializeField] private float maxSwipeTime = 0.6f;

        public static event System.Action<SwitchCommandKind> CommandIssued;

        readonly Queue<SwitchCommand> _queue = new Queue<SwitchCommand>();
        bool _tracking, _swiped;
        Vector2 _startPos;
        float _startTime;
        TrackTile _pressTile;
        int _pressState;

        PlayerRailRider _rider;
        Camera _cam;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        PointerEventData _ped;

        public int ConsumeDirection() => 0;

        void Awake()
        {
            // Feedback visual do toque (trilho tocado + conector que trocou).
            if (GetComponent<RailSwitchMVP.UI.TapFeedbackUI>() == null) gameObject.AddComponent<RailSwitchMVP.UI.TapFeedbackUI>();
        }

        public bool TryConsumeCommand(out SwitchCommand cmd)
        {
            if (_queue.Count > 0) { cmd = _queue.Dequeue(); return true; }
            cmd = default;
            return false;
        }

        void Update()
        {
            if (!ReadPointer(out bool down, out bool held, out bool up, out Vector2 pos)) return;

            if (down)
            {
                if (IsOverUI(pos)) { _tracking = false; return; }
                _tracking = true;
                _swiped = false;
                _startPos = pos;
                _startTime = Time.unscaledTime;
                CapturePressState();

                if (TryPick(pos, out int row, out int lane))
                {
                    // Próxima linha (ou ao lado): liga o SEU conector nele.
                    // 2+ linhas à frente: o trilho da sua rota logo antes dele conecta nele.
                    bool far = _pressTile != null && row >= _pressTile.Row + 2;
                    Emit(new SwitchCommand
                    {
                        kind = far ? SwitchCommandKind.RouteTile : SwitchCommandKind.TargetTile,
                        targetRow = row, targetLane = lane,
                    });
                }
                else
                    Emit(new SwitchCommand { kind = SwitchCommandKind.Relative, dir = pos.x < Screen.width * zoneSplit ? -1 : 1 });
                return;
            }

            if (!_tracking) return;

            if (held && !_swiped && TrySwipe(pos, out int dir))
            {
                // Swipe reconhecido no meio do gesto (responde antes de soltar o dedo).
                _swiped = true;
                Emit(new SwitchCommand { kind = SwitchCommandKind.OverrideFromPress, dir = dir, pressTile = _pressTile, pressState = _pressState });
            }

            if (up)
            {
                if (!_swiped && TrySwipe(pos, out int d2))
                    Emit(new SwitchCommand { kind = SwitchCommandKind.OverrideFromPress, dir = d2, pressTile = _pressTile, pressState = _pressState });
                _tracking = false;
            }
        }

        void Emit(SwitchCommand c)
        {
            c.time = Time.unscaledTime;
            _queue.Enqueue(c);
            CommandIssued?.Invoke(c.kind);
        }

        void CapturePressState()
        {
            if (_rider == null) _rider = FindFirstObjectByType<PlayerRailRider>();
            _pressTile = _rider != null ? _rider.CurrentTile : null;
            _pressState = _pressTile != null && _pressTile.Switch != null ? (int)_pressTile.Switch.State : 0;
        }

        bool TrySwipe(Vector2 pos, out int dir)
        {
            dir = 0;
            if (Time.unscaledTime - _startTime > maxSwipeTime) return false;
            var d = pos - _startPos;
            float minPx = Mathf.Max(minSwipePixels, Screen.width * minSwipeFraction);
            if (Mathf.Abs(d.x) < minPx) return false;
            if (Mathf.Abs(d.y) > Mathf.Abs(d.x) * maxVerticalRatio) return false;
            dir = d.x > 0 ? 1 : -1;
            return true;
        }

        // ------------------------------------------------------------ mira no trilho

        bool TryPick(Vector2 screen, out int row, out int lane)
        {
            row = lane = -1;
            if (_cam == null) _cam = Camera.main;
            var rm = RailManager.Instance;
            if (_cam == null || rm == null || _pressTile == null) return false;

            float best = Screen.width * pickRadius;
            bool found = false;
            int r0 = _pressTile.Row;
            for (int r = r0; r <= r0 + pickRowsAhead; r++)
            {
                var data = rm.GetRow(r);
                if (data == null || data.Tiles == null) continue;
                foreach (var t in data.Tiles)
                {
                    if (t == null || t.StartPoint == null || t.EndPoint == null) continue;
                    if (t == _pressTile) continue;   // o próprio trilho não é alvo
                    float d = DistanceToTile(screen, t) + (r - r0) * rowPenalty * Screen.width;
                    if (d < best)
                    {
                        best = d;
                        row = t.Row;
                        lane = t.Lane;
                        found = true;
                    }
                }
            }
            return found;
        }

        // Distância em pixels do toque ao segmento do trilho projetado na tela
        // (StartPoint → EndPoint, mais a "ponta" de entrada um pouco antes).
        float DistanceToTile(Vector2 p, TrackTile t)
        {
            var a3 = _cam.WorldToScreenPoint(t.StartPoint.position + Vector3.back * 1.2f);
            var b3 = _cam.WorldToScreenPoint(t.EndPoint.position);
            if (a3.z < 0 || b3.z < 0) return float.MaxValue;
            Vector2 a = a3, b = b3;
            var ab = b - a;
            float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-3f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * k);
        }

        // ------------------------------------------------------------ ponteiro

        bool ReadPointer(out bool down, out bool held, out bool up, out Vector2 pos)
        {
            var ts = Touchscreen.current;
            if (ts != null && (ts.primaryTouch.press.isPressed || ts.primaryTouch.press.wasReleasedThisFrame))
            {
                var t = ts.primaryTouch;
                down = t.press.wasPressedThisFrame;
                up = t.press.wasReleasedThisFrame;
                held = t.press.isPressed;
                pos = t.position.ReadValue();
                return true;
            }
            var m = Mouse.current;
            if (m != null)
            {
                down = m.leftButton.wasPressedThisFrame;
                up = m.leftButton.wasReleasedThisFrame;
                held = m.leftButton.isPressed;
                pos = m.position.ReadValue();
                return down || up || held;
            }
            down = held = up = false;
            pos = default;
            return false;
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (_ped == null) _ped = new PointerEventData(es);
            _ped.position = screenPos;
            _hits.Clear();
            es.RaycastAll(_ped, _hits);
            return _hits.Count > 0;
        }
    }
}
