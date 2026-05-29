using UnityEngine;
using UnityEngine.InputSystem;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Debug-only: atalhos de teclado para testar progressão de dificuldade.
    /// - R: chama ResetDifficulty (volta ao tier 0, zera distância acumulada).
    /// - T: força próximo tier (útil pra testar tiers superiores sem andar 1200m).
    /// - L: +debugXpPerPress de XP (subir account level / testar starting tier).
    /// - K: -debugXpPerPress de XP (remove; clampa em 0).
    /// - J: seta o account level direto pra debugTargetLevel.
    ///   Os 3 funcionam a qualquer momento; depois reinicie a run pra começar
    ///   no tier resolvido pela Camada 1.
    ///
    /// Não fazer parte de build de release. Iter 4 do MVP.
    /// </summary>
    public class DifficultyDebugController : MonoBehaviour
    {
        [Tooltip("Quando true, atalhos só funcionam se Application.isEditor || Debug.isDebugBuild.")]
        public bool restrictToDebugBuilds = false;

        [Tooltip("Quanto XP as teclas L (+) e K (−) movem por toque. Aumente pra " +
            "alcançar tiers altos em menos toques (ex: lvl 35 ≈ 89000 XP).")]
        public int debugXpPerPress = 10000;

        [Tooltip("Nível pro qual a tecla J pula direto (XP mínimo desse nível).")]
        public int debugTargetLevel = 20;

        void Update()
        {
            if (restrictToDebugBuilds && !(Application.isEditor || Debug.isDebugBuild))
                return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Atalhos de XP funcionam a QUALQUER momento (warmup/playing/gameover),
            // pois forçam o account level que decide o starting tier da próxima run.
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                if (kb.lKey.wasPressedThisFrame) pdm.DebugAddXP(debugXpPerPress);
                if (kb.kKey.wasPressedThisFrame) pdm.DebugAddXP(-debugXpPerPress);
                if (kb.jKey.wasPressedThisFrame) pdm.DebugSetLevel(debugTargetLevel);
            }

            // Pula durante Game Over — a tecla R aí vira "restart total da run"
            // (handled by GameOverController), não "reset de dificuldade".
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
                return;

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
