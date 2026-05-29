using UnityEngine;

namespace RailSwitchMVP.Core
{
    public enum GameState { Warmup, Playing, GameOver }

    public enum GameOverReason
    {
        DeadEnd,        // Switch aponta para lane vazia na próxima linha
        OutOfBounds,    // Switch aponta para fora do grid
        HitObstacle     // Reservado — Iteração futura
    }

    /// <summary>
    /// Singleton de estado global do jogo. Iter 2: estados Playing/GameOver.
    /// O PlayerRailRider lê IsPlaying para parar o movimento quando dá game over.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameState state = GameState.Playing;
        [SerializeField] private GameOverReason lastReason;

        public GameState State => state;
        public GameOverReason LastReason => lastReason;

        /// <summary>True quando state == Playing (gameplay normal).</summary>
        public bool IsPlaying => state == GameState.Playing;
        /// <summary>True em Warmup OU Playing (= NÃO game over). Use pra "player ainda controla?".</summary>
        public bool IsActive => state != GameState.GameOver;
        /// <summary>True só quando state == Playing (= scoring/progression contam).</summary>
        public bool IsScoring => state == GameState.Playing;
        /// <summary>True durante a sequência de warmup do início.</summary>
        public bool IsWarmup => state == GameState.Warmup;

        public event System.Action<GameOverReason> OnGameOver;
        public event System.Action OnWarmupEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            state = GameState.Warmup; // jogo começa em Warmup; transita pra Playing após countdown.
        }

        /// <summary>
        /// Termina a fase de Warmup. Chamado pelo WarmupController quando o
        /// countdown acaba (GO!). State vira Playing.
        /// </summary>
        public void EndWarmup()
        {
            if (state != GameState.Warmup) return;
            state = GameState.Playing;
            Debug.Log("[GameManager] Warmup ended — GO!");
            OnWarmupEnded?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void TriggerGameOver(GameOverReason reason)
        {
            if (state == GameState.GameOver) return;

            // Camada 3: dá ao ReviveController a chance de oferecer um continue ANTES de
            // comprometer o game over. Se ele assume (pausa + overlay), o game over só é
            // confirmado depois — no decline/timeout, via ConfirmGameOver.
            if (ReviveController.Instance != null && ReviveController.Instance.TryOfferContinue(reason))
                return;

            ConfirmGameOver(reason);
        }

        /// <summary>
        /// Finaliza o game over de fato (state → GameOver + OnGameOver). Chamado por
        /// TriggerGameOver quando não há revive disponível, ou pelo ReviveController
        /// quando o jogador recusa/ignora a oferta de continue.
        /// </summary>
        public void ConfirmGameOver(GameOverReason reason)
        {
            if (state == GameState.GameOver) return;
            state = GameState.GameOver;
            lastReason = reason;
            Debug.Log($"[GameManager] GAME OVER — {reason}");
            OnGameOver?.Invoke(reason);
        }
    }
}
