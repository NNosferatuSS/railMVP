using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Visual do switch no modelo "ponte elevadiça vindo de baixo": três meshes
    /// pré-existentes (uma por direção) giram no eixo X local. A direção
    /// SELECIONADA sobe pra 0°, as outras duas ficam em -90° (penduradas
    /// pra baixo, fora do caminho).
    ///
    /// Espera que cada mesh já venha com o pivot na dobradiça — a rotação é
    /// aplicada direto no Transform referenciado.
    ///
    /// Não decide nada de gameplay: só reflete o state do SwitchController.
    /// Timing/easing vêm de um SwitchVisualConfig (preset).
    /// </summary>
    public class SwitchVisual : MonoBehaviour
    {
        [Header("Meshes por direção (pivot na dobradiça)")]
        [Tooltip("Mesh da direção LEFT. Gira em X entre lowered e raised.")]
        [SerializeField] private Transform leftMesh;

        [Tooltip("Mesh da direção MIDDLE.")]
        [SerializeField] private Transform middleMesh;

        [Tooltip("Mesh da direção RIGHT.")]
        [SerializeField] private Transform rightMesh;

        [Header("Preset")]
        [Tooltip("Ângulos, durações e easing. Se vazio, usa fallbacks internos " +
            "(0 / -90, 0.18s subindo, 0.12s descendo, ease-out).")]
        [SerializeField] private SwitchVisualConfig config;

        // Fallbacks quando config == null.
        private const float FallbackRaised = 0f;
        private const float FallbackLowered = -90f;
        private const float FallbackRaiseDuration = 0.18f;
        private const float FallbackLowerDuration = 0.12f;

        // Estado do tween de uma mesh.
        struct Slot
        {
            public Transform t;
            public Renderer renderer;
            public Material originalMaterial;
            public float from;       // ângulo X no início do tween
            public float to;         // ângulo X alvo
            public float elapsed;    // tempo desde o início (inclui delay)
            public float delay;
            public float duration;
            public bool raising;     // true = usa a curva de subir
            public bool animating;
        }

        private readonly Slot[] _slots = new Slot[3];
        private bool _resolved;
        private SwitchState _current = SwitchState.Middle;

        /// <summary>Config ativo (pode ser null, aí valem os fallbacks).</summary>
        public SwitchVisualConfig Config => config;

        /// <summary>Direção atualmente levantada.</summary>
        public SwitchState Current => _current;

        public bool HasAllMeshes => leftMesh != null && middleMesh != null && rightMesh != null;

        void Awake() => Resolve();

        void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            AssignSlot(0, leftMesh);
            AssignSlot(1, middleMesh);
            AssignSlot(2, rightMesh);
        }

        void AssignSlot(int i, Transform t)
        {
            _slots[i].t = t;
            if (t == null) return;
            _slots[i].renderer = t.GetComponent<Renderer>();
            if (_slots[i].renderer == null) _slots[i].renderer = t.GetComponentInChildren<Renderer>();
            if (_slots[i].renderer != null) _slots[i].originalMaterial = _slots[i].renderer.sharedMaterial;
        }

        /// <summary>Injeta o preset em runtime (o gerador faz isso no spawn).</summary>
        public void SetConfig(SwitchVisualConfig cfg) => config = cfg;

        static int IndexOf(SwitchState s) => (int)s + 1;

        bool Extend => config != null && config.animMode == SwitchVisualConfig.AnimMode.ExtendZ;

        // "Valor" do slot: ângulo X (HingeX) ou escala Z (ExtendZ, 0..1).
        float RaisedAngle => Extend ? 1f : (config != null ? config.raisedAngleX : FallbackRaised);
        float LoweredAngle => Extend ? 0f : (config != null ? config.loweredAngleX : FallbackLowered);

        /// <summary>
        /// Sobe a mesh da direção pedida e baixa as outras duas.
        /// animate = false aplica o ângulo no mesmo frame (usado no spawn/reset
        /// do pool, pra tile reciclado não animar na cara do jogador).
        /// </summary>
        public void Apply(SwitchState state, bool animate)
        {
            Resolve();
            _current = state;

            int selected = IndexOf(state);

            if (!animate)
            {
                for (int i = 0; i < _slots.Length; i++)
                    SetImmediate(i, i == selected ? RaisedAngle : LoweredAngle);
                return;
            }

            // As que descem começam primeiro (sem delay).
            bool anyLowering = false;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == selected) continue;
                anyLowering |= StartTween(i, LoweredAngle, raising: false);
            }

            // Modo sequenciado: a nova só sobe depois da antiga terminar de
            // descer. Se nada estava descendo, não há o que esperar.
            float extraDelay = 0f;
            if (anyLowering && config != null && config.sequenceLowerThenRaise)
                extraDelay = config.lowerDuration;

            StartTween(selected, RaisedAngle, raising: true, extraDelay);
        }

        void SetImmediate(int i, float angle)
        {
            _slots[i].animating = false;
            if (_slots[i].t == null) return;
            WriteValue(i, angle);
        }

        float ReadValue(int i)
        {
            var t = _slots[i].t;
            return Extend ? t.localScale.z : NormalizeAngle(t.localEulerAngles.x);
        }

        void WriteValue(int i, float v)
        {
            var t = _slots[i].t;
            if (Extend)
            {
                var s = t.localScale;
                t.localScale = new Vector3(s.x, s.y, Mathf.Max(0f, v));
                // Recolhida = sem draw call.
                if (_slots[i].renderer != null) _slots[i].renderer.enabled = v > 0.001f;
                return;
            }
            var e = t.localEulerAngles;
            t.localEulerAngles = new Vector3(v, e.y, e.z);
        }

        /// <summary>
        /// Inicia o tween de uma mesh. Retorna false se não havia nada a fazer
        /// (mesh ausente, ou já parada no ângulo alvo) — o modo sequenciado usa
        /// esse retorno pra saber se precisa esperar alguém descer.
        /// </summary>
        bool StartTween(int i, float target, bool raising, float extraDelay = 0f)
        {
            if (_slots[i].t == null) return false;

            float from = ReadValue(i);
            // Já está no alvo e parado: nada a fazer.
            if (!_slots[i].animating && Mathf.Abs(from - target) < 0.01f) return false;

            _slots[i].from = from;
            _slots[i].to = target;
            _slots[i].elapsed = 0f;
            _slots[i].raising = raising;
            _slots[i].duration = raising
                ? (config != null ? config.raiseDuration : FallbackRaiseDuration)
                : (config != null ? config.lowerDuration : FallbackLowerDuration);
            // O beat de "a antiga baixa, depois a nova sobe" — só quem sobe espera.
            _slots[i].delay = raising
                ? (config != null ? config.raiseDelay : 0f) + extraDelay
                : 0f;
            _slots[i].animating = true;
            return true;
        }

        void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].animating) continue;
                if (_slots[i].t == null) { _slots[i].animating = false; continue; }

                _slots[i].elapsed += Time.deltaTime;

                float active = _slots[i].elapsed - _slots[i].delay;
                if (active < 0f) continue; // ainda no delay — mesh fica parada

                float duration = Mathf.Max(0.0001f, _slots[i].duration);
                float t = Mathf.Clamp01(active / duration);
                float eased = _slots[i].raising
                    ? (config != null ? config.EvaluateRaise(t) : EaseOut(t))
                    : (config != null ? config.EvaluateLower(t) : EaseOut(t));

                float angle = Mathf.LerpUnclamped(_slots[i].from, _slots[i].to, eased);

                if (t >= 1f)
                {
                    angle = _slots[i].to;
                    _slots[i].animating = false;
                }

                WriteValue(i, angle);
            }
        }

        static float EaseOut(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        // Euler vem sempre em 0..360; converte pra -180..180 pra que o lerp
        // de -90 para 0 não dê a volta longa (270 -> 360).
        static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        /// <summary>Renderer da mesh de uma direção (null se não atribuída).</summary>
        public Renderer GetRenderer(SwitchState s)
        {
            Resolve();
            return _slots[IndexOf(s)].renderer;
        }

        /// <summary>
        /// Material que a mesh tinha no prefab. Usado pra devolver as meshes
        /// baixadas ao normal depois que uma delas foi pintada de verde/vermelho.
        /// </summary>
        public Material GetOriginalMaterial(SwitchState s)
        {
            Resolve();
            return _slots[IndexOf(s)].originalMaterial;
        }
    }
}
