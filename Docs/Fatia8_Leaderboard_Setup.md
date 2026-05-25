# Fatia 8 — Leaderboard Online (Daily Challenge global)

> **Pré-requisitos:** Fatia 7A (Supabase setup + anon auth) + Fatia 7B
> (PDM sync) commitadas e validadas. Você precisa de `_AuthManager` na
> HomeScene e tabela `players` no Supabase.
>
> **Códigos prontos:** `LeaderboardEntry`, `LeaderboardManager`,
> `LeaderboardEntryUI` (component pro prefab da row), `LeaderboardPanelController`
> (panel orchestrator), `DebugPanelController` com seção Leaderboard,
> `GameOverController` submete após daily run, `HomeScreenController`
> wire do botão. Falta: SQL no dashboard + 2 GameObjects + prefab da row + panel UI.

**Tempo estimado:** 30-45min (SQL ~5min, UI da Home + prefab ~25min, validação ~10min).

---

## Mapa do que vamos fazer

1. **SQL no dashboard:** tabela `daily_results` + RLS + 2 funções (submit + my rank).
2. **`_LeaderboardManager` GameObject** na HomeScene.
3. **Prefab `LeaderboardEntry`** (1 row do leaderboard, ~3 textos).
4. **Panel `LeaderboardPanel`** na HomeScene (modal sobreposto, ScrollView + close).
5. **Wire** o botão Leaderboard da HomeScreenController.
6. **Testar:** Editor + Android build.

---

## Passo 1 — SQL no dashboard

1. Dashboard Supabase → **SQL Editor** → **New query**.
2. Cole **inteiro**:

```sql
-- ============================================================
-- Tabela daily_results — uma row por (user, dia). Server-side
-- garantia de "só melhor sobe" via função submit_daily_result.
-- ============================================================
create table if not exists public.daily_results (
  player_id uuid not null references auth.users(id) on delete cascade,
  challenge_date text not null,  -- yyyy-MM-dd UTC
  distance int not null,
  coins int not null default 0,
  tier int not null default 0,
  time_seconds real not null default 0,
  player_name text not null default 'Player',  -- denormalizado pra evitar JOIN
  created_at timestamptz not null default now(),

  primary key (player_id, challenge_date)
);

-- Index pra ordenar por distance dentro do mesmo dia.
create index if not exists daily_results_date_distance_idx
  on public.daily_results (challenge_date, distance desc);

-- ============================================================
-- RLS — leitura aberta (leaderboard global), write só pelo dono.
-- ============================================================
alter table public.daily_results enable row level security;

drop policy if exists "anyone reads results" on public.daily_results;
create policy "anyone reads results" on public.daily_results
  for select using (true);

drop policy if exists "users insert own results" on public.daily_results;
create policy "users insert own results" on public.daily_results
  for insert with check (auth.uid() = player_id);

drop policy if exists "users update own results" on public.daily_results;
create policy "users update own results" on public.daily_results
  for update using (auth.uid() = player_id) with check (auth.uid() = player_id);

-- ============================================================
-- Função submit_daily_result — upsert "só se melhor".
-- Chamada via POST /rest/v1/rpc/submit_daily_result com body
-- { p_distance, p_coins, p_tier, p_time_seconds, p_player_name }.
-- ============================================================
create or replace function public.submit_daily_result(
  p_distance int,
  p_coins int,
  p_tier int,
  p_time_seconds real,
  p_player_name text
) returns boolean
language plpgsql
security definer
set search_path = public
as $$
declare
  today_str text := to_char(now() at time zone 'UTC', 'YYYY-MM-DD');
  uid uuid := auth.uid();
begin
  if uid is null then
    return false;
  end if;

  insert into public.daily_results (
    player_id, challenge_date, distance, coins, tier, time_seconds, player_name
  ) values (
    uid, today_str, p_distance, coalesce(p_coins, 0), coalesce(p_tier, 0),
    coalesce(p_time_seconds, 0), coalesce(p_player_name, 'Player')
  )
  on conflict (player_id, challenge_date) do update
  set distance = excluded.distance,
      coins = excluded.coins,
      tier = excluded.tier,
      time_seconds = excluded.time_seconds,
      player_name = excluded.player_name,
      created_at = now()
  where excluded.distance > public.daily_results.distance;

  return true;
end; $$;

-- ============================================================
-- Função my_daily_rank — rank do user atual no daily de uma data.
-- Chamada via POST /rest/v1/rpc/my_daily_rank com body { p_date }.
-- Retorna 0 rows se user não tem result naquela data.
-- ============================================================
create or replace function public.my_daily_rank(p_date text)
returns table(rank int, distance int)
language plpgsql
security invoker
set search_path = public
as $$
declare
  uid uuid := auth.uid();
begin
  if uid is null then
    return;
  end if;

  return query
  with ranked as (
    select
      r.player_id,
      r.distance,
      (rank() over (order by r.distance desc))::int as r
    from public.daily_results r
    where r.challenge_date = p_date
  )
  select ranked.r as rank, ranked.distance::int as distance
  from ranked
  where ranked.player_id = uid;
end; $$;
```

