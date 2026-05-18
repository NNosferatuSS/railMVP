using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Player
{
    /// <summary>
    /// Iteração 1: movimento forward simples.
    /// O player anda em linha reta sobre o eixo Z, com velocidade vinda do DifficultyManager.
    /// Sem switches, sem transição entre tiles ainda.
    /// </summary>
    public class PlayerRailRider : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RailGenConfig config;
        [SerializeField] private DifficultyManager difficulty;

        [Header("Initial Tile (Iteração 1)")]
        [Tooltip("Tile inicial onde o player começa. Em iterações futuras isso é definido pelo RailManager.")]
        [SerializeField] private TrackTile startTile;

        [Header("Runtime (read-only)")]
        [SerializeField] private float currentSpeed;
        [SerializeField] private float distanceTraveled;

        public float CurrentSpeed => currentSpeed;
        public float DistanceTraveled => distanceTraveled;

        void Start()
        {
            // Posiciona o player no startPoint do tile inicial
            if (startTile != null && startTile.StartPoint != null)
            {
                Vector3 pos = startTile.StartPoint.position;
                pos.y = transform.position.y; // mantém altura atual do player
                transform.position = pos;
            }
            else
            {
                Debug.LogWarning("[PlayerRailRider] No startTile assigned. Player will start at world origin.");
            }

            // Garante facing forward
            transform.rotation = Quaternion.identity;
        }

        void Update()
        {
            // Lê speed do tier atual
            if (difficulty != null)
            {
                currentSpeed = difficulty.CurrentTier.playerSpeed;
            }

            // Move forward
            transform.position += Vector3.forward * (currentSpeed * Time.deltaTime);
            distanceTraveled = transform.position.z;

            // Atualiza dificuldade
            if (difficulty != null)
            {
                difficulty.UpdateDistance(distanceTraveled);
            }
        }
    }
}
