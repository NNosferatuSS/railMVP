using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Net;

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
        const string KeyAccountXp    = "RailMVP.AccountXP";
        const string KeyAccountLevel = "RailMVP.AccountLevel";  // derivado do XP; salvo só pra inspector/debug

        [Header("Runtime (read-only)")]
        [SerializeField] private int coins;
        [SerializeField] private int bestDistance;
        [SerializeField] private int bestCoins;
        [SerializeField] private int bestTier;
        [SerializeField] private float bestTime;
        [SerializeField] private int totalRuns;
        [SerializeField] private int equippedChar;
        [SerializeField] private string playerName = "Player";
        [SerializeField] private int accountXp;
        [SerializeField] private int accountLevel = 1;

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
        public int AccountXP => accountXp;
        public int AccountLevel => accountLevel;

        /// <summary>Resultado de UpdateBests — diz quais records foram batidos.</summary>
        public struct RecordResult
        {
            public bool distance, coins, tier, time;
            public bool Any => distance || coins || tier || time;
        }

        public event System.Action<int> OnCoinsChanged;
        public event System.Action<RecordResult> OnRecordsUpdated;
        public event System.Action<int> OnEquippedCharChanged;

        /// <summary>Disparado quando PlayerName muda via SetPlayerName (Fatia 9). UI escuta pra atualizar texts inline.</summary>
        public event System.Action<string> OnPlayerNameChanged;

        /// <summary>Disparado quando AddXP faz o account level subir. Payload = novo nível (Camada 1 da progressão adaptativa).</summary>
        public event System.Action<int> OnAccountLevelUp;

        /// <summary>
        /// Disparado após ApplyRemoteState — sinaliza pra UI "re-leia TUDO do PDM"
        /// (sem payload). Resolve o problema de pull em runtime atualizar coins
        /// mas não best/runs. Não dispara em Save() local (esses campos não mudam
        /// individualmente sem ter o próprio event tipado).
        /// </summary>
        public event System.Action OnFullStateChanged;

        /// <summary>Disparado após Save() pra observers que precisam sync (Fatia 7B). Suprimido durante ApplyRemoteState pra evitar loop pull→save→push.</summary>
        public event System.Action OnDataChanged;

        bool _suppressDataChanged;

        // Validação de player_name (Fatia 9). Trim, length 3-12, alphanumeric + espaço + underscore.
        public const int PlayerNameMinLength = 3;
        public const int PlayerNameMaxLength = 12;

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
            accountXp    = PlayerPrefs.GetInt(KeyAccountXp, 0);
            accountLevel = ComputeLevelFromXP(accountXp);  // XP é a fonte da verdade; level é derivado

            _ownedChars.Clear();
            // Default: char 0 (Runner) sempre owned.
            _ownedChars.Add(0);
            string csv = PlayerPrefs.GetString(KeyOwnedChars, "0");
            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out int idx))
                    _ownedChars.Add(idx);
            }

            Debug.Log($"[PDM] Loaded: coins={coins} bestDist={bestDistance}m runs={totalRuns} lvl={accountLevel} xp={accountXp} equipped={equippedChar} owned=[{string.Join(",", _ownedChars)}]");
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
            PlayerPrefs.SetInt(KeyAccountXp, accountXp);
            PlayerPrefs.SetInt(KeyAccountLevel, accountLevel);
            PlayerPrefs.SetString(KeyOwnedChars, string.Join(",", _ownedChars));
            PlayerPrefs.Save();

            if (!_suppressDataChanged) OnDataChanged?.Invoke();
        }

        // ============ Sync (Fatia 7B) ============

        /// <summary>
        /// Sobrescreve estado in-memory + PlayerPrefs com dados vindos do servidor.
        /// NÃO dispara OnDataChanged (evita loop pull→save→push). Dispara os
        /// eventos de UI normais (OnCoinsChanged, OnRecordsUpdated, OnEquippedCharChanged)
        /// pra Home/HUD refletirem o estado novo.
        /// </summary>
        public void ApplyRemoteState(PlayerRemoteState s)
        {
            if (s == null) return;

            coins        = s.coins;
            bestDistance = s.best_distance;
            bestCoins    = s.best_coins;
            bestTier     = s.best_tier;
            bestTime     = s.best_time;
            totalRuns    = s.total_runs;
            equippedChar = s.equipped_char;
            playerName   = string.IsNullOrEmpty(s.player_name) ? "Player" : s.player_name;

            _ownedChars.Clear();
            _ownedChars.Add(0);
            if (!string.IsNullOrEmpty(s.owned_chars))
            {
                foreach (var part in s.owned_chars.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int idx))
                        _ownedChars.Add(idx);
                }
            }

            _suppressDataChanged = true;
            Save();
            _suppressDataChanged = false;

            OnCoinsChanged?.Invoke(coins);
            OnEquippedCharChanged?.Invoke(equippedChar);
            OnPlayerNameChanged?.Invoke(playerName);
            OnFullStateChanged?.Invoke();
            Debug.Log($"[PDM] ApplyRemoteState — coins={coins} bestDist={bestDistance}m runs={totalRuns} equipped={equippedChar} name='{playerName}'");
        }

        /// <summary>Copia o estado atual pra um PlayerRemoteState (pra push em Fatia 7B). updated_at fica vazio — server seta via trigger.</summary>
        public void CopyToRemoteState(PlayerRemoteState target)
        {
            if (target == null) return;
            target.coins         = coins;
            target.best_distance = bestDistance;
            target.best_coins    = bestCoins;
            target.best_tier     = bestTier;
            target.best_time     = bestTime;
            target.total_runs    = totalRuns;
            target.equipped_char = equippedChar;
            target.owned_chars   = string.Join(",", _ownedChars);
            target.player_name   = playerName;
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

        // ============ Player Name (Fatia 9) ============

        /// <summary>
        /// Atualiza o nome do jogador com validação. Retorna true se válido (e
        /// persistiu). false se rejeitou (length errado, vazio).
        ///
        /// Em sucesso: dispara OnPlayerNameChanged (UI live update) + OnDataChanged
        /// (Sync push pro Supabase via Fatia 7B). Submits subsequentes pro
        /// leaderboard usam o nome novo automaticamente.
        ///
        /// Não atualiza rows já submetidas no daily_results — `player_name` é
        /// desnormalizado lá, e nosso submit_daily_result RPC só atualiza com
        /// distance > existing. Aceitável pra MVP.
        /// </summary>
        public bool SetPlayerName(string newName)
        {
            if (newName == null) return false;
            string trimmed = newName.Trim();
            if (trimmed.Length < PlayerNameMinLength || trimmed.Length > PlayerNameMaxLength)
            {
                Debug.LogWarning($"[PDM] SetPlayerName rejected: length {trimmed.Length} not in [{PlayerNameMinLength},{PlayerNameMaxLength}].");
                return false;
            }
            if (trimmed == playerName) return true; // no-op

            playerName = trimmed;
            Save();
            OnPlayerNameChanged?.Invoke(playerName);
            Debug.Log($"[PDM] PlayerName set to '{playerName}'.");
            return true;
        }

        // ============ Account Level / XP (Camada 1 — Progressão Adaptativa) ============

        /// <summary>XP necessário pra subir DO nível dado pro próximo. Curva: 100 * level * 1.5.</summary>
        public static int XpForNextLevel(int level) => Mathf.RoundToInt(100 * level * 1.5f);

        /// <summary>Recalcula o nível a partir do XP total acumulado (nível mínimo 1).</summary>
        public int ComputeLevelFromXP(int totalXP)
        {
            int level = 1;
            int xpRemaining = Mathf.Max(0, totalXP);
            while (xpRemaining >= XpForNextLevel(level))
            {
                xpRemaining -= XpForNextLevel(level);
                level++;
            }
            return level;
        }

        /// <summary>
        /// Soma XP lifetime e recalcula o account level. Persiste (Save) e dispara
        /// OnAccountLevelUp se o nível subiu. Chamado no fim de cada run. No-op se
        /// amount <= 0 (não persiste — caller cuida do Save se precisar).
        /// </summary>
        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            int prevLevel = accountLevel;
            accountXp += amount;
            accountLevel = ComputeLevelFromXP(accountXp);
            Save();
            if (accountLevel > prevLevel)
            {
                Debug.Log($"[PDM] Account level up: {prevLevel} → {accountLevel} (xp={accountXp})");
                OnAccountLevelUp?.Invoke(accountLevel);
            }
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

        /// <summary>XP total acumulado necessário pra ESTAR no nível dado (soma das curvas anteriores).</summary>
        public int XpRequiredForLevel(int level)
        {
            int total = 0;
            for (int k = 1; k < Mathf.Max(1, level); k++)
                total += XpForNextLevel(k);
            return total;
        }

        /// <summary>
        /// Debug: define o XP absoluto (clamp >= 0), recalcula o nível, salva e
        /// dispara OnAccountLevelUp se subiu. Base dos demais helpers de debug.
        /// Pra testar account level / starting tier adaptativo sem grind.
        /// </summary>
        public void DebugSetXP(int totalXp)
        {
            int prevLevel = accountLevel;
            accountXp = Mathf.Max(0, totalXp);
            accountLevel = ComputeLevelFromXP(accountXp);
            Save();
            Debug.Log($"[PDM] DebugSetXP → xp={accountXp} lvl={accountLevel} (era {prevLevel})");
            if (accountLevel > prevLevel) OnAccountLevelUp?.Invoke(accountLevel);
        }

        /// <summary>Debug: soma (ou subtrai, se negativo) XP. Clampa em 0.</summary>
        public void DebugAddXP(int delta) => DebugSetXP(accountXp + delta);

        /// <summary>Debug: pula direto pro XP mínimo do nível dado (nível >= 1).</summary>
        public void DebugSetLevel(int level) => DebugSetXP(XpRequiredForLevel(level));

        [Header("Debug — XP/Level (use o menu de contexto ⋮ do componente)")]
        [Tooltip("Valor usado pelos itens 'Add XP' / 'Remove XP' do menu de contexto.")]
        [SerializeField] private int debugXpAmount = 10000;
        [Tooltip("Nível usado pelo item 'Set Level' do menu de contexto.")]
        [SerializeField] private int debugLevelTarget = 20;

        [ContextMenu("Debug/Add XP")]
        void DebugAddXpFromInspector() => DebugAddXP(debugXpAmount);

        [ContextMenu("Debug/Remove XP")]
        void DebugRemoveXpFromInspector() => DebugAddXP(-debugXpAmount);

        [ContextMenu("Debug/Set Level")]
        void DebugSetLevelFromInspector() => DebugSetLevel(debugLevelTarget);

        [ContextMenu("Debug/Reset XP (Level 1)")]
        void DebugResetXpFromInspector() => DebugSetXP(0);

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
            accountXp = 0;
            accountLevel = 1;
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
            PlayerPrefs.DeleteKey(KeyAccountXp);
            PlayerPrefs.DeleteKey(KeyAccountLevel);
            PlayerPrefs.DeleteKey(KeyOwnedChars);
            PlayerPrefs.Save();

            OnCoinsChanged?.Invoke(0);
            Debug.Log("[PDM] All player data wiped.");
        }
    }
}
