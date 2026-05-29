using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tela de Game Over. Escuta GameManager.OnGameOver, popula stats finais
    /// e pausa o jogo. Ao sair (Restart ou Home), transfere as coins do run
    /// pro PlayerDataManager e atualiza best scores.
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        [Header("Panel & UI refs")]
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text reasonText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text bestTierText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button homeButton;

        [Header("High score (D)")]
        [Tooltip("Texto opcional que aparece quando algum record é batido. " +
            "Padrão: '★ NEW RECORD! Distance Coins Tier Time' (só os batidos).")]
        [SerializeField] private TMP_Text newRecordText;

        [Header("XP / Account Level (Camada 1)")]
        [Tooltip("XP ganho na run, ex '+120 XP'. Opcional — auto-skip se vazio.")]
        [SerializeField] private TMP_Text xpGainedText;
        [Tooltip("Aparece só quando a run faz subir de nível, ex 'LEVEL UP!  Lv 7 → 8'. Opcional.")]
        [SerializeField] private TMP_Text levelUpText;

        [Header("Rewarded Ad — 2x coins (Fatia 5)")]
        [Tooltip("Botão pra dobrar coins do run via rewarded ad. Auto-hide se " +
            "AdsManager ausente ou não pronto. Spec §5.2.")]
        [SerializeField] private Button doubleCoinsButton;
        [Tooltip("Texto do botão (default 'Watch Ad +N coins'). Substituído em runtime.")]
        [SerializeField] private TMP_Text doubleCoinsButtonText;

        [Header("Singletons (auto-resolved if empty)")]
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private RailGenConfig railConfig;

        private bool _isShowing;
        private bool _runCommitted;
        private bool _doubleCoinsClaimed; // 1 ad por GameOver, evita farm
        private bool _wasDailyRun;        // snapshot do IsDailyChallenge no momento do gameOver

        // Stats finais capturados quando o GameOver dispara — usados pra
        // transferir pro PDM no Restart/Home.
        private int _runMeters;
        private int _runCoins;
        private int _runTier;
        private float _runTime;
        private int _runXP; // calculado no DeathSequence (display) e reusado no commit

        // Mesmo padrão do HUDController: captura baseline no 1º LateUpdate
        // pra exibir distância "limpa" (a partir de 0) mesmo que o player
        // comece em world Z > 0.
        private bool _baselineCaptured;
        private float _distanceBaseline;

        void Start()
        {
            if (timer == null) timer = GameTimer.Instance;
            if (difficulty == null) difficulty = DifficultyManager.Instance;
            if (coinManager == null) coinManager = CoinManager.Instance;
            if (player == null) player = FindFirstObjectByType<PlayerRailRider>();

            if (panel != null) panel.SetActive(false);
            if (newRecordText != null) newRecordText.gameObject.SetActive(false);
            if (levelUpText != null) levelUpText.gameObject.SetActive(false);

            if (railConfig == null && difficulty != null && difficulty.Config != null)
            {
                // Fallback: tenta resolver via PlayerCameraRig (que serializa RailGenConfig).
                var rig = PlayerCameraRig.Instance;
                if (rig != null) railConfig = ResolveConfigFromRig(rig);
            }

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;

            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (homeButton != null)
                homeButton.onClick.AddListener(GoHome);
            if (doubleCoinsButton != null)
                doubleCoinsButton.onClick.AddListener(WatchAdForDoubleCoins);

            var ads = AdsManager.Instance;
            if (ads != null) ads.OnRewardedReadyChanged += HandleAdsReadyChanged;

            RefreshDoubleCoinsButton();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
            if (restartButton != null)
                restartButton.onClick.RemoveListener(Restart);
            if (homeButton != null)
                homeButton.onClick.RemoveListener(GoHome);
            if (doubleCoinsButton != null)
                doubleCoinsButton.onClick.RemoveListener(WatchAdForDoubleCoins);

            var ads = AdsManager.Instance;
            if (ads != null) ads.OnRewardedReadyChanged -= HandleAdsReadyChanged;
        }

        void HandleAdsReadyChanged(bool _) { RefreshDoubleCoinsButton(); }

        void LateUpdate()
        {
            // Captura o baseline de distância só APÓS o warmup (igual o HUDController),
            // pra que o score NÃO inclua a distância andada durante o warmup — senão
            // o painel final infla vs o HUD (bug do mismatch de baseline).
            if (!_baselineCaptured && player != null
                && (GameManager.Instance == null || !GameManager.Instance.IsWarmup))
            {
                _distanceBaseline = player.DistanceTraveled;
                _baselineCaptured = true;
            }

            if (!_isShowing) return;

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                Restart();
        }

        void HandleGameOver(GameOverReason reason)
        {
            if (_isShowing) return;
            _isShowing = true;

            var daily = DailyChallengeManager.Instance;
            _wasDailyRun = daily != null && daily.IsDailyChallenge;

            // Não mostra painel ainda — aguarda death sequence pra impacto.
            if (reasonText != null)
                reasonText.text = _wasDailyRun ? $"DAILY CHALLENGE — {FormatReason(reason)}" : FormatReason(reason);

            // Compute current run stats e cacheia pra transferir no Restart/Home.
            if (player != null)
            {
                float baseline = _baselineCaptured ? _distanceBaseline : player.DistanceTraveled;
                _runMeters = Mathf.Max(0, Mathf.FloorToInt(player.DistanceTraveled - baseline));
            }
            _runCoins = coinManager != null ? coinManager.Total : 0;
            _runTier = difficulty != null ? difficulty.CurrentTierIndex : 0;
            _runTime = timer != null ? timer.Elapsed : 0f;
            string curTimeStr = timer != null ? timer.FormatMMSS() : "00:00";

            // Atualiza bests via PlayerDataManager (PlayerPrefs sob o capô).
            PlayerDataManager.RecordResult broken = default;
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                broken = pdm.UpdateBests(_runMeters, _runCoins, _runTier, _runTime);
            }
            else
            {
                Debug.LogWarning("[GameOver] PlayerDataManager.Instance null — best scores não foram salvos. Adicione _PlayerDataManager na cena.");
            }

            // Daily Challenge: registra resultado se foi run daily. Best global (acima)
            // é independente — daily runs também contam pra bests gerais.
            DailyChallengeManager.DailyRecordResult dailyBroken = default;
            if (_wasDailyRun && daily != null)
            {
                dailyBroken = daily.EndChallenge(_runMeters);

                // Leaderboard online (Fatia 8): submeter só se quebrou local best.
                // Server-side function (submit_daily_result) faz check extra contra
                // o registro atual no banco — race entre devices fica safe.
                if (dailyBroken.brokeToday)
                {
                    var lb = LeaderboardManager.Instance;
                    if (lb != null) lb.SubmitResult(_runMeters, _runCoins, _runTier, _runTime);
                }
            }

            // Labels com (Best: X) inline. Star ★ se record batido.
            if (timeText != null)
            {
                string newTag = broken.time ? " ★" : "";
                string bestStr = pdm != null ? FormatTime(pdm.BestTime) : "—";
                timeText.text = $"Time: {curTimeStr}{newTag}  (Best: {bestStr})";
            }
            if (distanceText != null)
            {
                string newTag = broken.distance ? " ★" : "";
                string bestStr = pdm != null ? $"{pdm.BestDistance} m" : "—";
                distanceText.text = $"Distance: {_runMeters} m{newTag}  (Best: {bestStr})";
            }
            if (coinsText != null)
            {
                string newTag = broken.coins ? " ★" : "";
                string bestStr = pdm != null ? pdm.BestCoins.ToString() : "—";
                coinsText.text = $"Coins: {_runCoins}{newTag}  (Best: {bestStr})";
            }
            if (bestTierText != null)
            {
                string newTag = broken.tier ? " ★" : "";
                string bestStr = pdm != null ? pdm.BestTier.ToString() : "—";
                bestTierText.text = $"Tier: {_runTier}{newTag}  (Best: {bestStr})";
            }

            // Overlay "NEW RECORD!" se qualquer record batido (normal ou daily).
            if (newRecordText != null)
            {
                bool anyBroken = broken.Any || dailyBroken.brokeToday || dailyBroken.brokeEver;
                if (anyBroken)
                {
                    var stats = "";
                    if (broken.distance) stats += "Distance ";
                    if (broken.coins) stats += "Coins ";
                    if (broken.tier) stats += "Tier ";
                    if (broken.time) stats += "Time ";
                    if (dailyBroken.brokeEver) stats += "DailyEver ";
                    else if (dailyBroken.brokeToday) stats += "DailyToday ";
                    newRecordText.text = $"★ NEW RECORD! {stats.Trim()}";
                    newRecordText.gameObject.SetActive(true);
                }
                else
                {
                    newRecordText.gameObject.SetActive(false);
                }
            }

            RefreshDoubleCoinsButton();

            // Death sequence: slow-mo enquanto a PlayerCameraRig zooma + shake.
            // Após deathCamDuration, congela e mostra o painel.
            StartCoroutine(DeathSequence());
        }

        void RefreshDoubleCoinsButton()
        {
            if (doubleCoinsButton == null) return;

            // Botão só faz sentido: (1) GameOver showing, (2) tem coins pra dobrar,
            // (3) ainda não usou neste GameOver, (4) AdsManager está pronto (ou ausente = stub).
            bool hasCoins = _runCoins > 0;
            var ads = AdsManager.Instance;
            bool adReady = ads == null || ads.IsRewardedReady;
            bool show = _isShowing && hasCoins && !_doubleCoinsClaimed && adReady;

            doubleCoinsButton.gameObject.SetActive(show);
            doubleCoinsButton.interactable = show;
            if (doubleCoinsButtonText != null && show)
                doubleCoinsButtonText.text = $"Watch Ad +{_runCoins} coins";
        }

        public void WatchAdForDoubleCoins()
        {
            if (_doubleCoinsClaimed || _runCoins <= 0) return;

            var ads = AdsManager.Instance;
            if (ads == null)
            {
                // Modo stub (Editor sem AdsManager).
                GrantDoubleCoins();
                return;
            }

            bool shown = ads.TryShowRewarded(
                onSuccess: GrantDoubleCoins,
                onFailed: () => Debug.Log("[GameOver] 2x ad failed/skipped — sem bonus.")
            );
            if (!shown) RefreshDoubleCoinsButton();
        }

        void GrantDoubleCoins()
        {
            if (_doubleCoinsClaimed) return;
            _doubleCoinsClaimed = true;

            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.AddCoins(_runCoins); // credita o EXTRA _runCoins (dobra)
                pdm.Save();
            }
            Debug.Log($"[GameOver] 2x coins granted: +{_runCoins} extra.");

            // Refresca o display in-place pra refletir o novo total.
            if (coinsText != null)
                coinsText.text = $"Coins: {_runCoins * 2} (2x via ad)";

            RefreshDoubleCoinsButton();
        }

        IEnumerator DeathSequence()
        {
            float duration = railConfig != null ? railConfig.deathCamDuration : 1f;
            float slowMo = railConfig != null ? railConfig.deathCamSlowMo : 0.3f;

            Time.timeScale = slowMo;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 0f;

            // Calcula o XP agora: o MissionTracker.EndRun já rodou no OnGameOver,
            // então MissionsCompletedThisRun está consolidado (sem depender da
            // ordem dos handlers do evento).
            ComputeAndShowXP();

            if (panel != null) panel.SetActive(true);
        }

        // Calcula o XP da run (display) e prepara os textos +XP / LEVEL UP. O
        // crédito real acontece em CommitRunToPlayerData (Restart/Home), reusando
        // _runXP — mesmo padrão das coins (mostradas aqui, creditadas no sair).
        void ComputeAndShowXP()
        {
            int missions = MissionTracker.Instance != null ? MissionTracker.Instance.MissionsCompletedThisRun : 0;
            _runXP = Mathf.FloorToInt(_runMeters / 10f) + _runCoins + missions * 50;

            if (xpGainedText != null)
                xpGainedText.text = $"+{_runXP} XP";

            if (levelUpText == null) return;
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                int curLevel = pdm.AccountLevel;
                int projLevel = pdm.ComputeLevelFromXP(pdm.AccountXP + _runXP);
                if (projLevel > curLevel)
                {
                    levelUpText.text = $"LEVEL UP!  Lv {curLevel} → {projLevel}";
                    levelUpText.gameObject.SetActive(true);
                    return;
                }
            }
            levelUpText.gameObject.SetActive(false);
        }

        // Reflete a ref de RailGenConfig serializada no PlayerCameraRig — pra evitar
        // dois lugares com a mesma ref atribuída manualmente.
        static RailGenConfig ResolveConfigFromRig(PlayerCameraRig rig)
        {
            var field = typeof(PlayerCameraRig).GetField("config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? field.GetValue(rig) as RailGenConfig : null;
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            int min = total / 60;
            int sec = total % 60;
            return $"{min:D2}:{sec:D2}";
        }

        static string FormatReason(GameOverReason reason)
        {
            switch (reason)
            {
                case GameOverReason.DeadEnd:    return "Dead End";
                case GameOverReason.OutOfBounds: return "Out of Bounds";
                case GameOverReason.HitObstacle: return "Hit Obstacle";
                default: return reason.ToString();
            }
        }

        /// <summary>
        /// Recarrega a cena pra começar nova run. Transfere coins do run
        /// pro PDM e incrementa total de runs antes de recarregar.
        /// </summary>
        public void Restart()
        {
            CommitRunToPlayerData();
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        /// <summary>
        /// Volta pra HomeScene. Mesma transferência do Restart. Consome flag de Daily
        /// Challenge se ativa — próxima entrada na Game será modo normal.
        /// </summary>
        public void GoHome()
        {
            CommitRunToPlayerData();
            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.ConsumeChallengeFlag();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Home);
        }

        // Idempotente — só roda uma vez por GameOver (Restart e Home não
        // podem ambos creditar duas vezes se user spammar).
        void CommitRunToPlayerData()
        {
            if (_runCommitted) return;
            _runCommitted = true;

            var pdm = PlayerDataManager.Instance;
            if (pdm == null) return;
            pdm.AddCoins(_runCoins);
            pdm.IncrementTotalRuns();

            // XP de account level (Camada 1) — _runXP já foi calculado no
            // DeathSequence (ComputeAndShowXP). AddXP é no-op se 0.
            pdm.AddXP(_runXP);

            pdm.Save();
        }
    }
}
