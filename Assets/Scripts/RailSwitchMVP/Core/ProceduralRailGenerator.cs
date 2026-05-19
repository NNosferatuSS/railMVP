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

            int globalMax = Mathf.Max(1, config.globalMaxLanes);
            int tierMax = Mathf.Clamp(tier.maxLanes, 1, globalMax);

            // Range fixo de lanes ATIVAS deste tier: subset centrado em globalMax.
            // Ex: globalMax=9, tierMax=3 → lowerBound=3, upperBound=5 (lanes 3,4,5).
            //     globalMax=9, tierMax=5 → lowerBound=2, upperBound=6.
            //     globalMax=9, tierMax=9 → lowerBound=0, upperBound=8.
            int lowerBound = (globalMax - tierMax) / 2;
            int upperBound = lowerBound + tierMax - 1;

            int minPerRow = Mathf.Clamp(tier.minLanesPerRow, 1, tierMax);
            int maxPerRow = Mathf.Clamp(tier.maxLanesPerRow, minPerRow, tierMax);
            int criticalPathsPerRow = Mathf.Clamp(tier.criticalPathsPerRow, 1, tierMax);
            float lanePopChance = Mathf.Clamp01(tier.lanePopulationChance);

            // === Step 1 + 2: avançar critical paths da linha anterior ===
            var nextCriticalLanes = new HashSet<int>();

            if (previousCriticalLanes.Count == 0)
            {
                // Bootstrap (primeira linha): centro do grid global.
                int seed = (lowerBound + upperBound) / 2;
                nextCriticalLanes.Add(seed);
            }
            else
            {
                foreach (int prevLane in previousCriticalLanes)
                {
                    // Clamp em [lowerBound, upperBound] — se o tier encolheu,
                    // critical paths fora do range são puxados pra dentro.
                    int clamped = Mathf.Clamp(prevLane, lowerBound, upperBound);
                    int offset = Random.Range(-1, 2); // -1, 0, +1
                    int newLane = Mathf.Clamp(clamped + offset, lowerBound, upperBound);
                    nextCriticalLanes.Add(newLane);
                }
            }

            // Garante criticalPathsPerRow ativos
            int safety = tierMax * 4;
            while (nextCriticalLanes.Count < criticalPathsPerRow && safety-- > 0)
            {
                int addLane = Random.Range(lowerBound, upperBound + 1);
                nextCriticalLanes.Add(addLane);
            }

            // === Step 3: marcar lanes garantidas (em coordenadas globais) ===
            var lanePopulated = new bool[globalMax];
            foreach (int L in nextCriticalLanes)
                lanePopulated[L] = true;

            int totalCount = nextCriticalLanes.Count;

            // === Step 4: popular decoys (apenas dentro do range ativo do tier) ===
            for (int L = lowerBound; L <= upperBound; L++)
            {
                if (lanePopulated[L]) continue;
                if (totalCount >= maxPerRow) break;
                if (Random.value < lanePopChance)
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // === Step 5: enforçar mínimo (apenas dentro do range ativo) ===
            safety = tierMax * 4;
            while (totalCount < minPerRow && safety-- > 0)
            {
                int L = Random.Range(lowerBound, upperBound + 1);
                if (!lanePopulated[L])
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // === Step 6: instanciar tiles + spawnar moedas ===
            // RowData tem capacidade globalMax (X estável; lanes inativas ficam null).
            var row = new RowData(rowIndex, globalMax);
            var criticalLanesArr = new int[nextCriticalLanes.Count];
            int ci = 0;
            foreach (int L in nextCriticalLanes) criticalLanesArr[ci++] = L;
            row.CriticalLanes = criticalLanesArr;

            for (int L = 0; L < globalMax; L++)
            {
                if (!lanePopulated[L]) continue;

                Vector3 worldPos = TrackTile.ComputeWorldPosition(rowIndex, L, globalMax, config);
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
                tile.MaxLanesAtSpawn = globalMax;
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
