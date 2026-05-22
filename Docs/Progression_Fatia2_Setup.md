# Fatia 2 — Setup no Unity Editor (passo a passo)

> Códigos prontos: `MissionTracker`, `MissionEntryUI`, `HomeScreenController`
> atualizado, `DebugPanelController` com seção Missions, hook no
> `CollectibleCoin.Collect`. Falta o setup de cena.

**Tempo estimado:** 30–45 min (a maior parte é layout de UI das 6 missões).

---

## Bloco 1 — Adicionar `_MissionTracker` na HomeScene

1. Abra `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → clique direito → `Create Empty` → renomeie `_MissionTracker`.
3. Inspector → `Add Component` → `MissionTracker` → enter.
4. Não precisa atribuir refs. O componente carrega missões do PlayerPrefs
   no Awake e gera novas se a data mudou.
5. Salve a cena (`Ctrl+S`).

> **Importante:** o `_MissionTracker` precisa ficar na HomeScene (cena de
> entrada). Ele é `DontDestroyOnLoad` — vai sobreviver entre cenas e fazer
> auto-StartRun quando a GameScene carrega.

---

## Bloco 2 — Criar prefab `MissionEntry_Prefab`

Vamos criar **um** prefab da entrada de missão, configurar uma vez, e
depois instanciar 6 vezes na Home (3 daily + 3 weekly).

### 2.1 Criar a estrutura

1. Hierarchy (com Canvas selecionado) → clique direito no Canvas →
   `UI → Panel` → renomeie `MissionEntry_Template`.
2. Selecione o Panel:
   - Width `900`, Height `90`.
   - Image component: cor escura semi-transparente (ex: `#1A1A2E` alpha
     180). Isso só pra dar uma "card".
3. Adicione componente `Mission Entry UI` (script novo).

### 2.2 Layout interno (Horizontal Layout Group)

1. Selecione `MissionEntry_Template` → Add Component →
   `Horizontal Layout Group`.
   - Padding: Left/Right `12`, Top/Bottom `8`.
   - Spacing `12`.
   - Child Alignment: `Middle Left`.
   - Control Child Size: Width ☐, Height ☑.
   - Use Child Scale: ambos ☐.
   - Child Force Expand: Width ☐, Height ☐.

### 2.3 Filhos do MissionEntry_Template

Crie 4 filhos do `MissionEntry_Template` (clique direito nele):

**1) DescriptionText** (`UI → Text - TextMeshPro`):
- Layout Element component → Preferred Width `500`, Min Width `400`.
- Text: `Descrição da missão` (placeholder).
- Font Size: `22`. Alignment: Middle Left.
- Wrap: enable.

**2) ProgressText** (`UI → Text - TextMeshPro`):
- Layout Element → Preferred Width `120`.
- Text: `0 / 100`.
- Font Size: `20`. Alignment: Middle Center.

**3) RewardText** (`UI → Text - TextMeshPro`):
- Layout Element → Preferred Width `100`.
- Text: `+100`.
- Font Size: `22`. Alignment: Middle Center.
- Color: amarelo (`#FFD54F`).

**4) ClaimButton** (`UI → Button - TextMeshPro`):
- Layout Element → Preferred Width `160`, Preferred Height `60`.
- Renomeie o filho `Text (TMP)` pra `ClaimText` (opcional, fica claro).
- ClaimText: Text `Reclamar`, Font Size `20`.

### 2.4 Atribuir refs no MissionEntryUI

Selecione o `MissionEntry_Template`. No componente `Mission Entry UI`:

- **Description Text** → arrasta `DescriptionText`.
- **Progress Text** → `ProgressText`.
- **Reward Text** → `RewardText`.
- **Claim Button** → `ClaimButton`.
- **Claim Button Text** → o filho `ClaimText` do botão.

### 2.5 Converter em prefab

1. Crie a pasta `Assets/Prefabs/UI/` se ainda não existe.
2. Arraste `MissionEntry_Template` da Hierarchy pra `Assets/Prefabs/UI/`
   pra virar prefab. Confirma "Original Prefab".
3. Renomeie o asset prefab pra `MissionEntry_Prefab`.
4. Pode **deletar o `MissionEntry_Template` da Hierarchy** — só queríamos
   o prefab.

---

## Bloco 3 — Adicionar 6 entries de missão na HomeScene

### 3.1 Container "Daily Missions"

1. Hierarchy (Canvas selecionado) → clique direito → `UI → Panel` →
   renomeie `DailyMissionsContainer`.
   - Rect Transform: Anchor middle-center (Alt). Pos X `0`, Pos Y `-300`.
     Width `920`, Height `320`.
   - Image: alpha 0 (transparente) ou cor escura discreta.
2. Add Component → `Vertical Layout Group`:
   - Padding 8 all sides. Spacing `8`. Child Alignment: Upper Center.
   - Control Child Size: Width ☑, Height ☐.
   - Child Force Expand: Width ☑, Height ☐.
3. Adicione um título: filho `UI → Text - TextMeshPro` → `DailyTitle` →
   text `Missões Diárias`, Font Size `26`, Bold, Alignment Center.

