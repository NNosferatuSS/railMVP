using UnityEngine;
using UnityEngine.InputSystem;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Debug-only: atalhos de teclado para testar progressão de dificuldade.
    /// - R: chama ResetDifficulty (volta ao tier 0, zera distância acumulada).
    /// - T: força próximo tier (útil pra testar tiers superiores sem andar 1200m).
    ///
    /// Não fazer parte de build de release. Iter 4 do MVP.
    /// </summary>
    public class DifficultyDebugController : MonoBehaviour
    {
        [Tooltip("Quando true, atalhos só funcionam se Application.isEditor || Debug.isDebugBuild.")]
        public bool restrictToDebugBuilds = false;

        void Update()
        {
            if (restrictToDebugBuilds && !(Application.isEditor || Debug.isDebugBuild))
                return;

            // Pula durante Game Over — a tecla R aí vira "restart total da run"
            // (handled by GameOverController), não "reset de dificuldade".
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
                return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame)
            {
                if (DifficultyManager.Instance != null)
                    DifficultyManager.Instance.ResetDifficulty();
            }

            if (kb.tKey.wasPressedThisFrame)
            {
                if (DifficultyManager.Instance != null)
                    DifficultyManager.Instance.ForceNextTier();
            }
        }
    }
}
