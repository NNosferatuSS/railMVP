using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Shield pickup. Adiciona 1 carga ao PowerUpManager. Próximo evento letal
    /// (obstáculo letal ou barreira) é absorvido em vez de matar — stack ilimitado.
    /// </summary>
    public class ShieldPickup : PowerUpBase
    {
        protected override void Activate()
        {
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.GrantShield();
        }
    }
}
