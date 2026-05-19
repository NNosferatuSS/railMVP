using UnityEngine;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Time Freeze active item: seta Time.timeScale pra um valor BAIXO
    /// (mas não zero) por N segundos reais. Mundo + player + animações
    /// continuam se movendo, mas muito lentamente — player tem tempo
    /// pra pensar sem parada total brusca.
    ///
    /// Decrementa via unscaledDeltaTime (não afetado pelo próprio slow).
    /// </summary>
    public class TimeFreezeController : MonoBehaviour
    {
        public static TimeFreezeController Instance { get; private set; }

        [Tooltip("Duração em SEGUNDOS REAIS (unscaled).")]
        [SerializeField] private float durationSeconds = 3f;

        [Tooltip("Time scale aplicado durante o efeito. 0.15 = 15% da velocidade " +
            "normal — quase parado mas ainda visível movendo. 0 seria parada total.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float slowdownTimeScale = 0.15f;

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
            // Safety: nunca deixa o jogo travado em timeScale baixo ao destruir.
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
            Time.timeScale = slowdownTimeScale;
            Debug.Log($"[TimeFreeze] Activated for {durationSeconds:F1}s (timeScale={slowdownTimeScale})");
            return true;
        }

        void Update()
        {
            if (remaining <= 0f) return;
            // Usa unscaled pra continuar contando durante o próprio slow.
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
