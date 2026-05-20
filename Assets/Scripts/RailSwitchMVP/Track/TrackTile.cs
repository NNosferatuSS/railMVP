using System.Collections.Generic;
using UnityEngine;
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
        [Tooltip("MeshRenderer do trilho (Mesh child) cujo material é trocado conforme conectividade.")]
        [SerializeField] private MeshRenderer trackRenderer;
        [Tooltip("Material aplicado quando IsConnected = true. Sugerido verde.")]
        [SerializeField] private Material connectedMaterial;
        [Tooltip("Material aplicado quando IsConnected = false (dead-end garantido). Sugerido vermelho.")]
        [SerializeField] private Material disconnectedMaterial;

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

            // Backlink do switch para este tile (necessário para o TargetLane)
            if (Switch != null) Switch.OwnerTile = this;

            // Snapshot dos children do prefab antes de qualquer spawn dinâmico.
            // Awake roda uma vez por instância — esse snapshot persiste entre
            // usos do pool.
            _initialChildren = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                _initialChildren[i] = transform.GetChild(i);
        }

        /// <summary>
        /// Atualiza a flag IsConnected + troca o material do trilho.
        /// Chamado pelo RailManager quando a row N+1 é gerada (atualiza
        /// row N) e quando este tile é spawnado (default true até prova
        /// em contrário).
        /// </summary>
        public void SetConnected(bool connected)
        {
            IsConnected = connected;
            if (trackRenderer == null) return;
            var mat = connected ? connectedMaterial : disconnectedMaterial;
            if (mat != null) trackRenderer.sharedMaterial = mat;
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

            // Reset flags + connectivity default
            IsOnCriticalPath = false;
            SetConnected(true);

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