### 3.2 Instanciar 3 daily entries

1. Arraste `MissionEntry_Prefab` (do Project) pra dentro de
   `DailyMissionsContainer`. Renomeie a instância pra `Daily0`.
2. Repita 2 vezes mais → `Daily1`, `Daily2`.
3. O VerticalLayoutGroup do container vai posicioná-las automaticamente.

### 3.3 Container "Weekly Missions"

1. Duplique `DailyMissionsContainer` → renomeie `WeeklyMissionsContainer`.
2. Pos Y `-660` (abaixo do daily).
3. Mude o título filho pra `Missões Semanais`.
4. Renomeie os 3 entries internos pra `Weekly0`, `Weekly1`, `Weekly2`.

### 3.4 Wire no HomeScreenController

1. Selecione `_Home` (que tem o HomeScreenController).
2. No Inspector, expanda:
   - **Daily Entries** (array size 3): arraste `Daily0`, `Daily1`, `Daily2`.
   - **Weekly Entries** (array size 3): arraste `Weekly0`, `Weekly1`, `Weekly2`.

> Se o array veio `Size 0`, mude pra `3` primeiro e depois arrasta.

### 3.5 Ajustar layout final

A Home agora tem muita coisa. Sugestão de layout (Canvas 1920×1080):

- Top: Player name + Coins + Best (já existia, mantém Y altos).
- Centro: PlayButton (move pra Y `~50` se conflitar com missões).
- Baixo: Daily container (Y `-300`) + Weekly container (Y `-660`).

Ajuste posições/escalas até ficar legível. Não precisa ser bonito —
**funcional > pixel-perfect** na Fatia 2.

Salve a cena.

---

## Bloco 4 — Validar (7 critérios)

Play Mode partindo da HomeScene.

- [ ] **1.** Home aparece com 3 daily + 3 weekly listadas. Cada uma com
   descrição, `X/Y` progress (provavelmente `0 / Target`), reward e botão
   `—` (não-interativo) ou `Reclamar` (se já tinha completado).
- [ ] **2.** Clica `JOGAR` → joga uma run coletando moedas e passando por
   tiers → morre → clica `HOME`. Progresso de missões relevantes (coins,
   distance, tier, tilesWithCoins) atualizou na Home.
- [ ] **3.** F1 abre DebugPanel na GameScene → seção `Missions` mostra os
   6 slots com `[ ]` (incompleto), `[★]` (completo, não reclamado), `[✓]`
   (reclamado). Botões `Force D0/D1/D2` e `Force W0/W1/W2` funcionam.
- [ ] **4.** Use `Force D0` no debug → volta pra Home → entry 0 mostra
   botão `Reclamar` ativo → clica → coins do PlayerDataManager aumentam +
   botão vira `Reclamado` (cinza).
- [ ] **5.** Fecha Play e reabre → progresso e claims persistem.
- [ ] **6.** Debug `Reset All Missions` → todas voltam pra 0/target e
   não-claimed. PlayerPrefs limpas.
- [ ] **7.** Bonus — testa todos os tipos:
   - **single_run_coins (0/1/2):** colete N moedas numa run.
   - **single_run_distance (3/4/5):** chegue a Xm.
   - **single_run_time (6/7/8):** sobreviva Ys.
   - **total_runs (9/10):** complete N runs no dia.
   - **daily_total_coins (11/12):** sume coins em runs múltiplas.
   - **use_powerup (13/14/15):** pega Shield/Magnet/SlowDown.
   - **reach_tier (16/17):** chegue a tier 2/3.
   - **no_powerup_run (18):** termine uma run sem pegar nenhum power-up.
   - **tiles_with_coins (19):** colete moedas em 10 tiles distintos.

> Não precisa fazer todos hoje — basta o suficiente pra confirmar que os
> hooks estão chamando o tracker. Você pode confirmar via debug logs no
> Console.

---

## Bloco 5 — Commit

Quando os 7 critérios passarem, commitamos a Fatia 2.

---

## Troubleshooting

- **Home não mostra missões / array Mission Entries vazio** → você não
  arrastou os refs no `_Home`. Refazer passo 3.4.
- **Botão `Reclamar` nunca habilita mesmo com missão complete** → confira
  se o `MissionEntryUI` tem o `Claim Button` atribuído corretamente.
- **`MissionTracker.Instance null`** → falta o `_MissionTracker` na
  HomeScene. Bloco 1.
- **Pegou Shield mas missão `Use Shield` não progride** → confirma que o
  `_MissionTracker` está sobrevivendo entre cenas. Checa no Console se
  aparece `[MissionTracker] Loaded` ao abrir o app. Se duplica entre cenas,
  é bug. Se some, é DontDestroyOnLoad falhando.
- **`tiles_with_coins` não aumenta** → checa que o PlayerRailRider tem
  tag `Player` ou está sendo encontrado por `FindFirstObjectByType`.
- **Console pollui com `[MissionTracker] All daily missions complete
  today (...)` repetido** → expected: dispara uma vez ao bater todas 3.
  Se dispara várias vezes na mesma run, é bug.
