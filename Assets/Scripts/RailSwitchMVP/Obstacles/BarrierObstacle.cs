using UnityEngine;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Barreira: obstáculo BLOQUEANTE absorvível por Shield.
    /// - Player COM shield: consome 1 carga, barreira destruída, player passa.
    /// - Player SEM shield: Game Over com HitObstacle.
    ///
    /// Diferença vs LethalObstacle (que mata sempre, shield não ajuda):
    /// Barrier é "obstáculo que demanda Shield". Lethal é "ameaça que demanda
    /// evasão pelo switch". Dois tipos de leitura, Shield ganha identidade.
    /// </summary>
    public class BarrierObstacle : ObstacleBase
    {
        protected override void OnPlayerHit(Collider playerCollider)
        {
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.ConsumeShield())
            {
                // Shield salvou o player — slow-mo de impacto pra dar peso ao momento.
                if (PlayerCameraRig.Instance != null)
                    PlayerCameraRig.Instance.ImpactSlowmo();
                Destroy(gameObject);
                return;
            }

            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver(GameOverReason.HitObstacle);
            else
                Debug.LogWarning("[BarrierObstacle] Player hit but no GameManager.Instance.", this);
        }
    }
}
