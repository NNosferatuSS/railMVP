using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Algoritmo de geração procedural baseado em Critical Path (spec §4.2).
    /// Mantém o set de lanes do critical path da última linha gerada;
    /// a cada nova linha, avança cada path com offset ∈ {-1, 0, +1} com clamp
    /// nas bordas, garante criticalPathsPerRow, popula decoys conforme
    /// lanePopulationChance, e respeita minLanesPerRow/maxLanesPerRow.
    ///
    /// É um MonoBehaviour pra ser referenciado no Inspector e expor controles.
    /// Não controla streaming (rowsAhead/rowsBehind) — isso é trabalho do RailManager.
    /// </summary>
    public class ProceduralRailGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RailGenConfig config;
        [SerializeField] private GameObject tilePrefab;

        [Header("Runtime state (read-only)")]
        [SerializeField] private List<int> previousCriticalLanes = new List<int>();

        public RailGenConfig Config => config;
        public GameObject TilePrefab => tilePrefab;

        public void Configure(RailGenConfig cfg, GameObject prefab)
        {
            if (cfg != null) config = cfg;
            if (prefab != null) tilePrefab = prefab;
        }

        /// <summary>
        /// Limpa estado interno. Chame ao começar um novo run.
        /// </summary>
        public void ResetState()
        {
            previousCriticalLanes.Clear();
        }

        /// <summary>
        /// Gera a próxima linha do grid. Instancia os tiles e popula a RowData.
        /// </summary>
        /// <param name="rowIndex">Índice absoluto da linha (Z = row * (trackLength+rowGap) + ...).</param>
        /// <param name="tier">Snapshot de configuração de dificuldade ativo no momento.</param>
        /// <param name="parent">Parent opcional para os GameObjects spawnados (organização).</param>
        public RowData GenerateRow(int rowIndex, DifficultyTier tier, Transform parent = null)
        {
            if (config == null || tilePrefab == null)
            {
                Debug.LogError("[ProceduralRailGenerator] Config or TilePrefab not assigned.", this);
                return null;
            }

            int maxLanes = Mathf.Max(1, tier.maxLanes);
            int minPerRow = Mathf.Clamp(tier.minLanesPerRow, 1, maxLanes);
            int maxPerRow = Mathf.Clamp(tier.maxLanesPerRow, minPerRow, maxLanes);
            int criticalPathsPerRow = Mathf.Clamp(tier.criticalPathsPerRow, 1, maxLanes);
            float lanePopChance = Mathf.Clamp01(tier.lanePopulationChance);

            // === Step 1 + 2: avançar critical paths da linha anterior ===
            var nextCriticalLanes = new HashSet<int>();

            if (previousCriticalLanes.Count == 0)
            {
                // Bootstrap (primeira linha): centro do grid.
                int seed = maxLanes / 2;
                nextCriticalLanes.Add(seed);
            }
            else
            {
                foreach (int prevLane in previousCriticalLanes)
                {
                    int clamped = Mathf.Clamp(prevLane, 0, maxLanes - 1);
                    int offset = Random.Range(-1, 2); // -1, 0, +1
                    int newLane = Mathf.Clamp(clamped + offset, 0, maxLanes - 1);
                    nextCriticalLanes.Add(newLane);
                }
            }

            // Garante criticalPathsPerRow ativos
            int safety = maxLanes * 4;
            while (nextCriticalLanes.Count < criticalPathsPerRow && safety-- > 0)
            {
                int addLane = Random.Range(0, maxLanes);
                nextCriticalLanes.Add(addLane);
            }

            // === Step 3: marcar lanes garantidas ===
            var lanePopulated = new bool[maxLanes];
            foreach (int L in nextCriticalLanes)
                lanePopulated[L] = true;

            int totalCount = nextCriticalLanes.Count;

            // === Step 4: popular decoys ===
            for (int L = 0; L < maxLanes; L++)
            {
                if (lanePopulated[L]) continue;
                if (totalCount >= maxPerRow) break;
                if (Random.value < lanePopChance)
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // === Step 5: enforçar mínimo ===
            safety = maxLanes * 4;
            while (totalCount < minPerRow && safety-- > 0)
            {
                int L = Random.Range(0, maxLanes);
                if (!lanePopulated[L])
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // === Step 6: instanciar tiles + spawnar moedas ===
            var row = new RowData(rowIndex, maxLanes);
            var criticalLanesArr = new int[nextCriticalLanes.Count];
            int ci = 0;
            foreach (int L in nextCriticalLanes) criticalLanesArr[ci++] = L;
            row.CriticalLanes = criticalLanesArr;

            for (int L = 0; L < maxLanes; L++)
            {
                if (!lanePopulated[L]) continue;

                Vector3 worldPos = TrackTile.ComputeWorldPosition(rowIndex, L, maxLanes, config);
                var tileGo = Instantiate(tilePrefab, worldPos, Quaternion.identity, parent);
                tileGo.name = $"Tile_R{rowIndex}_L{L}";

                var tile = tileGo.GetComponent<TrackTile>();
                if (tile == null)
                {
                    Debug.LogError("[ProceduralRailGenerator] tilePrefab does not have a TrackTile component.", tilePrefab);
                    continue;
                }

                tile.Row = rowIndex;
                tile.Lane = L;
                tile.MaxLanesAtSpawn = maxLanes;
                tile.IsOnCriticalPath = nextCriticalLanes.Contains(L);

                // Estado inicial aleatório do switch
                if (tile.Switch != null)
                {
                    var randomState = (SwitchState)Random.Range(-1, 2);
                    tile.Switch.SetState(randomState);
                }

                // Moedas conforme tier (critical → mais moedas; decoy → menos/zero)
                if (tile.Coins != null)
                {
                    int coinCount = tile.IsOnCriticalPath
                        ? tier.coinsPerCriticalTile
                        : tier.coinsPerDecoyTile;
                    if (coinCount > 0)
                        tile.Coins.Spawn(coinCount, tile.IsOnCriticalPath);
                }

                row.Tiles[L] = tile;
            }

            // Atualiza estado para próxima chamada
            previousCriticalLanes.Clear();
            foreach (int L in nextCriticalLanes)
                previousCriticalLanes.Add(L);

            return row;
        }
    }
}
