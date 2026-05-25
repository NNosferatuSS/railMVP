# Spec — Battle Pass & Achievements (Structural Prep)

> **Status:** spec; not yet implemented.
> **Quando:** após OAuth, Realtime, IAP — quando ship/F&F já validados.
> **Pré-requisitos:** nenhum técnico. Beneficia de PDM sync (Fatia 7B) pronto.

---

## Premissa

Spec §11.6 prevê Battle Pass como "futuro". Esta spec serve pra:

1. Distinguir 3 sistemas semelhantes que se confundem: Missions, Achievements, Battle Pass.
2. Propor data model + code stubs **estruturais** que não exijam content design feito.
3. Documentar o caminho pra quando virar prioridade — sem se preocupar com balance/conteúdo agora.

**Realisticamente**: pra um indie solo dev, Battle Pass + content design é um
investimento alto. Esta spec deixa a **estrutura** pronta — o content (XP curve,
rewards, season theme) é skill separado e pode ser preenchido devagar.

## Os 3 sistemas — distinção clara

```
┌─────────────────────────────────────────────────────────┐
│ Missions (já implementado, Fatia 2)                    │
│  • Curto prazo (daily 24h, weekly 7d)                  │
│  • Pool de 12 tipos pre-definidos                      │
│  • Refresca automaticamente                            │
│  • Reward: coins (50-500 typical)                      │
│  • Objetivo: dar tarefas frescas + retention diária    │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ Achievements (proposto, novo)                          │
│  • Long-term, PERMANENTE (nunca refresca)              │
│  • "Faça X uma vez na vida" (ou cumulativo até alcançar)│
│  • Lista crescente conforme dev adiciona              │
│  • Reward: badge (visual cosmético) + coins one-time   │
│  • Objetivo: completionismo, sense of progress         │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ Battle Pass (proposto, novo)                           │
│  • Sazonal (1 mês typical) com início e FIM            │
│  • Track de XP → tiers → rewards (cosmetics, coins)    │
│  • Pode ter free + premium tracks (premium = paid)     │
│  • XP ganho via gameplay (missions completas, runs)    │
│  • Objetivo: engagement contínuo + monetização         │
└─────────────────────────────────────────────────────────┘
```

Resumo: **Missions = short-term repeat. Achievements = forever badges.
Battle Pass = seasonal monetized progression.**

---

## Parte 1 — Achievements

### Data model (PlayerPrefs + Supabase)

Achievement definitions são **hard-coded** em código C# (similar ao
`MissionTracker` Missions pool). Não tabela "definitions" no Supabase — é
balance de dev, não live ops.

```cs
namespace RailSwitchMVP.Meta {
public static class AchievementsCatalog {
    public static readonly AchievementDef[] All = new[] {
        new AchievementDef("first_run", "Primeira run", "Complete sua primeira run.", target: 1, rewardCoins: 100),
        new AchievementDef("dist_1km", "Iniciante", "Acumule 1000m totais.", target: 1000, rewardCoins: 200),
        new AchievementDef("dist_10km", "Maratonista", "Acumule 10000m totais.", target: 10000, rewardCoins: 1000),
        new AchievementDef("daily_winner_1", "Top 10 Diário", "Termine no top 10 do Daily Challenge.", target: 1, rewardCoins: 500),
        new AchievementDef("coins_1000", "Capitalista", "Acumule 1000 coins.", target: 1000, rewardCoins: 200),
        // ... 30+ defs total
    };
}

public class AchievementDef {
    public string Id;          // stable PK
    public string Title;
    public string Description;
    public int Target;
    public int RewardCoins;
    public string CosmeticUnlock; // optional (future)
}
}
```

State persisted in Supabase:
```sql
create table public.achievements_progress (
    player_id uuid references auth.users(id) on delete cascade,
    achievement_id text not null,
    progress int not null default 0,
    completed bool not null default false,
    claimed bool not null default false,
    completed_at timestamptz,
    primary key (player_id, achievement_id)
);
```

E PlayerPrefs cache (igual PDM pattern):
- `RailMVP.Ach.{id}.Progress` (int)
- `RailMVP.Ach.{id}.Claimed` (int 0/1)

### Code stub

