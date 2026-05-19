using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Ghost pickup. Por N tiles, player atravessa qualquer obstáculo
    /// (Lethal ou Barrier) sem dano e sem consumir Shield. Power-up raro.
    /// Stack estende duração.
    /// </summary>
    public class GhostPickup : PowerUpBase
    {
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.GhostDefaultTiles;
            PowerUpManager.Instance.GrantGhost(tiles);
        }
    }
}
