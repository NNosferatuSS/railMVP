using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Obstáculo letal: matar o player on contact.
    /// Único tipo no MVP2 Iter 1. Barreira (segundo tipo) chega na Iter 4.
    /// </summary>
    public class LethalObstacle : ObstacleBase
    {
        protected override void OnPlayerHit(Collider playerCollider)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(GameOverReason.HitObstacle);
            else
                Debug.LogWarning("[LethalObstacle] Player hit but no GameManager.Instance.", this);
        }
    }
}
