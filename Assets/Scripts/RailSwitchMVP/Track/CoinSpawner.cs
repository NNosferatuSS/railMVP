using UnityEngine;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Distribui moedas equidistantes ao longo do tile (entre StartPoint e EndPoint).
    /// Chamado pelo gerador procedural (Iter 3+) ou manualmente na cena (Iter 2).
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        [Header("References")]
        public GameObject coinPrefab;

        [Tooltip("Se vazios, o CoinSpawner usa os do TrackTile no mesmo GameObject.")]
        public Transform startPoint;
        public Transform endPoint;

        [Header("Layout")]
        [Range(0f, 0.45f)]
        [Tooltip("Fração do comprimento mantida livre em cada extremidade")]
        public float padding = 0.1f;

        [Tooltip("Elevação Y das moedas em relação à reta StartPoint→EndPoint")]
        public float coinHeight = 0.5f;

        [Header("Iter 2 — Setup manual")]
        [Tooltip("Quantidade de moedas a spawnar no Start(). Em Iter 3+ o gerador chama Spawn() diretamente.")]
        public int spawnOnStartCount = 3;

        [Tooltip("Marca para o gerador (Iter 3+). Em Iter 2 não tem efeito além de log.")]
        public bool isCriticalPath = true;

        void Start()
        {
            ResolvePointsFromTile();
            if (spawnOnStartCount > 0)
                Spawn(spawnOnStartCount, isCriticalPath);
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
        /// Spawna coinCount moedas equidistantes entre StartPoint e EndPoint.
        /// </summary>
        public void Spawn(int coinCount, bool critical)
        {
            if (coinCount <= 0) return;
            if (coinPrefab == null)
            {
                Debug.LogWarning($"[CoinSpawner] coinPrefab not set on {name}", this);
                return;
            }
            if (startPoint == null || endPoint == null)
            {
                Debug.LogWarning($"[CoinSpawner] start/end points not set on {name}", this);
                return;
            }

            for (int i = 0; i < coinCount; i++)
            {
                float t = Mathf.Lerp(padding, 1f - padding, (i + 0.5f) / coinCount);
                Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, t);
                pos.y += coinHeight;
                Instantiate(coinPrefab, pos, Quaternion.identity, transform);
            }
        }
    }
}
