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

        [Header("Power-ups indicators (MVP2 Iter 4)")]
        [Tooltip("Texto do Shield. Mostrado quando shield ativo, hidden quando 0 cargas.")]
        [SerializeField] private TMP_Text shieldText;
        [Tooltip("Texto do SlowDown. Mostrado quando ativo.")]
        [SerializeField] private TMP_Text slowDownText;
        [Tooltip("Texto do Magnet. Mostrado quando ativo.")]
        [SerializeField] private TMP_Text magnetText;

        [Header("References (auto-resolved if empty)")]
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private PowerUpManager powerUpManager;

        // Distância exibida é relativa ao Z inicial do player (que começa em ~5m
        // por causa do StartPoint do tile da row 0). Baseline capturado na 1ª LateUpdate.
        private bool _distanceBaselineSet;
        private float _distanceBaseline;

        void Start()
        {
            if (timer == null) timer = GameTimer.Instance;
            if (difficulty == null) difficulty = DifficultyManager.Instance;
            if (coinManager == null) coinManager = CoinManager.Instance;
            if (powerUpManager == null) powerUpManager = PowerUpManager.Instance;
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

            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpTick += HandlePowerUpTick;
                powerUpManager.OnPowerUpExpired += HandlePowerUpExpired;
            }
            // Estado inicial: tudo escondido
            SetPowerUpText(shieldText, "", false);
            SetPowerUpText(slowDownText, "", false);
            SetPowerUpText(magnetText, "", false);
        }

        void OnDestroy()
        {
            if (coinManager != null) coinManager.OnCoinsChanged -= HandleCoinsChanged;
            if (difficulty != null) difficulty.OnTierChanged -= HandleTierChanged;
            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpTick -= HandlePowerUpTick;
                powerUpManager.OnPowerUpExpired -= HandlePowerUpExpired;
            }
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

        void HandlePowerUpTick(PowerUpType type, int value)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    SetPowerUpText(shieldText, $"Shield x{value}", true);
                    break;
                case PowerUpType.SlowDown:
                    SetPowerUpText(slowDownText, $"Slow {value}", true);
                    break;
                case PowerUpType.Magnet:
                    SetPowerUpText(magnetText, $"Magnet {value}", true);
                    break;
                // DifficultyReset é instantâneo — não tem indicador.
            }
        }

        void HandlePowerUpExpired(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield:   SetPowerUpText(shieldText, "", false); break;
                case PowerUpType.SlowDown: SetPowerUpText(slowDownText, "", false); break;
                case PowerUpType.Magnet:   SetPowerUpText(magnetText, "", false); break;
            }
        }

        void SetPowerUpText(TMP_Text label, string text, bool visible)
        {
            if (label == null) return;
            label.text = text;
            if (label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
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
