# XP / Account Level → Supabase: Setup

> ✅ **CONCLUÍDO 2026-06-02** — coluna `account_xp` criada no Supabase e código
> integrado/validado. Doc mantido como referência. Os passos abaixo já foram executados.

Vincula o **account XP** (Camada 1 da progressão adaptativa) ao sync do Supabase.
Antes era local-only (PlayerPrefs no device); agora viaja com o resto do estado do
player (coins, bests, etc.) via `PlayerDataSync`.

O **código Unity já está pronto** (`PlayerRemoteState.account_xp`, `CopyToRemoteState`,
`ApplyRemoteState` recalcula o level). A coluna no Supabase já foi criada.

---

## ⚠️ ORDEM IMPORTA — leia antes

Rode o SQL **ANTES** de abrir/buildar o app com o código novo. Motivo:
- Se o app fizer o **pull** antes da coluna existir, o JSON do servidor não terá
  `account_xp`, o cliente lê como **0**, e o `ApplyRemoteState` **zeraria o XP local**.
- O **push** também falharia (PostgREST rejeita coluna inexistente → fica `DirtyOffline`).

Crie a coluna primeiro e o transitório não acontece.

---

## Passo 1 — SQL no Supabase (SQL Editor)

```sql
alter table public.players
  add column if not exists account_xp int not null default 0;
```

`if not exists` torna idempotente (rodar de novo não quebra). Não precisa mexer nas
RLS policies nem no trigger de `updated_at` — a coluna entra coberta pelas policies
existentes (que operam na row inteira).

**Verificar:** Table Editor → `public.players` → a coluna `account_xp` aparece (int,
default 0).

---

## Passo 2 — Unity

Nada a fazer no editor além de buildar/rodar — o código já está integrado:
- `PlayerRemoteState` ganhou `public int account_xp;` (antes de `updated_at`, então o
  `StripUpdatedAt` do push continua funcionando).
- `PlayerDataManager.CopyToRemoteState` envia `account_xp` no push.
- `PlayerDataManager.ApplyRemoteState` aplica `account_xp` no pull e **recalcula o
  `accountLevel`** via `ComputeLevelFromXP` (XP é a fonte da verdade; o servidor não
  guarda o level).

---

## Como testar

1. Rode o SQL (Passo 1).
2. Abra o app autenticado. Ganhe XP (jogue ou use o debug L/J).
3. Force um push: `PlayerDataSync.DebugPushNow()` (ou espere o debounce de 2s e o
   `OnApplicationPause/Focus`). No Supabase → `players` → confira `account_xp` atualizado.
4. Force um pull limpo: `PlayerDataSync.DebugWipeLocalAndPull()` — o XP deve **voltar do
   servidor** e o `Lv. N` na Home refletir o nível recalculado.

---

## Notas

- **Conflito = server wins** (last-write-wins via `updated_at`, igual coins/bests). Num
  cenário multi-device, o pull inicial sobrescreve o XP local pelo do servidor. Pro XP
  cumulativo isso é o mesmo trade-off já aceito pros coins — OK pro MVP.
- O servidor guarda só `account_xp`; o `account_level` é **derivado no cliente** (não há
  coluna de level, e não precisa).
