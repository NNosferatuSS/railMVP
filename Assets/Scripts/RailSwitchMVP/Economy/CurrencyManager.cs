using UnityEngine;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.Economy
{
    /// <summary>
    /// API única de moedas (facade). Singleton DontDestroyOnLoad.
    ///
    /// - Coins: NÃO é fonte da verdade aqui — DELEGA ao PlayerDataManager (que já
    ///   persiste e sincroniza com o Supabase). Espelha PDM.OnCoinsChanged como
    ///   OnCurrencyChanged(Coins, ...).
    /// - Gems: gerido por este manager (PlayerPrefs `RailMVP.Gems`) E sincronizado
    ///   com o Supabase (coluna `gems` em players) via Copy/ApplyRemoteState +
    ///   OnDataChanged (PlayerDataSync escuta).
    /// - EventTokens: local-only por ora (reservado pra Fase C, sem sync).
    ///
    /// A fonte das gems é decidida fora daqui: basta chamar Add(Gems, x, "fonte").
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        const string KeyGems        = "RailMVP.Gems";
        const string KeyEventTokens = "RailMVP.EventTokens";

        [Header("Runtime (read-only)")]
        [SerializeField] private int gems;
        [SerializeField] private int eventTokens;

        [Header("Debug")]
        [SerializeField] private int debugGemsAmount = 100;

        /// <summary>(tipo, novo total). Disparado em qualquer mudança de saldo.</summary>
        public event System.Action<CurrencyType, int> OnCurrencyChanged;

        /// <summary>Disparado quando gems/eventTokens mudam e persistem — PlayerDataSync escuta pra pushar. Suprimido durante ApplyRemoteState (evita loop pull→save→push).</summary>
        public event System.Action OnDataChanged;

        bool _suppressDataChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        void OnDestroy()
        {
            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            // Coins são do PDM; espelha o evento dele como OnCurrencyChanged(Coins).
            // PDM é DontDestroyOnLoad (vem da Home), então já existe aqui.
            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.OnCoinsChanged += HandleCoinsChanged;
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) Save(); }

        void HandleCoinsChanged(int total) => OnCurrencyChanged?.Invoke(CurrencyType.Coins, total);

        // ============ Load / Save ============

        void Load()
        {
            gems        = PlayerPrefs.GetInt(KeyGems, 0);
            eventTokens = PlayerPrefs.GetInt(KeyEventTokens, 0);
            Debug.Log($"[Currency] Loaded: gems={gems} eventTokens={eventTokens}");
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyGems, gems);
            PlayerPrefs.SetInt(KeyEventTokens, eventTokens);
            PlayerPrefs.Save();
            if (!_suppressDataChanged) OnDataChanged?.Invoke();
        }

        // ============ API ============

        public int GetBalance(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Coins:       return PlayerDataManager.Instance != null ? PlayerDataManager.Instance.Coins : 0;
                case CurrencyType.Gems:        return gems;
                case CurrencyType.EventTokens: return eventTokens;
                default: return 0;
            }
        }

        public bool CanAfford(CurrencyType type, int amount) => amount <= 0 || GetBalance(type) >= amount;

        /// <summary>Credita moeda. source é só pra rastreio/log futuro. Persiste e dispara OnCurrencyChanged.</summary>
        public void Add(CurrencyType type, int amount, string source)
        {
            if (amount <= 0) return;
            switch (type)
            {
                case CurrencyType.Coins:
                    var pdm = PlayerDataManager.Instance;
                    if (pdm != null) { pdm.AddCoins(amount); pdm.Save(); } // PDM dispara OnCoinsChanged → HandleCoinsChanged
                    break;
                case CurrencyType.Gems:
                    gems += amount;
                    Save();
                    OnCurrencyChanged?.Invoke(CurrencyType.Gems, gems);
                    break;
                case CurrencyType.EventTokens:
                    eventTokens += amount;
                    Save();
                    OnCurrencyChanged?.Invoke(CurrencyType.EventTokens, eventTokens);
                    break;
            }
        }

        /// <summary>Debita se houver saldo. Retorna false SEM modificar se faltar fundos. Nunca deixa negativo.</summary>
        public bool TrySpend(CurrencyType type, int amount, string reason)
        {
            if (amount <= 0) return false;
            switch (type)
            {
                case CurrencyType.Coins:
                    var pdm = PlayerDataManager.Instance;
                    if (pdm == null || !pdm.SpendCoins(amount)) return false;
                    pdm.Save();
                    return true;
                case CurrencyType.Gems:
                    if (gems < amount) return false;
                    gems -= amount;
                    Save();
                    OnCurrencyChanged?.Invoke(CurrencyType.Gems, gems);
                    return true;
                case CurrencyType.EventTokens:
                    if (eventTokens < amount) return false;
                    eventTokens -= amount;
                    Save();
                    OnCurrencyChanged?.Invoke(CurrencyType.EventTokens, eventTokens);
                    return true;
                default: return false;
            }
        }

        // ============ Sync (gems) — coins vão pelo PDM ============

        public void CopyToRemoteState(PlayerRemoteState target)
        {
            if (target == null) return;
            target.gems = gems;
        }

        public void ApplyRemoteState(PlayerRemoteState s)
        {
            if (s == null) return;
            gems = Mathf.Max(0, s.gems);
            _suppressDataChanged = true;
            Save();
            _suppressDataChanged = false;
            OnCurrencyChanged?.Invoke(CurrencyType.Gems, gems);
            Debug.Log($"[Currency] ApplyRemoteState — gems={gems}");
        }

        // ============ Debug ============

        public void DebugAddGems(int amount) => Add(CurrencyType.Gems, amount, "debug");

        [ContextMenu("Debug/Add Gems")]
        void DebugAddGemsFromInspector() => DebugAddGems(debugGemsAmount);

        [ContextMenu("Debug/Remove Gems")]
        void DebugRemoveGemsFromInspector() => TrySpend(CurrencyType.Gems, debugGemsAmount, "debug");

        [ContextMenu("Debug/Reset Gems")]
        void DebugResetGems()
        {
            gems = 0;
            Save();
            OnCurrencyChanged?.Invoke(CurrencyType.Gems, gems);
        }
    }
}
