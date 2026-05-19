using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Magnet pickup. Por N tiles, o player auto-coleta moedas das lanes
    /// adjacentes (lane ±1) via raio configurável no PowerUpManager.
    /// Stack estende duração.
    /// </summary>
    public class MagnetPickup : PowerUpBase
    {
        [Tooltip("Tiles de duração. 0 = usa o default do PowerUpManager.")]
        public int durationTiles = 0;

        protected override void Activate()
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.MagnetDefaultTiles;
            PowerUpManager.Instance.GrantMagnet(tiles);
        }
    }
}
