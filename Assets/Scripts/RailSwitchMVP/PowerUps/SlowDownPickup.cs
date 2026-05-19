using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// SlowDown pickup. Aplica -30% no SpeedMultiplier por N tiles. Stack
    /// estende a duração (não multiplica o slowdown — evita ficar parado).
    /// </summary>
    public class SlowDownPickup : PowerUpBase
    {
        [Tooltip("Tiles de duração. 0 = usa o default do PowerUpManager.")]
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.SlowDownDefaultTiles;
            PowerUpManager.Instance.GrantSlowDown(tiles);
        }
    }
}