3. **Run**.
4. Output esperado: **Success. No rows returned.**

**Como saber se deu certo:**
- Sidebar → **Database** → **Tables** → `daily_results` aparece (vazia).
- **Database** → **Functions** → `submit_daily_result` + `my_daily_rank` aparecem.
- **Authentication → Policies** → 3 policies pra `daily_results`.

---

## Passo 2 — `_LeaderboardManager` GameObject na HomeScene

1. Abrir `HomeScene.unity`.
2. Hierarchy → **Create Empty** → nomear `_LeaderboardManager`.
3. Inspector → **Add Component** → `LeaderboardManager` (`RailSwitchMVP.Meta.LeaderboardManager`).
4. Campos:
   - **Top Limit**: `50`.
   - **Cache Ttl Seconds**: `300`.
   - **Verbose Logs**: ✅.
5. **Ctrl+S.**

**Como saber se deu certo:** Hierarchy mostra `_LeaderboardManager` ao lado dos outros singletons.

---

## Passo 3 — Prefab `LeaderboardEntry`

A row do leaderboard. Vamos criar como prefab pro panel instanciar várias.

### 3.1 — Criar no Project window

1. **Project** window → `Assets/Prefabs/UI/` (cria a pasta se não existir).
2. Botão direito → **Create → UI → Panel** — vai criar uma `New Game Object` mas precisamos de UI. Use:
   - Right-click no panel **dentro da Hierarchy** (não no Project) — Unity só cria UI elements via Hierarchy.

   Alternativa: na Hierarchy temporariamente.

### 3.2 — Construir o template no Hierarchy

1. Na **HomeScene** (qualquer Canvas que tu já tenha), botão direito num Canvas → **UI → Panel** → nomear `LeaderboardEntryTemplate`.
2. **Image** do panel: cor `#FFFFFF` alpha `13` (5%).
3. **Rect Transform:** Width `600`, Height `60`.
4. **Add Component → Horizontal Layout Group**:
   - Padding: 10 (left/right), 5 (top/bottom).
   - Spacing: `20`.
   - Child Alignment: Middle Left.
   - Control Child Size: Width ☑, Height ☑.
   - Child Force Expand: Width ☐, Height ☑.

### 3.3 — Filhos da row

1. Right-click no `LeaderboardEntryTemplate` → **UI → Text - TextMeshPro** → nomear `RankText`.
   - Layout Element: Min Width `80`, Preferred Width `80`.
   - Text: `#1`. Font Size: `28`. Bold. Alignment Center.

2. Right-click no `LeaderboardEntryTemplate` → **UI → Text - TextMeshPro** → nomear `NameText`.
   - Layout Element: Flexible Width `1` (expande).
   - Text: `PlayerName`. Font Size: `24`. Alignment Left.

3. Right-click → **UI → Text - TextMeshPro** → nomear `DistanceText`.
   - Layout Element: Min Width `120`, Preferred Width `120`.
   - Text: `9999 m`. Font Size: `24`. Alignment Right.

### 3.4 — Component `LeaderboardEntryUI` no root

1. Selecionar `LeaderboardEntryTemplate` (root).
2. Inspector → **Add Component** → `LeaderboardEntryUI` (`RailSwitchMVP.UI.LeaderboardEntryUI`).
3. Drag refs:
   - **Rank Text** ← child `RankText`.
   - **Name Text** ← child `NameText`.
   - **Distance Text** ← child `DistanceText`.
   - **Background** ← Image do próprio root (auto-criada pelo Panel).
   - **Normal Color** / **My Row Color** podem ficar nos defaults.

### 3.5 — Salvar como prefab

1. Arrastar `LeaderboardEntryTemplate` (do Hierarchy) pra `Assets/Prefabs/UI/`.
2. Confirmar "Create Original Prefab".
3. **Deletar** o GameObject da Hierarchy (não queremos ele permanente na cena).

**Como saber se deu certo:**
- `Assets/Prefabs/UI/LeaderboardEntryTemplate.prefab` existe.
- Hierarchy não tem mais o template.

---

## Passo 4 — Panel `LeaderboardPanel` na HomeScene

Modal sobreposto. Mesmo padrão visual do ShopPanel (Fatia 4) — fundo escuro + container central com lista + close button.

### 4.1 — Backdrop

