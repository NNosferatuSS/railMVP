using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Lane Preview pickup. Por N tiles, o HUD mostra a direção pra
    /// alcançar o critical path da próxima row (← / ↑ / →).
    /// Stack estende duração.
    /// </summary>
    public class LanePreviewPickup : PowerUpBase
    {
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.LanePreviewDefaultTiles;
            PowerUpManager.Instance.GrantLanePreview(tiles);
        }
    }
}
