using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
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

        [Header("Singletons (auto-resolved if empty)")]
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CoinManager coinManager;

        private bool _isShowing;
        private bool _runCommitted;

        // Stats finais capturados quando o GameOver dispara — usados pra
        // transferir pro PDM no Restart/Home.
        private int _runMeters;
        private int _runCoins;
        private int _runTier;
        private float _runTime;

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

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;

            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (homeButton != null)
                homeButton.onClick.AddListener(GoHome);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
            if (restartButton != null)
                restartButton.onClick.RemoveListener(Restart);
            if (homeButton != null)
                homeButton.onClick.RemoveListener(GoHome);
        }

        void LateUpdate()
        {
            if (!_baselineCaptured && player != null)
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

            if (panel != null) panel.SetActive(true);

            if (reasonText != null)
                reasonText.text = FormatReason(reason);

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

            // Overlay "NEW RECORD!" se qualquer record batido.
            if (newRecordText != null)
            {
                if (broken.Any)
                {
                    var stats = "";
                    if (broken.distance) stats += "Distance ";
                    if (broken.coins) stats += "Coins ";
                    if (broken.tier) stats += "Tier ";
                    if (broken.time) stats += "Time ";
                    newRecordText.text = $"★ NEW RECORD! {stats.Trim()}";
                    newRecordText.gameObject.SetActive(true);
                }
                else
                {
                    newRecordText.gameObject.SetActive(false);
                }
            }

            Time.timeScale = 0f;
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
        /// Volta pra HomeScene. Mesma transferência do Restart.
        /// </summary>
        public void GoHome()
        {
            CommitRunToPlayerData();
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
            pdm.Save();
        }
    }
}
