using UnityEngine;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// HUD do MVP2 Iter 2. UGUI Canvas com 4 TMP_Text:
    /// - Top-left: tempo (mm:ss), distância (m), moedas.
    /// - Top-right: tier atual.
    ///
    /// Tempo + distância: poll em LateUpdate (cheap, todo frame).
    /// Moedas + tier: event-driven (OnCoinsChanged / OnTierChanged).
    ///
    /// Singletons auto-resolvidos no Start. Refs de Text via Inspector.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Top-left stack")]
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text coinsText;

        [Header("Top-right")]
        [SerializeField] private TMP_Text tierText;

        [Header("References (auto-resolved if empty)")]
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CoinManager coinManager;

        // Distância exibida é relativa ao Z inicial do player (que começa em ~5m
        // por causa do StartPoint do tile da row 0). Baseline capturado na 1ª LateUpdate.
        private bool _distanceBaselineSet;
        private float _distanceBaseline;

        void Start()
        {
            if (timer == null) timer = GameTimer.Instance;
            if (difficulty == null) difficulty = DifficultyManager.Instance;
            if (coinManager == null) coinManager = CoinManager.Instance;
            if (player == null) player = FindFirstObjectByType<PlayerRailRider>();

            if (coinManager != null)
            {
                coinManager.OnCoinsChanged += HandleCoinsChanged;
                HandleCoinsChanged(coinManager.Total);
            }

            if (difficulty != null)
            {
                difficulty.OnTierChanged += HandleTierChanged;
                HandleTierChanged(difficulty.CurrentTier);
            }
        }

        void OnDestroy()
        {
            if (coinManager != null) coinManager.OnCoinsChanged -= HandleCoinsChanged;
            if (difficulty != null) difficulty.OnTierChanged -= HandleTierChanged;
        }

        void LateUpdate()
        {
            // LateUpdate garante que PlayerRailRider.Update e GameTimer.Update já rodaram.
            if (timer != null && timeText != null)
                timeText.text = $"Time {timer.FormatMMSS()}";

            if (player != null && distanceText != null)
            {
                float raw = player.DistanceTraveled;
                if (!_distanceBaselineSet)
                {
                    _distanceBaseline = raw;
                    _distanceBaselineSet = true;
                }
                int meters = Mathf.Max(0, Mathf.FloorToInt(raw - _distanceBaseline));
                distanceText.text = $"Dist {meters} m";
            }
        }

        void HandleCoinsChanged(int total)
        {
            if (coinsText != null) coinsText.text = $"Coins {total}";
        }

        void HandleTierChanged(DifficultyTier _)
        {
            if (tierText == null || difficulty == null) return;
            tierText.text = $"Tier {difficulty.CurrentTierIndex}";
        }

        /// <summary>
        /// Chamado pela tela de Restart (MVP2 Iter 3) para resetar o baseline
        /// de distância junto com o player.
        /// </summary>
        public void ResetDistanceBaseline()
        {
            _distanceBaselineSet = false;
        }
    }
}
