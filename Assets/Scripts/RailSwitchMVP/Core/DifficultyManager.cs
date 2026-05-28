using UnityEngine;
using RailSwitchMVP.Config;

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

        [Header("Runtime (read-only)")]
        [SerializeField] private float distanceTraveled;
        [SerializeField] private int currentTierIndex;
        [SerializeField] private DifficultyTier currentTier;

        // Offset usado para que ResetDifficulty resete a distância LÓGICA
        // mesmo que o player continue em uma posição Z mundial alta.
        private float _distanceOffset;
        private float _lastRawDistance;

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
        }

        void OnDestroy()
        {
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
            currentTierIndex = 0;
            currentTier = config.tiers[0];
            Debug.Log($"[DifficultyManager] RESET → Tier 0 (speed={currentTier.playerSpeed}, maxLanes={currentTier.maxLanes})");
            OnDifficultyReset?.Invoke();
            OnTierChanged?.Invoke(currentTier);
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
            // e bypassa o auto-advance enquanto ativo.
            var ov = SpawnOverrideController.Instance;
            if (ov != null && ov.TryGetLockedTier(config.tiers.Count, out int lockedIdx))
            {
                if (lockedIdx != currentTierIndex)
                {
                    currentTierIndex = lockedIdx;
                    currentTier = config.tiers[lockedIdx];
                    Debug.Log($"[DifficultyManager] 🔒 LOCKED Tier {lockedIdx} (debug override)");
                    OnTierChanged?.Invoke(currentTier);
                }
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
