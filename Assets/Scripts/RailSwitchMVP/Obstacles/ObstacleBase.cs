using UnityEngine;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Base abstrata de qualquer obstáculo no tile. Provê o gancho de colisão
    /// com o player (trigger). Subclasses decidem o efeito (matar, drenar
    /// shield, deflectir, etc.).
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
            OnPlayerHit(other);
        }

        /// <summary>Implementação concreta do efeito no player.</summary>
        protected abstract void OnPlayerHit(Collider playerCollider);
    }
}
