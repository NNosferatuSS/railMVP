using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Lane Swap obstacle: ao tocar, inverte os inputs de switch (←/→) por N
    /// tiles (default 2). Stack RESETA duração (não acumula).
    ///
    /// Trap mental — confunde mas não mata. Player aprende a reagir
    /// (deixar inputs em paz OU se adaptar mentalmente).
    /// </summary>
    public class LaneSwapObstacle : ObstacleBase
    {
        [Tooltip("Duração em tiles. 0 = usa default do PowerUpManager.")]
        public int durationTiles = 0;

        protected override void OnPlayerHit(Collider playerCollider)
        {
            if (PowerUpManager.Instance == null) return;
            int tiles = durationTiles > 0 ? durationTiles : PowerUpManager.Instance.LaneSwapDebuffDefaultTiles;
            PowerUpManager.Instance.GrantLaneSwapDebuff(tiles);
            Destroy(gameObject);
        }
    }
}
