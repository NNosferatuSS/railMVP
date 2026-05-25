# Spec — Realtime Leaderboard

> **Status:** spec; not yet implemented.
> **Quando:** após OAuth Google linking estar pronta.
> **Pré-requisitos:** Fatia 8 (Leaderboard) commitada e funcionando.

---

## Problema

Leaderboard panel atual mostra snapshot estático do daily de hoje:

- Open() chama FetchToday + FetchMyRank.
- Cache 5min no `LeaderboardManager`.
- Open subsequente: força refresh (fix recente), mas não detecta novos submits *enquanto* o painel está aberto.

Se o jogo virar competitivo com player ativo e amigos online ao mesmo tempo,
o painel aberto seria muito mais legal se atualizasse **ao vivo** quando
alguém quebrasse um recorde.

## Solução proposta

Supabase tem **Realtime** native — WebSocket-based change feed em qualquer
tabela. Cliente subscreve, recebe push de INSERT/UPDATE/DELETE.

```
Player A submete novo result
       ↓
Supabase daily_results UPDATE → Realtime broadcast
       ↓
Player B (panel aberto) recebe push em ~50-200ms
       ↓
LeaderboardPanelController re-fetch (ou applies diff)
       ↓
UI atualiza com novo ranking
```

## Tecnologia: Supabase Realtime

### Como funciona o protocol

Supabase Realtime expõe um endpoint WebSocket em
`wss://<project>.supabase.co/realtime/v1/websocket?apikey=<anon-key>&vsn=1.0.0`.

Mensagens são JSON com formato Phoenix Channel:
```json
{
  "topic": "realtime:public:daily_results",
  "event": "phx_join",
  "payload": { "config": { "broadcast": { "self": false }, "presence": { "key": "" }, "postgres_changes": [{ "event": "*", "schema": "public", "table": "daily_results" }] } },
  "ref": "1"
}
```

Após join, recebe eventos:
```json
{
  "topic": "realtime:public:daily_results",
  "event": "postgres_changes",
  "payload": {
    "data": {
      "type": "UPDATE",
      "table": "daily_results",
      "record": { "player_id": "...", "distance": 850, "player_name": "Artur" },
      "old_record": { "distance": 600 }
    }
  }
}
```

Heartbeat: cliente envia `{event: "heartbeat", topic: "phoenix", payload: {}, ref: N}` a cada ~30s.

## Implementação em Unity

### Opção A — `supabase-csharp` (oficial)

- Repo: `github.com/supabase-community/supabase-csharp`.
- Pacote .NET completo com cliente Realtime built-in.
- Tem deps pesadas: Newtonsoft.Json, websocket-sharp, Microsoft.IO.RecyclableMemoryStream. **~5MB** addtl assemblies.
- Compatibilidade com Unity: pode dar dor de cabeça com IL2CPP stripping. Suporte oficial mas não rock-solid.

### Opção B — Hand-rolled WebSocket via `NativeWebSocket`

- Repo: `github.com/endel/NativeWebSocket` (Unity-specific, ~200KB).
- Dependência leve, IL2CPP-friendly.
- Implementação manual do Phoenix protocol Supabase: ~200-300 linhas.
- Mais controle, menos abstração misteriosa.

**Recomendação:** **Opção B**. Maior controle, deps mínimas, alinhado com nossa
decisão pré-existente de hand-roll Supabase REST (Fatia 7A) em vez de usar o
SDK completo.

## Arquitetura cliente

```
RealtimeManager (singleton DontDestroyOnLoad, novo)
  ├ WebSocket _ws (NativeWebSocket)
  ├ State: Disconnected, Connecting, Connected, Reconnecting
  ├ Connect() — abre WS com apikey, faz heartbeat
  ├ Subscribe(topic, callback) — joins channel, registra handler
  ├ Unsubscribe(topic)
  ├ Disconnect()
  └ events OnConnected, OnDisconnected, OnError

LeaderboardManager (modificado)
  ├ existente: Submit, FetchToday, FetchMyRank, cache
  ├ novo: SubscribeToTodayUpdates() — chama RealtimeManager
  ├ novo: OnRemoteUpdate(payload) — invalida cache, dispara event
  └ event OnLiveUpdate — pra UI subscribe

LeaderboardPanelController (modificado)
  ├ Open(): atualmente força refresh; agora ALSO subscribe ao live update
  ├ OnLiveUpdate handler: debounce 1s + re-fetch
  └ Close(): unsubscribe pra economizar bandwidth
```

