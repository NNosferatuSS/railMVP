using System;
using System.Collections;
using UnityEngine;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.Meta
{
    /// <summary>
    /// Auth Supabase (Fatia 7A). Singleton DontDestroyOnLoad. Anonymous sign-in
    /// no boot: refresh com refresh_token salvo se existir, senão signup novo.
    /// Persiste user_id + refresh_token em PlayerPrefs RailMVP.Auth.*.
    ///
    /// Fatia 7B (PDM sync) expõe o SupabaseClient via .Client pra reutilizar
    /// a sessão (access_token + refresh handling).
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        [Header("Supabase project (Project Settings → API no dashboard)")]
        [Tooltip("URL do project, ex: https://xxx.supabase.co")]
        [SerializeField] private string supabaseUrl = "";

        [Tooltip("Anon key (public, safe pra cliente). NÃO usar service_role aqui.")]
        [SerializeField] private string supabaseAnonKey = "";

        [Header("Behavior")]
        [Tooltip("Se true, tenta autenticar no Awake. Desligar pra debug/testes offline.")]
        [SerializeField] private bool autoSignInOnAwake = true;

        [Tooltip("Logs detalhados de cada request/response (sem expor tokens).")]
        [SerializeField] private bool verboseLogs = true;

        const string KUserId       = "RailMVP.Auth.UserId";
        const string KRefreshToken = "RailMVP.Auth.RefreshToken";

        SupabaseClient _client;

        /// <summary>UUID do usuário anônimo. Vazio se nunca autenticou.</summary>
        public string UserId { get; private set; } = "";

        public bool IsAuthenticated => !string.IsNullOrEmpty(UserId) && _client != null && _client.HasSession;

        /// <summary>Última mensagem de erro (sign-in / refresh). Vazia se sucesso.</summary>
        public string LastError { get; private set; } = "";

        /// <summary>Disparado quando auth fica pronta (signup novo ou refresh ok).</summary>
        public event Action OnAuthenticated;

        /// <summary>Disparado se signup/refresh falhou. App deve continuar offline.</summary>
        public event Action<string> OnAuthFailed;

        /// <summary>Acesso ao SupabaseClient pra outros sistemas (PDM sync na 7B).</summary>
        public SupabaseClient Client => _client;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _client = new SupabaseClient
            {
                Url = supabaseUrl,
                AnonKey = supabaseAnonKey,
            };

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseAnonKey))
            {
                Debug.LogWarning("[Auth] Supabase URL ou Anon Key vazios — auth não vai rodar. Preencha no Inspector do _AuthManager.");
                return;
            }

            if (autoSignInOnAwake)
                StartCoroutine(InitializeAuth());
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        IEnumerator InitializeAuth()
        {
            string savedRefresh = PlayerPrefs.GetString(KRefreshToken, "");
            string savedUserId = PlayerPrefs.GetString(KUserId, "");

            if (!string.IsNullOrEmpty(savedRefresh) && !string.IsNullOrEmpty(savedUserId))
            {
                if (verboseLogs) Debug.Log($"[Auth] Found saved session for user {Trunc(savedUserId)}, refreshing...");
                yield return RefreshExisting(savedRefresh, savedUserId);

                // Se o refresh falhou (refresh token revogado / expired), fallback pra novo anon signup.
                // Pode acontecer se o user foi deletado no dashboard ou o token expirou (>1 semana sem uso).
                if (!IsAuthenticated)
                {
                    if (verboseLogs) Debug.LogWarning($"[Auth] Refresh failed ({LastError}), falling back to new anonymous signup.");
                    ClearStoredSession();
                    yield return SignUpNewAnon();
                }
            }
            else
            {
                if (verboseLogs) Debug.Log("[Auth] No saved session, creating new anonymous user.");
                yield return SignUpNewAnon();
            }
        }

        IEnumerator SignUpNewAnon()
        {
            yield return _client.SignUpAnonymous(result =>
            {
                if (result.success)
                {
                    var p = result.payload;
                    _client.SetSession(p.access_token, p.refresh_token, p.expires_in);
                    UserId = p.user != null ? p.user.id : "";
                    LastError = "";
                    SaveSession();
                    Debug.Log($"[Auth] Signed up anon. UserId={Trunc(UserId)} expires_in={p.expires_in}s");
                    OnAuthenticated?.Invoke();
                }
                else
                {
                    LastError = result.error;
                    Debug.LogWarning($"[Auth] Anon signup failed: {result.error}");
                    OnAuthFailed?.Invoke(result.error);
                }
            });
        }

        IEnumerator RefreshExisting(string refreshToken, string expectedUserId)
        {
            yield return _client.RefreshSession(refreshToken, result =>
            {
                if (result.success)
                {
                    var p = result.payload;
                    _client.SetSession(p.access_token, p.refresh_token, p.expires_in);
                    UserId = p.user != null ? p.user.id : expectedUserId;
                    LastError = "";
                    SaveSession();
                    Debug.Log($"[Auth] Refreshed session. UserId={Trunc(UserId)} expires_in={p.expires_in}s");
                    OnAuthenticated?.Invoke();
                }
                else
                {
                    LastError = result.error;
                    if (verboseLogs) Debug.LogWarning($"[Auth] Refresh failed: {result.error}");
                    // Não invoca OnAuthFailed aqui — caller (InitializeAuth) faz fallback pra signup novo.
                }
            });
        }

        void SaveSession()
        {
            PlayerPrefs.SetString(KUserId, UserId);
            PlayerPrefs.SetString(KRefreshToken, _client.RefreshToken ?? "");
            PlayerPrefs.Save();
        }

        void ClearStoredSession()
        {
            UserId = "";
            _client.ClearSession();
            PlayerPrefs.DeleteKey(KUserId);
            PlayerPrefs.DeleteKey(KRefreshToken);
            PlayerPrefs.Save();
        }

        // ============ Debug ============

        public void DebugLogStatus()
        {
            Debug.Log($"[Auth] status — auth'd={IsAuthenticated} userId={Trunc(UserId)} hasSession={_client?.HasSession} needsRefresh={_client?.TokenNeedsRefresh} lastError='{LastError}'");
        }

        /// <summary>Apaga sessão local e tenta signup novo. Útil pra testar o ciclo de boot.</summary>
        public void DebugForceReauth()
        {
            ClearStoredSession();
            StartCoroutine(SignUpNewAnon());
        }

        /// <summary>Apaga sessão local sem re-autenticar. Próximo boot fará signup novo.</summary>
        public void DebugSignOut()
        {
            ClearStoredSession();
            Debug.Log("[Auth] DEBUG: signed out (no re-auth). Next boot will create new anon.");
        }

        // ============ Helpers ============

        static string Trunc(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(empty)";
            return id.Length > 8 ? id.Substring(0, 8) + "…" : id;
        }
    }
}
