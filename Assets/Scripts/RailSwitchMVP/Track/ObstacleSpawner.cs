using UnityEngine;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Spawna até 1 obstáculo no meio do tile. Paralelo ao CoinSpawner.
    /// Não conhece o prefab — quem chama (geralmente o ProceduralRailGenerator)
    /// passa qual tipo spawnar. Isso permite Iter 4 ter Barreira como
    /// segundo tipo sem mudar este componente.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Tooltip("Se vazios, o ObstacleSpawner usa os do TrackTile no mesmo GameObject.")]
        public Transform startPoint;
        public Transform endPoint;

        [Tooltip("Elevação Y do obstáculo em relação à reta StartPoint→EndPoint")]
        public float obstacleHeight = 0.5f;

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

        /// <summary>
        /// Instancia um obstáculo no meio do tile (t=0.5) elevado em obstacleHeight.
        /// Retorna o GameObject criado, ou null em erro.
        /// </summary>
        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            if (startPoint == null || endPoint == null)
            {
                Debug.LogWarning($"[ObstacleSpawner] start/end points not set on {name}", this);
                return null;
            }

            Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, 0.5f);
            pos.y += obstacleHeight;
            return Instantiate(prefab, pos, Quaternion.identity, transform);
        }
    }
}
