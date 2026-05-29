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
            // Grace period pós-revive (Camada 3): ignora o hit letal.
            if (ReviveController.Instance != null && ReviveController.Instance.IsGracePeriodActive)
                return;

            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(GameOverReason.HitObstacle);
            else
                Debug.LogWarning("[LethalObstacle] Player hit but no GameManager.Instance.", this);
        }
    }
}