```cs
public class AchievementsManager : MonoBehaviour {
    public static AchievementsManager Instance { get; }

    // events
    public event Action<string> OnAchievementCompleted;
    public event Action<string> OnAchievementClaimed;

    // API pública (similar a MissionTracker)
    public AchievementProgress GetProgress(string id);
    public void RecordProgress(string id, int delta);       // called by hook (game events)
    public bool TryClaim(string id);                         // user clicks Claim
    public IEnumerable<AchievementProgress> AllProgress();   // pra UI list

    // Hooks (chamados de game events)
    public void OnRunCompleted(int meters, int coins, int tier, float time);
    public void OnCoinsEarned(int amount);
    public void OnDailyChallengeFinished(int rank);
}

public struct AchievementProgress {
    public string Id;
    public int Progress;
    public bool Completed;
    public bool Claimed;
}
```

### Hooks no código existente

- `GameOverController.CommitRunToPlayerData` → `AchievementsManager.OnRunCompleted(meters, ...)`.
- `PDM.AddCoins` → `AchievementsManager.OnCoinsEarned(amount)` (ou listen via PDM event).
- `LeaderboardManager.FetchMyRank` callback → `AchievementsManager.OnDailyChallengeFinished(rank)`.

### UI — Achievement panel

Novo botão no Home (substitui stub futuro ou adiciona ao Profile panel).

```
┌────────────────────────────────────┐
│ Conquistas               12/30     │
├────────────────────────────────────┤
│  ✓ Primeira run           [✓]      │
│  ⏳ Iniciante (450/1000m)          │
│  ⏳ Maratonista (5234/10000m)      │
│  🔒 Capitalista (200/1000)         │
│  ★ NEW Top 10 Diário [RECLAMAR]   │
│  ...                                │
└────────────────────────────────────┘
```

Status icons:
- 🔒 Locked (não começou)
- ⏳ In progress (X/Y)
- ★ Completed but not claimed (CTA "RECLAMAR")
- ✓ Claimed (badge cinza)

### Esforço Achievements

| Etapa | Tempo |
|---|---|
| AchievementsCatalog hardcoded (~30 defs) | 1-2h (skill: design) |
| AchievementsManager singleton + PlayerPrefs + Supabase sync | 2-3h |
| SQL: tabela achievements_progress + RLS | 30min |
| Hooks no GameOverController + PDM + Leaderboard | 1h |
| UI panel (scroll list, status icons, claim button) | 2-3h |
| Testing | 1h |
| **Total** | **~8-10h (~2 sessões)** |

---

## Parte 2 — Battle Pass

### Conceitos chave

- **Season:** período fixo (1 mês). Start + end timestamp. Quando termina, próxima season começa automaticamente.
- **XP:** progresso linear. Ganha via gameplay.
- **Tier:** níveis na track. Cada N XP sobe 1 tier (curve exponential típico).
- **Track:** 1 free + 1 premium (premium desbloqueia via IAP).
- **Rewards:** coins, cosmetics, character unlocks.

### Data model

Hard-coded definitions per season (similar a achievements):
```cs
public class SeasonDef {
    public int SeasonId;          // 1, 2, 3...
    public string Name;            // "Verão 2026", "Halloween"
    public DateTime StartUtc;
    public DateTime EndUtc;
    public int[] XpPerTier;        // cumulative XP needed por tier
    public BattlePassReward[] FreeTrack;     // 1 reward per tier (or null)
    public BattlePassReward[] PremiumTrack;  // 1 reward per tier
    public int PremiumPriceR$;     // price for unlock
}

public class BattlePassReward {
    public RewardType Type;  // Coins, Character, ParticleEffect, etc
    public int Amount;       // pra Coins
    public string AssetId;   // pra cosmetics
}
```

Supabase:
```sql
create table public.season_progress (
    player_id uuid references auth.users(id) on delete cascade,
    season_id int not null,
    xp int not null default 0,
    current_tier int not null default 0,
    claimed_tiers_free int[] default '{}',     -- array de tiers que reclamou
    claimed_tiers_premium int[] default '{}',
    premium_unlocked bool not null default false,
    primary key (player_id, season_id)
);
```

### XP sources (proposta)

| Source | XP |
|---|---|
| Complete daily mission | 50 |
| Complete weekly mission | 100 |
| Run 1km | 25 |
| Run 5km in one run | 100 (bonus once per run) |
| Daily Challenge top 10 | 200 |
| Daily Challenge top 50 | 50 |
| Achievement completed | 100 |

XP curve example (tier 1 → 50, tier 2 → 100 cumulative, etc):
```
Tier 1: 50 XP    Tier 10: 1500 XP
Tier 2: 100 XP   Tier 20: 4000 XP
Tier 5: 350 XP   Tier 30: 7500 XP
```

Goal: full track ~30 tiers em 1 mês jogando moderado.

### Code stub

