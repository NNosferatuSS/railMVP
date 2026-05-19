using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// 2x Coins pickup. Por N tiles, todas as moedas coletadas valem 2x.
    /// Stack estende duração.
    /// </summary>
    public class DoubleCoinsPickup : PowerUpBase
    {
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.DoubleCoinsDefaultTiles;
            PowerUpManager.Instance.GrantDoubleCoins(tiles);
        }
    }
}
