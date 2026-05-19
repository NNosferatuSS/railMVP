using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Coin Radar pickup. Por N tiles, todas as moedas visíveis ficam
    /// com pulse de escala (mais visíveis de longe). Útil pra identificar
    /// rapidamente onde tá o critical path em tiers altos.
    /// Stack estende duração.
    /// </summary>
    public class CoinRadarPickup : PowerUpBase
    {
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.CoinRadarDefaultTiles;
            PowerUpManager.Instance.GrantCoinRadar(tiles);
        }
    }
}
