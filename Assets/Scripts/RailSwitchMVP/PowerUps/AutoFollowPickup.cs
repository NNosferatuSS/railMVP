using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Auto-follow pickup. Por N tiles (default 5), jogo segue o critical
    /// path sozinho. Manual input ainda funciona mas é sobrescrito por tile.
    /// Stack estende duração.
    /// </summary>
    public class AutoFollowPickup : PowerUpBase
    {
        public int durationTiles = 0; // 0 = usa default do PowerUpManager

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.AutoCriticalFollowDefaultTiles;
            PowerUpManager.Instance.GrantAutoCriticalFollow(tiles);
        }
    }
}
