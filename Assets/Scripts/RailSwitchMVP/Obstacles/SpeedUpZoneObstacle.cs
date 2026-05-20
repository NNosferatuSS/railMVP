using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Zona de aceleração: ao tocar, aplica SpeedUp debuff (player corre mais
    /// rápido por N tiles, default 6, 1.5x speed). Stack ADICIONA duração.
    ///
    /// Punição soft — não mata, mas reduz tempo de reação. Player com SpeedUp
    /// + tier alto pode ficar overwhelmed.
    /// </summary>
    public class SpeedUpZoneObstacle : ObstacleBase
    {
        [Tooltip("Duração em tiles. 0 = usa default do PowerUpManager.")]
        public int durationTiles = 0;

        protected override void OnPlayerHit(Collider playerCollider)
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.SpeedUpDebuffDefaultTiles;
            PowerUpManager.Instance.GrantSpeedUpDebuff(tiles);
            Destroy(gameObject);
        }
    }
}