## Throttling

Update por update seria spammy. Player picks que rebatem várias rows numa
sessão. Estratégia:

- Coalesce: marca "dirty" no recebimento, agenda re-fetch via coroutine
  com `WaitForSeconds(1f)`. Se outro evento chega antes do timeout, reset
  timer (debounce).
- Lifecycle: subscribe SÓ quando panel aberto. Unsubscribe ao fechar.
  Economiza bandwidth + battery.
- Reconnect: WebSocket pode cair (network blip). Auto-reconnect com
  exponential backoff (1s, 2s, 4s, 8s, max 30s).

## Subscription topic

```
realtime:public:daily_results
```

Com filter via `postgres_changes` config:
```json
{
  "event": "*",
  "schema": "public",
  "table": "daily_results"
}
```

Realtime entrega TODOS os updates da tabela. Sem filter por
`challenge_date` (não é triviall server-side). Cliente filtra: ignora updates
de challenge_date != hoje.

Cuidado: se 1000 players estão jogando, todos eles vão receber broadcast de
todos os updates. Pra MVP escala não é problema. Pra escala maior: filtrar
server-side via Postgres function publishing only relevant rows.

## RLS impact

Realtime respeita RLS. Como nossa policy SELECT é `using (true)`, todos
authenticated users recebem broadcasts de todos os updates. Bom pra
leaderboard global, mas se um dia adicionar friends-only leaderboards,
precisará policy de RLS mais inteligente.

## Setup Supabase

1. Dashboard → Database → **Replication**.
2. Verificar que tabela `daily_results` está na lista. Se não, **Add table to publication**.
3. Pronto. Realtime já está habilitado por padrão em projects Supabase Pro+ (Free também desde 2024).

## Pegadinhas

### 1. WebSocket em Android (build release)
- IL2CPP pode strippar bibliotecas WebSocket subjacentes. Garantir `link.xml` preserve.
- HTTPS/WSS sempre — Supabase rejeita WS plaintext.

### 2. Battery drain mobile
- Manter WebSocket aberto consome bateria. Mitigation: subscribe SÓ quando panel aberto. Considera também `Application.focusChanged` → disconnect on background.

### 3. Race conditions
- Cliente envia submit, recebe broadcast do próprio update segundos depois → re-fetch redundante. Workaround: ignorar broadcast se `record.player_id == own user_id` OR aceitar como overhead aceitável.

### 4. Heartbeat e timeout
- Padrão Supabase: heartbeat a cada 30s. Cliente que não envia heartbeat por 60s é desconectado server-side.
- Implementar via coroutine simples no RealtimeManager.

### 5. Reconnect window
- Se app vai pra background por >60s, WS é killed. Ao voltar foreground, reconnect + re-subscribe + re-fetch (catchup de updates perdidos).

## Esforço estimado

| Etapa | Tempo |
|---|---|
| NativeWebSocket integration + smoke test | 30min |
| RealtimeManager singleton (connect/heartbeat/subscribe) | 2-3h |
| LeaderboardManager.SubscribeToTodayUpdates + event | 30min |
| LeaderboardPanelController hook | 30min |
| Throttling / debounce / lifecycle | 1h |
| Reconnect logic + edge cases (background, network blip) | 1-2h |
| Testing: 2 devices simultâneos | 1h |
| **Total** | **~6-8h (~1-2 sessões)** |

Maior risco: WebSocket frame parsing manual quando Supabase muda formato
(raro mas acontece — fizeram mudanças em 2023 e 2024).

## Out of scope

- **Presence (quem está online):** Supabase Realtime suporta mas não precisamos pra leaderboard.
- **Broadcast custom messages:** chat in-game, taunts, etc. Future.
- **Server-side filter por challenge_date:** otimização escalar. Pra <1000 active users, cliente-side filter chega.

## Open questions

1. **`NativeWebSocket` ainda mantido?** Confirmar última release antes de comprar. Alternative: SocketIO Unity, mas mais pesado.
2. **Subscribe enquanto Game scene ativa** (não só Home/Leaderboard panel)? Se sim, oportunidade de mostrar toast in-game "Player X passou seu recorde!" — futuro feature, não pra MVP.
3. **Limite de subscriptions concorrentes:** Supabase Free tier permite 200 concurrent connections. Free F&F está safe; growth precisará Pro.
