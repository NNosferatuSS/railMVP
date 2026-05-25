using System;

namespace RailSwitchMVP.Net
{
    /// <summary>
    /// Mirror serializável da row da tabela public.players no Supabase
    /// (Fatia 7A schema). Field names em snake_case porque JsonUtility usa
    /// reflection direta nos nomes pra (de)serializar, e o PostgREST entrega
    /// JSON com colunas em snake_case.
    ///
    /// Usado pelo PlayerDataSync pra serializar push (PATCH/INSERT) e
    /// desserializar pull (GET). PlayerDataManager + DailyChallengeManager
    /// expõem ApplyRemoteState(PlayerRemoteState) que copia desta estrutura.
    /// </summary>
    [Serializable]
    public class PlayerRemoteState
    {
        public string id; // uuid string, igual ao auth.users.id

        // Core PDM
        public int coins;
        public int best_distance;
        public int best_coins;
        public int best_tier;
        public float best_time;
        public int total_runs;
        public int equipped_char;
        public string owned_chars = "0";  // CSV
        public string player_name = "Player";

        // Daily Challenge
        public string daily_today_date;
        public int daily_today_best_m;
        public int daily_best_ever_m;
        public string daily_best_ever_date;

        // Server-side trigger atualiza isto em todo UPDATE. Cliente NÃO envia
        // — só lê pra detectar mudanças server-side (last-write-wins).
        public string updated_at;
    }
}
