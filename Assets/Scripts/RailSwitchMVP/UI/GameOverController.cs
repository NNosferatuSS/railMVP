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

            if (timeText != null && timer != null)
                timeText.text = $"Time: {timer.FormatMMSS()}";

            if (distanceText != null && player != null)
            {
                float baseline = _baselineCaptured ? _distanceBaseline : player.DistanceTraveled;
                int meters = Mathf.Max(0, Mathf.FloorToInt(player.DistanceTraveled - baseline));
                distanceText.text = $"Distance: {meters} m";
            }

            if (coinsText != null && coinManager != null)
                coinsText.text = $"Coins: {coinManager.Total}";

            if (bestTierText != null && difficulty != null)
                bestTierText.text = $"Best Tier: {difficulty.CurrentTierIndex}";

            // Pausa total: movimento, gerador, timer (já parado), animações de coin.
            // O painel é o único elemento que continua interativo (graças à UI rodar
            // em unscaled time por default no Canvas).
            Time.timeScale = 0f;
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
