using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Track;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Iter 2: gerenciava registro de tiles hardcoded na cena.
    /// Iter 3: agora ALÉM disso, controla streaming — spawna linhas à frente
    /// do player via ProceduralRailGenerator e despawna linhas que ficam atrás.
    ///
    /// API antiga preservada (RegisterTile, GetRow) para backwards-compat —
    /// tiles continuam podendo se auto-registrar no Start.
    /// </summary>
    public class RailManager : MonoBehaviour
    {
        public static RailManager Instance { get; private set; }

        [Header("Generation")]
        [SerializeField] private RailGenConfig config;
        [SerializeField] private ProceduralRailGenerator generator;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private PlayerRailRider player;

        [Tooltip("Parent para os tiles spawnados (organização da hierarchy)")]
        [SerializeField] private Transform tilesParent;

        [Header("Runtime (read-only)")]
        [SerializeField] private int highestSpawnedRow = -1;
        [SerializeField] private int lowestSpawnedRow = int.MaxValue;

        private readonly Dictionary<int, RowData> _rows = new Dictionary<int, RowData>();

        public int RowCount => _rows.Count;
        public int HighestSpawnedRow => highestSpawnedRow;
        public int LowestSpawnedRow => lowestSpawnedRow;

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
            if (difficulty != null) difficulty.OnDifficultyReset -= HandleDifficultyReset;
        }

        void Start()
        {
            if (generator == null || config == null || difficulty == null)
            {
                Debug.LogWarning("[RailManager] Generator/Config/Difficulty not assigned — streaming desativado.");
                return;
            }

            difficulty.OnDifficultyReset += HandleDifficultyReset;

            // Bootstrap: gera as primeiras rowsAhead linhas a partir do índice 0.
            BootstrapInitialRows();

            // Se o Player ainda não tem startTile, dá um da linha 0 (centro do critical path).
            AssignPlayerStartTileIfNeeded();
        }

        // Flag setada por HandleDifficultyReset. Quando a próxima linha NOVA for
        // gerada (não as já bufferizadas), o RailManager semeia o generator com a
        // lane atual do player. Atrasar a seed garante que o anchor reflita onde
        // o player REALMENTE está no momento da geração, não a lane no instante do
        // reset (que pode ser ~12 linhas atrás).
        private bool _pendingTransitionSeed;

        /// <summary>
        /// Reset de dificuldade. Tiles e player permanecem onde estão. O generator
        /// é colocado em modo "pending transition" — a primeira linha NOVA gerada
        /// após este reset será semeada com a lane atual do player, iniciando uma
        /// transição que drifta o critical path de 1 lane/row até o centro canônico
        /// do tier 0. Isso evita DeadEnd injusto quando o player está longe do
        /// centro no momento do reset.
        /// </summary>
        void HandleDifficultyReset()
        {
            if (generator == null) return;

            generator.ResetState();
            _pendingTransitionSeed = true;
        }

        void Update()
        {
            if (generator == null || config == null || difficulty == null || player == null) return;
            if (player.CurrentTile == null) return;

            int playerRow = player.CurrentTile.Row;
            int effectiveRowsAhead = GetEffectiveRowsAhead();

            // Despawn-ahead: usado quando o SpawnOverrideController reduz rowsAhead.
            // Sem isso, rows com config velha ficariam à frente do player por mais
            // 12 tiles antes do override aparecer. Re-seed do gerador na row mais
            // alta sobrevivente pra que a continuação do critical path seja coerente.
            if (highestSpawnedRow > playerRow + effectiveRowsAhead)
            {
                while (highestSpawnedRow > playerRow + effectiveRowsAhead)
                {
                    DespawnRow(highestSpawnedRow);
                    highestSpawnedRow--;
                }
                ReseedGeneratorFromSurvivingTop();
            }

            // Spawn ahead
            while (highestSpawnedRow < playerRow + effectiveRowsAhead)
            {
                SpawnRow(highestSpawnedRow + 1);
            }

            // Despawn behind
            while (lowestSpawnedRow <= playerRow - config.rowsBehind)
            {
                DespawnRow(lowestSpawnedRow);
                lowestSpawnedRow++;
            }
        }

        int GetEffectiveRowsAhead()
        {
            int defaultAhead = config.rowsAhead;
            var ov = SpawnOverrideController.Instance;
            if (ov != null) return ov.GetEffectiveRowsAhead(defaultAhead);
            return defaultAhead;
        }

        void ReseedGeneratorFromSurvivingTop()
        {
            if (generator == null) return;
            if (!_rows.TryGetValue(highestSpawnedRow, out var top) || top == null) return;
            generator.SetPreviousCriticalLanes(top.CriticalLanes);
        }

        void BootstrapInitialRows()
        {
            generator.ResetState();
            int rowsToSpawn = Mathf.Max(2, config.rowsAhead);
            for (int i = 0; i < rowsToSpawn; i++)
                SpawnRow(i);
        }

        void AssignPlayerStartTileIfNeeded()
        {
            if (player == null) return;
            if (player.CurrentTile != null) return;

            var row0 = GetRow(0);
            if (row0 == null) return;

            // Prefere um tile que está no critical path; senão, primeiro disponível.
            TrackTile chosen = null;
            for (int L = 0; L < row0.Tiles.Length; L++)
            {
                if (row0.Tiles[L] != null && row0.Tiles[L].IsOnCriticalPath)
                {
                    chosen = row0.Tiles[L];
                    break;
                }
            }
            if (chosen == null)
            {
                for (int L = 0; L < row0.Tiles.Length; L++)
                {
                    if (row0.Tiles[L] != null) { chosen = row0.Tiles[L]; break; }
                }
            }

            if (chosen != null)
                player.SetStartTile(chosen);
        }

        void SpawnRow(int rowIndex)
        {
            // Se há uma transição pendente de reset, semeia com a lane ATUAL do player
            // antes de gerar — assim o anchor reflete onde o player está agora, não onde
            // ele estava ~12 rows atrás no instante do reset.
            if (_pendingTransitionSeed && player != null && player.CurrentTile != null && generator != null)
            {
                generator.SeedTransitionFromLane(player.CurrentTile.Lane);
                _pendingTransitionSeed = false;
            }

            var tier = difficulty.CurrentTier;
            var row = generator.GenerateRow(rowIndex, tier, tilesParent);
            if (row == null) return;

            _rows[rowIndex] = row;
            highestSpawnedRow = Mathf.Max(highestSpawnedRow, rowIndex);
            if (rowIndex < lowestSpawnedRow) lowestSpawnedRow = rowIndex;

            // Atualiza conectividade da row anterior (agora que sabemos
            // quais lanes estão populadas na row N+1).
            UpdateConnectivityForPreviousRow(rowIndex, row);
        }

        /// <summary>
        /// Quando uma nova row spawna, pede pros tiles da row anterior
        /// re-avaliarem suas conectividades (cor verde/vermelho do Arrow).
        /// Cada tile checa baseado na sua própria switch state — apontando
        /// pra lane vazia = vermelho, pra lane com tile = verde.
        /// </summary>
        void UpdateConnectivityForPreviousRow(int newRowIndex, RowData newRow)
        {
            var prevRow = GetRow(newRowIndex - 1);
            if (prevRow == null) return;

            for (int L = 0; L < prevRow.Tiles.Length; L++)
            {
                var tile = prevRow.Tiles[L];
                if (tile != null) tile.UpdateConnectivityVisual();
            }
        }

        void DespawnRow(int rowIndex)
        {
            if (!_rows.TryGetValue(rowIndex, out var row)) return;

            for (int L = 0; L < row.Tiles.Length; L++)
            {
                if (row.Tiles[L] == null) continue;

                var tileGo = row.Tiles[L].gameObject;
                // Pool retorna se disponível; senão, Destroy.
                if (PrefabPool.Instance != null)
                    PrefabPool.Instance.Release(tileGo);
                else
                    Destroy(tileGo);
            }
            _rows.Remove(rowIndex);
        }

        // ===== API preservada da Iter 2 =====

        /// <summary>
        /// Registra um tile manualmente (cena hardcoded ou fallback do TrackTile.Start).
        /// Idempotente: se a linha já existe (gerador procedural já criou), apenas
        /// confirma o slot — não sobrescreve CriticalLanes nem mexe na geometria.
        /// </summary>
        public void RegisterTile(TrackTile tile)
        {
            if (tile == null) return;

            if (!_rows.TryGetValue(tile.Row, out var row))
            {
                int initialCapacity = Mathf.Max(tile.MaxLanesAtSpawn, tile.Lane + 1);
                row = new RowData(tile.Row, initialCapacity);
                _rows[tile.Row] = row;
                highestSpawnedRow = Mathf.Max(highestSpawnedRow, tile.Row);
                if (tile.Row < lowestSpawnedRow) lowestSpawnedRow = tile.Row;
            }
            else if (tile.Lane >= row.Tiles.Length)
            {
                var bigger = new TrackTile[tile.Lane + 1];
                System.Array.Copy(row.Tiles, bigger, row.Tiles.Length);
                row.Tiles = bigger;
                row.MaxLanesAtSpawn = bigger.Length;
            }

            row.Tiles[tile.Lane] = tile;
        }

        public RowData GetRow(int rowIndex)
        {
            _rows.TryGetValue(rowIndex, out var r);
            return r;
        }

        public bool HasRow(int rowIndex) => _rows.ContainsKey(rowIndex);
    }
}
