using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Mantém o registro de linhas/tiles ativos. Iter 2: tiles se auto-registram
    /// no Awake (cena hardcoded). Iter 3: ProceduralRailGenerator passa a popular.
    /// </summary>
    public class RailManager : MonoBehaviour
    {
        public static RailManager Instance { get; private set; }

        private readonly Dictionary<int, RowData> _rows = new Dictionary<int, RowData>();

        public int RowCount => _rows.Count;

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
        }

        /// <summary>
        /// Registra um tile. Cria a RowData se ainda não existe.
        /// Expande o array de tiles se a lane do tile for maior que o MaxLanesAtSpawn original.
        /// </summary>
        public void RegisterTile(TrackTile tile)
        {
            if (tile == null) return;

            if (!_rows.TryGetValue(tile.Row, out var row))
            {
                int initialCapacity = Mathf.Max(tile.MaxLanesAtSpawn, tile.Lane + 1);
                row = new RowData(tile.Row, initialCapacity);
                _rows[tile.Row] = row;
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
