using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Resultado puro do planejamento de uma linha — sem GameObjects.
    /// Usado pelo stress test (Iter 5) e internamente por GenerateRow antes
    /// da instanciação.
    /// </summary>
    public struct RowPlan
    {
        public int RowIndex;
        public int GlobalMax;
        public int CanonLower;
        public int CanonUpper;
        public int ActiveLower;
        public int ActiveUpper;
        public bool[] LanePopulated;     // tamanho GlobalMax
        public int[] CriticalLanes;
        public int TotalTiles;
        public bool WasInTransition;     // a row foi gerada em modo transição?
    }

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

        [Tooltip("Prefab do obstáculo letal. Se vazio, MVP2 Iter 1 vira no-op " +
            "(nenhum obstáculo spawna mesmo com chance > 0 no tier).")]
        [SerializeField] private GameObject lethalObstaclePrefab;

        [Tooltip("Prefab da barreira (MVP2 Iter 4). Absorvida por shield. Se vazio, no-op.")]
        [SerializeField] private GameObject barrierObstaclePrefab;

        [Tooltip("Array de prefabs de power-up (MVP2 Iter 4). Sugestão: 4 elementos " +
            "(Shield, SlowDown, Magnet, DifficultyReset). Generator escolhe um random " +
            "uniformemente. Array vazio = no-op (power-ups não spawnam).")]
        [SerializeField] private GameObject[] powerUpPrefabs;

        [Header("Runtime state (read-only)")]
        [SerializeField] private List<int> previousCriticalLanes = new List<int>();

        [Tooltip("≥ 0 quando o gerador está em modo TRANSIÇÃO de reset — o critical path " +
            "drifta forçadamente 1 lane por linha em direção ao centro canônico do tier atual. " +
            "Volta para -1 quando o anchor alcança o centro.")]
        [SerializeField] private int transitionAnchorLane = -1;

        public RailGenConfig Config => config;
        public GameObject TilePrefab => tilePrefab;
        public bool IsInTransition => transitionAnchorLane >= 0;

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
            transitionAnchorLane = -1;
        }

        /// <summary>
        /// Inicia uma transição de reset semeada na lane atual do player.
        /// As próximas linhas terão o critical path drifting +1 lane/row em direção
        /// ao centro canônico do tier atual, expandindo o range ativo de geração
        /// conforme necessário para incluir o anchor (lane do player) e o drift.
        ///
        /// Útil quando ResetDifficulty é chamado mas o player está longe do centro:
        /// sem isso, as primeiras linhas geradas com o tier reduzido teriam critical
        /// path no centro canônico (longe do player), causando DeadEnd quase certo.
        /// </summary>
        public void SeedTransitionFromLane(int playerLane)
        {
            int globalMax = config != null ? Mathf.Max(1, config.globalMaxLanes) : 9;
            int clamped = Mathf.Clamp(playerLane, 0, globalMax - 1);
            transitionAnchorLane = clamped;
            previousCriticalLanes.Clear();
            previousCriticalLanes.Add(clamped);
            Debug.Log($"[Generator] Seeded transition from player lane {clamped} → drift toward canonical center.");
        }

        /// <summary>
        /// PLANEJA uma linha sem instanciar nada (função pura sobre o estado interno).
        /// Avança internamente <c>previousCriticalLanes</c> e <c>transitionAnchorLane</c>.
        /// Usado por GenerateRow (que adiciona instanciação) e por testes headless.
        /// </summary>
        public RowPlan PlanRow(int rowIndex, DifficultyTier tier)
        {
            if (config == null)
            {
                Debug.LogError("[ProceduralRailGenerator] Config not assigned.", this);
                return default;
            }

            int globalMax = Mathf.Max(1, config.globalMaxLanes);
            int tierMax = Mathf.Clamp(tier.maxLanes, 1, globalMax);

            // Range CANÔNICO de lanes deste tier: subset centrado em globalMax.
            // Ex: globalMax=9, tierMax=3 → canonLower=3, canonUpper=5 (lanes 3,4,5).
            //     globalMax=9, tierMax=5 → canonLower=2, canonUpper=6.
            //     globalMax=9, tierMax=9 → canonLower=0, canonUpper=8.
            int canonLower = (globalMax - tierMax) / 2;
            int canonUpper = canonLower + tierMax - 1;
            int canonCenter = (canonLower + canonUpper) / 2;

            // Range ATIVO desta linha. Em geração normal == canônico.
            // Durante transição de reset, expande pra incluir o anchor (lane do
            // player) e a posição drifted (próximo passo rumo ao centro).
            int activeLower = canonLower;
            int activeUpper = canonUpper;

            int minPerRow = Mathf.Clamp(tier.minLanesPerRow, 1, tierMax);
            int maxPerRow = Mathf.Clamp(tier.maxLanesPerRow, minPerRow, tierMax);
            int criticalPathsPerRow = Mathf.Clamp(tier.criticalPathsPerRow, 1, tierMax);
            float lanePopChance = Mathf.Clamp01(tier.lanePopulationChance);

            // === Step 1 + 2: avançar critical paths da linha anterior ===
            var nextCriticalLanes = new HashSet<int>();
            bool inTransition = transitionAnchorLane >= 0;

            if (inTransition)
            {
                // Modo TRANSIÇÃO: drift forçado de 1 lane/row em direção ao centro canônico.
                // Garante uma rota viável do player (anchor) até o centro do novo tier.
                int anchor = transitionAnchorLane;
                int drifted = anchor;
                if (anchor < canonCenter) drifted = anchor + 1;
                else if (anchor > canonCenter) drifted = anchor - 1;

                nextCriticalLanes.Add(drifted);

                // Expande active range pra cobrir o "corredor" anchor→drifted (e canônico).
                activeLower = Mathf.Min(activeLower, Mathf.Min(anchor, drifted));
                activeUpper = Mathf.Max(activeUpper, Mathf.Max(anchor, drifted));

                // Atualiza ou finaliza a transição.
                if (drifted == canonCenter || drifted == anchor)
                {
                    transitionAnchorLane = -1;
                    Debug.Log($"[Generator] Transition finished at lane {drifted} (= canonCenter {canonCenter}).");
                }
                else
                {
                    transitionAnchorLane = drifted;
                }
            }
            else if (previousCriticalLanes.Count == 0)
            {
                // Bootstrap (primeira linha): centro do grid global.
                nextCriticalLanes.Add(canonCenter);
            }
            else
            {
                foreach (int prevLane in previousCriticalLanes)
                {
                    int clamped = Mathf.Clamp(prevLane, canonLower, canonUpper);
                    int offset = Random.Range(-1, 2); // -1, 0, +1
                    int newLane = Mathf.Clamp(clamped + offset, canonLower, canonUpper);
                    nextCriticalLanes.Add(newLane);
                }
            }

            // Garante criticalPathsPerRow ativos — APENAS fora de transição.
            // Em transição, só o critical drifting (1 path) até voltar pro normal.
            int safety = tierMax * 4;
            if (!inTransition)
            {
                while (nextCriticalLanes.Count < criticalPathsPerRow && safety-- > 0)
                {
                    int addLane = Random.Range(canonLower, canonUpper + 1);
                    nextCriticalLanes.Add(addLane);
                }
            }

            // === Step 3: marcar lanes garantidas (em coordenadas globais) ===
            var lanePopulated = new bool[globalMax];
            foreach (int L in nextCriticalLanes)
                lanePopulated[L] = true;

            int totalCount = nextCriticalLanes.Count;

            // === Step 4: popular decoys (dentro do range ATIVO) ===
            for (int L = activeLower; L <= activeUpper; L++)
            {
                if (lanePopulated[L]) continue;
                if (totalCount >= maxPerRow) break;
                if (Random.value < lanePopChance)
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // === Step 5: enforçar mínimo (dentro do range ATIVO) ===
            int activeWidth = activeUpper - activeLower + 1;
            safety = activeWidth * 4;
            while (totalCount < minPerRow && safety-- > 0)
            {
                int L = Random.Range(activeLower, activeUpper + 1);
                if (!lanePopulated[L])
                {
                    lanePopulated[L] = true;
                    totalCount++;
                }
            }

            // Atualiza estado para próxima chamada
            previousCriticalLanes.Clear();
            foreach (int L in nextCriticalLanes)
                previousCriticalLanes.Add(L);

            var criticalLanesArr = new int[nextCriticalLanes.Count];
            int ci = 0;
            foreach (int L in nextCriticalLanes) criticalLanesArr[ci++] = L;

            return new RowPlan
            {
                RowIndex = rowIndex,
                GlobalMax = globalMax,
                CanonLower = canonLower,
                CanonUpper = canonUpper,
                ActiveLower = activeLower,
                ActiveUpper = activeUpper,
                LanePopulated = lanePopulated,
                CriticalLanes = criticalLanesArr,
                TotalTiles = totalCount,
                WasInTransition = inTransition,
            };
        }

        /// <summary>
        /// Gera a próxima linha do grid. Chama PlanRow internamente e materializa
        /// o resultado instanciando tiles + chamando CoinSpawner.
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

            var plan = PlanRow(rowIndex, tier);
            if (plan.LanePopulated == null) return null;

            var row = new RowData(rowIndex, plan.GlobalMax);
            row.CriticalLanes = plan.CriticalLanes;
            var criticalSet = new HashSet<int>(plan.CriticalLanes);

            for (int L = 0; L < plan.GlobalMax; L++)
            {
                if (!plan.LanePopulated[L]) continue;

                Vector3 worldPos = TrackTile.ComputeWorldPosition(rowIndex, L, plan.GlobalMax, config);
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
                tile.MaxLanesAtSpawn = plan.GlobalMax;
                tile.IsOnCriticalPath = criticalSet.Contains(L);

                if (tile.Switch != null)
                {
                    var randomState = (SwitchState)Random.Range(-1, 2);
                    tile.Switch.SetState(randomState);
                }

                if (tile.Coins != null)
                {
                    int coinCount = tile.IsOnCriticalPath
                        ? tier.coinsPerCriticalTile
                        : tier.coinsPerDecoyTile;
                    if (coinCount > 0)
                        tile.Coins.Spawn(coinCount, tile.IsOnCriticalPath);
                }

                // Hazards (Iter 1 + Iter 4): só em DECOYS. Critical path sempre limpo.
                // Ordem de rolagem: Lethal → (se não) Barrier → (se nada) PowerUp.
                // Tile recebe NO MÁXIMO um desses três.
                bool decoyHasHazard = false;
                if (!tile.IsOnCriticalPath && tile.Obstacles != null)
                {
                    // Lethal
                    if (lethalObstaclePrefab != null
                        && tier.obstacleChanceOnDecoy > 0f
                        && Random.value < tier.obstacleChanceOnDecoy)
                    {
                        tile.Obstacles.Spawn(lethalObstaclePrefab);
                        decoyHasHazard = true;
                    }
                    // Barrier (MVP2 Iter 4) — só se não rolou Lethal
                    else if (barrierObstaclePrefab != null
                        && tier.barrierChanceOnDecoy > 0f
                        && Random.value < tier.barrierChanceOnDecoy)
                    {
                        tile.Obstacles.Spawn(barrierObstaclePrefab);
                        decoyHasHazard = true;
                    }
                }

                // Power-ups (MVP2 Iter 4): em critical e decoy, com chances diferentes.
                // Tile com hazard NÃO recebe power-up (regra de design).
                if (!decoyHasHazard && tile.PowerUps != null && powerUpPrefabs != null && powerUpPrefabs.Length > 0)
                {
                    float chance = tile.IsOnCriticalPath
                        ? tier.powerUpChanceOnCritical
                        : tier.powerUpChanceOnDecoy;
                    if (chance > 0f && Random.value < chance)
                    {
                        int idx = Random.Range(0, powerUpPrefabs.Length);
                        var prefab = powerUpPrefabs[idx];
                        if (prefab != null) tile.PowerUps.Spawn(prefab);
                    }
                }

                row.Tiles[L] = tile;
            }

            return row;
        }
    }
}
