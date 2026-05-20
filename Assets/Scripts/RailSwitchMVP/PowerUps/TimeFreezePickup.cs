using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// TimeFreeze pickup. Coloca o item no ActiveItemSlot (não auto-ativa).
    /// Player aperta Space pra usar — efeito é Time.timeScale ~0.15 por
    /// N segundos reais (configurável no TimeFreezeController).
    ///
    /// Se slot já tem outro item, substitui (com log no PowerUpManager).
    /// </summary>
    public class TimeFreezePickup : PowerUpBase
    {
        protected override void Activate()
        {
            if (ActiveItemSlot.Instance != null)
                ActiveItemSlot.Instance.SetItem(ActiveItemType.TimeFreeze);
        }
    }
}
