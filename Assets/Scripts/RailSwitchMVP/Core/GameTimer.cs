using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Cronômetro do run. Conta tempo desde o Play (ou último Reset),
    /// pausa automaticamente quando o GameManager dispara OnGameOver.
    /// Lido pelo HUDController e (Iter 3) pela tela de Game Over.
    /// </summary>
    public class GameTimer : MonoBehaviour
    {
        public static GameTimer Instance { get; private set; }

        [SerializeField] private float elapsedSeconds;
        [SerializeField] private bool isRunning = true;

        public float Elapsed => elapsedSeconds;
        public bool IsRunning => isRunning;

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
            // Awakes terminaram — agora GameManager.Instance é seguro.
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
        }

        void Update()
        {
            if (!isRunning) return;
            // Não conta tempo durante warmup (jogo não começou ainda).
            if (GameManager.Instance != null && GameManager.Instance.IsWarmup) return;
            elapsedSeconds += Time.deltaTime;
        }

        void HandleGameOver(GameOverReason _)
        {
            isRunning = false;
        }

        /// <summary>
        /// Reinicia o timer e retoma a contagem. Chamado pela tela de Restart
        /// (MVP2 Iter 3) — ou manualmente para debug.
        /// </summary>
        public void ResetTimer()
        {
            elapsedSeconds = 0f;
            isRunning = true;
        }

        /// <summary>Retorna o tempo formatado como "mm:ss".</summary>
        public string FormatMMSS()
        {
            int total = Mathf.FloorToInt(elapsedSeconds);
            int minutes = total / 60;
            int seconds = total % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }
    }
}