1. HomeScene → Canvas → Right-click → **UI → Panel** → `LeaderboardPanel`.
2. Rect Transform: anchor stretch/stretch. Left/Top/Right/Bottom `0`.
3. Image: cor `#000000` alpha `200`.
4. **Desativar** a checkbox do GameObject no topo do Inspector (panel começa hidden).

### 4.2 — Header

1. Filho de `LeaderboardPanel` → **UI → Text - TextMeshPro** → `HeaderText`.
   - Anchor: top-center. Pos Y `-100`. Width `800`. Height `80`.
   - Text: `Daily Challenge` (substituído em runtime).
   - Font Size: `48`. Bold. Alignment Center.

2. Filho → **UI → Button - TextMeshPro** → `CloseButton`.
   - Anchor: top-right. Pos X `-50`. Pos Y `-50`. Width `80`. Height `80`.
   - Child Text: `X`. Font Size: `36`.

### 4.3 — Banner de "Tua posição"

1. Filho de `LeaderboardPanel` → **UI → Text - TextMeshPro** → `MyRankBannerText`.
   - Anchor: top-center. Pos Y `-200`. Width `800`. Height `60`.
   - Text: `Você: #-- — -- m`. Font Size: `32`. Bold. Alignment Center.
   - Color: dourado claro (ex: `#FFE07A`).

### 4.4 — Scroll View

1. Filho de `LeaderboardPanel` → **UI → Scroll View** → `EntriesScrollView`.
   - Anchor: middle center. Pos Y `0`. Width `700`. Height `900`.
   - Scrollbar Horizontal: desligar.
   - No `Content` interno (filho do Viewport):
     - Width `680`.
     - Add Component **Vertical Layout Group**: Spacing `8`, Padding `8` all.
       Control Child Size: Width ☑, Height ☐. Force Expand: Width ☑.
     - Add Component **Content Size Fitter**: Vertical Fit = `Preferred Size`.

### 4.5 — Empty state e Loading

1. Filho de `LeaderboardPanel` → **UI → Text - TextMeshPro** → `EmptyStateText`.
   - Anchor: middle center. Pos `0,0`. Width `600`. Height `100`.
   - Text: `Ninguém jogou o desafio de hoje ainda. Seja o primeiro!`.
   - Font Size: `28`. Alignment Center. Color cinza claro.
   - **Desativar** (controle visual fica com o LeaderboardPanelController).

2. Filho → **UI → Text - TextMeshPro** → `LoadingIndicator`.
   - Anchor: middle center. Pos `0,0`. Width `300`. Height `60`.
   - Text: `Carregando...`. Font Size: `28`. Alignment Center.
   - **Desativar** (idem).

### 4.6 — Component `LeaderboardPanelController`

1. Selecionar `LeaderboardPanel` root.
2. **Add Component** → `LeaderboardPanelController`.
3. Drag refs:
   - **Leaderboard Panel** ← `LeaderboardPanel` (auto-ref pro próprio root).
   - **Close Button** ← `CloseButton`.
   - **Header Text** ← `HeaderText`.
   - **My Rank Banner Text** ← `MyRankBannerText`.
   - **Loading Indicator** ← `LoadingIndicator` (o GameObject).
   - **Entries Container** ← o `Content` do Scroll View (filho do Viewport).
   - **Entry Prefab** ← `Assets/Prefabs/UI/LeaderboardEntryTemplate.prefab`.
   - **Empty State Panel** ← `EmptyStateText` GameObject.

**Como saber se deu certo:**
- Selecionar `LeaderboardPanel`, todos os slots populados.
- Painel está desativado (não aparece em Play).

---

## Passo 5 — Wire o botão Leaderboard no `HomeScreenController`

1. Hierarchy → selecionar o GameObject que tem `HomeScreenController`.
2. Inspector → **Leaderboard (Fatia 8)**:
   - **Leaderboard Button**: arrastar o botão Leaderboard que já existe na Home (era stub).
   - **Leaderboard Controller**: arrastar `LeaderboardPanel` (que tem o `LeaderboardPanelController`).
3. **Ctrl+S.**

**Como saber se deu certo:** Em Play, o botão Leaderboard está clicável (não mais inerte). Clicar abre o panel.

---

## Passo 6 — Testar no Editor

### 6.1 — Submeter resultado

1. **Play**. Aguardar boot (auth + sync OK).
2. Clica **JOGAR DESAFIO**.
3. Joga uma run, morre com X metros (ex: 320m).
4. Console esperado:
   ```
   [Daily] EndChallenge — meters=320 today=320 ever=320 brokeToday=True brokeEver=True
   [LB] Submit OK — distance=320
   ```
