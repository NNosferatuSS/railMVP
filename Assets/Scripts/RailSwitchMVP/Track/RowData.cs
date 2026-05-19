namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Dados de uma linha do grid. POCO. Tiles indexados por lane (null = lane vazia).
    /// </summary>
    public class RowData
    {
        public int RowIndex;
        public int MaxLanesAtSpawn;
        public TrackTile[] Tiles;
        public int[] CriticalLanes;

        public RowData(int rowIndex, int maxLanesAtSpawn)
        {
            RowIndex = rowIndex;
            MaxLanesAtSpawn = maxLanesAtSpawn;
            Tiles = new TrackTile[maxLanesAtSpawn];
            CriticalLanes = System.Array.Empty<int>();
        }

        public bool HasTile(int lane) =>
            lane >= 0 && lane < Tiles.Length && Tiles[lane] != null;
    }
}
