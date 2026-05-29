using UnityEngine;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

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

        [Header("Power-ups indicators (PostMVP2.2)")]
        [SerializeField] private TMP_Text doubleCoinsText;
        [SerializeField] private TMP_Text ghostText;
        [Tooltip("Lane Preview: mostra '← / ↑ / →' apontando ao critical da próxima row.")]
        [SerializeField] private TMP_Text lanePreviewText;
        [SerializeField] private TMP_Text coinRadarText;

        [Header("PostMVP2.3")]
        [Tooltip("Texto do active item no slot. 'Item: -' quando vazio.")]
        [SerializeField] private TMP_Text activeItemText;
        [Tooltip("Teleport (tile-based window). Mostra 'Teleport N (Shift+←/→)' quando ativo.")]
        [SerializeField] private TMP_Text teleportText;

        [Header("PostMVP2.4 — Idea 3")]
        [Tooltip("Auto-follow critical path power-up. Mostra 'AutoFollow N' quando ativo.")]
        [SerializeField] private TMP_Text autoFollowText;

        [Header("PostMVP2.5 — Debuffs")]
        [Tooltip("SpeedUp debuff (obstacle). Mostra '⚡ Fast N' quando ativo.")]
        [SerializeField] private TMP_Text speedUpDebuffText;
        [Tooltip("Lane Swap debuff (inputs invertidos). Mostra '↔ Swap N' quando ativo.")]
        [SerializeField] private TMP_Text laneSwapDebuffText;

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

            if (player != null)
                player.OnTileEntered += HandlePlayerTileEntered;

            // Active item slot events
            if (ActiveItemSlot.Instance != null)
            {
                ActiveItemSlot.Instance.OnItemAcquired += HandleActiveItemChanged;
                ActiveItemSlot.Instance.OnItemUsed += HandleActiveItemUsed;
                UpdateActiveItemText(ActiveItemSlot.Instance.HeldItem);
            }
            else
            {
                UpdateActiveItemText(ActiveItemType.None);
            }

            // Estado inicial: tudo escondido
            SetPowerUpText(shieldText, "", false);
            SetPowerUpText(slowDownText, "", false);
            SetPowerUpText(magnetText, "", false);
            SetPowerUpText(doubleCoinsText, "", false);
            SetPowerUpText(ghostText, "", false);
            SetPowerUpText(lanePreviewText, "", false);
            SetPowerUpText(coinRadarText, "", false);
            SetPowerUpText(teleportText, "", false);
            SetPowerUpText(autoFollowText, "", false);
            SetPowerUpText(speedUpDebuffText, "", false);
            SetPowerUpText(laneSwapDebuffText, "", false);
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
            if (player != null) player.OnTileEntered -= HandlePlayerTileEntered;
            if (ActiveItemSlot.Instance != null)
            {
                ActiveItemSlot.Instance.OnItemAcquired -= HandleActiveItemChanged;
                ActiveItemSlot.Instance.OnItemUsed -= HandleActiveItemUsed;
            }
        }

        void HandlePlayerTileEntered(TrackTile newTile)
        {
            // Lane preview precisa re-calcular direção quando o player muda de tile.
            UpdateLanePreviewText();
        }

        void HandleActiveItemChanged(ActiveItemType type) => UpdateActiveItemText(type);
        void HandleActiveItemUsed(ActiveItemType type) => UpdateActiveItemText(ActiveItemType.None);

        void UpdateActiveItemText(ActiveItemType type)
        {
            if (activeItemText == null) return;
            if (type == ActiveItemType.None)
            {
                activeItemText.text = "Item: -";
                return;
            }
            activeItemText.text = $"Item: {type} (Space)";
        }

        void LateUpdate()
        {
            // LateUpdate garante que PlayerRailRider.Update e GameTimer.Update já rodaram.
            if (tierText != null)
            {
                bool locked = SpawnOverrideController.Instance != null
                    && SpawnOverrideController.Instance.tierLockEnabled;
                tierText.color = locked ? Color.red : Color.white;
            }

            if (timer != null && timeText != null)
                timeText.text = $"Time {timer.FormatMMSS()}";

            if (player != null && distanceText != null)
            {
                bool inWarmup = GameManager.Instance != null && GameManager.Instance.IsWarmup;
                if (inWarmup)
                {
                    distanceText.text = "Dist 0 m";
                    // Não captura baseline ainda — espera warmup terminar.
                }
                else
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
                case PowerUpType.DoubleCoins:
                    SetPowerUpText(doubleCoinsText, $"2x Coins {value}", true);
                    break;
                case PowerUpType.Ghost:
                    SetPowerUpText(ghostText, $"Ghost {value}", true);
                    break;
                case PowerUpType.LanePreview:
                    UpdateLanePreviewText();
                    break;
                case PowerUpType.CoinRadar:
                    SetPowerUpText(coinRadarText, $"Radar {value}", true);
                    break;
                case PowerUpType.Teleport:
                    SetPowerUpText(teleportText, $"Teleport {value} (Shift+←/→)", true);
                    break;
                case PowerUpType.AutoCriticalFollow:
                    SetPowerUpText(autoFollowText, $"AutoFollow {value}", true);
                    break;
                case PowerUpType.SpeedUpDebuff:
                    SetPowerUpText(speedUpDebuffText, $"⚡ Fast {value}", true);
                    break;
                case PowerUpType.LaneSwapDebuff:
                    SetPowerUpText(laneSwapDebuffText, $"↔ Swap {value}", true);
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
                case PowerUpType.DoubleCoins: SetPowerUpText(doubleCoinsText, "", false); break;
                case PowerUpType.Ghost: SetPowerUpText(ghostText, "", false); break;
                case PowerUpType.LanePreview: SetPowerUpText(lanePreviewText, "", false); break;
                case PowerUpType.CoinRadar: SetPowerUpText(coinRadarText, "", false); break;
                case PowerUpType.Teleport: SetPowerUpText(teleportText, "", false); break;
                case PowerUpType.AutoCriticalFollow: SetPowerUpText(autoFollowText, "", false); break;
                case PowerUpType.SpeedUpDebuff: SetPowerUpText(speedUpDebuffText, "", false); break;
                case PowerUpType.LaneSwapDebuff: SetPowerUpText(laneSwapDebuffText, "", false); break;
            }
        }

        void UpdateLanePreviewText()
        {
            if (lanePreviewText == null) return;
            if (powerUpManager == null || !powerUpManager.HasLanePreview)
            {
                if (lanePreviewText.gameObject.activeSelf)
                    lanePreviewText.gameObject.SetActive(false);
                return;
            }
            if (player == null || player.CurrentTile == null) return;

            var rm = RailManager.Instance;
            if (rm == null) return;
            var nextRow = rm.GetRow(player.CurrentTile.Row + 1);
            if (nextRow == null) return;

            int currentLane = player.CurrentTile.Lane;
            int? offset = null;

            // 1ª passada: critical path (±1)
            int[] candidates = { 0, -1, 1 };
            foreach (var c in candidates)
            {
                int target = currentLane + c;
                if (nextRow.HasTile(target) && nextRow.IsCriticalLane(target))
                {
                    offset = c;
                    break;
                }
            }

            // Fallback: qualquer tile populado
            if (!offset.HasValue)
            {
                foreach (var c in candidates)
                {
                    int target = currentLane + c;
                    if (nextRow.HasTile(target)) { offset = c; break; }
                }
            }

            string dirArrow;
            switch (offset)
            {
                case -1: dirArrow = "<- LEFT"; break;
                case 1:  dirArrow = "RIGHT ->"; break;
                case 0:  dirArrow = "^ STAY"; break;
                default: dirArrow = "?"; break;
            }
            SetPowerUpText(lanePreviewText, $"Next: {dirArrow} ({powerUpManager.LanePreviewTilesRemaining})", true);
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
