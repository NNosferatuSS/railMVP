using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Battle Pass. Persistência em PlayerPrefs (níveis, XP, premium bool,
    /// níveis reclamados por track). XP entra via AddXP (missions daily/weekly).
    /// Não é gasto por corrida — spec §S6.
    /// </summary>
    public class BattlePassManager : MonoBehaviour
    {
        public static BattlePassManager Instance { get; private set; }

        [Header("Config")]
        [Tooltip("XP por nível. Cresce linear (500 * level) por default.")]
        [SerializeField] private int xpBase = 500;
        [SerializeField] private int maxLevel = 100;
        [SerializeField] private int seasonNumber = 1;
        [SerializeField] private string seasonName = "SWITCHYARD";
        [Tooltip("Dias restantes na temporada (display; timer real deve vir do server em prod).")]
        [SerializeField] private int seasonDaysLeft = 22;

        const string KLevel = "bp.level";
        const string KXP    = "bp.xp";
        const string KPrem  = "bp.premium";
        const string KClaimedFree = "bp.claimed.free";     // csv de níveis reclamados
        const string KClaimedPrem = "bp.claimed.premium";

        int _level, _xp;
        bool _premium;
        HashSet<int> _claimedFree = new HashSet<int>();
        HashSet<int> _claimedPremium = new HashSet<int>();

        public int Level => _level;
        public int XP => _xp;
        public int XPForNextLevel => xpBase * Mathf.Max(1, _level);
        public bool Premium => _premium;
        public int SeasonNumber => seasonNumber;
        public string SeasonName => seasonName;
        public int SeasonDaysLeft => seasonDaysLeft;

        public event Action OnChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            _xp += amount;
            while (_xp >= XPForNextLevel && _level < maxLevel)
            {
                _xp -= XPForNextLevel;
                _level++;
            }
            if (_level >= maxLevel) _xp = 0;
            Save();
            OnChanged?.Invoke();
        }

        public bool IsClaimed(int level, bool premiumTrack) =>
            (premiumTrack ? _claimedPremium : _claimedFree).Contains(level);

        public bool CanClaim(int level, bool premiumTrack)
        {
            if (level > _level) return false;
            if (premiumTrack && !_premium) return false;
            return !IsClaimed(level, premiumTrack);
        }

        public bool TryClaim(int level, bool premiumTrack)
        {
            if (!CanClaim(level, premiumTrack)) return false;
            (premiumTrack ? _claimedPremium : _claimedFree).Add(level);
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void UnlockPremium()
        {
            _premium = true;
            Save();
            OnChanged?.Invoke();
        }

        void Save()
        {
            PlayerPrefs.SetInt(KLevel, _level);
            PlayerPrefs.SetInt(KXP, _xp);
            PlayerPrefs.SetInt(KPrem, _premium ? 1 : 0);
            PlayerPrefs.SetString(KClaimedFree, string.Join(",", _claimedFree));
            PlayerPrefs.SetString(KClaimedPrem, string.Join(",", _claimedPremium));
            PlayerPrefs.Save();
        }

        void Load()
        {
            _level = PlayerPrefs.GetInt(KLevel, 1);
            _xp = PlayerPrefs.GetInt(KXP, 0);
            _premium = PlayerPrefs.GetInt(KPrem, 0) == 1;
            _claimedFree = ParseCsv(PlayerPrefs.GetString(KClaimedFree, ""));
            _claimedPremium = ParseCsv(PlayerPrefs.GetString(KClaimedPrem, ""));
        }

        static HashSet<int> ParseCsv(string csv)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(csv)) return set;
            foreach (var p in csv.Split(','))
                if (int.TryParse(p, out var n)) set.Add(n);
            return set;
        }
    }
}
