using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Teleport pickup. Concede window tile-based de Teleport (Shift+←/→).
    /// Passive — efeito ativa imediatamente, não vai pro slot.
    /// Stack estende duração.
    /// </summary>
    public class TeleportPickup : PowerUpBase
    {
        public int durationTiles = 0; // 0 = usa default do PowerUpManager

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.TeleportDefaultTiles;
            PowerUpManager.Instance.GrantTeleport(tiles);
        }
    }
}
