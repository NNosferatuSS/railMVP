using System;

namespace RailSwitchMVP.Net
{
    /// <summary>
    /// Linha do leaderboard daily. Mirror das colunas que o cliente lê de
    /// public.daily_results (Fatia 8). Field names em snake_case pra
    /// JsonUtility bater com o JSON do PostgREST.
    ///
    /// O campo <c>rank</c> NÃO existe na tabela — é preenchido client-side
    /// usando o índice na lista ordenada (FetchToday ordena por distance desc).
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string player_id;
        public string challenge_date;
        public int distance;
        public int coins;
        public int tier;
        public float time_seconds;
        public string player_name;
        public string created_at;

        [NonSerialized] public int rank; // client-computed (1-based)
    }
}
