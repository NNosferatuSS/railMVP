using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Distribui moedas em slots do tile (grid compartilhado com Obstacle/PowerUp).
    /// Recebe targetCount e o set de slots já reservados por hazard/power-up;
    /// nunca spawna em cima de algo reservado e clampa pra slots disponíveis.
    /// Chamado pelo ProceduralRailGenerator a cada row gerada.
    /// </summary>
    public class CoinSpawner : MonoBehaviour
    {
        [Header("References")]
        public GameObject coinPrefab;

        [Header("Layout")]
        [Tooltip("Elevação Y das moedas em relação à reta StartPoint→EndPoint")]
        public float coinHeight = 0.5f;

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
        /// Spawna até <paramref name="targetCount"/> moedas nos slots livres do tile.
        /// Estratégia define se a distribuição é determinística (UniformGrid) ou
        /// random sem repetição (RandomFree). Slots em <paramref name="reservedSlots"/>
        /// são sempre respeitados.
        /// </summary>
        public void Spawn(int targetCount, int totalSlots, float padding, HashSet<int> reservedSlots, CoinPlacement strategy)
        {
            if (targetCount <= 0) return;
            if (coinPrefab == null)
            {
                Debug.LogWarning($"[CoinSpawner] coinPrefab not set on {name}", this);
                return;
            }
            if (Tile == null)
            {
                Debug.LogWarning($"[CoinSpawner] TrackTile não encontrado em {name}", this);
                return;
            }
            if (totalSlots <= 0) return;

            if (strategy == CoinPlacement.RandomFree)
            {
                SpawnRandomFree(targetCount, totalSlots, padding, reservedSlots);
                return;
            }

            SpawnUniformGrid(targetCount, totalSlots, padding, reservedSlots);
        }

        // UniformGrid: coins em posições determinísticas do grid completo;
        // skip se reservadas. Preserva stride uniforme entre coins/hazards/powerups.
        void SpawnUniformGrid(int targetCount, int totalSlots, float padding, HashSet<int> reservedSlots)
        {
            int clampedCount = Mathf.Min(targetCount, totalSlots);
            for (int i = 0; i < clampedCount; i++)
            {
                int slot;
                if (clampedCount == 1)
                    slot = totalSlots / 2;
                else
                    slot = Mathf.RoundToInt(i * (totalSlots - 1) / (float)(clampedCount - 1));

                if (reservedSlots != null && reservedSlots.Contains(slot)) continue;

                Vector3 pos = Tile.GetSlotPosition(slot, totalSlots, padding, coinHeight);
                Instantiate(coinPrefab, pos, Quaternion.identity, transform);
            }
        }

        // RandomFree: sorteia targetCount slots livres sem repetição via
        // shuffle parcial Fisher-Yates. Stride entre coins varia tile a tile.
        void SpawnRandomFree(int targetCount, int totalSlots, float padding, HashSet<int> reservedSlots)
        {
            var free = new List<int>(totalSlots);
            for (int s = 0; s < totalSlots; s++)
                if (reservedSlots == null || !reservedSlots.Contains(s))
                    free.Add(s);
            if (free.Count == 0) return;

            int actualCount = Mathf.Min(targetCount, free.Count);
            for (int i = 0; i < actualCount; i++)
            {
                int j = Random.Range(i, free.Count);
                int picked = free[j];
                free[j] = free[i];
                free[i] = picked;

                Vector3 pos = Tile.GetSlotPosition(picked, totalSlots, padding, coinHeight);
                Instantiate(coinPrefab, pos, Quaternion.identity, transform);
            }
        }
    }
}
