using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Base abstrata de qualquer obstáculo no tile. Provê o gancho de colisão
    /// com o player (trigger). Subclasses decidem TUDO sobre o efeito —
    /// inclusive se Shield protege ou não.
    ///
    /// Decisão de design (pós-MVP2): Shield NÃO é universal. Cada subclasse
    /// decide se aceita absorção:
    /// - LethalObstacle: mata sempre. Shield não ajuda. Player tem que evitar.
    /// - BarrierObstacle: Shield consome 1 carga e passa. Sem shield, mata.
    ///
    /// Ghost power-up (PostMVP2.2): se ativo, TODO obstáculo é ignorado
    /// silenciosamente. Não consome Shield, não chama OnPlayerHit.
    ///
    /// Convenção: obstáculos têm Collider com IsTrigger=true.
    /// O player tem tag "Player".
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ObstacleBase : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            // Log diagnóstico: dispara em TODO trigger pra ajudar debugar prefabs
            // que "não fazem nada" (Trigger missing, tag errada, etc.).
            Debug.Log($"[Obstacle/{gameObject.name}] OnTriggerEnter with '{other.name}' (tag={other.tag})");

            if (!other.CompareTag("Player"))
            {
                Debug.Log($"[Obstacle/{gameObject.name}] Other is not Player. Ignored.");
                return;
            }

            // Ghost = atravessa qualquer obstáculo sem efeito.
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.IsGhost)
            {
                Debug.Log($"[Obstacle/{gameObject.name}] Ghost active — pass-through.");
                return;
            }

            OnPlayerHit(other);
        }

        /// <summary>Implementação concreta do efeito no player.</summary>
        protected abstract void OnPlayerHit(Collider playerCollider);
    }
}
