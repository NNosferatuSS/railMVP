# Leaderboard Global (best distance da run normal): Setup

Ranking global de **best distance** das runs normais (endless), como aba nova no painel
de leaderboard já existente (ao lado do Diário).

**Insight de arquitetura:** NÃO há tabela nem submit dedicados. O `best_distance` já
existe e já sincroniza na tabela `players` (via `PlayerDataManager`). O ranking global é
só uma query ordenada sobre `players`, exposta por 2 RPCs `SECURITY DEFINER`.

O **código Unity já está pronto** (`LeaderboardManager.FetchGlobalTop`/`FetchMyGlobalRank`,
abas no `LeaderboardPanelController`). Faltam o **SQL** + **wiring de 2 botões**.

---

## Passo 1 — SQL no Supabase (SQL Editor)

```sql
-- Top N do ranking global de best distance. SECURITY DEFINER pra ler de players de
-- forma controlada (a RLS de players só deixa ler a própria row) e expor SÓ
-- player_name + best_distance — ranking público, sem vazar coins/email.
create or replace function public.global_distance_top(p_limit int default 50)
returns table (player_id uuid, player_name text, distance int)
language sql
stable
security definer
set search_path = public
as $$
  select id, player_name, best_distance
  from public.players
  where best_distance > 0
  order by best_distance desc, updated_at asc
  limit greatest(1, least(coalesce(p_limit, 50), 200));
$$;

-- Rank global do user atual (1-based). 0 rows se best_distance = 0 (sem recorde).
create or replace function public.my_global_distance_rank()
returns table (rank int, distance int)
language sql
stable
security definer
set search_path = public
as $$
  with ranked as (
    select id, best_distance,
           rank() over (order by best_distance desc) as r
    from public.players
    where best_distance > 0
  )
  select r::int, best_distance
  from ranked
  where id = auth.uid();
$$;

grant execute on function public.global_distance_top(int) to authenticated;
grant execute on function public.my_global_distance_rank() to authenticated;
```

**Verificar:** SQL Editor → rode `select * from public.global_distance_top(10);` → deve
listar os players com best_distance > 0, ordenados desc.

---

## Passo 2 — Unity (wiring de 2 botões de aba)

No painel de leaderboard que já existe (o do daily), adicione **2 botões de aba** no
topo (ex: "Diário" e "Global") e ligue no `LeaderboardPanelController`:

| Campo (novo) | Arraste |
|---|---|
| Daily Tab Button | botão "Diário" |
| Global Tab Button | botão "Global" |

Tudo o resto é **reusado** (a mesma lista/scroll, o mesmo prefab de row
`LeaderboardEntryUI`, o banner "Você: #X", o header). Não precisa de prefab novo, manager
novo, nem mexer no `LeaderboardManager` da cena (que ganha os métodos automaticamente).

Comportamento:
- Abre na última aba escolhida (default Diário). A aba ativa fica não-clicável (feedback).
- **Diário:** idêntico ao de antes (daily challenge do dia + botão "JOGAR DESAFIO").
- **Global:** ranking de best distance; header "Recordes Globais — Distância"; banner
  "Você: #X — Ym" ou "Sem recorde ainda. Jogue uma run!". O botão "JOGAR DESAFIO" some
  (só faz sentido no daily).

> Se você deixar os 2 campos de aba vazios, o painel se comporta como antes (só daily) —
> as abas são opcionais.

---

## Como testar

1. Rode o SQL (Passo 1).
2. Tenha players com `best_distance > 0` no Supabase. Pra ver ranking com várias linhas:
   jogue runs normais (deixe o `PlayerDataSync` pushar — debounce ~2s / ao pausar), ou
   insira rows de teste via SQL.
3. Home → abrir Leaderboard → aba **Global**: vê o ranking por distância, sua row em
   destaque, e o banner com tua posição. Aba **Diário**: tudo como antes.

---

## Notas

- **Sem submit dedicado:** o best distance sobe no servidor pelo **sync normal do PDM**
  (quando você bate o recorde, `UpdateBests` salva e o `PlayerDataSync` pusha). Pra
  aparecer/atualizar no global, precisa ter sincronizado (auth + `_PlayerDataSync` na cena).
- **Empates:** o `my_global_distance_rank` usa `rank()` (empatados dividem a posição); o
  top numera sequencialmente no cliente (1..N). Diferença só aparece em empates — ok pro MVP.
- **Seed aleatório:** a run normal tem seed aleatório, então o ranking tem componente de
  sorte — decisão consciente (a imprevisibilidade faz parte do jogo).
