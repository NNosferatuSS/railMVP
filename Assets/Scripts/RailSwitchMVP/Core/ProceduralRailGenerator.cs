using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Track;
using RailSwitchMVP.Obstacles;

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

        [Tooltip("Prefab da SpeedUp zone (PostMVP2.5). Acelera o player por N tiles.")]
        [SerializeField] private GameObject speedUpZonePrefab;

        [Tooltip("Prefab do Lane Swap (PostMVP2.5). Inverte ←/→ por N tiles.")]
        [SerializeField] private GameObject laneSwapObstaclePrefab;

        [Tooltip("Prefab do Vortex (PostMVP2.5). Rouba escolha de switch (push pra outra lane).")]
        [SerializeField] private GameObject vortexObstaclePrefab;

        [System.Serializable]
        public struct PowerUpPrefabBinding
        {
            public PowerUpType type;
            public GameObject prefab;
        }

        [Tooltip("Bindings (type → prefab) de cada power-up que existe no jogo. " +
            "Cada tier escolhe um subset destes via DifficultyTier.powerUpPool com pesos. " +
            "Lista vazia = no-op (power-ups não spawnam mesmo com chance > 0).")]
        [SerializeField] private List<PowerUpPrefabBinding> powerUpPrefabs = new List<PowerUpPrefabBinding>();

        // Cache type→prefab pra weighted pick não pagar O(n) toda chamada.
        // Rebuildado on-demand (mudou ref ou conta). Null = ainda não build.
        private Dictionary<PowerUpType, GameObject> _powerUpByType;

        // Cores e símbolos dos warnings sobre hazards (pós-MVP2 UI hint).
        // Centralizados aqui pra fácil tunar visual sem mexer em prefabs.
        private static readonly Color _lethalWarningColor = new Color(1f, 0.2f, 0.2f); // vermelho saturado
        private static readonly Color _barrierWarningColor = new Color(1f, 0.85f, 0f); // amarelo brilhante
        private const string _lethalWarningSymbol = "X";
        private const string _barrierWarningSymbol = "!";

        [Tooltip("Quantas rows antes do hazard o ícone de warning aparece. " +
            "3 = warning visível 3 tiles ANTES do hazard real (dá tempo de planejar o switch).")]
        [SerializeField] private int warningRowsAhead = 3;

        [Header("Runtime state (read-only)")]
        [SerializeField] private List<int> previousCriticalLanes = new List<int>();

        [Tooltip("≥ 0 quando o gerador está em modo TRANSIÇÃO de reset — o critical path " +
            "drifta forçadamente 1 lane por linha em direção ao centro canônico do tier atual. " +
            "Volta para -1 quando o anchor alcança o centro.")]
        [SerializeField] private int transitionAnchorLane = -1;

        public RailGenConfig Config => config;
        public GameObject TilePrefab => tilePrefab;
        public bool IsInTransition => transitionAnchorLane >= 0;

        // Exposto pra SpawnOverrideController popular sua UI com a lista atual.
        // Retorna array flat de prefabs (skipa entries sem prefab) — compat com
        // o caminho debug-UI que indexa por GameObject reference.
        public GameObject[] PowerUpPrefabs
        {
            get
            {
                if (powerUpPrefabs == null || powerUpPrefabs.Count == 0)
                    return System.Array.Empty<GameObject>();
                var list = new List<GameObject>(powerUpPrefabs.Count);
                foreach (var b in powerUpPrefabs)
                    if (b.prefab != null) list.Add(b.prefab);
                return list.ToArray();
            }
        }

        GameObject GetPowerUpPrefab(PowerUpType type)
        {
            if (_powerUpByType == null || _powerUpByType.Count != (powerUpPrefabs?.Count ?? 0))
                RebuildPowerUpCache();
            if (_powerUpByType == null) return null;
            return _powerUpByType.TryGetValue(type, out var go) ? go : null;
        }

        void RebuildPowerUpCache()
        {
            _powerUpByType = new Dictionary<PowerUpType, GameObject>();
            if (powerUpPrefabs == null) return;
            foreach (var b in powerUpPrefabs)
            {
                if (b.prefab == null) continue;
                _powerUpByType[b.type] = b.prefab; // último ganha em caso de duplicata
            }
        }

        GameObject GetHazardPrefab(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.Lethal:   return lethalObstaclePrefab;
                case HazardKind.Barrier:  return barrierObstaclePrefab;
                case HazardKind.SpeedUp:  return speedUpZonePrefab;
                case HazardKind.LaneSwap: return laneSwapObstaclePrefab;
                case HazardKind.Vortex:   return vortexObstaclePrefab;
                default: return null;
            }
        }

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
        /// Reseta o estado de critical-lanes pro conjunto fornecido. Usado quando
        /// o RailManager despawna rows à frente (ex: override de rowsAhead via debug)
        /// — o gerador precisa saber a partir de onde continuar.
        /// </summary>
        public void SetPreviousCriticalLanes(int[] lanes)
        {
            previousCriticalLanes.Clear();
            if (lanes == null) return;
            foreach (int L in lanes) previousCriticalLanes.Add(L);
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

            // Idea 1 — Warmup rows: primeiras N rows são single-lane center, sem
            // drift, sem decoys. Resto da lógica é skip.
            if (rowIndex < config.warmupRowCount)
                return PlanWarmupRow(rowIndex, globalMax);

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

                // Spawn via pool se disponível; senão, fallback pra Instantiate normal.
                GameObject tileGo;
                bool fromPool = PrefabPool.Instance != null;
                if (fromPool)
                {
                    tileGo = PrefabPool.Instance.Spawn(tilePrefab, worldPos, Quaternion.identity, parent);
                }
                else
                {
                    tileGo = Instantiate(tilePrefab, worldPos, Quaternion.identity, parent);
                }
                tileGo.name = $"Tile_R{rowIndex}_L{L}";

                var tile = tileGo.GetComponent<TrackTile>();
                if (tile == null)
                {
                    Debug.LogError("[ProceduralRailGenerator] tilePrefab does not have a TrackTile component.", tilePrefab);
                    continue;
                }

                // Limpa estado de uso anterior (no-op em Instantiate fresh).
                if (fromPool) tile.ResetForReuse();

                tile.Row = rowIndex;
                tile.Lane = L;
                tile.MaxLanesAtSpawn = plan.GlobalMax;
                tile.IsOnCriticalPath = criticalSet.Contains(L);

                // Idea 1 — warmup row: tile center sem switch state aleatório
                // (mantém Middle), sem coins/hazards/power-ups.
                bool isWarmupRow = rowIndex < config.warmupRowCount;

                if (tile.Switch != null)
                {
                    var initialState = isWarmupRow
                        ? SwitchState.Middle
                        : (SwitchState)Random.Range(-1, 2);
                    tile.Switch.SetState(initialState);
                }

                // Reserva de slots: hazard primeiro, depois power-up, depois coins
                // preenchem o que sobrou. Garante zero overlap entre os 3.
                int totalSlots = Mathf.Max(1, config.coinSlotsPerTile);
                float slotPadding = Mathf.Clamp(config.coinSlotPadding, 0f, 0.45f);
                HashSet<int> reservedSlots = null;

                // Hazards: roteado via SpawnOverrideController quando presente.
                // Master OFF (ou controller ausente) = comportamento clássico (só decoy, cascata Lethal→...→Vortex).
                // Master ON = chances/locations vindas da UI F2 (pode incluir critical).
                bool tileHasHazard = false;
                if (!isWarmupRow && tile.Obstacles != null)
                {
                    HazardResolution hz;
                    bool useOverride = SpawnOverrideController.Instance != null
                        && SpawnOverrideController.Instance.MasterEnabled;
                    if (useOverride)
                    {
                        hz = SpawnOverrideController.Instance.ResolveHazardOverride(
                            tile.IsOnCriticalPath,
                            lethalObstaclePrefab, barrierObstaclePrefab,
                            speedUpZonePrefab, laneSwapObstaclePrefab, vortexObstaclePrefab);
                    }
                    else
                    {
                        hz = ResolveHazardClassic(tile.IsOnCriticalPath, tier);
                    }

                    if (hz.prefab != null)
                    {
                        int hazardSlot = PickSlot(config.hazardSlotStrategy, totalSlots, reservedSlots);
                        if (reservedSlots == null) reservedSlots = new HashSet<int>();
                        reservedSlots.Add(hazardSlot);

                        var hazardGo = tile.Obstacles.Spawn(hz.prefab, hazardSlot, totalSlots, slotPadding);
                        if (hz.kind == HazardKind.Lethal)
                            AttachWarning(hazardGo, _lethalWarningColor, _lethalWarningSymbol);
                        else if (hz.kind == HazardKind.Barrier)
                            AttachWarning(hazardGo, _barrierWarningColor, _barrierWarningSymbol);
                        tileHasHazard = true;
                    }
                }

                // Power-ups: idem rota via override. Tile com hazard nunca recebe.
                if (!isWarmupRow && !tileHasHazard && tile.PowerUps != null && powerUpPrefabs != null && powerUpPrefabs.Count > 0)
                {
                    bool useOverride = SpawnOverrideController.Instance != null
                        && SpawnOverrideController.Instance.MasterEnabled;
                    GameObject puPrefab = useOverride
                        ? SpawnOverrideController.Instance.ResolvePowerUpPrefabOverride(tile.IsOnCriticalPath, PowerUpPrefabs)
                        : ResolvePowerUpClassic(tile.IsOnCriticalPath, tier);
                    if (puPrefab != null)
                    {
                        int puSlot = PickSlot(config.powerUpSlotStrategy, totalSlots, reservedSlots);
                        if (reservedSlots == null) reservedSlots = new HashSet<int>();
                        reservedSlots.Add(puSlot);
                        tile.PowerUps.Spawn(puPrefab, puSlot, totalSlots, slotPadding);
                    }
                }

                // Coins por último — recebem o set de slots reservados e
                // a strategy (UniformGrid ou RandomFree).
                // Slice 2: count é sample em [min, max] por tile.
                if (!isWarmupRow && tile.Coins != null)
                {
                    // Decoy passa por uma chance configurável de receber coins (controle
                    // granular por tier). Critical path sempre recebe.
                    bool decoySkipsCoins = !tile.IsOnCriticalPath && Random.value >= tier.decoyCoinChance;
                    if (!decoySkipsCoins)
                    {
                        int coinMin = tile.IsOnCriticalPath ? tier.criticalCoinsMin : tier.decoyCoinsMin;
                        int coinMax = tile.IsOnCriticalPath ? tier.criticalCoinsMax : tier.decoyCoinsMax;
                        if (coinMax < coinMin) coinMax = coinMin;
                        int coinCount = coinMin == coinMax ? coinMin : Random.Range(coinMin, coinMax + 1);
                        if (coinCount > 0)
                            tile.Coins.Spawn(coinCount, totalSlots, slotPadding, reservedSlots, config.coinSlotStrategy);
                    }
                }

                row.Tiles[L] = tile;

                // Força atualização da cor de conectividade (Idea 2).
                // Switch.SetState pode não ter mudado state (já era Middle, etc),
                // então não dispararia o evento. Mas tile.Row é novo agora.
                tile.UpdateConnectivityVisual();
            }

            return row;
        }

        /// <summary>
        /// Planeja uma row de WARMUP: single tile no centro do grid, sem
        /// hazards/coins/power-ups. Usado pelas primeiras N rows do jogo
        /// pra dar tempo do player se orientar.
        /// Atualiza previousCriticalLanes pro centro pra que a primeira row
        /// procedural depois do warmup drifte coerentemente.
        /// </summary>
        RowPlan PlanWarmupRow(int rowIndex, int globalMax)
        {
            int centerLane = globalMax / 2;
            var lanePopulated = new bool[globalMax];
            lanePopulated[centerLane] = true;

            // Anchor critical no centro pra continuidade pós-warmup.
            previousCriticalLanes.Clear();
            previousCriticalLanes.Add(centerLane);
            // Reset transição (se acaso o reset rolou durante warmup).
            transitionAnchorLane = -1;

            return new RowPlan
            {
                RowIndex = rowIndex,
                GlobalMax = globalMax,
                CanonLower = centerLane,
                CanonUpper = centerLane,
                ActiveLower = centerLane,
                ActiveUpper = centerLane,
                LanePopulated = lanePopulated,
                CriticalLanes = new[] { centerLane },
                TotalTiles = 1,
                WasInTransition = false,
            };
        }

        /// <summary>
        /// Adiciona um HazardWarning (ícone flutuante) num hazard spawnado,
        /// posicionado N rows à frente do hazard (mais perto do player).
        /// Player vê o aviso bem antes de chegar no hazard.
        /// </summary>
        void AttachWarning(GameObject hazardGo, Color color, string symbol)
        {
            if (hazardGo == null || config == null) return;
            var warning = hazardGo.GetComponent<HazardWarning>();
            if (warning == null) warning = hazardGo.AddComponent<HazardWarning>();

            // Offset Z negativo = ícone aparece "N rows antes" do hazard
            // (mais próximo do player, no eixo Z forward).
            float leadZ = -warningRowsAhead * (config.trackLength + config.rowGap);
            warning.Setup(color, symbol, leadZ);
        }

        /// <summary>
        /// Escolhe um slot livre conforme a strategy. Funciona graceful mesmo se
        /// todos os slots estão reservados (retorna o central — não deveria
        /// acontecer com slot count saudável, mas evita exception).
        /// </summary>
        int PickSlot(SlotPlacement strategy, int totalSlots, HashSet<int> reservedSlots)
        {
            if (totalSlots <= 1) return 0;
            int center = totalSlots / 2;

            bool IsFree(int s) => reservedSlots == null || !reservedSlots.Contains(s);

            if (strategy == SlotPlacement.CenterSlot)
            {
                if (IsFree(center)) return center;
                // Centro reservado: busca o livre mais próximo do centro,
                // alternando ±1, ±2... pra ter preferência simétrica.
                for (int d = 1; d < totalSlots; d++)
                {
                    int left = center - d;
                    int right = center + d;
                    if (left >= 0 && IsFree(left)) return left;
                    if (right < totalSlots && IsFree(right)) return right;
                }
                return center; // todos reservados, fallback
            }

            // RandomFree
            // Coleta os livres e sorteia. Lista alocada por chamada — é OK pq
            // só roda quando há hazard/powerup (raro vs total de tiles).
            var freeBuf = new List<int>(totalSlots);
            for (int s = 0; s < totalSlots; s++)
                if (IsFree(s)) freeBuf.Add(s);
            if (freeBuf.Count == 0) return center;
            return freeBuf[Random.Range(0, freeBuf.Count)];
        }

        // Fallback usado quando SpawnOverrideController não está na cena.
        // Modelo: 1 chance global (hazardChanceOnDecoy) + weighted pick no pool.
        HazardResolution ResolveHazardClassic(bool isCritical, DifficultyTier tier)
        {
            if (isCritical) return HazardResolution.None;
            if (tier.hazardChanceOnDecoy <= 0f || tier.hazardPool == null || tier.hazardPool.Count == 0)
                return HazardResolution.None;
            if (Random.value >= tier.hazardChanceOnDecoy) return HazardResolution.None;

            HazardKind kind = WeightedPickHazard(tier.hazardPool.entries);
            GameObject prefab = GetHazardPrefab(kind);
            if (prefab == null) return HazardResolution.None;
            return new HazardResolution { prefab = prefab, kind = kind };
        }

        GameObject ResolvePowerUpClassic(bool isCritical, DifficultyTier tier)
        {
            float chance = isCritical ? tier.powerUpChanceOnCritical : tier.powerUpChanceOnDecoy;
            if (chance <= 0f || Random.value >= chance) return null;
            if (tier.powerUpPool == null || tier.powerUpPool.Count == 0) return null;

            PowerUpType type;
            if (!TryWeightedPickPowerUp(tier.powerUpPool.entries, out type)) return null;
            return GetPowerUpPrefab(type);
        }

        // Sorteia uma HazardKind do pool ponderado. Pesos ≤ 0 são ignorados.
        // Se soma de pesos = 0, retorna None (caller trata).
        HazardKind WeightedPickHazard(List<HazardWeight> pool)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].weight > 0f) total += pool[i].weight;
            if (total <= 0f) return HazardKind.None;

            float r = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].weight <= 0f) continue;
                acc += pool[i].weight;
                if (r < acc) return pool[i].kind;
            }
            return pool[pool.Count - 1].kind; // fallback numérico
        }

        bool TryWeightedPickPowerUp(List<PowerUpWeight> pool, out PowerUpType type)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].weight > 0f) total += pool[i].weight;
            if (total <= 0f) { type = default; return false; }

            float r = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].weight <= 0f) continue;
                acc += pool[i].weight;
                if (r < acc) { type = pool[i].type; return true; }
            }
            type = pool[pool.Count - 1].type;
            return true;
        }
    }
}
