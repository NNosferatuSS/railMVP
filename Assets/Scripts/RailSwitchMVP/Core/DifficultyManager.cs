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

        public DifficultyConfig Config => config;
        public DifficultyTier CurrentTier => currentTier;
        public int CurrentTierIndex => currentTierIndex;
        public float DistanceTraveled => distanceTraveled;

        public event System.Action<DifficultyTier> OnTierChanged;

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
        /// </summary>
        public void ResetDifficulty()
        {
            if (config == null || config.tiers == null || config.tiers.Count == 0)
            {
                Debug.LogError("[DifficultyManager] Cannot reset: no tiers configured.");
                return;
            }

            distanceTraveled = 0f;
            currentTierIndex = 0;
            currentTier = config.tiers[0];
            OnTierChanged?.Invoke(currentTier);
        }

        /// <summary>
        /// Atualiza a distância percorrida e avança o tier se atingiu o próximo trigger.
        /// Chamado pelo PlayerRailRider a cada frame.
        /// </summary>
        public void UpdateDistance(float distance)
        {
            distanceTraveled = distance;

            if (config == null || config.tiers == null) return;

            while (currentTierIndex + 1 < config.tiers.Count
                && distanceTraveled >= config.tiers[currentTierIndex + 1].triggerAtDistance)
            {
                currentTierIndex++;
                currentTier = config.tiers[currentTierIndex];
                OnTierChanged?.Invoke(currentTier);
            }
        }
    }
}
