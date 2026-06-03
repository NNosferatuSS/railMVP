using UnityEngine;
using RailSwitchMVP.Economy;

namespace RailSwitchMVP.Collectibles
{
    /// <summary>
    /// Gema coletável. Ao tocar o player, adiciona 1 gem via CurrencyManager e
    /// auto-destrói. Spawna na pista com chance muito baixa (configurada no RailGenConfig).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GemPickup : MonoBehaviour
    {
        [Tooltip("Quantas gems esta pedra vale. Default 1.")]
        public int value = 1;

        [Tooltip("Velocidade de rotação visual (graus/s no eixo Y). 0 = sem giro.")]
        public float spinSpeed = 90f;

        [Tooltip("Frequência do brilho pulsante (Hz).")]
        public float pulseFrequency = 2f;

        [Tooltip("Amplitude da escala do pulse (0.2 = vai de 0.8x até 1.2x).")]
        [Range(0f, 0.5f)]
        public float pulseAmplitude = 0.2f;

        private Vector3 _baseScale;

        void Awake()
        {
            _baseScale = transform.localScale;
        }

        void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
            transform.localScale = _baseScale * pulse;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Collect();
        }

        public void Collect()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.Add(CurrencyType.Gems, value, "pickup");

            Debug.Log($"[GemPickup] +{value} gem(s) coletada(s)!");
            Destroy(gameObject);
        }
    }
}
