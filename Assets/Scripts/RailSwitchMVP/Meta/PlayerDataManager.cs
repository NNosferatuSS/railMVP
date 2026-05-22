using System.Collections.Generic;
using UnityEngine;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Fonte da verdade pra todo dado persistente do jogador: coins, best
    /// scores, total de runs, personagens owned/equipped. Singleton
    /// DontDestroyOnLoad — sobrevive entre cenas (Home ↔ Game).
    ///
    /// Backed por PlayerPrefs. As 4 chaves de best (Distance/Coins/Tier/Time)
    /// reusam o naming do antigo HighScoreManager (`RailMVP.BestX`) pra
    /// preservar records salvos na v0.2.0.
    ///
    /// Save() é chamado em pontos de boundary: OnApplicationPause(true),
    /// OnApplicationFocus(false), fim de run, claim de missão. Mudanças
    /// in-memory (SetInt/SetString) não custam disco até Save().
    /// </summary>
    public class PlayerDataManager : MonoBehaviour
    {
        public static PlayerDataManager Instance { get; private set; }

        // Backwards-compat com HighScoreManager (NÃO renomear).
        const string KeyBestDistance = "RailMVP.BestDistance";
        const string KeyBestCoins    = "RailMVP.BestCoins";
        const string KeyBestTier     = "RailMVP.BestTier";
        const string KeyBestTime     = "RailMVP.BestTime";

        const string KeyCoins        = "RailMVP.Coins";
        const string KeyTotalRuns    = "RailMVP.TotalRuns";
        const string KeyOwnedChars   = "RailMVP.OwnedChars";   // CSV "0,1,2"
        const string KeyEquippedChar = "RailMVP.EquippedChar";
        const string KeyPlayerName   = "RailMVP.PlayerName";

        [Header("Runtime (read-only)")]
        [SerializeField] private int coins;
        [SerializeField] private int bestDistance;
        [SerializeField] private int bestCoins;
        [SerializeField] private int bestTier;
        [SerializeField] private float bestTime;
        [SerializeField] private int totalRuns;
        [SerializeField] private int equippedChar;
        [SerializeField] private string playerName = "Player";

        // Set-based pra checagem O(1). Persistido como CSV.
        private readonly HashSet<int> _ownedChars = new HashSet<int>();

        public int Coins => coins;
        public int BestDistance => bestDistance;
        public int BestCoins => bestCoins;
        public int BestTier => bestTier;
        public float BestTime => bestTime;
        public int TotalRuns => totalRuns;
        public int EquippedChar => equippedChar;
        public string PlayerName => playerName;

        /// <summary>Resultado de UpdateBests — diz quais records foram batidos.</summary>
        public struct RecordResult
        {
            public bool distance, coins, tier, time;
            public bool Any => distance || coins || tier || time;
        }

        public event System.Action<int> OnCoinsChanged;
        public event System.Action<RecordResult> OnRecordsUpdated;
        public event System.Action<int> OnEquippedCharChanged;

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

        // ============ Load / Save ============

        public void Load()
        {
            coins        = PlayerPrefs.GetInt(KeyCoins, 0);
            bestDistance = PlayerPrefs.GetInt(KeyBestDistance, 0);
            bestCoins    = PlayerPrefs.GetInt(KeyBestCoins, 0);
            bestTier     = PlayerPrefs.GetInt(KeyBestTier, 0);
            bestTime     = PlayerPrefs.GetFloat(KeyBestTime, 0f);
            totalRuns    = PlayerPrefs.GetInt(KeyTotalRuns, 0);
            equippedChar = PlayerPrefs.GetInt(KeyEquippedChar, 0);
            playerName   = PlayerPrefs.GetString(KeyPlayerName, "Player");

            _ownedChars.Clear();
            // Default: char 0 (Runner) sempre owned.
            _ownedChars.Add(0);
            string csv = PlayerPrefs.GetString(KeyOwnedChars, "0");
            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out int idx))
                    _ownedChars.Add(idx);
            }

            Debug.Log($"[PDM] Loaded: coins={coins} bestDist={bestDistance}m runs={totalRuns} equipped={equippedChar} owned=[{string.Join(",", _ownedChars)}]");
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyCoins, coins);
            PlayerPrefs.SetInt(KeyBestDistance, bestDistance);
            PlayerPrefs.SetInt(KeyBestCoins, bestCoins);
            PlayerPrefs.SetInt(KeyBestTier, bestTier);
            PlayerPrefs.SetFloat(KeyBestTime, bestTime);
            PlayerPrefs.SetInt(KeyTotalRuns, totalRuns);
            PlayerPrefs.SetInt(KeyEquippedChar, equippedChar);
            PlayerPrefs.SetString(KeyPlayerName, playerName);
            PlayerPrefs.SetString(KeyOwnedChars, string.Join(",", _ownedChars));
            PlayerPrefs.Save();
        }

        // ============ Coins ============

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            coins += amount;
            OnCoinsChanged?.Invoke(coins);
        }

        /// <summary>Retorna true se gastou (saldo suficiente); false sem efeito.</summary>
        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || coins < amount) return false;
            coins -= amount;
            OnCoinsChanged?.Invoke(coins);
            return true;
        }

        // ============ Runs & Bests ============

        public void IncrementTotalRuns()
        {
            totalRuns++;
        }

        /// <summary>
        /// Atualiza bests com stats de uma run. Salva em PlayerPrefs (e dispara
        /// OnRecordsUpdated) se qualquer record foi batido.
        /// </summary>
        public RecordResult UpdateBests(int distance, int runCoins, int tier, float time)
        {
            var result = new RecordResult();

            if (distance > bestDistance) { bestDistance = distance; result.distance = true; }
            if (runCoins > bestCoins)    { bestCoins = runCoins;    result.coins = true; }
            if (tier > bestTier)         { bestTier = tier;         result.tier = true; }
            if (time > bestTime)         { bestTime = time;         result.time = true; }

            if (result.Any)
            {
                Save();
                Debug.Log($"[PDM] Record(s) broken: dist={result.distance} coins={result.coins} tier={result.tier} time={result.time}");
                OnRecordsUpdated?.Invoke(result);
            }

            return result;
        }

        // ============ Characters ============

        public bool IsCharacterOwned(int index) => _ownedChars.Contains(index);

        public void UnlockCharacter(int index)
        {
            if (_ownedChars.Add(index))
                Debug.Log($"[PDM] Unlocked char {index}");
        }

        public void EquipCharacter(int index)
        {
            if (!_ownedChars.Contains(index))
            {
                Debug.LogWarning($"[PDM] Cannot equip char {index} — not owned.");
                return;
            }
            if (equippedChar == index) return;
            equippedChar = index;
            OnEquippedCharChanged?.Invoke(index);
        }

        // ============ Debug ============

        /// <summary>Reseta só personagens (mantém coins, bests, runs).</summary>
        public void DebugResetCharacters()
        {
            _ownedChars.Clear();
            _ownedChars.Add(0);
            equippedChar = 0;
            Save();
            OnEquippedCharChanged?.Invoke(0);
            Debug.Log("[PDM] Characters reset (Runner only).");
        }

        /// <summary>Apaga todos os dados (debug). Sem confirmação.</summary>
        public void WipeAll()
        {
            coins = 0;
            bestDistance = 0;
            bestCoins = 0;
            bestTier = 0;
            bestTime = 0f;
            totalRuns = 0;
            equippedChar = 0;
            playerName = "Player";
            _ownedChars.Clear();
            _ownedChars.Add(0);

            PlayerPrefs.DeleteKey(KeyCoins);
            PlayerPrefs.DeleteKey(KeyBestDistance);
            PlayerPrefs.DeleteKey(KeyBestCoins);
            PlayerPrefs.DeleteKey(KeyBestTier);
            PlayerPrefs.DeleteKey(KeyBestTime);
            PlayerPrefs.DeleteKey(KeyTotalRuns);
            PlayerPrefs.DeleteKey(KeyEquippedChar);
            PlayerPrefs.DeleteKey(KeyPlayerName);
            PlayerPrefs.DeleteKey(KeyOwnedChars);
            PlayerPrefs.Save();

            OnCoinsChanged?.Invoke(0);
            Debug.Log("[PDM] All player data wiped.");
        }
    }
}
