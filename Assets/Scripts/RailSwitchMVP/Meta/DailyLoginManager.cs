using System;
using System.Globalization;
using UnityEngine;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Login diário + Daily Ad Chest (spec §2 + §5.1.B). Singleton
    /// DontDestroyOnLoad. Ambos em um arquivo só porque compartilham
    /// lógica de data UTC e ambos creditam coins via PlayerDataManager.
    ///
    /// LOGIN: ciclo de 7 dias com recompensas escalonadas
    /// (50/100/150/200/300/400/500). Sem streak penalizado — pular dias
    /// só avança +1 normal sem reset.
    ///
    /// CHEST: 1x por dia, +150 coins. Stub (sem Unity Ads ainda) — vira
    /// rewarded ad de verdade na Fatia 5.
    /// </summary>
    public class DailyLoginManager : MonoBehaviour
    {
        public static DailyLoginManager Instance { get; private set; }

        // ============ Login config ============

        public const int LoginCycleLength = 7;

        // Day 1..7 → index 0..6. Spec §2.2.
        public static readonly int[] LoginRewards = { 50, 100, 150, 200, 300, 400, 500 };

        const string KLoginDay       = "RailMVP.DailyLogin.Day";        // último dia reclamado (0 = nunca, 1-7)
        const string KLoginLastClaim = "RailMVP.DailyLogin.LastClaim";  // yyyy-MM-dd UTC

        // ============ Chest config ============

        public const int ChestReward = 150;
        const string KChestLastDate = "RailMVP.AdChest.LastDate";

        // ============ State ============

        int _lastClaimedDay;        // 0 = nunca; senão 1-7
        string _lastClaimDate = "";
        string _chestLastDate = "";

        public int LastClaimedDay => _lastClaimedDay;
        public string LastClaimDate => _lastClaimDate;
        public string ChestLastDate => _chestLastDate;

        /// <summary>Dia que vai ser reclamado se o usuário clicar Reclamar agora (1-7).</summary>
        public int NextDay => (_lastClaimedDay % LoginCycleLength) + 1;

        /// <summary>Coins que o NextDay paga.</summary>
        public int NextDayReward => LoginRewards[NextDay - 1];

        public event Action OnLoginClaimed;
        public event Action OnChestClaimed;

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
            Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Save();
        }

        void Load()
        {
            _lastClaimedDay = PlayerPrefs.GetInt(KLoginDay, 0);
            _lastClaimDate = PlayerPrefs.GetString(KLoginLastClaim, "");
            _chestLastDate = PlayerPrefs.GetString(KChestLastDate, "");
            Debug.Log($"[DailyLogin] Loaded — day={_lastClaimedDay} lastClaim='{_lastClaimDate}' chestDate='{_chestLastDate}' next=Day{NextDay}(+{NextDayReward})");
        }

        void Save()
        {
            PlayerPrefs.SetInt(KLoginDay, _lastClaimedDay);
            PlayerPrefs.SetString(KLoginLastClaim, _lastClaimDate);
            PlayerPrefs.SetString(KChestLastDate, _chestLastDate);
            PlayerPrefs.Save();
        }

        // ============ Login ============

        /// <summary>True se o usuário ainda não reclamou hoje (popup deve aparecer).</summary>
        public bool ShouldShowPopup() => _lastClaimDate != TodayUtc();

        /// <summary>
        /// Credita NextDayReward no PlayerDataManager, avança ciclo, salva.
        /// No-op se já reclamou hoje.
        /// </summary>
        public void ClaimLogin()
        {
            if (!ShouldShowPopup())
            {
                Debug.Log("[DailyLogin] Already claimed today, ignoring.");
                return;
            }
            int day = NextDay;
            int reward = NextDayReward;

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.AddCoins(reward);
                PlayerDataManager.Instance.Save();
            }

            _lastClaimedDay = day;
            _lastClaimDate = TodayUtc();
            Save();
            Debug.Log($"[DailyLogin] Claimed Day {day} → +{reward} coins. Next will be Day {NextDay}.");
            OnLoginClaimed?.Invoke();
        }

        // ============ Chest ============

        public bool IsChestAvailable() => _chestLastDate != TodayUtc();

        /// <summary>
        /// STUB Fatia 3 — credita direto sem ad. Fatia 5 vai trocar isso por
        /// AdsManager.ShowRewardedAd(callback) e só dar a recompensa no callback
        /// de sucesso.
        /// </summary>
        public void ClaimChest()
        {
            if (!IsChestAvailable())
            {
                Debug.Log("[DailyChest] Already claimed today, ignoring.");
                return;
            }

            // TODO Fatia 5: substituir por AdsManager.ShowRewardedAd(success → grant).
            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.AddCoins(ChestReward);
                PlayerDataManager.Instance.Save();
            }
            _chestLastDate = TodayUtc();
            Save();
            Debug.Log($"[DailyChest] Claimed → +{ChestReward} coins.");
            OnChestClaimed?.Invoke();
        }

        // ============ Debug ============

        /// <summary>Reseta last claim → popup volta a aparecer (sem reset do ciclo).</summary>
        public void DebugForceLoginAvailable()
        {
            _lastClaimDate = "";
            Save();
            Debug.Log("[DailyLogin] DEBUG: popup forced available.");
        }

        /// <summary>Simula "pular pra amanhã" sem mudar a data do sistema.
        /// Apaga o lastClaim — próximo claim avança +1 dia normal.</summary>
        public void DebugAdvanceCycleStep()
        {
            DebugForceLoginAvailable();
        }

        public void DebugResetAll()
        {
            _lastClaimedDay = 0;
            _lastClaimDate = "";
            _chestLastDate = "";
            PlayerPrefs.DeleteKey(KLoginDay);
            PlayerPrefs.DeleteKey(KLoginLastClaim);
            PlayerPrefs.DeleteKey(KChestLastDate);
            PlayerPrefs.Save();
            Debug.Log("[DailyLogin] DEBUG: reset all (login + chest).");
        }

        public void DebugForceChestAvailable()
        {
            _chestLastDate = "";
            Save();
            Debug.Log("[DailyChest] DEBUG: chest forced available.");
        }

        // ============ Helpers ============

        static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
