using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Barreira: segundo tipo de obstáculo. Mecanicamente idêntica ao Lethal
    /// (mata se não tem shield). O que distingue é o VISUAL (faixa amarela/preta)
    /// e a curva de spawn separada (barrierChanceOnDecoy independente de
    /// obstacleChanceOnDecoy).
    ///
    /// Shield absorption está no ObstacleBase — todos herdam.
    /// </summary>
    public class BarrierObstacle : ObstacleBase
    {
        protected override void OnPlayerHit(Collider playerCollider)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(GameOverReason.HitObstacle);
            else
                Debug.LogWarning("[BarrierObstacle] Player hit but no GameManager.Instance.", this);
        }
    }
}
