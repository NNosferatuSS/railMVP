using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// TimeFreeze pickup. Consumido NA COLISÃO: dispara o slow-mo imediatamente
    /// (Time.timeScale baixo por N segundos reais, configurável no
    /// TimeFreezeController). Não vai mais pro slot — o sistema de inventário
    /// foi removido. No-op se o efeito já estiver ativo.
    /// </summary>
    public class TimeFreezePickup : PowerUpBase
    {
        protected override void Activate()
        {
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.GrantTimeFreeze();
        }
    }
}
