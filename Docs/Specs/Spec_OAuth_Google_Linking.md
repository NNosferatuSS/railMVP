# Spec — OAuth Google Linking (cross-device anon)

> **Status:** spec; not yet implemented.
> **Quando:** próxima sessão.
> **Pré-requisitos:** Fatias 7A + 7B + 9 commitadas. Auth anônima funcionando.

---

## ✅ Investigação da API Supabase (2026-05-29 — viabilidade confirmada)

Resolve a **Open Question #1** (no fim deste doc). Resumo do que a investigação achou:

- **Viável:** dá pra linkar uma identidade OAuth (Google) a um user **anônimo** via
  `linkIdentity` — e, crucial pra mobile/Unity, **com ID token nativo** (sem redirect de
  browser): `supabase.auth.linkIdentity({ provider: 'google', token: idToken, access_token })`
  (a.k.a. `linkIdentityWithIdToken`). Suporte a idToken no link foi adicionado no
  **supabase-js v2.78.0**. O `user_id` é preservado (linka ao anon, não cria novo).
- **⚠️ Pré-requisito NOVO (a spec não enfatizava):** **"Enable Manual Linking"** tem que
  estar ligado no dashboard do Supabase (Authentication → settings; self-host:
  `GOTRUE_SECURITY_MANUAL_LINKING_ENABLED=true`). Sem isso, `linkIdentity` falha. Entra na
  Fase 0 (setup), junto com habilitar o provider Google.
- **⚠️ Armadilha do nonce (Google id_token):** known issue do GoTrue — ele hasheia o nonce
  (SHA-256 hex) e compara com o do id_token, mas o id_token do Google não traz nonce
  hasheado e o Google não exige nonce no fluxo de id_token. Tende a dar "nonces mismatch".
  Mitigação usual: **omitir o nonce**. Tratar com cuidado na implementação.
- **⚠️ Unlink exige ≥ 2 identidades:** `unlinkIdentity` só funciona se o user tiver pelo
  menos 2 identidades linkadas. Um user anon+Google pode ter só a identidade Google →
  **talvez não dê pra "Desvincular Google"** (ficaria sem identidade). Validar; a UI de
  Desvincular pode precisar ser condicional ou não-oferecida nesse caso.
- **Cliente Unity é REST cru** (não usa supabase-js): o `signInWithIdToken` mapeia pra
  `POST /auth/v1/token?grant_type=id_token` (com `provider`, `id_token`, `nonce`). O
  endpoint REST exato do **link com id_token** NÃO está na doc pública — **confirmar no
  source do auth-js (`GoTrueClient._linkIdentity`) v2.78+ ou no GoTrue** na hora de codar.

