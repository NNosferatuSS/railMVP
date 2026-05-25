using System;
using System.Globalization;
using UnityEngine;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Daily Challenge (Fatia 6, spec §11.1). Singleton DontDestroyOnLoad.
    /// Seed determinístico do dia UTC — todos os jogadores rodam o mesmo level
    /// no daily mode. Persiste best do dia + best-ever em PlayerPrefs
    /// (RailMVP.Daily.*). Backend / leaderboard global vêm nas Fatias 7-8.
    ///
    /// Fluxo:
    /// 1. HomeScreenController.OnClickDailyChallenge → StartChallenge() + LoadScene(Game).
    /// 2. ProceduralRailGenerator.Start() → Random.InitState(GetSessionSeed()).
    /// 3. GameOverController.HandleGameOver → se IsDailyChallenge, EndChallenge(meters).
    /// 4. GameOverController.GoHome → ConsumeChallengeFlag() (Restart deixa a flag pra retry).
    /// </summary>
    public class DailyChallengeManager : MonoBehaviour
    {
        public static DailyChallengeManager Instance { get; private set; }

        const string KTodayDate    = "RailMVP.Daily.TodayDate";
        const string KTodayBestM   = "RailMVP.Daily.TodayBestM";
        const string KBestEverM    = "RailMVP.Daily.BestEverM";
        const string KBestEverDate = "RailMVP.Daily.BestEverDate";

        [Header("Runtime (read-only)")]
        [SerializeField] private string todayDate = "";
        [SerializeField] private int todayBestM;
        [SerializeField] private int bestEverM;
        [SerializeField] private string bestEverDate = "";

        bool _isDailyChallenge;

        /// <summary>True se a run atual é Daily Challenge.</summary>
        public bool IsDailyChallenge => _isDailyChallenge;

        /// <summary>Best do dia (m). 0 se ainda não jogou hoje.</summary>
        public int TodayBestM => IsToday(todayDate) ? todayBestM : 0;

        /// <summary>Best de qualquer daily challenge histórico (m).</summary>
        public int BestEverM => bestEverM;

        /// <summary>Data em que o best-ever foi batido (yyyy-MM-dd UTC).</summary>
        public string BestEverDate => bestEverDate;

        /// <summary>True se já jogou daily challenge hoje (best registrado).</summary>
        public bool HasPlayedToday() => IsToday(todayDate);

        public event Action OnDailyResultRecorded;

        /// <summary>Disparado quando qualquer campo daily muda e foi persistido (pra Fatia 7B sync). Suprimido durante ApplyRemoteState.</summary>
        public event Action OnDataChanged;

        bool _suppressDataChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) Save(); }

        // ============ Load / Save ============

        void Load()
        {
            todayDate    = PlayerPrefs.GetString(KTodayDate, "");
            todayBestM   = PlayerPrefs.GetInt(KTodayBestM, 0);
            bestEverM    = PlayerPrefs.GetInt(KBestEverM, 0);
            bestEverDate = PlayerPrefs.GetString(KBestEverDate, "");
            Debug.Log($"[Daily] Loaded — todayDate='{todayDate}' todayBest={todayBestM}m bestEver={bestEverM}m ({bestEverDate})");
        }

        void Save()
        {
            PlayerPrefs.SetString(KTodayDate, todayDate);
            PlayerPrefs.SetInt(KTodayBestM, todayBestM);
            PlayerPrefs.SetInt(KBestEverM, bestEverM);
            PlayerPrefs.SetString(KBestEverDate, bestEverDate);
            PlayerPrefs.Save();

            if (!_suppressDataChanged) OnDataChanged?.Invoke();
        }

        // ============ Sync (Fatia 7B) ============

        /// <summary>
        /// Sobrescreve estado in-memory + PlayerPrefs com dados vindos do servidor.
        /// NÃO dispara OnDataChanged (evita loop). Dispara OnDailyResultRecorded
        /// pra UI da Home refletir.
        /// </summary>
        public void ApplyRemoteState(PlayerRemoteState s)
        {
            if (s == null) return;
            todayDate    = s.daily_today_date ?? "";
            todayBestM   = s.daily_today_best_m;
            bestEverM    = s.daily_best_ever_m;
            bestEverDate = s.daily_best_ever_date ?? "";

            _suppressDataChanged = true;
            Save();
            _suppressDataChanged = false;

            OnDailyResultRecorded?.Invoke();
            Debug.Log($"[Daily] ApplyRemoteState — todayDate='{todayDate}' todayBest={todayBestM}m bestEver={bestEverM}m");
        }

        /// <summary>Copia o estado daily atual pra um PlayerRemoteState (pra push em Fatia 7B).</summary>
        public void CopyToRemoteState(PlayerRemoteState target)
        {
            if (target == null) return;
            target.daily_today_date     = todayDate;
            target.daily_today_best_m   = todayBestM;
            target.daily_best_ever_m    = bestEverM;
            target.daily_best_ever_date = bestEverDate;
        }

        // ============ Seed ============

        /// <summary>
        /// Seed pra ser passado a Random.InitState() antes da primeira geração.
        /// Daily mode → seed determinístico do dia (yyyyMMdd como int). Senão,
        /// Environment.TickCount (random normal por run).
        /// </summary>
        public int GetSessionSeed()
        {
            return _isDailyChallenge ? GetTodaySeed() : Environment.TickCount;
        }

        /// <summary>Seed determinístico do dia UTC atual (formato yyyyMMdd como int, ex: 20260525).</summary>
        public int GetTodaySeed()
        {
            return int.Parse(DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }

        // ============ Flow ============

        /// <summary>Marca a próxima run como Daily Challenge. Chamar antes de LoadScene(Game).</summary>
        public void StartChallenge()
        {
            _isDailyChallenge = true;
            Debug.Log($"[Daily] StartChallenge — seed={GetTodaySeed()}");
        }

        /// <summary>
        /// Limpa a flag. Chamado pelo GoHome do GameOver. Restart deliberadamente NÃO
        /// chama — re-rodar o mesmo seed determinístico é o ponto do daily.
        /// </summary>
        public void ConsumeChallengeFlag()
        {
            _isDailyChallenge = false;
            Debug.Log("[Daily] ConsumeChallengeFlag — back to normal mode.");
        }

        /// <summary>
        /// Registra o resultado de uma daily run. Atualiza today/ever se aplicável e
        /// dispara OnDailyResultRecorded. Idempotente em runs piores que o best.
        /// </summary>
        public DailyRecordResult EndChallenge(int meters)
        {
            string today = TodayUtc();

            // Detecta virada de dia (best de ontem já não conta pra hoje).
            if (todayDate != today)
            {
                todayDate = today;
                todayBestM = 0;
            }

            var result = new DailyRecordResult
            {
                brokeToday = meters > todayBestM,
                brokeEver = meters > bestEverM,
            };

            if (result.brokeToday) todayBestM = meters;
            if (result.brokeEver)
            {
                bestEverM = meters;
                bestEverDate = today;
            }

            Save();
            Debug.Log($"[Daily] EndChallenge — meters={meters} today={todayBestM} ever={bestEverM} brokeToday={result.brokeToday} brokeEver={result.brokeEver}");
            OnDailyResultRecorded?.Invoke();
            return result;
        }

        public struct DailyRecordResult
        {
            public bool brokeToday;
            public bool brokeEver;
        }

        // ============ Debug ============

        public void DebugResetToday()
        {
            todayDate = "";
            todayBestM = 0;
            Save();
            Debug.Log("[Daily] DEBUG: today's best reset.");
        }

        public void DebugWipe()
        {
            todayDate = "";
            todayBestM = 0;
            bestEverM = 0;
            bestEverDate = "";
            PlayerPrefs.DeleteKey(KTodayDate);
            PlayerPrefs.DeleteKey(KTodayBestM);
            PlayerPrefs.DeleteKey(KBestEverM);
            PlayerPrefs.DeleteKey(KBestEverDate);
            PlayerPrefs.Save();
            Debug.Log("[Daily] DEBUG: all daily data wiped.");
        }

        public void DebugToggleDailyMode()
        {
            _isDailyChallenge = !_isDailyChallenge;
            Debug.Log($"[Daily] DEBUG: isDailyChallenge={_isDailyChallenge} (seed={GetSessionSeed()})");
        }

        // ============ Helpers ============

        static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        static bool IsToday(string date) => date == TodayUtc();
    }
}
