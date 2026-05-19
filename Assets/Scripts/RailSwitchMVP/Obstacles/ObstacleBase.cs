using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Base abstrata de qualquer obstáculo no tile. Provê o gancho de colisão
    /// com o player (trigger). Subclasses decidem o efeito (matar, etc.).
    ///
    /// Shield absorption: implementada AQUI na base. Se PowerUpManager tem
    /// shield disponível, consome 1 carga, destrói o obstáculo e NÃO chama
    /// OnPlayerHit. Tanto Lethal quanto Barrier herdam esse comportamento.
    ///
    /// Convenção: obstáculos têm Collider com IsTrigger=true e tag opcional.
    /// O player tem tag "Player" (já configurada na Iter 2 do MVP1).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ObstacleBase : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // Shield absorve qualquer obstáculo (Lethal e Barrier).
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.ConsumeShield())
            {
                Destroy(gameObject);
                return;
            }

            OnPlayerHit(other);
        }

        /// <summary>Implementação concreta do efeito no player (chamado se NÃO houver shield).</summary>
        protected abstract void OnPlayerHit(Collider playerCollider);
    }
}
