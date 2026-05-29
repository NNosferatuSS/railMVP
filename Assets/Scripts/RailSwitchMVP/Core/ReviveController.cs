using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Camada 3 — Continue após Morte. Intercepta o game over (via
    /// GameManager.TriggerGameOver → TryOfferContinue): se o jogador ainda tem
    /// continues, pausa o jogo e mostra um overlay (ad ou coins) com countdown.
    /// Ao reviver, reposiciona o player num tile do critical path recuando alguns
    /// metros e concede um grace period de invencibilidade. Se recusar/expirar,
    /// confirma o game over de verdade.
    ///
    /// O grace (IsGracePeriodActive) é consultado por LethalObstacle e
    /// PlayerRailRider pra não matar o player logo após reviver.
    /// </summary>
    public class ReviveController : MonoBehaviour
    {
        public static ReviveController Instance { get; private set; }

        [Header("Config")]
        [SerializeField] private ReviveConfig config;
        [Tooltip("Usado só pra ler o espaçamento de row (trackLength+rowGap) e converter " +
            "o setback em metros pra rows.")]
        [SerializeField] private RailGenConfig railConfig;
        [SerializeField] private PlayerRailRider player;

        [Header("UI Overlay (oferta de continue)")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private Button adButton;
        [SerializeField] private TMP_Text adButtonText;
        [SerializeField] private Button coinsButton;
        [SerializeField] private TMP_Text coinsButtonText;
        [SerializeField] private Button declineButton;
        [SerializeField] private TMP_Text countdownText;

        [Header("Runtime (read-only)")]
        [SerializeField] private int continuesUsedThisRun;

        private float _graceUntil;
        private GameOverReason _pendingReason;
        private bool _offering;
        private Coroutine _countdownCo;

        public int ContinuesUsedThisRun => continuesUsedThisRun;

        /// <summary>True durante a janela de invencibilidade pós-revive.</summary>
        public bool IsGracePeriodActive => Time.time < _graceUntil;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerRailRider>();
            if (overlayPanel != null) overlayPanel.SetActive(false);
            if (adButton != null) adButton.onClick.AddListener(OnAdClicked);
            if (coinsButton != null) coinsButton.onClick.AddListener(OnCoinsClicked);
            if (declineButton != null) declineButton.onClick.AddListener(DeclineContinue);
            ResetForNewRun();

            // Diagnóstico de wiring — se algo essencial faltar, o painel não abre.
            if (config == null)
                Debug.LogWarning("[Revive] ReviveConfig NÃO atribuído — CanContinue é sempre false, o painel nunca abre.", this);
            else if (config.maxContinuesPerRun <= 0)
                Debug.LogWarning($"[Revive] maxContinuesPerRun = {config.maxContinuesPerRun} — revive desabilitado.", this);
            if (overlayPanel == null)
                Debug.LogWarning("[Revive] overlayPanel NÃO atribuído — o jogo pausa mas nenhum painel aparece.", this);
            if (player == null)
                Debug.LogWarning("[Revive] player não encontrado/atribuído — o revive não vai reposicionar.", this);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (adButton != null) adButton.onClick.RemoveListener(OnAdClicked);
            if (coinsButton != null) coinsButton.onClick.RemoveListener(OnCoinsClicked);
            if (declineButton != null) declineButton.onClick.RemoveListener(DeclineContinue);
        }

        // ============ API ============

        public bool CanContinue() => config != null && continuesUsedThisRun < config.maxContinuesPerRun;

        public int GetCurrentContinueCost() => config != null ? config.GetContinueCost(continuesUsedThisRun) : 0;

        /// <summary>
        /// Chamado pelo GameManager.TriggerGameOver. Se ainda há continue disponível,
        /// pausa o jogo + mostra o overlay e retorna true (game over NÃO é confirmado).
        /// Senão retorna false (segue pro game over normal).
        /// </summary>
        public bool TryOfferContinue(GameOverReason reason)
        {
            if (_offering) return true;
            if (!CanContinue())
            {
                Debug.Log($"[Revive] Sem oferta de continue → game over normal " +
                    $"(config={(config != null)}, usados={continuesUsedThisRun}/" +
                    $"{(config != null ? config.maxContinuesPerRun : 0)}).");
                return false;
            }

            Debug.Log($"[Revive] Oferecendo continue #{continuesUsedThisRun + 1} " +
                $"(reason={reason}, overlayPanel={(overlayPanel != null)}).");
            _pendingReason = reason;
            _offering = true;
            Time.timeScale = 0f;
            ShowOverlay();
            _countdownCo = StartCoroutine(CountdownRoutine());
            return true;
        }

        public void ResetForNewRun()
        {
            continuesUsedThisRun = 0;
            _graceUntil = 0f;
            _offering = false;
        }

        // ============ Overlay ============

        void ShowOverlay()
        {
            int cost = GetCurrentContinueCost();
            var ads = AdsManager.Instance;
            bool adReady = config.allowAdForContinue && (ads == null || ads.IsRewardedReady);
            bool canAfford = PlayerDataManager.Instance != null && PlayerDataManager.Instance.Coins >= cost;

            if (overlayPanel != null) overlayPanel.SetActive(true);
            if (adButton != null)
            {
                adButton.gameObject.SetActive(adReady);
                adButton.interactable = adReady;
                if (adButtonText != null) adButtonText.text = "Continuar (Ad)";
            }
            if (coinsButton != null)
            {
                coinsButton.interactable = canAfford;
                if (coinsButtonText != null) coinsButtonText.text = $"Continuar ({cost} coins)";
            }
        }

        void HideOverlay()
        {
            if (overlayPanel != null) overlayPanel.SetActive(false);
        }

        IEnumerator CountdownRoutine()
        {
            float t = config.offerCountdownSeconds;
            while (t > 0f)
            {
                if (countdownText != null) countdownText.text = Mathf.CeilToInt(t).ToString();
                t -= Time.unscaledDeltaTime; // unscaled — o jogo está pausado (timeScale = 0)
                yield return null;
            }
            DeclineContinue();
        }

        void StopCountdown()
        {
            if (_countdownCo != null) { StopCoroutine(_countdownCo); _countdownCo = null; }
        }

        // ============ Escolhas ============

        void OnAdClicked()
        {
            var ads = AdsManager.Instance;
            if (ads == null) { ExecuteRevive(isAd: true); return; } // stub (Editor sem AdsManager)
            bool shown = ads.TryShowRewarded(
                onSuccess: () => ExecuteRevive(isAd: true),
                onFailed: () => Debug.Log("[Revive] Ad failed/skipped — sem revive."));
            if (!shown) Debug.Log("[Revive] Ad não pôde ser exibido agora.");
        }

        void OnCoinsClicked()
        {
            int cost = GetCurrentContinueCost();
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) return;
            if (!pdm.SpendCoins(cost)) { Debug.Log("[Revive] Coins insuficientes."); return; }
            pdm.Save();
            ExecuteRevive(isAd: false);
        }

        /// <summary>Recusa/expira a oferta → confirma o game over de verdade.</summary>
        public void DeclineContinue()
        {
            StopCountdown();
            HideOverlay();
            _offering = false;
            Time.timeScale = 1f; // o DeathSequence do GameOver ajusta a partir daqui
            if (GameManager.Instance != null) GameManager.Instance.ConfirmGameOver(_pendingReason);
        }

        // ============ Revive ============

        void ExecuteRevive(bool isAd)
        {
            StopCountdown();
            HideOverlay();

            DoRespawn();
            continuesUsedThisRun++;
            _graceUntil = Time.time + (config != null ? config.reviveGraceSeconds : 1.5f);
            _offering = false;
            Time.timeScale = 1f;
            Debug.Log($"[Revive] Continue #{continuesUsedThisRun} via {(isAd ? "ad" : "coins")} " +
                $"— grace {(config != null ? config.reviveGraceSeconds : 1.5f)}s.");
        }

        // Reposiciona o player: recua reviveSetbackDistance (clampado às rows
        // existentes) e acha um tile do critical path; reseta o switch pra Middle.
        void DoRespawn()
        {
            if (player == null || player.CurrentTile == null || RailManager.Instance == null) return;

            int currentRow = player.CurrentTile.Row;
            float spacing = railConfig != null ? (railConfig.trackLength + railConfig.rowGap) : 0f;
            int rowsBack = (config != null && spacing > 0.01f)
                ? Mathf.RoundToInt(config.reviveSetbackDistance / spacing)
                : 0;
            int targetRow = Mathf.Clamp(currentRow - rowsBack, RailManager.Instance.LowestSpawnedRow, currentRow);

            // Acha o critical tile na targetRow; se a row recuada não tiver, avança
            // até a row atual procurando um tile válido (fallback robusto).
            TrackTile tile = null;
            for (int r = targetRow; r <= currentRow && tile == null; r++)
                tile = FindSafeTile(r);
            if (tile == null) tile = player.CurrentTile;

            if (tile.Switch != null) tile.Switch.SetState(SwitchState.Middle);
            player.RespawnAt(tile);
        }

        // Critical path primeiro; senão qualquer tile populado da row.
        static TrackTile FindSafeTile(int rowIndex)
        {
            var row = RailManager.Instance.GetRow(rowIndex);
            if (row == null || row.Tiles == null) return null;
            for (int L = 0; L < row.Tiles.Length; L++)
                if (row.Tiles[L] != null && row.Tiles[L].IsOnCriticalPath) return row.Tiles[L];
            for (int L = 0; L < row.Tiles.Length; L++)
                if (row.Tiles[L] != null) return row.Tiles[L];
            return null;
        }
    }
}
