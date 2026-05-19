using UnityEngine;

namespace RailSwitchMVP.Collectibles
{
    /// <summary>
    /// Moeda coletável. Trigger esférico — quando o player encosta, soma no
    /// CoinManager e auto-destrói. Sem física.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollectibleCoin : MonoBehaviour
    {
        [Tooltip("Quantos pontos esta moeda vale")]
        public int value = 1;

        [Tooltip("Velocidade de rotação visual (graus/s no eixo Y). 0 = sem giro.")]
        public float spinSpeed = 180f;

        void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Collect();
        }

        /// <summary>
        /// Coleta a moeda (soma no CoinManager + destrói). Chamável diretamente
        /// pelo MagnetPickup (PowerUpManager.Update) — sem precisar de colisão
        /// física, pra coletar moedas em lanes adjacentes.
        /// </summary>
        public void Collect()
        {
            if (CoinManager.Instance != null)
                CoinManager.Instance.AddCoins(value);
            Destroy(gameObject);
        }
    }
}
