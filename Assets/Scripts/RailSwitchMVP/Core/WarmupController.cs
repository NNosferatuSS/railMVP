using System.Collections;
using UnityEngine;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Gerencia a fase de Warmup do começo do jogo (Idea 1).
    /// - Player atravessa N rows single-lane center em 0.5x speed.
    /// - Quando entra na ÚLTIMA warmup row, dispara countdown 3-2-1-GO!
    /// - Após GO!, chama GameManager.EndWarmup → state = Playing.
    /// - Texto do countdown mostrado via TMP_Text (atribuído no Inspector,
    ///   geralmente um overlay grande centralizado no HUD).
    /// </summary>
    public class WarmupController : MonoBehaviour
    {
        public static WarmupController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private RailGenConfig config;
        [Tooltip("Overlay TMP_Text que mostra 3-2-1-GO!. Auto-hide quando warmup termina.")]
        [SerializeField] private TMP_Text countdownText;

        [Header("Tunables")]
        [Tooltip("Tempo de cada step do countdown em segundos reais.")]
        [SerializeField] private float countdownStepSeconds = 1f;

        [Tooltip("Tempo que o \"GO!\" fica visível antes de sumir.")]
        [SerializeField] private float goDisplaySeconds = 0.8f;

        private PlayerRailRider _player;
        private bool _subscribed;
        private bool _countdownStarted;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            // UI inicial: vazio até o countdown começar.
            if (countdownText != null)
            {
                countdownText.text = "";
                countdownText.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            // Lazy subscribe ao player.
            if (!_subscribed)
            {
                _player = FindFirstObjectByType<PlayerRailRider>();
                if (_player != null)
                {
                    _player.OnTileEntered += HandleTileEntered;
                    _subscribed = true;
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_player != null) _player.OnTileEntered -= HandleTileEntered;
        }

        void HandleTileEntered(TrackTile newTile)
        {
            if (_countdownStarted) return;
            if (config == null) return;
            if (GameManager.Instance == null || !GameManager.Instance.IsWarmup) return;
            if (newTile == null) return;

            // Trigger countdown quando entrar na ÚLTIMA warmup row.
            int lastWarmupRow = config.warmupRowCount - 1;
            if (newTile.Row == lastWarmupRow)
            {
                _countdownStarted = true;
                StartCoroutine(RunCountdown());
            }
        }

        IEnumerator RunCountdown()
        {
            if (countdownText != null) countdownText.gameObject.SetActive(true);

            string[] steps = { "3", "2", "1" };
            foreach (var step in steps)
            {
                if (countdownText != null) countdownText.text = step;
                yield return new WaitForSecondsRealtime(countdownStepSeconds);
            }

            // GO!
            if (countdownText != null) countdownText.text = "GO!";
            if (GameManager.Instance != null) GameManager.Instance.EndWarmup();

            yield return new WaitForSecondsRealtime(goDisplaySeconds);

            if (countdownText != null)
            {
                countdownText.text = "";
                countdownText.gameObject.SetActive(false);
            }
        }
    }
}