```cs
public class SeasonManager : MonoBehaviour {
    public static SeasonManager Instance { get; }
    
    public SeasonDef CurrentSeason;           // hardcoded array, picks current by date
    public SeasonProgress CurrentProgress;    // from PlayerPrefs + Supabase
    
    public event Action<int> OnTierReached;
    public event Action<int> OnRewardClaimed;
    
    // API
    public void AddXp(int amount);                // hook from gameplay
    public bool TryClaimTier(int tier, bool premium);
    public bool UnlockPremium();                  // IAP triggered
}
```

### UI — Battle Pass track

Visual mais complexo — track horizontal scrollable:

```
┌────────────────────────────────────────────────────────┐
│ Season 1 — Verão 2026                                  │
│ 28 dias restantes • XP: 1234 / 1500 (Tier 9 → 10)     │
├────────────────────────────────────────────────────────┤
│                                                          │
│ Free  [ 100c ][ 50c ][ ICN ][ ★ NEW ★ ][ 100c ]...      │
│        ✓      ✓      ✓      CLAIM      🔒 T10           │
│  ──────●──────●──────●──────●──────────●──────         │
│        T6     T7     T8     T9         T10              │
│  ──────●──────●──────●──────●──────────●──────         │
│ Prem  [ CHAR ][ EFX ][ 500c][ 200c ][ CHAR ]...         │
│        🔒     🔒     🔒     🔒        🔒                │
│                                                          │
│  [ UNLOCK PREMIUM — R$ 9,99 ]                           │
└────────────────────────────────────────────────────────┘
```

Bastante UI work. Recomendação MVP: começa com track simples (vertical
list em vez de horizontal scroller), iterar visual depois.

### Esforço Battle Pass

| Etapa | Tempo |
|---|---|
| Season catalog (hardcoded, 1 season) — design XP curve + rewards | 2-3h (skill: design) |
| SeasonManager singleton + PlayerPrefs + Supabase sync | 3-4h |
| SQL: tabela season_progress + RLS | 30min |
| Hooks XP em gameplay (missions, runs, daily) | 1-2h |
| UI: progress bar + track view (lista vertical simple MVP) | 3-4h |
| IAP premium unlock integration | 1h (assumindo IAP já pronta) |
| Cosmetics system (se rewards incluem cosméticos) | 2-4h (depende escopo) |
| Testing | 2h |
| **Total estrutural** | **~15-20h (~3-4 sessões)** |

Content design (curve XP, balance, season theme) é **separado** e pode
levar tempo iterativo testando com F&F antes de shipar.

---

## Migração e ordem recomendada

1. **Achievements first** — menos complexo (sem timer/season), mas estrutura
   semelhante. Implementar primeiro pra validar pattern.
2. **Battle Pass depois** — reusa achievements pattern + adiciona seasonal.
3. **Cosmetics system** — quando o catálogo de rewards começar a precisar
   de mais que coins.

## O que ficar pronto agora (sem implementar)

Pra "deixar estrutura preparada":

- [ ] SQL files preparados em `Docs/Specs/` (não rodados):
   - `achievements_progress` table
   - `season_progress` table
- [ ] Catalog stubs em C# (commits sem ativar): `AchievementsCatalog.cs`
   com array vazio, `SeasonCatalog.cs` com 0 seasons.
- [ ] Issue trackers (Github / Trello / Notion) com items pra balance design.
- [ ] Quando virar prioridade: começar pelo Achievement Manager (menor risco).

## Out of scope (explicitamente)

- **Cosmetics implementation:** depende de sistema de assets unlockable
  (chars já são unlockable via shop, mas cores/efeitos/particles não).
  Sistema separado.
- **Live ops via servidor:** A/B testing tier rewards, dynamic events. Big
  infra, pós-MVP escala real.
- **Season auto-cycling:** quando season 1 termina, season 2 começa. Pra
  MVP, hardcode 1 season e ship sem auto-cycle (manual quando expirar).

## Open questions

1. **Cosmetics escopo:** colors do trail player? Particle effects ao
   switch? Character emotes? Define-se o tipo antes do design das rewards.
2. **Free vs Premium balance:** % de rewards "obrigatórios" no free
   (mantém engagement) vs reservados premium (justifica compra). 30/70 ou 50/50?
3. **Como anunciar nova season:** push notification? Toast no Home?
   Splash modal? Tem efeito direto em retention metrics.
4. **Battle Pass pode ter consumable em vez de subscription?** Sim — Coin
   Pack-style "destrava esta season". Mais simples que recurring sub
   pra MVP. Defer subscription pra futuro.
