using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Pool de GameObjects keyed por prefab. Reusa instâncias em vez de
    /// Instantiate/Destroy — reduz GC alloc especialmente em mobile.
    ///
    /// API:
    /// - Spawn(prefab, pos, rot, parent): retorna instância (reusada ou nova).
    /// - Release(go): retorna ao pool (SetActive false + reparent ao container).
    ///
    /// Singleton via GameObject na cena. Pools desfazem-se com scene reload.
    /// </summary>
    public class PrefabPool : MonoBehaviour
    {
        public static PrefabPool Instance { get; private set; }

        [Header("Tunables")]
        [SerializeField] private int defaultCapacity = 16;
        [SerializeField] private int maxSize = 200;

        // Pools por prefab InstanceID.
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new();
        // Mapa instância → prefab pra saber qual pool retornar.
        private readonly Dictionary<int, int> _instanceToPrefab = new();
        // Pra createFunc poder capturar o prefab certo.
        private readonly Dictionary<int, GameObject> _prefabsById = new();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _pools.Clear();
            _instanceToPrefab.Clear();
            _prefabsById.Clear();
        }

        /// <summary>
        /// Pega uma instância do prefab. Cria nova se o pool tá vazio.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null) return null;

            var pool = GetOrCreatePool(prefab);
            var go = pool.Get();
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);

            _instanceToPrefab[go.GetInstanceID()] = prefab.GetInstanceID();
            return go;
        }

        /// <summary>
        /// Retorna instância ao pool. Se não foi pego de um pool conhecido,
        /// faz Destroy (fallback seguro).
        /// </summary>
        public void Release(GameObject go)
        {
            if (go == null) return;

            int instId = go.GetInstanceID();
            if (!_instanceToPrefab.TryGetValue(instId, out var prefabId))
            {
                Destroy(go);
                return;
            }

            if (_pools.TryGetValue(prefabId, out var pool))
            {
                pool.Release(go);
            }
            else
            {
                Destroy(go);
            }
        }

        ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (_pools.TryGetValue(id, out var existing)) return existing;

            _prefabsById[id] = prefab;

            var pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: go => go.SetActive(true),
                actionOnRelease: go =>
                {
                    go.SetActive(false);
                    go.transform.SetParent(transform, false);
                },
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
            _pools[id] = pool;
            return pool;
        }
    }
}
