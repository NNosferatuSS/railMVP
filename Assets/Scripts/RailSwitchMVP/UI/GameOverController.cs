using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using RailSwitchMVP.Core;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tela de Game Over (MVP2 Iter 3). Escuta GameManager.OnGameOver,
    /// ativa o painel, popula stats finais e pausa o jogo (Time.timeScale = 0).
    /// Botão Restart e tecla R recarregam a cena pra começar do zero.
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
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
            if (restartButton != null)
                restartButton.onClick.RemoveListener(Restart);
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

            // Compute current run stats.
            int curMeters = 0;
            if (player != null)
            {
                float baseline = _baselineCaptured ? _distanceBaseline : player.DistanceTraveled;
                curMeters = Mathf.Max(0, Mathf.FloorToInt(player.DistanceTraveled - baseline));
            }
            int curCoins = coinManager != null ? coinManager.Total : 0;
            int curTier = difficulty != null ? difficulty.CurrentTierIndex : 0;
            float curTime = timer != null ? timer.Elapsed : 0f;
            string curTimeStr = timer != null ? timer.FormatMMSS() : "00:00";

            // Atualiza bests (PlayerPrefs).
            HighScoreManager.RecordResult broken = default;
            var hsm = HighScoreManager.Instance;
            if (hsm != null) broken = hsm.TryUpdate(curMeters, curCoins, curTier, curTime);

            // Labels com (Best: X) inline. Star ★ se record batido.
            if (timeText != null)
            {
                string newTag = broken.time ? " ★" : "";
                string bestStr = hsm != null ? FormatTime(hsm.BestTime) : "—";
                timeText.text = $"Time: {curTimeStr}{newTag}  (Best: {bestStr})";
            }
            if (distanceText != null)
            {
                string newTag = broken.distance ? " ★" : "";
                string bestStr = hsm != null ? $"{hsm.BestDistance} m" : "—";
                distanceText.text = $"Distance: {curMeters} m{newTag}  (Best: {bestStr})";
            }
            if (coinsText != null)
            {
                string newTag = broken.coins ? " ★" : "";
                string bestStr = hsm != null ? hsm.BestCoins.ToString() : "—";
                coinsText.text = $"Coins: {curCoins}{newTag}  (Best: {bestStr})";
            }
            if (bestTierText != null)
            {
                string newTag = broken.tier ? " ★" : "";
                string bestStr = hsm != null ? hsm.BestTier.ToString() : "—";
                bestTierText.text = $"Tier: {curTier}{newTag}  (Best: {bestStr})";
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
        /// Recarrega a cena. Restaura Time.timeScale antes de carregar pra que
        /// a próxima sessão não inicie pausada.
        /// </summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
