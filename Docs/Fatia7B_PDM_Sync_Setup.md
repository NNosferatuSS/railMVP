# Fatia 7B — PDM Sync Setup

> **Pré-requisito:** Fatia 7A funcional. Você precisa de `_AuthManager` na
> HomeScene + tabela `players` no Supabase (schema com colunas Daily +
> trigger `updated_at` já criado em 7A) + RLS policies ativas.
>
> **Códigos prontos:** `PlayerDataSync.cs` (coordenador), `PlayerRemoteState.cs`
> (POCO), `PlayerDataManager` + `DailyChallengeManager` ganharam
> `ApplyRemoteState` + `OnDataChanged`. `DebugPanelController` tem seção Sync.
> Falta só adicionar 1 GameObject na cena.

**Tempo estimado:** 5min de setup + 15min de validação end-to-end.

---

## Bloco 1 — `_PlayerDataSync` na HomeScene

1. Abrir `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → área vazia → **Create Empty** → renomear `_PlayerDataSync`.
3. Inspector → **Add Component** → digitar `PlayerDataSync` → selecionar
   (`RailSwitchMVP.Net.PlayerDataSync`).
4. Campos no Inspector (defaults estão bons):
   - **Push Debounce Seconds**: `2` — coalesce de mudanças rápidas em um único PATCH.
   - **Verbose Logs**: ✅ marcado (desligar em release).
5. **Ctrl+S**.

**Como saber se deu certo:**
- Hierarchy mostra `_PlayerDataSync` ao lado de `_PlayerDataManager`,
  `_DailyChallengeManager`, `_AuthManager`.
- O componente PlayerDataSync existe (sem erros vermelhos).

**Ordem dos managers na Hierarchy não importa** — os singletons resolvem
referências via `.Instance` no Start (não Awake), e o Sync chama
`HandleAuthReady()` manualmente se a auth já estava pronta antes.

---

## Bloco 2 — Testar no Editor (sequência crítica)

### 2.1 — Primeira execução (pull → no row → push initial)

1. **Play** na HomeScene.
2. Console esperado (ordem aproximada):
   ```
   [PDM] Loaded: coins=... bestDist=... ...
   [Daily] Loaded — todayDate='...' todayBest=...
   [Auth] Signed up anon. UserId=12345678…
   [Sync] Auth ready → initial pull.
   [Sync] No remote row, pushing initial state.
   [Sync] Push OK — updated_at=2026-05-25T14:32:18.123456+00
   ```
3. **Abrir dashboard Supabase** → Table Editor → `players`.
4. Refresh. **1 row** deve aparecer com:
   - `id`: o UUID do user logado
   - `coins`, `best_distance`, etc: valores atuais do seu PDM local
   - `updated_at`: timestamp recente

### 2.2 — Mudança local → push debounced

5. No Editor, abra DebugPanel (F1).
6. Seção "Player data" → clica **+1000 coins**.
7. Console:
   ```
   [PDM] (... coins novo ...)
   ```
8. Aguardar 2s (debounce).
9. Console:
   ```
   [Sync] Push OK — updated_at=<novo timestamp>
   ```
10. Dashboard → refresh `players` row → `coins` atualizado.

### 2.3 — Pull from server (verificar que server wins)

11. **Dashboard** → Table Editor → `players` → edita o `coins` da sua row
    diretamente pra um valor diferente (ex: 9999). Save.
12. No Editor (com Play ainda rodando), abre DebugPanel → seção "Sync (Fatia 7B)".
13. Clica **Pull now**.
14. Console:
    ```
    [PDM] ApplyRemoteState — coins=9999 ...
    [Sync] Pull OK — applied remote.
    ```
15. Volta pra Home (se não estava). Texto "Coins: 9999" mostra o novo valor.

### 2.4 — Stop + Play (verificar persistência do refresh token + pull subsequente)

16. **Stop** o Play.
17. **Play** de novo.
18. Console:
    ```
    [Auth] Found saved session for user 12345678…, refreshing...
    [Auth] Refreshed session.
    [Sync] Auth ready → initial pull.
    [Sync] Pull OK — applied remote.
    ```
19. Estado local mostra os valores do dashboard.

### 2.5 — Wipe local + re-pull (simulação de reinstalação parcial)

20. DebugPanel → Sync → **Wipe+Pull**.
21. Console:
    ```
    [PDM] All player data wiped.
    [Daily] DEBUG: all daily data wiped.
    [Sync] Pull OK — applied remote.
    [PDM] ApplyRemoteState — coins=9999 ...
    ```
22. Valores do server foram restaurados.

> **Importante:** Wipe+Pull NÃO faz signout. O user_id continua o mesmo
> (refresh token ainda persistido). Se você quiser simular reinstalação
> completa, primeiro faz Wipe+Pull, depois Auth → Sign out, depois Stop+Play.
> Aí vira um user novo no dashboard.

---

## Bloco 3 — Testar Android build

Mesmas validações de 2.1-2.4 no device, mais cross-device:

### 3.1 — Build & Run

1. **File → Build Settings → Build And Run.**
2. Capturar logs:
   ```powershell
   $adb = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
   & $adb logcat -c
   & $adb logcat | Select-String "Auth|Sync|PDM|Daily"
   ```
3. Esperado mesmas linhas `[Sync]` + `[Auth]` que no Editor.
4. **Dashboard** mostrará 2 rows agora (uma do Editor, uma do device — UUIDs diferentes).

### 3.2 — Cross-device last-write-wins

5. No **Editor**: aumenta coins via debug → Push now.
6. Dashboard reflete.
7. Mas isto NÃO afeta o device — eles têm `user_id` diferentes (cada anon
   é único). Pra testar cross-device de verdade, você precisa **mesmo user_id
   em dois places** — não é trivial com anon.

> **Cross-device com anon auth** funciona só se você transferir o refresh
> token de um device pro outro manualmente. Fatia futura: linkar email/OAuth
> ao anon user → daí o user_id é o mesmo entre dispositivos. Pra MVP, anon
> é device-locked por design.

---

## Critérios de validação

- [ ] `_PlayerDataSync` na HomeScene, sem erros vermelhos no console.
- [ ] **Editor 1ª run:** dashboard ganha row com user_id correto.
- [ ] **Editor +1000 coins:** após ~2s, dashboard reflete o novo valor.
- [ ] **Editor edita coins no dashboard + Pull now:** estado local atualiza.
- [ ] **Editor Stop+Play:** refresh token funciona, pull traz estado salvo.
- [ ] **Wipe+Pull:** local zera, depois server restaura.
- [ ] **Android build:** mesma sequência funciona, row do device aparece.

---

## Edge cases conhecidos

### 1. Auth não pronta quando user clica algo
PDM dispara `OnDataChanged` → Sync ignora (não pull completo).
Próxima oportunidade: quando pull completa (initial), o Sync detecta
diff implícito (server wins na 1ª vez). Mudanças locais feitas durante
essa janela de ~2-3s podem ser sobrescritas. **Aceitável pra MVP** —
janela é pequena, user geralmente não interage com a UI antes da auth.

### 2. Network down durante push
`Status` vai pra `DirtyOffline`, `LastError` mostra a falha. Próximo
`OnDataChanged` (qualquer Save) ou `OnApplicationFocus` re-trigeram push.
Sem backoff exponencial pra MVP — confiamos que a próxima ação do user
vem em segundos.

### 3. Refresh token revogado (user deletado no dashboard)
`AuthManager.InitializeAuth` faz fallback automático pra signup novo. Mas
**o user_id vai mudar** — daí o pull novo encontra zero rows, e push
initial cria uma row nova com o new id. **Dados do user anterior são
órfãos no dashboard** (não são deletados automaticamente).

Pra limpar dados orfãos no futuro: query SQL ad-hoc, ou cron de cleanup.

### 4. Dois devices, mesmo user_id (improvável com anon, mas possível)
Last-write-wins. Você verá saltos de coins se ambos editarem
simultaneamente. Para MVP isso é OK — anon = device-locked na prática.

---

## Próximas fatias

- **Fatia 8** — Leaderboard online. Cria tabela `daily_results`, push do
  resultado de cada Daily Challenge, GET top global + posição do user,
  UI substituindo o stub `leaderboardButton`.
- **Futuro** — Linkar anon user a email/OAuth pra cross-device + recuperação.

---

## Notas técnicas

- **Endpoint upsert:** `POST /rest/v1/players?on_conflict=id` com header
  `Prefer: resolution=merge-duplicates,return=representation`. PostgREST
  faz INSERT se não existe, UPDATE se existe, em 1 roundtrip. RLS policy
  de INSERT exige `auth.uid() = id`, então o JSON enviado precisa incluir
  `id` igual ao UUID do JWT.
- **`updated_at` é strippado do JSON outgoing** porque mandar string vazia
  quebra a conversão pra `timestamptz` no Postgres. O trigger `touch_updated_at`
  da Fatia 7A garante que o campo é atualizado em todo UPDATE; default `now()`
  cobre INSERT.
- **`Accept: application/json` (default)** retorna array. Wrapper
  `{"items":[...]}` em volta + JsonUtility resolve o limite de Unity de
  não desserializar top-level arrays.
- **Sync NÃO usa o `OnDataChanged` event durante `ApplyRemoteState`** —
  flag `_suppressDataChanged` em PDM e Daily evita loop infinito.
