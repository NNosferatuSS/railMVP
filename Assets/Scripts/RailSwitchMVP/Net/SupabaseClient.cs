using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RailSwitchMVP.Net
{
    /// <summary>
    /// Wrapper minimalista pra Supabase REST API (Fatia 7A). UnityWebRequest +
    /// coroutines. Stateful — guarda access token, refresh token, expiry.
    ///
    /// Auth: caller (AuthManager) chama SignUpAnonymous / RefreshSession, recebe
    /// session via callback. SetSession atualiza estado interno. Daí em diante,
    /// Post/Get vão automaticamente com Authorization Bearer.
    ///
    /// Data (Fatia 7B): PostgREST endpoints /rest/v1/{table}. Insert é POST,
    /// query é GET, upsert é POST com Prefer: resolution=merge-duplicates.
    /// Métodos genéricos Post/Get cobrem ambos casos.
    /// </summary>
    public class SupabaseClient
    {
        public string Url { get; set; }
        public string AnonKey { get; set; }

        string _accessToken;
        string _refreshToken;
        DateTime _accessTokenExpires = DateTime.MinValue;

        public bool HasSession => !string.IsNullOrEmpty(_accessToken);

        /// <summary>True se o access token vai expirar nos próximos 60s (precisa refresh).</summary>
        public bool TokenNeedsRefresh => HasSession && DateTime.UtcNow.AddSeconds(60) >= _accessTokenExpires;

        public string AccessToken => _accessToken;
        public string RefreshToken => _refreshToken;

        public void SetSession(string accessToken, string refreshToken, int expiresInSeconds)
        {
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _accessTokenExpires = DateTime.UtcNow.AddSeconds(Math.Max(0, expiresInSeconds));
        }

        public void ClearSession()
        {
            _accessToken = null;
            _refreshToken = null;
            _accessTokenExpires = DateTime.MinValue;
        }

        // ============ Auth-specific endpoints ============

        [Serializable]
        public class AuthResponse
        {
            public string access_token;
            public string token_type;
            public int expires_in;
            public string refresh_token;
            public AuthUser user;
        }

        [Serializable]
        public class AuthUser
        {
            public string id;
            public bool is_anonymous;
        }

        /// <summary>
        /// Cria um usuário anônimo no Supabase Auth. Requer "Anonymous Sign-Ins"
        /// habilitado em Authentication → Providers no dashboard.
        ///
        /// Sucesso → result.success=true, e auth payload populado com access_token
        /// + refresh_token + user.id. Caller deve chamar SetSession() com esses
        /// valores pra ativar as requests subsequentes.
        /// </summary>
        public IEnumerator SignUpAnonymous(Action<AuthRequestResult> onComplete)
        {
            yield return SendAuthRequest("/auth/v1/signup", "{}", onComplete);
        }

        /// <summary>
        /// Troca o refresh token por um access token novo. Usado quando o atual
        /// expirou (após ~1h) ou no boot quando temos refresh salvo em PlayerPrefs.
        /// </summary>
        public IEnumerator RefreshSession(string refreshToken, Action<AuthRequestResult> onComplete)
        {
            string body = "{\"refresh_token\":\"" + EscapeJson(refreshToken) + "\"}";
            yield return SendAuthRequest("/auth/v1/token?grant_type=refresh_token", body, onComplete);
        }

        IEnumerator SendAuthRequest(string path, string jsonBody, Action<AuthRequestResult> onComplete)
        {
            string fullUrl = Url.TrimEnd('/') + path;
            using (var req = new UnityWebRequest(fullUrl, "POST"))
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", AnonKey);

                yield return req.SendWebRequest();

                var result = new AuthRequestResult
                {
                    statusCode = (int)req.responseCode,
                    body = req.downloadHandler != null ? req.downloadHandler.text : "",
                    error = req.error,
                };

                bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
                if (ok)
                {
                    try
                    {
                        result.payload = JsonUtility.FromJson<AuthResponse>(result.body);
                        result.success = result.payload != null && !string.IsNullOrEmpty(result.payload.access_token);
                        if (!result.success) result.error = "auth response had no access_token";
                    }
                    catch (Exception ex)
                    {
                        result.success = false;
                        result.error = $"json parse: {ex.Message}";
                    }
                }
                else
                {
                    result.success = false;
                    if (string.IsNullOrEmpty(result.error))
                        result.error = $"http {result.statusCode}: {result.body}";
                }

                onComplete?.Invoke(result);
            }
        }

        public class AuthRequestResult
        {
            public bool success;
            public int statusCode;
            public string body;
            public string error;
            public AuthResponse payload;
        }

        // ============ Generic REST endpoints (Fatia 7B usa) ============

        public class RequestResult
        {
            public bool success;
            public int statusCode;
            public string body;
            public string error;
        }

        /// <summary>
        /// GET genérico contra qualquer path do Supabase (ex: /rest/v1/players?id=eq.xxx).
        /// Sempre envia apikey. Envia Authorization Bearer se HasSession.
        /// </summary>
        public IEnumerator Get(string path, Action<RequestResult> onComplete)
        {
            yield return SendDataRequest("GET", path, null, null, onComplete);
        }

        /// <summary>POST genérico — corpo JSON. Pra inserts/upserts no PostgREST.</summary>
        public IEnumerator Post(string path, string jsonBody, string preferHeader, Action<RequestResult> onComplete)
        {
            yield return SendDataRequest("POST", path, jsonBody, preferHeader, onComplete);
        }

        /// <summary>PATCH genérico — atualiza row(s) parcialmente.</summary>
        public IEnumerator Patch(string path, string jsonBody, Action<RequestResult> onComplete)
        {
            yield return SendDataRequest("PATCH", path, jsonBody, null, onComplete);
        }

        IEnumerator SendDataRequest(string method, string path, string jsonBody, string preferHeader, Action<RequestResult> onComplete)
        {
            string fullUrl = Url.TrimEnd('/') + path;
            using (var req = new UnityWebRequest(fullUrl, method))
            {
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                    req.uploadHandler = new UploadHandlerRaw(bodyBytes);
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", AnonKey);
                if (HasSession)
                    req.SetRequestHeader("Authorization", "Bearer " + _accessToken);
                if (!string.IsNullOrEmpty(preferHeader))
                    req.SetRequestHeader("Prefer", preferHeader);

                yield return req.SendWebRequest();

                var result = new RequestResult
                {
                    statusCode = (int)req.responseCode,
                    body = req.downloadHandler != null ? req.downloadHandler.text : "",
                    error = req.error,
                };
                bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
                result.success = ok;
                if (!ok && string.IsNullOrEmpty(result.error))
                    result.error = $"http {result.statusCode}: {result.body}";

                onComplete?.Invoke(result);
            }
        }

        // ============ Helpers ============

        // Escape básico pra strings em JSON manual. Suficiente pros refresh tokens
        // (que são base64 URL-safe — sem chars que precisem escape além de "), mas
        // pra payloads complexos use JsonUtility.ToJson na origem.
        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
