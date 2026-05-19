using UnityEngine;

namespace RailSwitchMVP.Core
{
    public enum GameState { Playing, GameOver }

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
        public bool IsPlaying => state == GameState.Playing;

        public event System.Action<GameOverReason> OnGameOver;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            state = GameState.Playing;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void TriggerGameOver(GameOverReason reason)
        {
            if (state == GameState.GameOver) return;
            state = GameState.GameOver;
            lastReason = reason;
            Debug.Log($"[GameManager] GAME OVER — {reason}");
            OnGameOver?.Invoke(reason);
        }
    }
}
