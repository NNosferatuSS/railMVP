using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Advertisements;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Wrapper Unity Ads (com.unity.ads 4.x). Singleton DontDestroyOnLoad.
    /// Inicializa no Awake, mantém um rewarded "quente" carregado, reloads
    /// automaticamente após cada show.
    ///
    /// API única pra callers:
    /// - <see cref="IsRewardedReady"/> — true quando o ad está pronto pra exibir.
    /// - <see cref="TryShowRewarded(Action, Action)"/> — exibe, dispara callback
    ///   de sucesso após o user completar o ad. Falha (ad não carregado, user
    ///   skipped antes do término) → onFailed.
    ///
    /// Spec §5.2: caller deve ESCONDER o botão se IsRewardedReady == false
    /// (não dar fallback "grátis" em produção). Editor sem Game ID configurado
    /// → testMode dá sempre placeholder ad que sempre completa.
    /// </summary>
    public class AdsManager : MonoBehaviour,
        IUnityAdsInitializationListener,
        IUnityAdsLoadListener,
        IUnityAdsShowListener
    {
        public static AdsManager Instance { get; private set; }

        [Header("Unity Ads Game IDs (do dashboard)")]
        [Tooltip("Game ID Android. Dashboard → Monetization → Project Settings → Game IDs.")]
        [SerializeField] private string androidGameId = "0000000";

        [Tooltip("Game ID iOS. Vazio se só Android por ora.")]
        [SerializeField] private string iosGameId = "0000000";

        [Header("Placement IDs (do dashboard, ou os defaults da Unity)")]
        [Tooltip("Default 'Rewarded_Android' / 'Rewarded_iOS' (auto-criados pela Unity).")]
        [SerializeField] private string rewardedAndroidId = "Rewarded_Android";
        [SerializeField] private string rewardedIosId = "Rewarded_iOS";

        [Header("Behavior")]
        [Tooltip("true = test ads (placeholder skippable). Sempre true em dev. " +
            "false só pra builds de produção com Game IDs reais.")]
        [SerializeField] private bool testMode = true;

        [Tooltip("Logs detalhados de cada callback do Unity Ads SDK.")]
        [SerializeField] private bool verboseLogs = true;

        [Header("Mock Mode (DEV ONLY)")]
        [Tooltip("Quando true, BYPASSA o Unity Ads SDK totalmente. " +
            "Simula init imediato + 'ad' que dura mockAdDuration segundos e chama onSuccess. " +
            "Útil quando o backend ainda não está servindo ads (projeto novo) ou pra dev offline. " +
            "DESLIGAR antes de release.")]
        [SerializeField] private bool useMockAds = false;

        [Tooltip("Duração da 'tela preta simulando ad' em segundos. " +
            "Só usado se useMockAds = true.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float mockAdDuration = 1.5f;

        [Tooltip("Quando true, mock sempre chama onSuccess. " +
            "Quando false, alterna success/fail (pra testar onFailed paths).")]
        [SerializeField] private bool mockAdAlwaysSucceeds = true;

        // Estado mock — alterna sucesso/falha quando mockAdAlwaysSucceeds = false.
        private bool _mockNextOutcomeSuccess = true;

        private string _activeGameId;
        private string _activeRewardedId;
        private bool _initialized;
        private bool _rewardedLoaded;

        // Callbacks do show ativo — limpos ao final pra evitar double-fire.
        private Action _pendingSuccess;
        private Action _pendingFail;

        public bool IsInitialized => _initialized;
        public bool IsRewardedReady => _initialized && _rewardedLoaded;

        /// <summary>
        /// Disparado quando IsRewardedReady flipa (load completo OU consumido).
        /// UIs que mostram botões dependentes de ad devem subscrever.
        /// </summary>
        public event Action<bool> OnRewardedReadyChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_IOS
            _activeGameId = iosGameId;
            _activeRewardedId = rewardedIosId;
#else
            _activeGameId = androidGameId;
            _activeRewardedId = rewardedAndroidId;
#endif
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (useMockAds)
            {
                if (verboseLogs) Debug.Log("[Ads-MOCK] Mock mode ON — bypassing Unity Ads SDK.");
                _initialized = true;
                _rewardedLoaded = true;
                // Notifica imediatamente que está pronto.
                OnRewardedReadyChanged?.Invoke(true);
                return;
            }

            if (Advertisement.isInitialized)
            {
                _initialized = true;
                LoadRewarded();
                return;
            }

            if (string.IsNullOrEmpty(_activeGameId))
            {
                Debug.LogWarning("[Ads] Game ID vazio — AdsManager não vai inicializar. " +
                    "Configure no Inspector ou esconda botões que dependem de IsRewardedReady.");
                return;
            }

            // DEV ONLY — hardcoded consent + privacy mode destravam header bidding
            // em LGPD/GDPR e maximizam inventory disponível (mixed audience flag).
            // Produção: substituir pelo resultado real de um CMP/popup de consent.
            var gdprMetaData = new MetaData("gdpr");
            gdprMetaData.Set("consent", "true");
            Advertisement.SetMetaData(gdprMetaData);

            var privacyMetaData = new MetaData("privacy");
            privacyMetaData.Set("mode", "mixed");
            Advertisement.SetMetaData(privacyMetaData);

            if (verboseLogs) Debug.Log($"[Ads] Initialize gameId={_activeGameId} test={testMode}");
            Advertisement.Initialize(_activeGameId, testMode, this);
        }

        // ==================================================
        // API pública pros callers (chest, GameOver 2x coins)
        // ==================================================

        /// <summary>
        /// Tenta exibir um rewarded ad. Se não carregado, retorna false
        /// imediatamente — onFailed NÃO é invocado (caller já deve ter
        /// escondido o botão via IsRewardedReady).
        ///
        /// Se carregado, exibe e dispara onSuccess no callback de
        /// "user completou o ad" (ou onFailed se skip/falha).
        ///
        /// Após o show (sucesso ou falha), automaticamente carrega o próximo.
        /// </summary>
        public bool TryShowRewarded(Action onSuccess, Action onFailed = null)
        {
            if (!IsRewardedReady)
            {
                if (verboseLogs) Debug.Log("[Ads] TryShowRewarded recusado — rewarded não pronto.");
                return false;
            }

            _pendingSuccess = onSuccess;
            _pendingFail = onFailed;
            _rewardedLoaded = false; // marca como consumido até o reload terminar
            OnRewardedReadyChanged?.Invoke(false);

            if (useMockAds)
            {
                if (verboseLogs) Debug.Log($"[Ads-MOCK] Show (duration={mockAdDuration}s)");
                StartCoroutine(MockShowRoutine());
                return true;
            }

            if (verboseLogs) Debug.Log($"[Ads] Show {_activeRewardedId}");
            Advertisement.Show(_activeRewardedId, this);
            return true;
        }

        // Simula um ad: aguarda mockAdDuration unscaled, dispara success/fail
        // conforme mockAdAlwaysSucceeds (ou alterna), recarrega o próximo.
        IEnumerator MockShowRoutine()
        {
            yield return new WaitForSecondsRealtime(mockAdDuration);

            bool success = mockAdAlwaysSucceeds || _mockNextOutcomeSuccess;
            if (!mockAdAlwaysSucceeds) _mockNextOutcomeSuccess = !_mockNextOutcomeSuccess;

            if (verboseLogs) Debug.Log($"[Ads-MOCK] Show complete: {(success ? "COMPLETED" : "SKIPPED")}");
            FlushPending(success);

            // "Reload": volta a ficar disponível pra próxima.
            _rewardedLoaded = true;
            OnRewardedReadyChanged?.Invoke(true);
        }

        // ==================================================
        // Unity Ads SDK callbacks (interfaces)
        // ==================================================

        public void OnInitializationComplete()
        {
            _initialized = true;
            if (verboseLogs) Debug.Log("[Ads] Initialization complete.");
            LoadRewarded();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogWarning($"[Ads] Initialization failed: {error} — {message}");
        }

        void LoadRewarded()
        {
            if (!_initialized) return;
            if (verboseLogs) Debug.Log($"[Ads] Load {_activeRewardedId}");
            Advertisement.Load(_activeRewardedId, this);
        }

        public void OnUnityAdsAdLoaded(string placementId)
        {
            if (placementId == _activeRewardedId)
            {
                _rewardedLoaded = true;
                if (verboseLogs) Debug.Log($"[Ads] Loaded {placementId}");
                OnRewardedReadyChanged?.Invoke(true);
            }
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            Debug.LogWarning($"[Ads] Failed to load {placementId}: {error} — {message}");
        }

        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            Debug.LogWarning($"[Ads] Show failure {placementId}: {error} — {message}");
            FlushPending(false);
            LoadRewarded();
        }

        public void OnUnityAdsShowStart(string placementId)
        {
            if (verboseLogs) Debug.Log($"[Ads] Show start {placementId}");
        }

        public void OnUnityAdsShowClick(string placementId)
        {
            if (verboseLogs) Debug.Log($"[Ads] Show click {placementId}");
        }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
        {
            if (verboseLogs) Debug.Log($"[Ads] Show complete {placementId}: {showCompletionState}");
            bool success = (showCompletionState == UnityAdsShowCompletionState.COMPLETED);
            FlushPending(success);
            LoadRewarded();
        }

        void FlushPending(bool success)
        {
            var s = _pendingSuccess;
            var f = _pendingFail;
            _pendingSuccess = null;
            _pendingFail = null;
            if (success) s?.Invoke();
            else f?.Invoke();
        }

        // ==================================================
        // Debug helpers (chamáveis pelo DebugPanel)
        // ==================================================

        public void DebugLogState()
        {
            Debug.Log($"[Ads] gameId={_activeGameId} placement={_activeRewardedId} " +
                $"init={_initialized} loaded={_rewardedLoaded} test={testMode} mock={useMockAds}");
        }
    }
}
