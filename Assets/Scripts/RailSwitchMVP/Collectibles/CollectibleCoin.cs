using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Collectibles
{
    /// <summary>
    /// Moeda coletável. Trigger esférico — quando o player encosta, soma no
    /// CoinManager e auto-destrói. Sem física.
    ///
    /// PostMVP2.2: aplica CoinMultiplier do PowerUpManager (DoubleCoins).
    /// Quando CoinRadar ativo, pulsa escala pra ficar mais visível de longe.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CollectibleCoin : MonoBehaviour
    {
        [Tooltip("Quantos pontos esta moeda vale")]
        public int value = 1;

        [Tooltip("Velocidade de rotação visual (graus/s no eixo Y). 0 = sem giro.")]
        public float spinSpeed = 180f;

        [Tooltip("Frequência do pulse (Hz) quando CoinRadar ativo.")]
        public float radarPulseFrequency = 4f;

        [Tooltip("Amplitude do pulse (0.3 = vai de 0.7x até 1.3x do tamanho base).")]
        [Range(0f, 1f)]
        public float radarPulseAmplitude = 0.3f;

        private Vector3 _baseScale;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            // CoinRadar visual: pulse scale.
            bool radar = PowerUpManager.Instance != null && PowerUpManager.Instance.HasCoinRadar;
            if (radar)
            {
                float pulse = 1f + Mathf.Sin(Time.time * radarPulseFrequency * Mathf.PI * 2f) * radarPulseAmplitude;
                transform.localScale = _baseScale * pulse;
            }
            else if (transform.localScale != _baseScale)
            {
                transform.localScale = _baseScale;
            }
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
        ///
        /// Aplica CoinMultiplier do PowerUpManager (1 normal, 2 com DoubleCoins).
        /// </summary>
        public void Collect()
        {
            if (CoinManager.Instance != null)
            {
                int multiplier = PowerUpManager.Instance != null ? PowerUpManager.Instance.CoinMultiplier : 1;
                CoinManager.Instance.AddCoins(value * multiplier);
            }
            Destroy(gameObject);
        }
    }
}
