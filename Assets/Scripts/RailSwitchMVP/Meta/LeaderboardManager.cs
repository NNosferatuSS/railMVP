using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Leaderboard online do Daily Challenge (Fatia 8). Singleton DontDestroyOnLoad.
    ///
    /// Submit: chamado pelo GameOverController após Daily run que bateu local best.
    /// Via RPC <c>submit_daily_result</c> (server compara distance, só atualiza
    /// se melhor — evita race entre devices).
    ///
    /// Fetch top: GET /rest/v1/daily_results?challenge_date=eq.X&order=distance.desc&limit=50.
    /// Fetch my rank: RPC <c>my_daily_rank</c> (window function server-side).
    ///
    /// Cache em memória por 5min — evita refetch repetido se user abre/fecha
    /// painel várias vezes. Invalidado em Submit success.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        [Header("Behavior")]
        [Tooltip("Quantos entries pedir no top global. 50 é spec §11.3 inicial.")]
        [Range(10, 200)]
        [SerializeField] private int topLimit = 50;

        [Tooltip("Segundos que o cache do top + my rank permanecem válidos sem refetch.")]
        [Range(5f, 600f)]
        [SerializeField] private float cacheTtlSeconds = 300f;

        [Tooltip("Logs detalhados.")]
        [SerializeField] private bool verboseLogs = true;

        LeaderboardEntry[] _topCache;
        DateTime _topCacheAt = DateTime.MinValue;

        int _myRankCache = -1;
        int _myDistanceCache = 0;
        DateTime _myRankCacheAt = DateTime.MinValue;

        // Global leaderboard (best distance da run normal) — caches separados do daily.
        LeaderboardEntry[] _globalTopCache;
        DateTime _globalTopCacheAt = DateTime.MinValue;
        int _myGlobalRankCache = -1;
        int _myGlobalDistanceCache = 0;
        DateTime _myGlobalRankCacheAt = DateTime.MinValue;

        public LeaderboardEntry[] TopCache => _topCache;
        public int MyRank => _myRankCache;        // -1 = não computado/ausente
        public int MyDistance => _myDistanceCache;
        public string LastError { get; private set; } = "";

        public event Action OnLeaderboardChanged;

        // Wrapper porque JsonUtility não desserializa top-level arrays.
        [Serializable] class EntriesWrapper { public LeaderboardEntry[] items; }
        [Serializable] class MyRankRow { public int rank; public int distance; }
        [Serializable] class MyRankWrapper { public MyRankRow[] items; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ============ Submit ============

        /// <summary>
        /// Envia resultado de Daily run. RPC server-side só atualiza se distance
        /// > registro atual. Invalida cache em sucesso.
        /// </summary>
        public void SubmitResult(int distance, int coins, int tier, float timeSeconds)
        {
            StartCoroutine(SubmitRoutine(distance, coins, tier, timeSeconds));
        }

        IEnumerator SubmitRoutine(int distance, int coins, int tier, float timeSeconds)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                LastError = "submit: not authenticated";
                if (verboseLogs) Debug.LogWarning("[LB] " + LastError);
                yield break;
            }
            var pdm = PlayerDataManager.Instance;
            string playerName = pdm != null ? pdm.PlayerName : "Player";

            // RPC body usa parâmetros prefixados p_ pra evitar ambiguidade com colunas no SQL.
            string body = "{" +
                $"\"p_distance\":{distance}," +
                $"\"p_coins\":{coins}," +
                $"\"p_tier\":{tier}," +
                $"\"p_time_seconds\":{timeSeconds.ToString("F2", CultureInfo.InvariantCulture)}," +
                $"\"p_player_name\":\"{EscapeJson(playerName)}\"" +
                "}";

            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Post("/rest/v1/rpc/submit_daily_result", body, null, r => result = r);

            if (result == null || !result.success)
            {
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[LB] Submit failed: {LastError}");
                yield break;
            }

            LastError = "";
            // Invalida caches (rank pode ter mudado).
            _topCacheAt = DateTime.MinValue;
            _myRankCacheAt = DateTime.MinValue;
            if (verboseLogs) Debug.Log($"[LB] Submit OK — distance={distance}");
            OnLeaderboardChanged?.Invoke();
        }

        // ============ Fetch top ============

        /// <summary>
        /// Top N do dia. Usa cache se válido. callback recebe o array (pode estar
        /// vazio se sem dados ou erro — checa LastError).
        /// </summary>
        public void FetchToday(Action<LeaderboardEntry[]> onComplete, bool forceRefresh = false)
        {
            if (!forceRefresh && CacheValid(_topCacheAt) && _topCache != null)
            {
                onComplete?.Invoke(_topCache);
                return;
            }
            StartCoroutine(FetchTopRoutine(onComplete));
        }

        IEnumerator FetchTopRoutine(Action<LeaderboardEntry[]> onComplete)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                LastError = "fetch: not authenticated";
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
                yield break;
            }

            string today = TodayUtc();
            string path = $"/rest/v1/daily_results?challenge_date=eq.{today}&order=distance.desc&limit={topLimit}";
            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Get(path, r => result = r);

            if (result == null || !result.success)
            {
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[LB] FetchTop failed: {LastError}");
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
                yield break;
            }

            try
            {
                string body = string.IsNullOrEmpty(result.body) ? "[]" : result.body;
                var wrapper = JsonUtility.FromJson<EntriesWrapper>("{\"items\":" + body + "}");
                _topCache = wrapper != null && wrapper.items != null ? wrapper.items : System.Array.Empty<LeaderboardEntry>();
                for (int i = 0; i < _topCache.Length; i++) _topCache[i].rank = i + 1;
                _topCacheAt = DateTime.UtcNow;
                LastError = "";
                if (verboseLogs) Debug.Log($"[LB] FetchTop OK — {_topCache.Length} entries");
                onComplete?.Invoke(_topCache);
            }
            catch (Exception ex)
            {
                LastError = "fetch parse: " + ex.Message;
                Debug.LogWarning($"[LB] FetchTop parse error: {ex.Message}\nbody: {result.body}");
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
            }
        }

        // ============ Fetch my rank ============

        /// <summary>
        /// Rank do usuário atual no daily de hoje. callback recebe (rank, distance).
        /// rank == -1 significa que o user ainda não submeteu hoje.
        /// </summary>
        public void FetchMyRank(Action<int, int> onComplete, bool forceRefresh = false)
        {
            if (!forceRefresh && CacheValid(_myRankCacheAt))
            {
                onComplete?.Invoke(_myRankCache, _myDistanceCache);
                return;
            }
            StartCoroutine(FetchMyRankRoutine(onComplete));
        }

        IEnumerator FetchMyRankRoutine(Action<int, int> onComplete)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                LastError = "rank: not authenticated";
                onComplete?.Invoke(-1, 0);
                yield break;
            }

            string today = TodayUtc();
            string body = "{\"p_date\":\"" + today + "\"}";
            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Post("/rest/v1/rpc/my_daily_rank", body, null, r => result = r);

            if (result == null || !result.success)
            {
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[LB] FetchMyRank failed: {LastError}");
                onComplete?.Invoke(-1, 0);
                yield break;
            }

            try
            {
                string responseBody = string.IsNullOrEmpty(result.body) ? "[]" : result.body;
                var wrapper = JsonUtility.FromJson<MyRankWrapper>("{\"items\":" + responseBody + "}");
                if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
                {
                    _myRankCache = wrapper.items[0].rank;
                    _myDistanceCache = wrapper.items[0].distance;
                }
                else
                {
                    // Função retornou 0 rows → user ainda não jogou hoje.
                    _myRankCache = -1;
                    _myDistanceCache = 0;
                }
                _myRankCacheAt = DateTime.UtcNow;
                LastError = "";
                if (verboseLogs) Debug.Log($"[LB] FetchMyRank OK — rank={_myRankCache} distance={_myDistanceCache}");
                onComplete?.Invoke(_myRankCache, _myDistanceCache);
            }
            catch (Exception ex)
            {
                LastError = "rank parse: " + ex.Message;
                Debug.LogWarning($"[LB] FetchMyRank parse error: {ex.Message}\nbody: {result.body}");
                onComplete?.Invoke(-1, 0);
            }
        }

        // ============ Global — best distance da run normal ============
        // Lê da tabela `players` (best_distance já sincroniza via PDM) através de
        // RPCs SECURITY DEFINER — não há tabela/submit dedicados. O best distance
        // sobe no servidor pelo sync normal do PlayerDataManager.

        /// <summary>Top N do ranking global de best distance. callback recebe o array.</summary>
        public void FetchGlobalTop(Action<LeaderboardEntry[]> onComplete, bool forceRefresh = false)
        {
            if (!forceRefresh && CacheValid(_globalTopCacheAt) && _globalTopCache != null)
            {
                onComplete?.Invoke(_globalTopCache);
                return;
            }
            StartCoroutine(FetchGlobalTopRoutine(onComplete));
        }

        IEnumerator FetchGlobalTopRoutine(Action<LeaderboardEntry[]> onComplete)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                LastError = "global fetch: not authenticated";
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
                yield break;
            }

            string body = "{\"p_limit\":" + topLimit + "}";
            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Post("/rest/v1/rpc/global_distance_top", body, null, r => result = r);

            if (result == null || !result.success)
            {
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[LB] FetchGlobalTop failed: {LastError}");
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
                yield break;
            }

            try
            {
                string respBody = string.IsNullOrEmpty(result.body) ? "[]" : result.body;
                var wrapper = JsonUtility.FromJson<EntriesWrapper>("{\"items\":" + respBody + "}");
                _globalTopCache = wrapper != null && wrapper.items != null ? wrapper.items : System.Array.Empty<LeaderboardEntry>();
                for (int i = 0; i < _globalTopCache.Length; i++) _globalTopCache[i].rank = i + 1;
                _globalTopCacheAt = DateTime.UtcNow;
                LastError = "";
                if (verboseLogs) Debug.Log($"[LB] FetchGlobalTop OK — {_globalTopCache.Length} entries");
                onComplete?.Invoke(_globalTopCache);
            }
            catch (Exception ex)
            {
                LastError = "global fetch parse: " + ex.Message;
                Debug.LogWarning($"[LB] FetchGlobalTop parse error: {ex.Message}\nbody: {result.body}");
                onComplete?.Invoke(System.Array.Empty<LeaderboardEntry>());
            }
        }

        /// <summary>Rank global do user atual. callback recebe (rank, distance). rank == -1 = sem recorde ainda.</summary>
        public void FetchMyGlobalRank(Action<int, int> onComplete, bool forceRefresh = false)
        {
            if (!forceRefresh && CacheValid(_myGlobalRankCacheAt))
            {
                onComplete?.Invoke(_myGlobalRankCache, _myGlobalDistanceCache);
                return;
            }
            StartCoroutine(FetchMyGlobalRankRoutine(onComplete));
        }

        IEnumerator FetchMyGlobalRankRoutine(Action<int, int> onComplete)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                LastError = "global rank: not authenticated";
                onComplete?.Invoke(-1, 0);
                yield break;
            }

            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Post("/rest/v1/rpc/my_global_distance_rank", "{}", null, r => result = r);

            if (result == null || !result.success)
            {
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[LB] FetchMyGlobalRank failed: {LastError}");
                onComplete?.Invoke(-1, 0);
                yield break;
            }

            try
            {
                string respBody = string.IsNullOrEmpty(result.body) ? "[]" : result.body;
                var wrapper = JsonUtility.FromJson<MyRankWrapper>("{\"items\":" + respBody + "}");
                if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
                {
                    _myGlobalRankCache = wrapper.items[0].rank;
                    _myGlobalDistanceCache = wrapper.items[0].distance;
                }
                else
                {
                    _myGlobalRankCache = -1;
                    _myGlobalDistanceCache = 0;
                }
                _myGlobalRankCacheAt = DateTime.UtcNow;
                LastError = "";
                if (verboseLogs) Debug.Log($"[LB] FetchMyGlobalRank OK — rank={_myGlobalRankCache} distance={_myGlobalDistanceCache}");
                onComplete?.Invoke(_myGlobalRankCache, _myGlobalDistanceCache);
            }
            catch (Exception ex)
            {
                LastError = "global rank parse: " + ex.Message;
                Debug.LogWarning($"[LB] FetchMyGlobalRank parse error: {ex.Message}\nbody: {result.body}");
                onComplete?.Invoke(-1, 0);
            }
        }

        bool CacheValid(DateTime cacheAt) => (DateTime.UtcNow - cacheAt).TotalSeconds < cacheTtlSeconds;

        // ============ Debug ============

        public void DebugLogStatus()
        {
            int topCount = _topCache != null ? _topCache.Length : 0;
            double topAgeSec = (DateTime.UtcNow - _topCacheAt).TotalSeconds;
            Debug.Log($"[LB] top={topCount} entries (age {topAgeSec:F0}s) | myRank={_myRankCache} myDist={_myDistanceCache} | lastErr='{LastError}'");
        }

        public void DebugForceSubmit(int distance, int coins, int tier, float time)
            => SubmitResult(distance, coins, tier, time);

        public void DebugFetchTopNow()  => FetchToday(_ => { }, forceRefresh: true);
        public void DebugFetchRankNow() => FetchMyRank((_, __) => { }, forceRefresh: true);

        // ============ Helpers ============

        static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
