using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Track
{
    public enum SwitchState
    {
        Left = -1,
        Middle = 0,
        Right = 1
    }

    /// <summary>
    /// Conector de 3 posições no fim de cada tile. Define qual lane da
    /// próxima linha o player vai tentar atingir ao sair do tile.
    ///
    /// O visual é delegado ao SwitchVisual (3 meshes, ponte elevadiça). Se não
    /// houver SwitchVisual, cai no visual legado de seta única girando em Y.
    /// </summary>
    public class SwitchController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private SwitchState state = SwitchState.Middle;

        [Header("References")]
        [Tooltip("Tile dono deste switch — atribuído pelo TrackTile no Awake.")]
        public TrackTile OwnerTile;

        [Tooltip("Visual de 3 meshes (ponte elevadiça). Auto-resolvido nos children " +
            "se ficar vazio.")]
        [SerializeField] private SwitchVisual visual;

        [Tooltip("LEGADO: seta única que rotaciona em Y. Só é usada se não houver " +
            "SwitchVisual atribuído.")]
        public Transform ArrowVisual;

        // Config injetado pelo gerador no spawn — fonte do ângulo da seta legada.
        // null = usa o default (45°). Mantido por instância pra ler ao vivo.
        private RailGenConfig _config;
        private const float DefaultDegreesPerStep = 45f;

        public SwitchState State => state;

        /// <summary>Visual de 3 meshes (pode ser null em prefabs antigos).</summary>
        public SwitchVisual Visual => visual;

        /// <summary>Lane absoluta para a qual o switch aponta na próxima linha.</summary>
        public int TargetLane => (OwnerTile != null ? OwnerTile.Lane : 0) + (int)state;

        /// <summary>
        /// Disparado quando o state muda (via Nudge ou SetState). Usado pelo
        /// TrackTile pra atualizar a cor do connector em tempo real.
        /// </summary>
        public event System.Action<SwitchState> OnStateChanged;

        /// <summary>Cosmético (áudio): qualquer switch mudou por INPUT do jogador (Nudge).</summary>
        public static event System.Action<SwitchState> AnyNudged;

        void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<SwitchVisual>();
        }

        void Start()
        {
            // Sem animação no primeiro frame — o tile nasce com uma direção
            // levantada e as outras duas já baixadas.
            ApplyVisual(animate: false);
        }

        /// <summary>
        /// Injeta o RailGenConfig (chamado pelo gerador no spawn). Fonte do
        /// ângulo da seta legada; o SwitchVisual usa o próprio preset.
        /// </summary>
        public void SetConfig(RailGenConfig config)
        {
            _config = config;
            if (visual == null) ApplyVisual(animate: false);
        }

        /// <summary>Input do player: empurra o state ±1 e anima a troca.</summary>
        public void Nudge(int dir)
        {
            int next = Mathf.Clamp((int)state + dir, -1, 1);
            if (next == (int)state) return;
            state = (SwitchState)next;
            ApplyVisual(animate: true);
            OnStateChanged?.Invoke(state);
            AnyNudged?.Invoke(state);
        }

        /// <summary>
        /// Define o state direto. immediate = true aplica o visual sem animar
        /// (spawn / reset de pool / respawn) e força o apply mesmo que o state
        /// já fosse esse — o tile pode ter vindo do pool com as meshes tortas.
        /// </summary>
        public void SetState(SwitchState s, bool immediate = false)
        {
            bool changed = state != s;
            if (!changed && !immediate) return;

            state = s;
            ApplyVisual(animate: !immediate);
            if (changed) OnStateChanged?.Invoke(state);
        }

        void ApplyVisual(bool animate)
        {
            if (visual != null)
            {
                visual.Apply(state, animate);
                return;
            }
            UpdateLegacyArrowRotation();
        }

        // Visual antigo: uma seta só, girando em Y proporcional ao state.
        void UpdateLegacyArrowRotation()
        {
            if (ArrowVisual == null) return;
            float degPerStep = _config != null ? _config.switchArrowDegreesPerStep : DefaultDegreesPerStep;
            float angle = (int)state * degPerStep;
            ArrowVisual.localEulerAngles = new Vector3(0f, angle, 0f);
        }
    }
}