5. Dashboard Supabase → **Table Editor** → `daily_results`. Refresh. **1 row** aparece com tua player_id + challenge_date de hoje + distance=320.

### 6.2 — Abrir leaderboard

6. Volta pra Home. Clica botão **Leaderboard**.
7. Panel abre. Header: `Daily Challenge — DD/MM/YYYY`.
8. Loading aparece brevemente.
9. Banner mostra: `Você: #1 — 320 m`.
10. Lista mostra 1 row destacada (cor dourada): `#1  Player  320 m`.

### 6.3 — Quebrar o próprio recorde

11. Close → Daily Challenge de novo → 450m.
12. Console: `[LB] Submit OK — distance=450`.
13. Dashboard → row atualizou pra 450 (na mesma row, on conflict update).
14. Home → Leaderboard → banner `Você: #1 — 450 m`.

### 6.4 — Submeter pior (não deve atualizar)

15. Daily Challenge → 200m. (brokeToday=False, então cliente NÃO submete.)
16. Console: `[Daily] EndChallenge — meters=200 today=450 ever=450 brokeToday=False` — sem linha `[LB] Submit`.
17. Dashboard continua 450.

### 6.5 — DebugPanel

18. F1 → seção **Leaderboard (Fatia 8)**.
19. Mostra `top: 1 entries | myRank: #1 (450m)`.
20. Botão **Force +500m** → submete `500m` direto (bypassa daily flow).
21. Dashboard atualiza pra 500. Re-abrir Leaderboard → 500m.

---

## Passo 7 — Build Android + cross-device

1. **Build & Run**.
2. Capturar logs:
   ```powershell
   $adb = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
   & $adb logcat -c
   & $adb logcat | Select-String "LB|Daily|Sync"
   ```
3. No device: joga daily, morre. `[LB] Submit OK`.
4. Dashboard mostra 2 rows agora (Editor user + device user, UUIDs diferentes — anon auth é device-locked).
5. Leaderboard no device → mostra 2 entries (top 2 do dia). Tua row destacada.

---

## Critérios de validação

- [ ] SQL rodou sem erro; tabela + 2 funções aparecem no dashboard.
- [ ] `_LeaderboardManager` na HomeScene.
- [ ] Prefab `LeaderboardEntryTemplate` em `Assets/Prefabs/UI/`.
- [ ] `LeaderboardPanel` na HomeScene, ativável via close button.
- [ ] Botão Leaderboard wire no HomeScreenController.
- [ ] **Editor:** daily run com novo best → `[LB] Submit OK` + row no dashboard.
- [ ] **Editor:** abrir panel → entries renderizadas, tua row highlight.
- [ ] **Editor:** result pior NÃO submete (cliente filtra).
- [ ] **Editor:** Force +500m via DebugPanel → dashboard atualiza.
- [ ] **Android:** mesmas validações + 2 rows na tabela.

---

## Edge cases conhecidos

### 1. Anti-cheat zero
Cliente pode chamar `submit_daily_result(999999, ...)` via DevTools no Editor. Pra MVP, aceitável — comunidade pequena, friends-and-family. **Defer:** Edge Function que recebe seed+inputs e re-roda a run server-side antes de aceitar.

### 2. Player name = "Player" pra todo mundo
PDM.PlayerName default é "Player" e não há UI pra editar. Leaderboard fica meio chato com tudo "Player". **Quick fix:** DebugPanel já tem botões que podiam ter "set name". **Real fix:** Fatia futura de "perfil" + edição inline.

### 3. Cache stale durante submit do mesmo session
Submit invalida cache. Próximo open do panel refetch. Mas se você abriu panel ANTES do submit (cache populado), e abre de novo logo após submit → cache foi invalidado, refetch ok.

### 4. Failure de submit fica silencioso pro user
`[LB] Submit failed: ...` só no Console. Não tem retoast/retry UI. **Aceitável MVP:** próxima run que quebrar best tenta de novo. Mas se for a sessão final do user, o submit fica perdido. **Defer:** queue persistido de submits.

### 5. Função my_daily_rank com 100k+ rows
Window function escaneia tudo. Pode ficar lento. Index `(challenge_date, distance desc)` ajuda mas não elimina. Pra MVP <1000 users é instant. **Defer:** materialized view atualizada a cada N minutos.

---

## Próximas fatias (pós §11)

Conforme spec §11:
- **§11.4 — IAP** (Remove Ads + Coin Pack) quando tiver jogadores reais.
- **§11.5 — Battle Pass S2/S3** — futuro pós-MVP de verdade.
- **Profile UI** — edição de nome (Fatia "9"?) pra resolver o problema do nome genérico.
- **Anti-cheat server-side** quando o app ganhar tração e cheating virar problema.
