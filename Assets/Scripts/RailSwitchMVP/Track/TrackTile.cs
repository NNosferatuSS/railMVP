using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Representa um trilho individual em uma posição (Row, Lane).
    /// Iter 1: geometria + gizmos.
    /// Iter 2: + SwitchController, + CoinSpawner, + auto-registro no RailManager.
    /// Iter 3: instanciado pelo ProceduralRailGenerator (em vez de hardcoded na cena).
    /// </summary>
    public class TrackTile : MonoBehaviour
    {
        [Header("Grid Position")]
        public int Row;
        public int Lane;

        [Tooltip("maxLanes ativo no momento que este tile foi spawnado")]
        public int MaxLanesAtSpawn;

        [Header("Anchor Points")]
        [Tooltip("Ponto onde o player entra no tile")]
        public Transform StartPoint;

        [Tooltip("Ponto onde o player sai do tile (e onde o switch fica)")]
        public Transform EndPoint;

        [Header("Procedural State")]
        [Tooltip("Este tile faz parte do critical path?")]
        public bool IsOnCriticalPath;

        [Tooltip("Há ao menos 1 lane vizinha (±1) com tile na próxima row?\n" +
            "Setado pelo RailManager quando a row N+1 é gerada.\n" +
            "Default true (assume conectado até ser provado o contrário).")]
        public bool IsConnected = true;

        [Header("Connectivity visuals (Idea 2)")]
        [Tooltip("Renderer do visual que indica conectividade. Recomendação: " +
            "arrastar o MeshRenderer do ARROW (= switch/connector), não do " +
            "Mesh inteiro do trilho. Assim só o connector muda de cor.\n" +
            "Pode aceitar QUALQUER Renderer (MeshRenderer, SkinnedMeshRenderer, etc.).")]
        [FormerlySerializedAs("trackRenderer")]
        [SerializeField] private Renderer connectivityRenderer;
        [Tooltip("Material aplicado quando IsConnected = true (verde sugerido).")]
        [SerializeField] private Material connectedMaterial;
        [Tooltip("Material aplicado quando IsConnected = false (vermelho sugerido).")]
        [SerializeField] private Material disconnectedMaterial;

        // Logs ONCE de configuração faltando (evita spam no console).
        private static bool _warnedRendererNull;
        private static bool _warnedMaterialNull;

        [Header("Components")]
        public SwitchController Switch;
        public CoinSpawner Coins;
        public ObstacleSpawner Obstacles;
        public PowerUpSpawner PowerUps;

        [Header("Debug")]
        [SerializeField] private RailGenConfig debugConfig;

        // Snapshot dos children que vieram do prefab (vs spawnados em runtime).
        // Usado pelo ResetForReuse pra distinguir "estrutura permanente" de
        // "conteúdo dinâmico" sem precisar de marcadores manuais nos prefabs.
        private Transform[] _initialChildren;

        void Awake()
        {
            // Auto-resolve componentes (caso o prefab já esteja completo)
            if (Switch == null) Switch = GetComponentInChildren<SwitchController>();
            if (Coins == null) Coins = GetComponentInChildren<CoinSpawner>();
            if (Obstacles == null) Obstacles = GetComponentInChildren<ObstacleSpawner>();
            if (PowerUps == null) PowerUps = GetComponentInChildren<PowerUpSpawner>();

            // Backlink do switch para este tile + subscribe pra atualizar
            // a cor de conectividade em tempo real quando o switch muda.
            if (Switch != null)
            {
                Switch.OwnerTile = this;
                Switch.OnStateChanged += HandleSwitchStateChanged;
            }

            // Snapshot dos children do prefab antes de qualquer spawn dinâmico.
            // Awake roda uma vez por instância — esse snapshot persiste entre
            // usos do pool.
            _initialChildren = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                _initialChildren[i] = transform.GetChild(i);
        }

        void OnDestroy()
        {
            if (Switch != null) Switch.OnStateChanged -= HandleSwitchStateChanged;
        }

        void HandleSwitchStateChanged(SwitchState _) => UpdateConnectivityVisual();

        /// <summary>
        /// Re-avalia conectividade DESTE tile baseado no estado atual do
        /// Switch: o tile é "conectado" (verde) se Switch.TargetLane (lane
        /// pra qual o switch aponta) tem tile na próxima row. Senão, vermelho.
        ///
        /// Chamado quando:
        /// - Switch state muda (via OnStateChanged).
        /// - Próxima row spawna (RailManager.UpdateConnectivityForPreviousRow).
        /// - Tile é spawnado (após ResetForReuse + setup do switch).
        /// </summary>
        public void UpdateConnectivityVisual()
        {
            // Agora que warmup permite input livre, cor reflete a switch state
            // real (player aperta Left em warmup → próxima warmup tem só center →
            // Arrow fica VERMELHO avisando "vai dar dead-end aí"). Aprendizado
            // por tentativa-e-erro.
            bool connected = ComputeConnectedFromSwitch();
            IsConnected = connected;
            ApplyConnectivityMaterial(connected);
        }

        bool ComputeConnectedFromSwitch()
        {
            if (Switch == null) return true; // sem switch = sempre verde
            var rm = RailManager.Instance;
            if (rm == null) return true; // sem manager = não sabemos, default true
            var nextRow = rm.GetRow(Row + 1);
            if (nextRow == null) return true; // sem next row spawnada = default true

            int targetLane = Switch.TargetLane;
            // OutOfBounds = nunca conectado.
            if (targetLane < 0 || targetLane >= nextRow.MaxLanesAtSpawn) return false;
            // Tile vazio na lane destino = vermelho.
            return nextRow.HasTile(targetLane);
        }

        void ApplyConnectivityMaterial(bool connected)
        {
            if (connectivityRenderer == null)
            {
                if (!_warnedRendererNull)
                {
                    Debug.LogWarning("[TrackTile] connectivityRenderer NÃO atribuído no TrackTile_Prefab. " +
                        "Trilhos coloridos (Idea 2) não vão mudar de cor. " +
                        "Atribua o MeshRenderer do Arrow no Inspector → Track Tile → Connectivity Renderer.", this);
                    _warnedRendererNull = true;
                }
                return;
            }

            var mat = connected ? connectedMaterial : disconnectedMaterial;
            if (mat == null)
            {
                if (!_warnedMaterialNull)
                {
                    Debug.LogWarning($"[TrackTile] {(connected ? "connectedMaterial" : "disconnectedMaterial")} " +
                        "NÃO atribuído no TrackTile_Prefab. Atribua materials verde/vermelho no Inspector.", this);
                    _warnedMaterialNull = true;
                }
                return;
            }

            connectivityRenderer.sharedMaterial = mat;
        }

        // Mantido por compat — chama UpdateConnectivityVisual.
        [System.Obsolete("Use UpdateConnectivityVisual() — lógica nova baseada na switch state.")]
        public void SetConnected(bool connected)
        {
            IsConnected = connected;
            ApplyConnectivityMaterial(connected);
        }

        /// <summary>
        /// Limpa estado dinâmico antes de reutilizar o tile do pool. Destrói
        /// todos os children que NÃO vieram do prefab original (coins,
        /// obstáculos, power-ups, warning icons), reseta o switch pro Middle,
        /// zera flags. Chamado pelo gerador após PrefabPool.Spawn.
        /// </summary>
        public void ResetForReuse()
        {
            // Switch volta pro centro
            if (Switch != null) Switch.SetState(SwitchState.Middle);

            // Reset flags
            IsOnCriticalPath = false;
            // Connectivity visual atualiza via UpdateConnectivityVisual quando
            // switch state ou next row mudarem. Por ora deixa verde (default)
            // até o generator setar tudo.
            #pragma warning disable CS0618
            SetConnected(true);
            #pragma warning restore CS0618

            // Destroi children dinâmicos. Se _initialChildren é null (primeira
            // ativação, Awake ainda não rodou), nada a fazer.
            if (_initialChildren == null) return;

            var keep = new HashSet<Transform>(_initialChildren);
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!keep.Contains(child))
                    Destroy(child.gameObject);
            }
        }

        void Start()
        {
            // Registra no RailManager. Iter 2: cena hardcoded.
            // Iter 3: o gerador procedural pode optar por registrar diretamente em vez disso.
            if (RailManager.Instance != null)
                RailManager.Instance.RegisterTile(this);
        }

        void OnDrawGizmos()
        {
            if (debugConfig == null || !debugConfig.debugDrawCriticalPath) return;

            Gizmos.color = IsOnCriticalPath
                ? debugConfig.criticalPathGizmoColor
                : debugConfig.decoyGizmoColor;

            if (StartPoint != null && EndPoint != null)
            {
                Vector3 center = (StartPoint.position + EndPoint.position) * 0.5f;
                center.y += 1.5f;
                Vector3 size = new Vector3(
                    0.8f,
                    0.2f,
                    Vector3.Distance(StartPoint.position, EndPoint.position) * 0.9f
                );
                Gizmos.DrawWireCube(center, size);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(StartPoint.position + Vector3.up * 0.5f, 0.2f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(EndPoint.position + Vector3.up * 0.5f, 0.3f);
            }
        }

        /// <summary>
        /// Helper: calcula a posição mundial do center do tile.
        /// X é estável entre tiers: usa <c>config.globalMaxLanes</c> em vez do
        /// maxLanes do tier ativo. Lane N tem sempre o mesmo X mundial, garantindo
        /// que o switch ±1 sempre move exatamente 1 laneSpacing visualmente.
        /// </summary>
        public static Vector3 ComputeWorldPosition(int row, int lane, int globalMaxLanes, RailGenConfig config)
        {
            float x = (lane - (globalMaxLanes - 1) / 2f) * config.laneSpacing;
            float z = row * (config.trackLength + config.rowGap) + config.trackLength * 0.5f;
            return new Vector3(x, 0f, z);
        }
    }
}
