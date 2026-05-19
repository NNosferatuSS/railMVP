using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Obstáculo letal: mata o player no contato — SEMPRE.
    /// Shield NÃO ajuda contra Lethal (diferenciação pós-MVP2: Shield protege
    /// apenas contra Barrier). Player tem que EVITAR via switch.
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
