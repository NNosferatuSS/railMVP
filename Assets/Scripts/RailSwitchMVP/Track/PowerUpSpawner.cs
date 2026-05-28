using UnityEngine;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Spawna até 1 power-up num slot do tile. Paralelo ao CoinSpawner/
    /// ObstacleSpawner. Não conhece o prefab — quem chama (gerador) passa
    /// tipo e slot. O grid de slots é compartilhado com Coins/Obstacles
    /// (definido em RailGenConfig), então reservar um slot aqui evita overlap
    /// com moedas.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [Tooltip("Elevação Y do power-up. Um pouco acima dos obstáculos pra distinção visual.")]
        public float spawnHeight = 0.8f;

        private TrackTile _tile;

        TrackTile Tile
        {
            get
            {
                if (_tile == null) _tile = GetComponent<TrackTile>();
                return _tile;
            }
        }

        public GameObject Spawn(GameObject prefab, int slotIndex, int totalSlots, float padding)
        {
            if (prefab == null) return null;
            if (Tile == null)
            {
                Debug.LogWarning($"[PowerUpSpawner] TrackTile não encontrado em {name} — não é possível posicionar slot.", this);
                return null;
            }

            Vector3 pos = Tile.GetSlotPosition(slotIndex, totalSlots, padding, spawnHeight);
            return Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }
}
