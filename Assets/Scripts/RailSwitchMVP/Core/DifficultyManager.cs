using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Gerencia o tier de dificuldade ativo em runtime.
    /// Atualiza com base na distância percorrida pelo player.
    /// Pode ser resetado (ResetDifficulty) para voltar ao tier 0.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        public static DifficultyManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private DifficultyConfig config;

        [Header("Adaptive Start (Camada 1)")]
        [Tooltip("Se true, o run começa no tier resolvido pelo account level do jogador " +
            "(via DifficultyConfig.startingTierRules). Após o warmup, sobe em rampa do Tier 0 " +
            "até o tier-piso e então adianta a distância pro trigger desse tier.")]
        [SerializeField] private bool adaptiveStartEnabled = true;

        [Tooltip("RailGenConfig — usado só pra ler o espaçamento de row (trackLength+rowGap) " +
            "e medir os degraus da rampa em 'rows'. Vazio = sem rampa (o tier-piso é aplicado " +
            "de uma vez no fim do warmup).")]
        [SerializeField] private RailGenConfig railConfig;

        [Tooltip("Quantas rows o player percorre pra subir cada degrau da rampa de starting tier. " +
            "O 1º degrau (→ Tier 1) é imediato no GO; dali sobe +1 a cada N rows até o tier-piso. " +
            "0 = sem rampa (salto direto no fim do warmup).")]
        [Min(0)]
        [SerializeField] private int rampRowsPerStep = 2;

        [Header("Runtime (read-only)")]
        [SerializeField] private float distanceTraveled;
        [SerializeField] private int currentTierIndex;
        [SerializeField] private DifficultyTier currentTier;

        // Offset usado para que ResetDifficulty resete a distância LÓGICA
        // mesmo que o player continue em uma posição Z mundial alta.
        private float _distanceOffset;
        private float _lastRawDistance;

        // Tier inicial pendente (Camada 1 / head start da Camada 2). -1 = nenhum.
        // Só é efetivado ao fim do warmup, pois durante o warmup UpdateDistance
        // re-âncora o offset a cada frame e apagaria a distância semeada.
        private int _pendingStartTierIndex = -1;

        // Rampa de starting tier: sobe Tier 0 → _rampTargetTier por rows percorridas
        // (a partir de _rampStartZ), e ao chegar no alvo semeia a distância no trigger
        // dele. Enquanto ativa, suspende o auto-advance normal por distância.
        private bool _rampActive;
        private int _rampTargetTier;
        private float _rampStartZ;

        public DifficultyConfig Config => config;

        // No Editor, lê live do SO pra que edições no Inspector durante Play
        // propaguem imediatamente (próxima row gerada, próximo frame de speed
        // do player, etc.). Em build, mantém o struct cacheado — comportamento
        // estável e barato no hot path.
        public DifficultyTier CurrentTier
        {
#if UNITY_EDITOR
            get => (config != null && config.tiers != null
                    && currentTierIndex >= 0 && currentTierIndex < config.tiers.Count)
                ? config.tiers[currentTierIndex]
                : currentTier;
#else
            get => currentTier;
#endif
        }

        public int CurrentTierIndex => currentTierIndex;
        public float DistanceTraveled => distanceTraveled;

        public event System.Action<DifficultyTier> OnTierChanged;
        public event System.Action OnDifficultyReset;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (config == null)
            {
                Debug.LogError("[DifficultyManager] DifficultyConfig is not assigned!");
                return;
            }

            ResetDifficulty();

            // Resolve o tier-piso já no Awake (PlayerDataManager é DontDestroyOnLoad, já
            // existe vindo da Home). A rampa em si só começa no fim do warmup — o run
            // arranca sempre no Tier 0 (warmup estreito/didático intocado).
            if (adaptiveStartEnabled && PlayerDataManager.Instance != null)
                _pendingStartTierIndex = ResolveStartTier(PlayerDataManager.Instance.AccountLevel, -1);
        }

        void Start()
        {
            // Subscreve o fim do warmup pra iniciar a rampa de starting tier no momento
            // certo (durante o warmup o offset é re-ancorado todo frame).
            if (GameManager.Instance != null)
                GameManager.Instance.OnWarmupEnded += HandleWarmupEnded;

            // Sem warmup (cena de teste / já Playing): inicia agora.
            if (GameManager.Instance == null || !GameManager.Instance.IsWarmup)
                BeginAdaptiveStart();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnWarmupEnded -= HandleWarmupEnded;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Volta ao tier 0 imediatamente e zera a distância.
        /// Disparável via Inspector (botão "Reset Difficulty" no menu de contexto do componente).
        /// </summary>
        [ContextMenu("Reset Difficulty")]
        public void ResetDifficulty()
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0)
            {
                Debug.LogError("[DifficultyManager] Cannot reset: no tiers configured.");
                return;
            }

            // Re-âncora a distância lógica no Z atual do player.
            // Sem isso, no próximo UpdateDistance(playerZ) o tier seria reanimado.
            _distanceOffset = _lastRawDistance;
            distanceTraveled = 0f;
            _rampActive = false;
            currentTierIndex = 0;
            currentTier = config.tiers[0];
            Debug.Log($"[DifficultyManager] RESET → Tier 0 (speed={currentTier.playerSpeed}, maxLanes={currentTier.maxLanes})");
            OnDifficultyReset?.Invoke();
            OnTierChanged?.Invoke(currentTier);
        }

        /// <summary>
        /// Define o tier-piso do run (Camada 1: speed floor por progressão).
        /// headStartTierOverride >= 0 sobrepõe a regra de account level (Camada 2).
        /// A rampa só arranca ao fim do warmup; se o jogo já saiu do warmup
        /// (ou não há GameManager), começa imediatamente.
        /// </summary>
        public void StartRunWithAdaptiveTier(int accountLevel, int headStartTierOverride = -1)
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0) return;

            _pendingStartTierIndex = ResolveStartTier(accountLevel, headStartTierOverride);

            if (GameManager.Instance == null || !GameManager.Instance.IsWarmup)
                BeginAdaptiveStart();
        }

        // Resolve o índice do tier-piso (regra de account level ou override de head
        // start), já clampado ao range válido. Não aplica nada.
        int ResolveStartTier(int accountLevel, int headStartTierOverride)
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0) return 0;
            int t = headStartTierOverride >= 0
                ? headStartTierOverride
                : config.GetStartingTierIndex(accountLevel);
            return Mathf.Clamp(t, 0, config.tiers.Count - 1);
        }

        void HandleWarmupEnded()
        {
            BeginAdaptiveStart();
        }

        // Decide entre rampa e salto. Rampa exige tier-piso > 0, rampRowsPerStep > 0
        // e railConfig (pra medir rows). Senão, aplica o tier-piso de uma vez (salto).
        void BeginAdaptiveStart()
        {
            if (_pendingStartTierIndex < 0) return;
            if (config == null || config.tiers == null || config.tiers.Count == 0) return;

            int target = Mathf.Clamp(_pendingStartTierIndex, 0, config.tiers.Count - 1);
            float rowSpacing = railConfig != null ? (railConfig.trackLength + railConfig.rowGap) : 0f;

            if (target == 0 || rampRowsPerStep <= 0 || rowSpacing <= 0.01f)
            {
                ApplyPendingStartTier(); // salto direto
                return;
            }

            // Rampa: o 1º degrau é IMEDIATO no GO (o warmup já foi o respiro do Tier 0);
            // dali sobe +1 a cada rampRowsPerStep rows até o tier-piso.
            _pendingStartTierIndex = -1;
            _rampActive = true;
            _rampTargetTier = target;
            _rampStartZ = _lastRawDistance;
            int firstTier = Mathf.Min(1, target);
            if (currentTierIndex != firstTier) ApplyTierIdentity(firstTier);
            _distanceOffset = _lastRawDistance;
            distanceTraveled = 0f;
            Debug.Log($"[DifficultyManager] Adaptive ramp → Tier {firstTier}→{target} a cada {rampRowsPerStep} rows (1º step imediato).");
        }

        // Avança a rampa conforme as rows percorridas. Ao alcançar o tier-piso,
        // adianta a distância lógica pro trigger dele e devolve pro auto-advance normal.
        void TickAdaptiveRamp()
        {
            float rowSpacing = railConfig.trackLength + railConfig.rowGap; // railConfig != null garantido em BeginAdaptiveStart
            float traveled = _lastRawDistance - _rampStartZ;
            int steps = Mathf.FloorToInt(traveled / (rowSpacing * rampRowsPerStep));
            // +1: o 1º degrau é imediato (steps=0 → Tier 1), sem ficar parado no Tier 0.
            int desiredTier = Mathf.Clamp(steps + 1, 0, _rampTargetTier);

            if (desiredTier > currentTierIndex)
                ApplyTierIdentity(desiredTier);

            if (currentTierIndex >= _rampTargetTier)
            {
                _rampActive = false;
                _distanceOffset = _lastRawDistance - config.tiers[_rampTargetTier].triggerAtDistance;
                distanceTraveled = _lastRawDistance - _distanceOffset;
                Debug.Log($"[DifficultyManager] Ramp completa → Tier {_rampTargetTier} " +
                    $"(seededDist={distanceTraveled:F0}m). Auto-advance normal retomado.");
            }
        }

        // Aplica só a IDENTIDADE do tier (largura/velocidade lidas pelo gerador e
        // pelo player). Não mexe na distância.
        void ApplyTierIdentity(int idx)
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0) return;
            idx = Mathf.Clamp(idx, 0, config.tiers.Count - 1);
            currentTierIndex = idx;
            currentTier = config.tiers[idx];
            OnTierChanged?.Invoke(currentTier);
        }

        // Salto direto (sem rampa): garante a identidade e semeia a distância LÓGICA no
        // triggerAtDistance do tier-piso via offset — sem tocar o transform do player,
        // então o scoreDistance (baseline do GameOver/HUD) segue contando a partir de 0.
        void ApplyPendingStartTier()
        {
            if (_pendingStartTierIndex < 0) return;
            if (config == null || config.tiers == null || config.tiers.Count == 0) return;

            int idx = Mathf.Clamp(_pendingStartTierIndex, 0, config.tiers.Count - 1);
            _pendingStartTierIndex = -1;

            if (currentTierIndex != idx)
                ApplyTierIdentity(idx);

            _distanceOffset = _lastRawDistance - config.tiers[idx].triggerAtDistance;
            distanceTraveled = _lastRawDistance - _distanceOffset;

            if (idx > 0)
                Debug.Log($"[DifficultyManager] Adaptive start (salto) → Tier {idx} " +
                    $"(speed={currentTier.playerSpeed}, maxLanes={currentTier.maxLanes}, " +
                    $"seededDist={distanceTraveled:F0}m)");
        }

        /// <summary>
        /// Atualiza a distância percorrida e avança o tier se atingiu o próximo trigger.
        /// Chamado pelo PlayerRailRider a cada frame.
        /// </summary>
        public void UpdateDistance(float distance)
        {
            _lastRawDistance = distance;
            distanceTraveled = distance - _distanceOffset;

            // Durante warmup, distância NÃO avança tiers — re-âncora offset
            // pra que distance "lógica" comece em 0 quando o jogo iniciar.
            if (GameManager.Instance != null && GameManager.Instance.IsWarmup)
            {
                _distanceOffset = distance;
                distanceTraveled = 0f;
                return;
            }

            if (config == null || config.tiers == null) return;

            // Debug: tier lock via SpawnOverrideController força um tier específico
            // e bypassa o auto-advance (e a rampa) enquanto ativo.
            var ov = SpawnOverrideController.Instance;
            if (ov != null && ov.TryGetLockedTier(config.tiers.Count, out int lockedIdx))
            {
                _rampActive = false;
                if (lockedIdx != currentTierIndex)
                {
                    currentTierIndex = lockedIdx;
                    currentTier = config.tiers[lockedIdx];
                    Debug.Log($"[DifficultyManager] 🔒 LOCKED Tier {lockedIdx} (debug override)");
                    OnTierChanged?.Invoke(currentTier);
                }
                return;
            }

            // Rampa de starting tier (Camada 1): sobe por rows até o tier-piso e
            // suspende o auto-advance por distância enquanto roda.
            if (_rampActive)
            {
                TickAdaptiveRamp();
                return;
            }

            while (currentTierIndex + 1 < config.tiers.Count
                && distanceTraveled >= config.tiers[currentTierIndex + 1].triggerAtDistance)
            {
                currentTierIndex++;
                currentTier = config.tiers[currentTierIndex];
                Debug.Log($"[DifficultyManager] ↑ Tier {currentTierIndex} @ {distanceTraveled:F1}m " +
                    $"(speed={currentTier.playerSpeed}, maxLanes={currentTier.maxLanes}, " +
                    $"crit={currentTier.criticalPathsPerRow}, " +
                    $"coins=[{currentTier.criticalCoinsMin}-{currentTier.criticalCoinsMax}])");
                OnTierChanged?.Invoke(currentTier);
            }
        }

        /// <summary>
        /// Avança 1 tier manualmente (debug). Não-op se já está no último.
        /// </summary>
        [ContextMenu("Force Next Tier")]
        public void ForceNextTier()
        {
            if (config == null || config.tiers == null) return;
            if (currentTierIndex + 1 >= config.tiers.Count) return;
            currentTierIndex++;
            currentTier = config.tiers[currentTierIndex];
            Debug.Log($"[DifficultyManager] ⏭ FORCED Tier {currentTierIndex} (speed={currentTier.playerSpeed}, maxLanes={currentTier.maxLanes})");
            OnTierChanged?.Invoke(currentTier);
        }
    }
}
