using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RailSwitchMVP.InputSys
{
    /// <summary>
    /// Touch input pra switch lane em mobile. Dois modos:
    ///
    /// - <b>TapZones (default)</b>: tap na metade esquerda → -1, metade direita → +1.
    ///   Detecta no frame em que `primaryTouch.press.wasPressedThisFrame == true`,
    ///   matching o "consume on key down" do KeyboardDirectionalInput.
    ///   Mais previsível pra switch game (entrada binária = geometria binária).
    ///
    /// - <b>Swipe</b>: gesto horizontal Began→Ended com threshold por fração da
    ///   largura. Menos previsível em telas pequenas / dedos lentos.
    ///
    /// Touches sobre UI (botões USE / TELE / DBG / SPW) são ignorados via
    /// EventSystem.RaycastAll — permite ter botões na tela sem que toques
    /// neles disparem switch.
    ///
    /// Em desktop sem Touchscreen, Touchscreen.current == null e Update é no-op.
    /// </summary>
    public class TouchDirectionalInput : MonoBehaviour, IDirectionalInput
    {
        public enum TouchMode { TapZones, Swipe }

        [Header("Mode")]
        [Tooltip("TapZones (default) = tap em qualquer ponto da metade esq/dir = switch. " +
            "Swipe = gesto horizontal com threshold.")]
        [SerializeField] private TouchMode mode = TouchMode.TapZones;

        [Header("Tap Zones")]
        [Tooltip("Fração da largura da tela que define a divisória. 0.5 = metade. " +
            "Default 0.5 (não tem zona morta no meio).")]
        [Range(0.3f, 0.7f)]
        public float zoneSplit = 0.5f;

        [Tooltip("Margem morta no centro em fração de largura. 0 = sem dead zone. " +
            "0.05 = 5% da largura ignorados ao redor da divisória (evita tap ambíguo).")]
        [Range(0f, 0.2f)]
        public float deadZoneFraction = 0f;

        [Header("Swipe (só usado se Mode = Swipe)")]
        [Tooltip("Distância mínima de swipe como fração da largura da tela. 0.06 = 6%.")]
        [Range(0.02f, 0.3f)]
        public float minSwipeFraction = 0.06f;

        [Tooltip("Distância mínima absoluta em pixels (piso pra telas muito pequenas).")]
        public float minSwipePixels = 40f;

        [Tooltip("Ratio max vertical/horizontal. 1.0 = aceita até 45°.")]
        [Range(0.2f, 2f)]
        public float maxVerticalRatio = 1f;

        [Tooltip("Tempo máximo entre Begin e End pra contar como swipe (segundos).")]
        public float maxSwipeTime = 0.6f;

        // ---- runtime ----
        private int _queuedDir;

        // swipe state
        private Vector2 _startPos;
        private float _startTime;
        private bool _tracking;
        private bool _startedOverUI;

        // reusável pra evitar GC
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private PointerEventData _pointer;

        public int ConsumeDirection()
        {
            int d = _queuedDir;
            _queuedDir = 0;
            return d;
        }

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null) return;

            if (mode == TouchMode.TapZones) UpdateTapZones(ts);
            else UpdateSwipe(ts);
        }

        // ============================================================
        // Tap zones — usa wasPressedThisFrame (down edge confiável)
        // ============================================================
        void UpdateTapZones(Touchscreen ts)
        {
            var touch = ts.primaryTouch;
            if (!touch.press.wasPressedThisFrame) return;

            Vector2 pos = touch.position.ReadValue();
            if (IsOverUI(pos)) return;

            float splitX = Screen.width * zoneSplit;
            float dead = Screen.width * deadZoneFraction * 0.5f;

            if (pos.x < splitX - dead) _queuedDir = -1;
            else if (pos.x > splitX + dead) _queuedDir = 1;
            // else: dentro da dead zone, ignora
        }

        // ============================================================
        // Swipe — mantido pra A/B test, não é default
        // ============================================================
        void UpdateSwipe(Touchscreen ts)
        {
            var touch = ts.primaryTouch;
            var phase = touch.phase.ReadValue();
            var pos = touch.position.ReadValue();

            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _startPos = pos;
                    _startTime = Time.unscaledTime;
                    _tracking = true;
                    _startedOverUI = IsOverUI(pos);
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                    if (_tracking && !_startedOverUI) TryQueueSwipe(pos);
                    _tracking = false;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _tracking = false;
                    break;
            }
        }

        void TryQueueSwipe(Vector2 endPos)
        {
            float dt = Time.unscaledTime - _startTime;
            if (dt > maxSwipeTime) return;

            Vector2 delta = endPos - _startPos;
            float minPx = Mathf.Max(minSwipePixels, Screen.width * minSwipeFraction);
            if (Mathf.Abs(delta.x) < minPx) return;
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * maxVerticalRatio) return;

            _queuedDir = delta.x > 0f ? 1 : -1;
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;

            if (_pointer == null) _pointer = new PointerEventData(es);
            _pointer.position = screenPos;
            _raycastResults.Clear();
            es.RaycastAll(_pointer, _raycastResults);
            return _raycastResults.Count > 0;
        }
    }
}
