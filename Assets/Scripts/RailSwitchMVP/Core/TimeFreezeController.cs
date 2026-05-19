using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Time Freeze active item: seta Time.timeScale = 0 por N segundos reais.
    /// Mundo + player + animations pausam. Player ainda pode mudar switch
    /// (input é unscaled). Útil pra "respirar" em tiers altos.
    ///
    /// Decrementa via unscaledDeltaTime (não afetado pelo próprio freeze).
    /// </summary>
    public class TimeFreezeController : MonoBehaviour
    {
        public static TimeFreezeController Instance { get; private set; }

        [Tooltip("Duração em SEGUNDOS REAIS (unscaled). Não é tile-based porque o jogo está pausado.")]
        [SerializeField] private float durationSeconds = 3f;

        [SerializeField] private float remaining;

        public float DurationSeconds => durationSeconds;
        public float Remaining => remaining;
        public bool IsActive => remaining > 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Safety: nunca deixa o jogo travado em timeScale=0 ao destruir.
            if (IsActive) Time.timeScale = 1f;
        }

        /// <summary>
        /// Tenta ativar Time Freeze. Retorna false se já está ativo
        /// (sem stack — refuse).
        /// </summary>
        public bool TryActivate()
        {
            if (IsActive) return false;
            // Não pode ativar durante Game Over (Time.timeScale já é 0 por outro motivo).
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return false;

            remaining = durationSeconds;
            Time.timeScale = 0f;
            Debug.Log($"[TimeFreeze] Activated for {durationSeconds:F1}s");
            return true;
        }

        void Update()
        {
            if (remaining <= 0f) return;
            // Usa unscaled pra continuar contando durante o próprio freeze.
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                Time.timeScale = 1f;
                Debug.Log("[TimeFreeze] Expired");
            }
        }
    }
}
