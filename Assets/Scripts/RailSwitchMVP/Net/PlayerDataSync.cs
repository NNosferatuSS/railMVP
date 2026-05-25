using System;
using System.Collections;
using UnityEngine;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.Net
{
    /// <summary>
    /// Coordinador de sync com Supabase (Fatia 7B). Singleton DontDestroyOnLoad.
    /// Mantém PDM + DailyChallengeManager em sincronia com a tabela `players`
    /// no Supabase. Lifecycle:
    ///
    /// 1. Awake — subscribe AuthManager.OnAuthenticated + PDM/Daily OnDataChanged.
    /// 2. Auth pronta → PullFromServer:
    ///    - row existe → ApplyRemoteState (server wins).
    ///    - row ausente → PushInitial (INSERT current local).
    /// 3. Mudanças locais → debounce 2s → PATCH /rest/v1/players?id=eq.<uid>.
    /// 4. Falha de rede → fica dirty, retry no próximo evento ou OnApplicationFocus.
    ///
    /// Conflict resolution: last-write-wins via updated_at do server (trigger
    /// Postgres auto-mantém). Sem timestamps client-side — confiamos no servidor.
    ///
    /// Pra desligar sync, remova `_PlayerDataSync` da HomeScene — PDM volta a ser
    /// local-only sem código mudar.
    /// </summary>
    public class PlayerDataSync : MonoBehaviour
    {
        public static PlayerDataSync Instance { get; private set; }

        public enum SyncStatus
        {
            Idle,           // sem trabalho pendente, last sync OK
            WaitingAuth,    // aguardando AuthManager.OnAuthenticated
            Pulling,
            Pushing,
            DirtyOffline,   // push falhou, fila pra retry
            Error,          // falha crítica (parse, RLS, etc) — manual intervention
        }

        [Header("Behavior")]
        [Tooltip("Segundos de debounce entre eventos locais antes de pushar. " +
            "Permite coalescer múltiplos Save() rápidos em um único PATCH.")]
        [Range(0.1f, 10f)]
        [SerializeField] private float pushDebounceSeconds = 2f;

        [Tooltip("Logs detalhados de cada pull/push.")]
        [SerializeField] private bool verboseLogs = true;

        public SyncStatus Status { get; private set; } = SyncStatus.WaitingAuth;
        public string LastError { get; private set; } = "";
        public string LastSyncedAt { get; private set; } = "";  // server-side updated_at do último pull/push OK

        bool _initialPullComplete;
        Coroutine _debouncedPushCoroutine;

        // Wrapper porque JsonUtility não desserializa top-level arrays.
        [Serializable]
        class PlayerArrayWrapper { public PlayerRemoteState[] items; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var auth = AuthManager.Instance;
            if (auth == null)
            {
                Debug.LogWarning("[Sync] AuthManager.Instance null — sync desativado. Adicione _AuthManager na HomeScene.");
                Status = SyncStatus.Error;
                LastError = "no AuthManager";
                return;
            }

            auth.OnAuthenticated += HandleAuthReady;
            auth.OnAuthFailed += HandleAuthFailed;

            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnDataChanged += HandleLocalDataChanged;

            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.OnDataChanged += HandleLocalDataChanged;

            // Se auth já estava pronta antes do nosso Start (ordem dos Awake na cena),
            // dispara o handler manualmente — eventos perdidos.
            if (auth.IsAuthenticated) HandleAuthReady();
        }

        void OnApplicationPause(bool paused) { if (paused) FlushPendingPush(); }
        void OnApplicationFocus(bool hasFocus) { if (!hasFocus) FlushPendingPush(); }

        // ============ Handlers de eventos ============

        void HandleAuthReady()
        {
            if (verboseLogs) Debug.Log("[Sync] Auth ready → initial pull.");
            StartCoroutine(PullFromServer(isInitial: true));
        }

        void HandleAuthFailed(string err)
        {
            Debug.LogWarning($"[Sync] Auth failed: {err} — sync ficará offline. App continua usando PlayerPrefs local.");
            Status = SyncStatus.Error;
            LastError = "auth: " + err;
        }

        void HandleLocalDataChanged()
        {
            if (!_initialPullComplete)
            {
                if (verboseLogs) Debug.Log("[Sync] Data changed before initial pull — ignorado.");
                return;
            }
            SchedulePush();
        }

        // ============ Push debounce ============

        void SchedulePush()
        {
            if (_debouncedPushCoroutine != null) StopCoroutine(_debouncedPushCoroutine);
            _debouncedPushCoroutine = StartCoroutine(DebouncedPushRoutine());
        }

        IEnumerator DebouncedPushRoutine()
        {
            yield return new WaitForSecondsRealtime(pushDebounceSeconds);
            _debouncedPushCoroutine = null;
            yield return PushNow();
        }

        // Cancela debounce e dispara push imediato. Usado em OnApplicationPause
        // pra não perder mudanças se app for killed antes do debounce expirar.
        void FlushPendingPush()
        {
            if (_debouncedPushCoroutine == null) return;
            StopCoroutine(_debouncedPushCoroutine);
            _debouncedPushCoroutine = null;
            StartCoroutine(PushNow());
        }

        // ============ Pull ============

        public IEnumerator PullFromServer(bool isInitial = false)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                Status = SyncStatus.WaitingAuth;
                yield break;
            }

            Status = SyncStatus.Pulling;
            string path = $"/rest/v1/players?id=eq.{auth.UserId}&select=*";
            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Get(path, r => result = r);

            if (result == null || !result.success)
            {
                Status = SyncStatus.DirtyOffline;
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[Sync] Pull failed: {LastError}");
                yield break;
            }

            // Wrap pra parse de top-level array.
            string body = string.IsNullOrEmpty(result.body) ? "[]" : result.body;
            PlayerArrayWrapper wrapper;
            try
            {
                wrapper = JsonUtility.FromJson<PlayerArrayWrapper>("{\"items\":" + body + "}");
            }
            catch (Exception ex)
            {
                Status = SyncStatus.Error;
                LastError = "pull parse: " + ex.Message;
                Debug.LogWarning($"[Sync] Pull parse error: {ex.Message}\nbody: {body}");
                yield break;
            }

            if (wrapper == null || wrapper.items == null || wrapper.items.Length == 0)
            {
                // Sem row → primeiro acesso desse user → push initial.
                if (verboseLogs) Debug.Log("[Sync] No remote row, pushing initial state.");
                _initialPullComplete = true;
                yield return PushNow();
                yield break;
            }

            var row = wrapper.items[0];
            ApplyRemoteRowToLocal(row);
            LastSyncedAt = row.updated_at ?? "";
            Status = SyncStatus.Idle;
            _initialPullComplete = true;
            if (verboseLogs) Debug.Log($"[Sync] Pull OK — applied remote. updated_at={LastSyncedAt}");
        }

        void ApplyRemoteRowToLocal(PlayerRemoteState row)
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.ApplyRemoteState(row);
            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.ApplyRemoteState(row);
        }

        // ============ Push ============

        public IEnumerator PushNow()
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                Status = SyncStatus.WaitingAuth;
                yield break;
            }

            Status = SyncStatus.Pushing;

            var state = new PlayerRemoteState { id = auth.UserId };
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.CopyToRemoteState(state);
            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.CopyToRemoteState(state);

            string json = JsonUtility.ToJson(state);
            // updated_at é mantido pelo trigger server-side — remove do payload
            // pra não conflitar (campo é "" por default no POCO, mas PostgREST
            // aceita strings vazias como NULL pra timestamp e o trigger não
            // dispararia em INSERT explicito de NULL). Stripar é mais limpo.
            json = StripUpdatedAt(json);

            // Upsert: POST com Prefer: resolution=merge-duplicates faz INSERT
            // se não existe, UPDATE se existe. Single roundtrip.
            SupabaseClient.RequestResult result = null;
            yield return auth.Client.Post(
                "/rest/v1/players?on_conflict=id",
                json,
                "resolution=merge-duplicates,return=representation",
                r => result = r);

            if (result == null || !result.success)
            {
                Status = SyncStatus.DirtyOffline;
                LastError = result != null ? result.error : "no response";
                Debug.LogWarning($"[Sync] Push failed: {LastError}");
                yield break;
            }

            // Resposta com return=representation contém a row final (com updated_at
            // novo). Atualiza LastSyncedAt.
            try
            {
                string body = result.body;
                var wrapper = JsonUtility.FromJson<PlayerArrayWrapper>("{\"items\":" + body + "}");
                if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
                    LastSyncedAt = wrapper.items[0].updated_at ?? LastSyncedAt;
            }
            catch (Exception ex)
            {
                if (verboseLogs) Debug.Log($"[Sync] Push response parse warning: {ex.Message}");
            }

            LastError = "";
            Status = SyncStatus.Idle;
            if (verboseLogs) Debug.Log($"[Sync] Push OK — updated_at={LastSyncedAt}");
        }

        // Remove o campo updated_at do JSON antes de enviar. Se mandasse "",
        // o Postgres rejeitaria a conversão pra timestamptz no INSERT. Pulando
        // o campo, o default (now()) + trigger preenchem corretamente.
        // JsonUtility serializa fields na ordem da declaração; updated_at é o
        // último campo de PlayerRemoteState, então sempre vem como
        // ,"updated_at":"<value>"} — pattern simples basta.
        static string StripUpdatedAt(string json)
        {
            int idx = json.IndexOf(",\"updated_at\":");
            if (idx < 0) return json;
            int closeBrace = json.LastIndexOf('}');
            if (closeBrace < idx) return json;
            return json.Substring(0, idx) + "}";
        }

        // ============ Debug ============

        public void DebugLogStatus()
        {
            Debug.Log($"[Sync] status={Status} lastSyncedAt={LastSyncedAt} lastError='{LastError}' debouncePending={(_debouncedPushCoroutine != null)}");
        }

        /// <summary>Pull forçado (ignora se já tem state local — server wins).</summary>
        public void DebugPullNow() { StartCoroutine(PullFromServer(isInitial: false)); }

        /// <summary>Push forçado (cancela debounce se houver).</summary>
        public void DebugPushNow()
        {
            if (_debouncedPushCoroutine != null) StopCoroutine(_debouncedPushCoroutine);
            _debouncedPushCoroutine = null;
            StartCoroutine(PushNow());
        }

        /// <summary>Wipe local (PDM + Daily) e re-pull do server. CUIDADO: destrutivo localmente. Útil pra testar sync.</summary>
        public void DebugWipeLocalAndPull()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.WipeAll();
            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.DebugWipe();
            _initialPullComplete = false;
            StartCoroutine(PullFromServer(isInitial: true));
        }
    }
}
