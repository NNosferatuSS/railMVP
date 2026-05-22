using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Tipos de missão (spec §3.3 + §4). 12 tipos cobrem o pool de 20 daily + 10 weekly.
    /// </summary>
    public enum MissionKind
    {
        SingleRunCoins,
        SingleRunDistance,
        SingleRunTime,
        TotalRuns,
        DailyTotalCoins,
        UsePowerUp,
        ReachTier,
        NoPowerUpRun,
        TilesWithCoins,
        WeeklyTotalCoins,
        WeeklyTotalRuns,
        DailyMissionsComplete,
    }

    /// <summary>Definição estática de uma missão no pool.</summary>
    [Serializable]
    public struct MissionDef
    {
        public int Id;
        public string Description;
        public MissionKind Kind;
        public float Target;
        public int Reward;
        public string Param;   // pra UsePowerUp: "shield"/"magnet"/"slowdown".

        public MissionDef(int id, string desc, MissionKind kind, float target, int reward, string param = "")
        {
            Id = id; Description = desc; Kind = kind; Target = target; Reward = reward; Param = param;
        }
    }

    /// <summary>Snapshot pra UI consumir.</summary>
    public struct MissionEntry
    {
        public int Id;
        public string Description;
        public float Progress;
        public float Target;
        public int Reward;
        public bool IsComplete;
        public bool IsClaimed;
    }

    /// <summary>
    /// Singleton que rastreia missões diárias e semanais. Carrega/gera no
    /// startup com base em DayOfYear e ISOWeek. Hooks (CoinManager,
    /// PowerUpManager, DifficultyManager, GameManager) são plugados no Awake
    /// e no SceneManager.sceneLoaded.
    ///
    /// Persistência: PlayerPrefs com chaves "RailMVP.Missions.*" + spec §3.4/§4.4.
    ///
    /// Decisões:
    /// - Pools são hardcoded em arrays static readonly. Fácil migrar pra
    ///   ScriptableObject depois.
    /// - single_run_* mantém max(progress, currentRun) no slot — UI mostra
    ///   "best run so far" mas IsComplete continua sendo "atingiu target".
    /// - daily_total_coins / total_runs usam contadores globais persistidos
    ///   à parte (zeram quando vira o dia/semana).
    /// </summary>
    public class MissionTracker : MonoBehaviour
    {
        public static MissionTracker Instance { get; private set; }

        public const int DailySlots = 3;
        public const int WeeklySlots = 3;

        // ============ Pool de missões (hardcoded, spec §3.2 + §4.2) ============

        static readonly MissionDef[] DailyPool = new MissionDef[]
        {
            new MissionDef( 0, "Colete 100 moedas em uma única run", MissionKind.SingleRunCoins,    100, 100),
            new MissionDef( 1, "Colete 200 moedas em uma única run", MissionKind.SingleRunCoins,    200, 150),
            new MissionDef( 2, "Colete 300 moedas em uma única run", MissionKind.SingleRunCoins,    300, 200),
            new MissionDef( 3, "Chegue a 300m em uma run",            MissionKind.SingleRunDistance, 300, 100),
            new MissionDef( 4, "Chegue a 600m em uma run",            MissionKind.SingleRunDistance, 600, 150),
            new MissionDef( 5, "Chegue a 1000m em uma run",           MissionKind.SingleRunDistance,1000, 200),
            new MissionDef( 6, "Sobreviva 30 segundos em uma run",    MissionKind.SingleRunTime,      30, 100),
            new MissionDef( 7, "Sobreviva 60 segundos em uma run",    MissionKind.SingleRunTime,      60, 150),
            new MissionDef( 8, "Sobreviva 90 segundos em uma run",    MissionKind.SingleRunTime,      90, 200),
            new MissionDef( 9, "Complete 2 runs",                     MissionKind.TotalRuns,           2, 100),
            new MissionDef(10, "Complete 5 runs",                     MissionKind.TotalRuns,           5, 200),
            new MissionDef(11, "Colete 500 moedas no total do dia",   MissionKind.DailyTotalCoins,   500, 150),
            new MissionDef(12, "Colete 1000 moedas no total do dia",  MissionKind.DailyTotalCoins, 1000, 300),
            new MissionDef(13, "Use um Shield em uma run",            MissionKind.UsePowerUp,          1, 100, "shield"),
            new MissionDef(14, "Use um Magnet em uma run",            MissionKind.UsePowerUp,          1, 100, "magnet"),
            new MissionDef(15, "Use um SlowDown em uma run",          MissionKind.UsePowerUp,          1, 100, "slowdown"),
            new MissionDef(16, "Atinja o Tier 2 de dificuldade",      MissionKind.ReachTier,           2, 150),
            new MissionDef(17, "Atinja o Tier 3 de dificuldade",      MissionKind.ReachTier,           3, 200),
            new MissionDef(18, "Complete uma run sem usar nenhum power-up", MissionKind.NoPowerUpRun,  1, 200),
            new MissionDef(19, "Colete moedas em 10 tiles diferentes numa run", MissionKind.TilesWithCoins, 10, 150),
        };

        static readonly MissionDef[] WeeklyPool = new MissionDef[]
        {
            new MissionDef( 0, "Colete 3.000 moedas no total da semana", MissionKind.WeeklyTotalCoins, 3000, 500),
            new MissionDef( 1, "Colete 6.000 moedas no total da semana", MissionKind.WeeklyTotalCoins, 6000, 800),
            new MissionDef( 2, "Complete 20 runs na semana",             MissionKind.WeeklyTotalRuns,    20, 500),
            new MissionDef( 3, "Complete 40 runs na semana",             MissionKind.WeeklyTotalRuns,    40, 800),
            new MissionDef( 4, "Chegue a 1500m em uma run",              MissionKind.SingleRunDistance,1500, 600),
            new MissionDef( 5, "Chegue a 2500m em uma run",              MissionKind.SingleRunDistance,2500,1000),
            new MissionDef( 6, "Atinja o Tier 4 em uma run",             MissionKind.ReachTier,           4, 700),
            new MissionDef( 7, "Sobreviva 3 minutos em uma run",         MissionKind.SingleRunTime,     180, 800),
            new MissionDef( 8, "Complete todas as 3 missões diárias em 3 dias diferentes", MissionKind.DailyMissionsComplete, 3, 600),
            new MissionDef( 9, "Colete moedas em 50 tiles numa única run", MissionKind.TilesWithCoins,  50, 700),
        };

        // ============ Persistência keys ============

        const string KMissionsDate = "RailMVP.Missions.Date";
        const string KDailyId      = "RailMVP.Missions.Daily{0}.Id";
        const string KDailyProg    = "RailMVP.Missions.Daily{0}.Progress";
        const string KDailyClaimed = "RailMVP.Missions.Daily{0}.Claimed";

        const string KWeeklyWeek    = "RailMVP.Missions.Week";
        const string KWeeklyId      = "RailMVP.Missions.Weekly{0}.Id";
        const string KWeeklyProg    = "RailMVP.Missions.Weekly{0}.Progress";
        const string KWeeklyClaimed = "RailMVP.Missions.Weekly{0}.Claimed";

        const string KDailyCoinsToday = "RailMVP.Missions.DailyCoinsToday";
        const string KDailyRunsToday  = "RailMVP.Missions.DailyRunsToday";
        const string KWeeklyCoins     = "RailMVP.Missions.WeeklyCoins";
        const string KWeeklyRuns      = "RailMVP.Missions.WeeklyRuns";
        const string KAllDailyDates   = "RailMVP.Missions.AllDailyDates";  // CSV de yyyy-MM-dd

        // ============ State ============

        int[] _dailyIds = new int[DailySlots];
        float[] _dailyProg = new float[DailySlots];
        bool[] _dailyClaimed = new bool[DailySlots];

        int[] _weeklyIds = new int[WeeklySlots];
        float[] _weeklyProg = new float[WeeklySlots];
        bool[] _weeklyClaimed = new bool[WeeklySlots];

        int _dailyCoinsToday;
        int _dailyRunsToday;
        int _weeklyCoinsTotal;
        int _weeklyRunsTotal;
        readonly HashSet<string> _allDailyCompleteDates = new HashSet<string>();

        // Per-run trackers (resetam em StartRun).
        int _runCoins;
        int _runMaxTier;
        readonly HashSet<int> _runTilesWithCoin = new HashSet<int>();
        bool _runUsedAnyPowerUp;
        readonly HashSet<string> _runPowerUpsUsed = new HashSet<string>();

        // Pra deltas em OnCoinsChanged.
        int _lastKnownCoinTotal;

        public event Action OnMissionsChanged;

        // ============ Lifecycle ============

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadOrGenerate();
            SubscribeHooks();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeHooks();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) CommitProgress();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CommitProgress();
        }

        // ============ Geração + persistência ============

        void LoadOrGenerate()
        {
            string today = TodayUtc();
            int weekNow = CurrentWeekIso();

            // Daily
            string savedDate = PlayerPrefs.GetString(KMissionsDate, "");
            if (savedDate != today)
            {
                GenerateDaily(today);
            }
            else
            {
                for (int i = 0; i < DailySlots; i++)
                {
                    _dailyIds[i]     = PlayerPrefs.GetInt(string.Format(KDailyId, i), 0);
                    _dailyProg[i]    = PlayerPrefs.GetFloat(string.Format(KDailyProg, i), 0f);
                    _dailyClaimed[i] = PlayerPrefs.GetInt(string.Format(KDailyClaimed, i), 0) == 1;
                }
            }

            // Weekly
            int savedWeek = PlayerPrefs.GetInt(KWeeklyWeek, -1);
            if (savedWeek != weekNow)
            {
                GenerateWeekly(weekNow);
            }
            else
            {
                for (int i = 0; i < WeeklySlots; i++)
                {
                    _weeklyIds[i]     = PlayerPrefs.GetInt(string.Format(KWeeklyId, i), 0);
                    _weeklyProg[i]    = PlayerPrefs.GetFloat(string.Format(KWeeklyProg, i), 0f);
                    _weeklyClaimed[i] = PlayerPrefs.GetInt(string.Format(KWeeklyClaimed, i), 0) == 1;
                }
            }

            _dailyCoinsToday   = (savedDate == today) ? PlayerPrefs.GetInt(KDailyCoinsToday, 0) : 0;
            _dailyRunsToday    = (savedDate == today) ? PlayerPrefs.GetInt(KDailyRunsToday, 0) : 0;
            _weeklyCoinsTotal  = (savedWeek == weekNow) ? PlayerPrefs.GetInt(KWeeklyCoins, 0) : 0;
            _weeklyRunsTotal   = (savedWeek == weekNow) ? PlayerPrefs.GetInt(KWeeklyRuns, 0) : 0;

            _allDailyCompleteDates.Clear();
            string csv = PlayerPrefs.GetString(KAllDailyDates, "");
            if (!string.IsNullOrEmpty(csv))
            {
                foreach (var d in csv.Split(','))
                    if (!string.IsNullOrWhiteSpace(d)) _allDailyCompleteDates.Add(d.Trim());
            }

            Debug.Log($"[MissionTracker] Loaded — daily=[{_dailyIds[0]},{_dailyIds[1]},{_dailyIds[2]}] weekly=[{_weeklyIds[0]},{_weeklyIds[1]},{_weeklyIds[2]}] coinsToday={_dailyCoinsToday} weekly={_weeklyCoinsTotal}/{_weeklyRunsTotal}");
        }

        void GenerateDaily(string today)
        {
            int baseIdx = DateTime.UtcNow.DayOfYear % DailyPool.Length;
            for (int i = 0; i < DailySlots; i++)
            {
                _dailyIds[i] = (baseIdx + i) % DailyPool.Length;
                _dailyProg[i] = 0f;
                _dailyClaimed[i] = false;
            }
            _dailyCoinsToday = 0;
            _dailyRunsToday = 0;
            PlayerPrefs.SetString(KMissionsDate, today);
            Debug.Log($"[MissionTracker] New daily missions for {today}: [{_dailyIds[0]},{_dailyIds[1]},{_dailyIds[2]}]");
        }

        void GenerateWeekly(int weekNow)
        {
            int baseIdx = weekNow % WeeklyPool.Length;
            for (int i = 0; i < WeeklySlots; i++)
            {
                _weeklyIds[i] = (baseIdx + i) % WeeklyPool.Length;
                _weeklyProg[i] = 0f;
                _weeklyClaimed[i] = false;
            }
            _weeklyCoinsTotal = 0;
            _weeklyRunsTotal = 0;
            PlayerPrefs.SetInt(KWeeklyWeek, weekNow);
            Debug.Log($"[MissionTracker] New weekly missions for week {weekNow}: [{_weeklyIds[0]},{_weeklyIds[1]},{_weeklyIds[2]}]");
        }

        /// <summary>Salva tudo em PlayerPrefs. Chamado em EndRun, claim, pause.</summary>
        public void CommitProgress()
        {
            for (int i = 0; i < DailySlots; i++)
            {
                PlayerPrefs.SetInt(string.Format(KDailyId, i), _dailyIds[i]);
                PlayerPrefs.SetFloat(string.Format(KDailyProg, i), _dailyProg[i]);
                PlayerPrefs.SetInt(string.Format(KDailyClaimed, i), _dailyClaimed[i] ? 1 : 0);
            }
            for (int i = 0; i < WeeklySlots; i++)
            {
                PlayerPrefs.SetInt(string.Format(KWeeklyId, i), _weeklyIds[i]);
                PlayerPrefs.SetFloat(string.Format(KWeeklyProg, i), _weeklyProg[i]);
                PlayerPrefs.SetInt(string.Format(KWeeklyClaimed, i), _weeklyClaimed[i] ? 1 : 0);
            }
            PlayerPrefs.SetInt(KDailyCoinsToday, _dailyCoinsToday);
            PlayerPrefs.SetInt(KDailyRunsToday, _dailyRunsToday);
            PlayerPrefs.SetInt(KWeeklyCoins, _weeklyCoinsTotal);
            PlayerPrefs.SetInt(KWeeklyRuns, _weeklyRunsTotal);
            PlayerPrefs.SetString(KAllDailyDates, string.Join(",", _allDailyCompleteDates));
            PlayerPrefs.Save();
        }

        // ============ Hooks externos ============

        void SubscribeHooks()
        {
            if (CoinManager.Instance != null)
            {
                _lastKnownCoinTotal = CoinManager.Instance.Total;
                CoinManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            }
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.OnPowerUpActivated += HandlePowerUpActivated;
            if (DifficultyManager.Instance != null)
                DifficultyManager.Instance.OnTierChanged += HandleTierChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
        }

        void UnsubscribeHooks()
        {
            if (CoinManager.Instance != null)
                CoinManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.OnPowerUpActivated -= HandlePowerUpActivated;
            if (DifficultyManager.Instance != null)
                DifficultyManager.Instance.OnTierChanged -= HandleTierChanged;
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
        }

        // Re-subscribe a cada cena carregada — os singletons da GameScene
        // são recriados (não DontDestroyOnLoad). HomeScene não tem nenhum
        // deles; subscribe é no-op lá.
        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnsubscribeHooks();
            SubscribeHooks();

            // StartRun automaticamente ao carregar a GameScene.
            if (scene.name == SceneNames.Game)
            {
                StartRun();
            }
        }

        void HandleCoinsChanged(int total)
        {
            int delta = total - _lastKnownCoinTotal;
            _lastKnownCoinTotal = total;
            if (delta > 0) OnCoinsCollected(delta);
        }

        void HandlePowerUpActivated(PowerUpType type)
        {
            string name = type.ToString().ToLowerInvariant();
            OnPowerUpUsed(name);
        }

        void HandleTierChanged(Config.DifficultyTier tier)
        {
            if (DifficultyManager.Instance != null)
                OnTierReached(DifficultyManager.Instance.CurrentTierIndex);
        }

        void HandleGameOver(GameOverReason reason)
        {
            float distance = 0f;
            float time = 0f;
            var player = UnityEngine.Object.FindFirstObjectByType<RailSwitchMVP.Player.PlayerRailRider>();
            if (player != null) distance = player.DistanceTraveled;
            if (GameTimer.Instance != null) time = GameTimer.Instance.Elapsed;
            EndRun(distance, time, _runUsedAnyPowerUp);
        }

        // ============ API pública (tracking) ============

        public void StartRun()
        {
            _runCoins = 0;
            _runMaxTier = 0;
            _runTilesWithCoin.Clear();
            _runUsedAnyPowerUp = false;
            _runPowerUpsUsed.Clear();
            _lastKnownCoinTotal = CoinManager.Instance != null ? CoinManager.Instance.Total : 0;
        }

        public void OnCoinsCollected(int amount)
        {
            if (amount <= 0) return;
            _runCoins += amount;
            _dailyCoinsToday += amount;
            _weeklyCoinsTotal += amount;
            // Não atualiza slots agora; consolidação em EndRun (mais barato).
        }

        public void OnPowerUpUsed(string powerUpName)
        {
            if (string.IsNullOrEmpty(powerUpName)) return;
            _runUsedAnyPowerUp = true;
            _runPowerUpsUsed.Add(powerUpName);
        }

        public void OnTierReached(int tierIndex)
        {
            if (tierIndex > _runMaxTier) _runMaxTier = tierIndex;
        }

        /// <summary>
        /// Chamado por CollectibleCoin.Collect (após AddCoins). Conta tile do
        /// player no momento da coleta — duplicatas filtradas por HashSet.
        /// </summary>
        public void OnTileWithCoin(int tileInstanceId)
        {
            _runTilesWithCoin.Add(tileInstanceId);
        }

        public void EndRun(float distance, float seconds, bool usedAnyPowerUp)
        {
            _runUsedAnyPowerUp = _runUsedAnyPowerUp || usedAnyPowerUp;
            _dailyRunsToday++;
            _weeklyRunsTotal++;

            // Consolida slots — para cada slot, calcula novo progress.
            UpdateSlotsForRun(distance, seconds);

            // Verifica "todas as 3 dailies completas hoje" pra weekly mission tipo 8.
            CheckAllDailiesCompleteToday();

            CommitProgress();
            OnMissionsChanged?.Invoke();
        }

        void UpdateSlotsForRun(float distance, float seconds)
        {
            for (int i = 0; i < DailySlots; i++)
                _dailyProg[i] = ComputeProgress(DailyPool[_dailyIds[i]], _dailyProg[i], distance, seconds, /*weekly*/ false);
            for (int i = 0; i < WeeklySlots; i++)
                _weeklyProg[i] = ComputeProgress(WeeklyPool[_weeklyIds[i]], _weeklyProg[i], distance, seconds, /*weekly*/ true);
        }

        float ComputeProgress(MissionDef def, float prev, float distance, float seconds, bool weekly)
        {
            switch (def.Kind)
            {
                case MissionKind.SingleRunCoins:    return Mathf.Max(prev, _runCoins);
                case MissionKind.SingleRunDistance: return Mathf.Max(prev, distance);
                case MissionKind.SingleRunTime:     return Mathf.Max(prev, seconds);
                case MissionKind.TotalRuns:         return _dailyRunsToday;
                case MissionKind.DailyTotalCoins:   return _dailyCoinsToday;
                case MissionKind.UsePowerUp:
                    return _runPowerUpsUsed.Contains(def.Param) ? Mathf.Max(prev, 1f) : prev;
                case MissionKind.ReachTier:         return Mathf.Max(prev, _runMaxTier);
                case MissionKind.NoPowerUpRun:      return !_runUsedAnyPowerUp ? Mathf.Max(prev, 1f) : prev;
                case MissionKind.TilesWithCoins:    return Mathf.Max(prev, _runTilesWithCoin.Count);
                case MissionKind.WeeklyTotalCoins:  return _weeklyCoinsTotal;
                case MissionKind.WeeklyTotalRuns:   return _weeklyRunsTotal;
                case MissionKind.DailyMissionsComplete: return _allDailyCompleteDates.Count;
                default: return prev;
            }
        }

        void CheckAllDailiesCompleteToday()
        {
            for (int i = 0; i < DailySlots; i++)
            {
                var def = DailyPool[_dailyIds[i]];
                if (_dailyProg[i] < def.Target) return;
            }
            string today = TodayUtc();
            if (_allDailyCompleteDates.Add(today))
                Debug.Log($"[MissionTracker] All daily missions complete today ({today})!");
        }

        // ============ API pública (UI / queries) ============

        public MissionEntry GetDailyMission(int slot)
        {
            if (slot < 0 || slot >= DailySlots) return default;
            return ToEntry(DailyPool[_dailyIds[slot]], _dailyProg[slot], _dailyClaimed[slot]);
        }

        public MissionEntry GetWeeklyMission(int slot)
        {
            if (slot < 0 || slot >= WeeklySlots) return default;
            return ToEntry(WeeklyPool[_weeklyIds[slot]], _weeklyProg[slot], _weeklyClaimed[slot]);
        }

        public bool IsDailyComplete(int slot) => GetDailyMission(slot).IsComplete;
        public bool IsDailyClaimed(int slot)  => slot >= 0 && slot < DailySlots && _dailyClaimed[slot];
        public bool IsWeeklyComplete(int slot) => GetWeeklyMission(slot).IsComplete;
        public bool IsWeeklyClaimed(int slot) => slot >= 0 && slot < WeeklySlots && _weeklyClaimed[slot];

        public void ClaimDaily(int slot)
        {
            if (slot < 0 || slot >= DailySlots) return;
            if (_dailyClaimed[slot]) return;
            var def = DailyPool[_dailyIds[slot]];
            if (_dailyProg[slot] < def.Target) return;

            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.AddCoins(def.Reward);
            _dailyClaimed[slot] = true;
            CommitProgress();
            if (PlayerDataManager.Instance != null) PlayerDataManager.Instance.Save();
            Debug.Log($"[MissionTracker] Daily slot {slot} claimed: +{def.Reward} coins");
            OnMissionsChanged?.Invoke();
        }

        public void ClaimWeekly(int slot)
        {
            if (slot < 0 || slot >= WeeklySlots) return;
            if (_weeklyClaimed[slot]) return;
            var def = WeeklyPool[_weeklyIds[slot]];
            if (_weeklyProg[slot] < def.Target) return;

            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.AddCoins(def.Reward);
            _weeklyClaimed[slot] = true;
            CommitProgress();
            if (PlayerDataManager.Instance != null) PlayerDataManager.Instance.Save();
            Debug.Log($"[MissionTracker] Weekly slot {slot} claimed: +{def.Reward} coins");
            OnMissionsChanged?.Invoke();
        }

        // ============ Debug ============

        /// <summary>Força progress = target no slot daily (debug).</summary>
        public void DebugForceCompleteDaily(int slot)
        {
            if (slot < 0 || slot >= DailySlots) return;
            _dailyProg[slot] = DailyPool[_dailyIds[slot]].Target;
            CommitProgress();
            OnMissionsChanged?.Invoke();
        }

        public void DebugForceCompleteWeekly(int slot)
        {
            if (slot < 0 || slot >= WeeklySlots) return;
            _weeklyProg[slot] = WeeklyPool[_weeklyIds[slot]].Target;
            CommitProgress();
            OnMissionsChanged?.Invoke();
        }

        /// <summary>
        /// Avança as 3 daily pra os próximos 3 IDs no pool (sem mexer na data).
        /// Reseta progress e claimed. Útil pra testar todas as 20 daily no
        /// mesmo dia. Cicla pelo pool inteiro a cada N chamadas.
        /// </summary>
        public void DebugCycleDaily()
        {
            for (int i = 0; i < DailySlots; i++)
            {
                _dailyIds[i] = (_dailyIds[i] + DailySlots) % DailyPool.Length;
                _dailyProg[i] = 0f;
                _dailyClaimed[i] = false;
            }
            CommitProgress();
            OnMissionsChanged?.Invoke();
            Debug.Log($"[MissionTracker] Cycled daily → IDs [{_dailyIds[0]},{_dailyIds[1]},{_dailyIds[2]}]");
        }

        public void DebugCycleWeekly()
        {
            for (int i = 0; i < WeeklySlots; i++)
            {
                _weeklyIds[i] = (_weeklyIds[i] + WeeklySlots) % WeeklyPool.Length;
                _weeklyProg[i] = 0f;
                _weeklyClaimed[i] = false;
            }
            CommitProgress();
            OnMissionsChanged?.Invoke();
            Debug.Log($"[MissionTracker] Cycled weekly → IDs [{_weeklyIds[0]},{_weeklyIds[1]},{_weeklyIds[2]}]");
        }

        public void DebugResetAll()
        {
            PlayerPrefs.DeleteKey(KMissionsDate);
            PlayerPrefs.DeleteKey(KWeeklyWeek);
            for (int i = 0; i < DailySlots; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KDailyId, i));
                PlayerPrefs.DeleteKey(string.Format(KDailyProg, i));
                PlayerPrefs.DeleteKey(string.Format(KDailyClaimed, i));
            }
            for (int i = 0; i < WeeklySlots; i++)
            {
                PlayerPrefs.DeleteKey(string.Format(KWeeklyId, i));
                PlayerPrefs.DeleteKey(string.Format(KWeeklyProg, i));
                PlayerPrefs.DeleteKey(string.Format(KWeeklyClaimed, i));
            }
            PlayerPrefs.DeleteKey(KDailyCoinsToday);
            PlayerPrefs.DeleteKey(KDailyRunsToday);
            PlayerPrefs.DeleteKey(KWeeklyCoins);
            PlayerPrefs.DeleteKey(KWeeklyRuns);
            PlayerPrefs.DeleteKey(KAllDailyDates);
            PlayerPrefs.Save();

            _dailyCoinsToday = 0;
            _dailyRunsToday = 0;
            _weeklyCoinsTotal = 0;
            _weeklyRunsTotal = 0;
            _allDailyCompleteDates.Clear();

            LoadOrGenerate();
            OnMissionsChanged?.Invoke();
            Debug.Log("[MissionTracker] All mission data reset.");
        }

        // ============ Helpers ============

        static MissionEntry ToEntry(MissionDef def, float prog, bool claimed)
        {
            return new MissionEntry
            {
                Id = def.Id,
                Description = def.Description,
                Progress = prog,
                Target = def.Target,
                Reward = def.Reward,
                IsComplete = prog >= def.Target,
                IsClaimed = claimed,
            };
        }

        static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        static int CurrentWeekIso()
        {
            var now = DateTime.UtcNow;
            return ISOWeek.GetWeekOfYear(now);
        }
    }
}
