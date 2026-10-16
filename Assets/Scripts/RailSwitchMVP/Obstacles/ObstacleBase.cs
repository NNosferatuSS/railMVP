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
        /// <summary>Hazard esmagado pelo cometa (vira moedas; efeitos no CometController).</summary>
        public static event System.Action<ObstacleBase> Smashed;

        bool _smashed;

        /// <summary>Esmaga este obstáculo: sem efeito no player, avisa e some.</summary>
        public void Smash()
        {
            if (_smashed) return;
            _smashed = true;
            Smashed?.Invoke(this);
            Destroy(gameObject);
        }

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

            // Cometa = esmaga (vira moedas) em vez de sofrer o efeito.
            if (PowerUpManager.Instance != null && PowerUpManager.Instance.HasComet)
            {
                Smash();
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
