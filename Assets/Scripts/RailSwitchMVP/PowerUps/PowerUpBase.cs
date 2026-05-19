using UnityEngine;

namespace RailSwitchMVP.PowerUps
{
    /// <summary>
    /// Pickup base. GameObject visível no tile que, ao tocar o player,
    /// chama Activate() e destrói a si mesmo.
    ///
    /// Activate é virtual — subclasses fazem o grant correspondente no
    /// PowerUpManager (ou ação direta, no caso do DifficultyReset).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class PowerUpBase : MonoBehaviour
    {
        [Tooltip("Velocidade de rotação visual (graus/s no eixo Y). 0 = sem giro.")]
        public float spinSpeed = 120f;

        void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            Activate();
            Destroy(gameObject);
        }

        protected abstract void Activate();
    }
}