Fontes: [Identity Linking](https://supabase.com/docs/guides/auth/auth-identity-linking),
[Anonymous Sign-Ins](https://supabase.com/docs/guides/auth/auth-anonymous),
[Sign in with ID token](https://supabase.com/docs/reference/javascript/auth-signinwithidtoken),
[linkIdentity ref](https://supabase.com/docs/reference/javascript/auth-linkidentity),
[GoTrue nonce issue #412](https://github.com/supabase/auth/issues/412).

---

## Problema

Anon Auth atual cria um `user_id` UUID único **por device/instalação**. Consequências:

- Player reinstala app → vira user_id novo → perde tudo (coins, daily best, leaderboard rank).
- Player troca de phone → mesmo problema.
- Player joga em 2 devices → 2 users separados, 2 rows no leaderboard.

Pra MVP de F&F está OK (cada amigo testa no próprio device). Mas pra qualquer
audiência maior, perder progresso na reinstalação é um pain point sério.

## Solução proposta

Linkar o anon user atual a uma conta **Google** via Supabase Auth. Após link:

- Mesmo `user_id` (UUID) continua sendo o canônico.
- Mas agora pode Sign-In via Google em qualquer device → JWT volta pro mesmo `user_id`.
- Player data + Daily progress + Leaderboard entry: tudo "segue" o user.

Escopo desta spec: **Google apenas**, Android-first. iOS deferred mas o flow é o
mesmo (Supabase suporta Apple Sign-In nativamente — basta habilitar quando for hora).

## Arquitetura Supabase + Google

```
┌──────────────────────────────────────────────────────────┐
│ Google Cloud Console                                      │
│  ├ Project (criar 1 pro RailMVP)                          │
│  ├ OAuth Consent Screen (configurar app name + scopes)    │
│  └ OAuth 2.0 Client ID (tipo: Android)                    │
│      ├ Package name: com.nosfera3d.railmvp                │
│      ├ SHA-1 cert fingerprint (do keystore Android)       │
│      └ Web client ID (precisa do tipo Web também,         │
│         pro Supabase backend trocar token por session)    │
└──────────────────────────────────────────────────────────┘
                            ↓
┌──────────────────────────────────────────────────────────┐
│ Supabase Dashboard                                        │
│  └ Authentication → Providers → Google → ENABLE          │
│      ├ Client ID: <do Google Cloud, Web client>           │
│      ├ Client Secret: <do Google Cloud, Web client>       │
│      └ Authorized redirect URLs: já default               │
└──────────────────────────────────────────────────────────┘
                            ↓
┌──────────────────────────────────────────────────────────┐
│ Unity client                                              │
│  ├ Native Google Sign-In (Android SDK + plugin)           │
│  ├ Recebe ID token do Google                              │
│  ├ Envia pro Supabase via signInWithIdToken               │
│  └ Se já tinha anon session, faz linkIdentity em vez de  │
│     signIn novo                                           │
└──────────────────────────────────────────────────────────┘
```

## Setup Google Cloud (one-time, ~30min)

1. `console.cloud.google.com` → criar project `RailMVP`.
2. APIs & Services → OAuth consent screen:
   - User Type: External
   - App name: `Rail Switch`, support email, developer email.
   - Scopes: básicos (email, profile, openid).
3. APIs & Services → Credentials:
   - **Web client** (precisa pra Supabase): Create OAuth Client ID → Type Web → name `RailMVP Supabase Web`. Authorized redirect URIs: `https://<project>.supabase.co/auth/v1/callback`. **Anotar Client ID + Secret.**
   - **Android client**: Create OAuth Client ID → Type Android → package `com.nosfera3d.railmvp` → SHA-1 do keystore release (precisa da Fatia Pre-launch ou debug keystore pra dev).
4. Anotar tudo num password manager.

## Setup Supabase (~5min)

1. Dashboard → Authentication → Providers → **Google** → Enable.
2. Paste Client ID + Secret (do Web client).
3. Save.

## Setup Unity — Google Sign-In SDK

Duas opções:

### Opção A: Google Sign-In Unity Plugin (Google oficial, deprecated mas funciona)
- Repo: `github.com/googlesamples/google-signin-unity` (deprecated mas estável).
- Adicionar via OpenUPM ou .unitypackage download.
- API: `GoogleSignIn.DefaultInstance.SignIn()` retorna `GoogleSignInUser` com `IdToken`.

### Opção B: Native Android SDK direct (Credential Manager API)
- Google Identity Service via Java native + Unity AndroidJavaObject calls.
- Mais código boilerplate mas sem dependência de plugin morto.
- Atual recomendação Google.

**Recomendação:** Opção A pra MVP — Opção B se precisar de polimento futuro.

## Código — AuthManager extensão

```cs
public class AuthManager : MonoBehaviour {
    // ... existente ...

    // Fatia 11 — novos métodos
    public bool IsAnonymous => /* check JWT claims is_anonymous */;
    public string LinkedEmail => /* da JWT, vazio se anon */;

    /// <summary>
    /// Inicia flow de Google Sign-In + link. Se já tinha anon session,
    /// chama linkIdentity (preserva user_id). Senão, signInWithIdToken cria
    /// um user novo (Google-only, não-anon).
    /// </summary>
    public void LinkWithGoogle(Action<bool, string> onComplete) {
        StartCoroutine(LinkWithGoogleRoutine(onComplete));
    }

    IEnumerator LinkWithGoogleRoutine(Action<bool, string> onComplete) {
        // 1. Trigger native Google Sign-In, get ID token
        var task = GoogleSignIn.DefaultInstance.SignIn();
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted || task.IsCanceled) {
            onComplete?.Invoke(false, "Google sign-in cancelado/falhou");
            yield break;
        }
        string idToken = task.Result.IdToken;

        // 2. Trocar com Supabase
        // Se IsAnonymous → linkIdentity (preserva user_id)
        // Senão → signInWithIdToken (login normal)
        string endpoint = IsAnonymous
            ? "/auth/v1/user/identities/link"   // hipotetico — verificar API real
            : "/auth/v1/token?grant_type=id_token";

        // ... POST com id_token + provider=google
        // ... salva JWT + refresh_token novos
    }

    public void UnlinkGoogle(Action<bool, string> onComplete) {
        // POST /auth/v1/user/identities/<identity_id>?provider=google
    }
}
```

> **Nota:** API exata do Supabase `linkIdentity` pode ter mudado entre versões — confirmar na docs ao implementar. Pseudocódigo acima é orientação geral.

## UI — Profile Panel extensão

Aproveitar o ProfilePanel já existente (Fatia 9). Adicionar nova seção:

```
┌─────────────────────────────────┐
│ Editar Perfil                   │
├─────────────────────────────────┤
│ Nome: [_____________]           │
│                                  │
│ ──── Conta ────                  │
│ Status: Anonimo (este device)   │  ← se anon
│ [ Vincular com Google ]          │
│                                  │
│ —OU—                             │
│                                  │
│ Status: artur@gmail.com          │  ← se linked
│ [ Desvincular Google ]           │
│                                  │
│ [ SALVAR ]                       │
└─────────────────────────────────┘
```

Estados:
- **Anon (default):** botão "Vincular com Google", explicação "Vincule pra sincronizar entre dispositivos."
- **Linked:** mostra email, botão "Desvincular" (com confirmação — destrutivo).
- **Loading durante link:** spinner + texto "Conectando com Google...".

## Edge cases a tratar

### Já existe Google user com mesmo email
- Cenário: player linkou no device A, depois reinstala no device B e tenta linkar mesmo Google account.
- Comportamento: Supabase reconhece email, retorna o **mesmo user_id** original (NÃO cria novo). Conflito: device B tem agora anon user separado que vai ser abandonado.
- Solução: durante link, se Supabase retornar user_id ≠ atual, **substituir o user_id local** pelo do Google. Anon antigo fica órfão no servidor (cleanup futuro).
- UI feedback: "Conta encontrada! Carregando seus dados anteriores..."

### Player linka, depois desvincula
- Comportamento: user_id permanece o mesmo, JWT vira anon de novo. Próxima reinstalação perde tudo (volta ao status quo pré-link).
- UI: alerta "Tem certeza? Você não poderá acessar essa conta de outro dispositivo."

### Player linka 2 Google accounts no mesmo user
- Supabase suporta múltiplas identidades por user. Não vamos UI isso pra MVP — primeiro link só.

### Linka offline
- Não funciona — operação requer network. UI mostra "Sem conexão. Tente novamente."

### Cancelou no diálogo Google
- Retorna sem erro fatal. UI volta ao estado anterior, sem mensagem agressiva.

## Migration: usuários anon existentes

Pre-launch tem só F&F users (5-10). Nenhum precisa "ser migrado" — o link é
opcional. User anon continua funcionando, só não tem cross-device.

Quando linkar pela 1ª vez, o user_id permanece e dados ficam intactos.
Próximas Sign-Ins via Google em qualquer device acessam o mesmo user.

## Esforço estimado

| Etapa | Tempo |
|---|---|
| Google Cloud + Supabase setup | 30-45min |
| Google Sign-In plugin integration | 30min |
| AuthManager.LinkWithGoogle implementation | 2-3h |
| ProfilePanel UI extension + states | 1h |
| Testing: link, unlink, reinstall, cross-device | 1-2h |
| Setup doc com screenshots Cloud Console | 30min |
| **Total** | **~5-7h (~1-2 sessões)** |

Maior risco: Google Sign-In plugin tem bugs conhecidos com Android 14, AAB
formats novos, etc. Worst case: passa pra Opção B (Credential Manager nativo)
que é mais código mas zero dependência de plugin third-party.

## Out of scope (defer)

- **iOS Apple Sign-In:** mesmo padrão, mas requer Apple Developer account + iOS keystore. Quando for hora.
- **Migrar histórico de leaderboard:** rows com nome antigo "Player" não retroactivam quando user linka e muda nome. Fix futuro: function `update_my_results_player_name`.
- **Conflict resolution avançado:** se device A e device B linkam no mesmo Google account em horários diferentes, mantemos last-write-wins. Não vamos UI escolha de "qual versão manter".

## Open questions

1. **Supabase API atual** suporta `linkIdentity` em anon → permanent? Confirmar nas docs antes de codar. Se não, alternativa é signOut → signInWithIdToken (perde anon data) ou hack server-side via Edge Function.
2. **Display name pré-link**: usuário Google tem `name` no perfil. Devemos auto-popular `PlayerName` com isso na 1ª link? Ou deixar manter o nome custom dele?
3. **Desativar anon depois?** Algumas apps desabilitam anon após primeiro link forçado. RailMVP fica permissivo: anon sempre disponível.
