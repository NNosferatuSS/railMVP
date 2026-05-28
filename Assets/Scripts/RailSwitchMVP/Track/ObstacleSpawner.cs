using UnityEngine;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Spawna até 1 obstáculo num slot do tile. Paralelo ao CoinSpawner.
    /// Não conhece o prefab — quem chama (geralmente o ProceduralRailGenerator)
    /// passa qual tipo spawnar e em qual slot. O grid de slots é o mesmo
    /// compartilhado por Coins/PowerUps (definido em RailGenConfig), então
    /// reservar um slot aqui impede que CoinSpawner spawne em cima.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Tooltip("Elevação Y do obstáculo em relação à reta StartPoint→EndPoint")]
        public float obstacleHeight = 0.5f;

        // Cache do TrackTile pra GetSlotPosition; resolvido lazy no Spawn.
        private TrackTile _tile;

        TrackTile Tile
        {
            get
            {
                if (_tile == null) _tile = GetComponent<TrackTile>();
                return _tile;
            }
        }

        /// <summary>
        /// Instancia um obstáculo no slot indicado (usa o grid de slots do tile).
        /// Retorna o GameObject criado, ou null em erro.
        /// </summary>
        public GameObject Spawn(GameObject prefab, int slotIndex, int totalSlots, float padding)
        {
            if (prefab == null) return null;
            if (Tile == null)
            {
                Debug.LogWarning($"[ObstacleSpawner] TrackTile não encontrado em {name} — não é possível posicionar slot.", this);
                return null;
            }

            Vector3 pos = Tile.GetSlotPosition(slotIndex, totalSlots, padding, obstacleHeight);
            return Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }
}
