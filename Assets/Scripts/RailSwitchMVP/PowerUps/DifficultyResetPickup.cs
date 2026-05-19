using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// DifficultyReset pickup. Efeito instantâneo: chama
    /// PowerUpManager.GrantDifficultyReset, que delega pro
    /// DifficultyManager.ResetDifficulty (mesma transição semeada do debug R).
    /// Sem state persistente, sem stack — só dispara e some.
    /// </summary>
    public class DifficultyResetPickup : PowerUpBase
    {
        protected override void Activate()
        {
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.GrantDifficultyReset();
        }
    }
}
