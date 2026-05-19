using UnityEngine;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Spawna até 1 power-up no meio do tile. Paralelo ao CoinSpawner/
    /// ObstacleSpawner. Não conhece o prefab — quem chama (gerador) passa
    /// qual tipo spawnar.
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        [Tooltip("Se vazios, o PowerUpSpawner usa os do TrackTile no mesmo GameObject.")]
        public Transform startPoint;
        public Transform endPoint;

        [Tooltip("Elevação Y do power-up. Um pouco acima dos obstáculos pra distinção visual.")]
        public float spawnHeight = 0.8f;

        void Awake()
        {
            ResolvePointsFromTile();
        }

        void ResolvePointsFromTile()
        {
            if (startPoint != null && endPoint != null) return;
            var tile = GetComponent<TrackTile>();
            if (tile == null) return;
            if (startPoint == null) startPoint = tile.StartPoint;
            if (endPoint == null) endPoint = tile.EndPoint;
        }

        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            if (startPoint == null || endPoint == null)
            {
                Debug.LogWarning($"[PowerUpSpawner] start/end points not set on {name}", this);
                return null;
            }

            Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, 0.5f);
            pos.y += spawnHeight;
            return Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }
}
