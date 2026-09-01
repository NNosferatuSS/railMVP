# Handoff: Rail Switch — UI completa (HUD + meta-game)

## Overview
Rail Switch é um endless runner top-down de trilhos. O jogador corre sobre uma malha de trilhos e, a cada bifurcação, escolhe o trilho seguro tocando na **metade esquerda ou direita da tela**. O caminho errado é morte. Power-ups, hazards e um meta-game de moedas/gems/battle pass sustentam a retenção.

Este pacote especifica **toda a UI 2D do jogo** — HUD in-game, overlays e as 10 telas de meta-game. Nada de conteúdo 3D do mundo (trilhos, personagem, cenário) está aqui.

**Plataforma alvo:** Unity 2021.3 LTS · URP · mobile (iOS/Android) retrato.

## About the Design Files
Os arquivos `.dc.html` neste bundle são **referências de design feitas em HTML** — protótipos que mostram a aparência e o comportamento pretendidos. **Não são código para portar.** A tarefa é **recriar esses designs no ambiente do projeto Unity**, com uGUI (Canvas + RectTransform + TextMeshPro) e os sprites já exportados em `assets/`.

Se o projeto já tem prefabs de UI ou um sistema de temas, siga os padrões existentes; o que está aqui é a especificação visual, não a arquitetura.

## Fidelity
**High-fidelity.** Cores, tipografia, espaçamentos e tamanhos são finais. Todos os valores abaixo estão em **pt de design (canvas 390 × 844)**; os sprites em `assets/` estão exportados em **3× (1170 × 2532)**.

---

## Design Tokens

### Cores
| Token | Hex | Uso |
|---|---|---|
| accent | `#22E3FF` | trem do jogador, foco, links, seleção |
| safe | `#7CFF4F` | caminho correto, CTA primário |
| danger | `#FF4D5E` | trilho falso, morte, ação destrutiva |
| coins | `#FFC23D` | moeda soft, deals |
| gems | `#7C4DFF` | moeda premium, item ativo, battle pass |
| hazard | `#FF3EA5` | avisos não-letais (lane swap, vortex) |
| hazard_speed | `#FFB050` | speed-up zone |
| bg_deep | `#04060C` | fundo mais escuro, scrim |
| bg_mid | `#0A1024` | fundo médio |
| bg_lift | `#1B2A52` | topo do gradiente de meta |
| bg_city | `#0D0A1C` | fundo das telas de economia |
| rail_tile | `#4C505C` | dormentes |
| panel_fill | `#FFFFFF` @ 5% | fundo de painel |
| panel_stroke | `#FFFFFF` @ 12% | borda de painel |
| text_hi / mid / low | `#FFFFFF` @ 100% / 60% / 40% | hierarquia de texto |

Scrim de overlay: `#04060C` @ 72%.

### Tipografia
Duas famílias, ambas Google Fonts (gerar atlas TMP):

**Chakra Petch** — números, títulos, CTA
- Hero (distância no Game Over): 56–58, peso 700
- Título de tela: 34, peso 700
- CTA: 22–30, peso 700, tracking `.10em`
- Stat: 15–22, peso 700
- Header de tela: 17, peso 700, tracking `.10em`

**Barlow Semi Condensed** — texto de UI, labels
- Section label: 13, peso 700, tracking `.14em`, MAIÚSCULAS
- Body: 14, peso 500, line-height 1.4
- Caption: 11, peso 600, tracking `.18em`, MAIÚSCULAS
- Micro label: 9–10, peso 600, tracking `.16em`–`.26em`

**Nunca abaixo de 11 pt em tela.**

### Espaçamento e raio
- Spacing: 4 / 8 / 12 / 16 / 24
- Radius: 4 / 8 / 12 / 14 / 16 / 18 / 20 / 22 / 999
- Margem lateral padrão das telas: **16 pt** (24 pt em modais e no Game Over)
- Safe area: 44 pt topo, 34 pt base
- Alvo de toque mínimo: **44 × 44 pt**

### Sombras / glow
Já **baked** nos sprites. Não adicionar Outline/Shadow no Unity — dobra o efeito.
Botões têm inner-shadow inferior de 4 px (3 px nos pequenos); o estado *pressed* usa o sprite `_pressed` (4 px mais baixo) e desloca o conteúdo 3 px em Y.

---

## Arquitetura de Canvas sugerida

```
GameScene
 └ Canvas_HUD          (Screen Space - Overlay, sort 0)
    ├ Safe_Area          (script de safe area)
    ├ TopStats
    ├ HazardBanner       (pool de 1, animado)
    ├ PowerUpRail        (VerticalLayoutGroup, 3–4 slots)
    ├ BottomInput        (2 zonas invisíveis, esq/dir)
    ├ SwitchIndicator
    └ ActiveItemFAB
 └ Canvas_Overlays     (sort 10)
    ├ Warmup321
    ├ PausePanel
    └ GameOverPanel

MetaScene
 └ Canvas_Meta         (Screen Space - Overlay)
    ├ Background        (Image full-screen, bg_meta_grid ou bg_meta_city)
    ├ ScreenHost        (troca de telas)
    ├ BottomNav         (5 slots, persistente)
    └ ToastLayer        (sort acima de tudo)
```

Canvas Scaler: **Scale With Screen Size**, referência **390 × 844**, Match **0.5**.

---

## Screens / Views

### HUD in-game (`Rail Switch UI.dc.html` → opção `2a`)
**Propósito:** ler a corrida sem tirar os olhos dos trilhos. Todo elemento fica fora da faixa central vertical, onde a malha é lida.

**Layout (de cima para baixo):**
- **Distância** — canto sup. esq., 16 pt de margem, 18 pt do topo. Chakra Petch 34/700 branco, sufixo "M" em Barlow 15/600 @60%. Sombra de texto `0 2px 12px rgba(0,0,0,.7)`.
- **Linha de moedas** — abaixo da distância, gap 4. Ícone de moeda 18 pt + valor Chakra Petch 19/600 `#FFC23D` + "BEST 5 263" Barlow 11/600 @45% tracking `.14em`, margem esquerda 6.
- **Tier pill** — canto sup. dir. Fundo `#7C4DFF` @22%, borda `#A078FF` @50%, radius 8, padding 5×10. Label "TIER" Barlow 10/600 `#C9B3FF` + número Chakra 15/700 branco.
- **Botão de pause** — abaixo do pill, 34 × 34, radius 10, `btn_icon_pause`.
- **Hazard banner** — largura total menos 14 pt de cada lado, topo em 78 pt. Altura 56. Sprite `banner_hazard_[cor]`, radius 12. Conteúdo: triângulo de aviso 22 pt (pulsando 1.1 s), título Chakra 13/700 tracking `.06em`, subtítulo Barlow 11/500 @66%, contador Chakra 15/700 na cor do hazard, alinhado à direita. Só aparece quando um hazard está a ≤ 3 s.
- **Trilha de power-ups** — coluna à esquerda, x 12, y 150, gap 12. Cada puck 52 × 52: `puck_ring_*` como Image **Filled / Radial 360 / Origin Top / Clockwise** (fillAmount = tempo restante / duração), `puck_tile_[cor]` no centro, ícone do power-up por cima, `badge_gold` no canto inf. dir. com os segundos em Chakra 10/700. Máx. 3 pucks visíveis + 1 slot vazio (`puck_empty`).
- **Zonas de input** — metade esquerda e metade direita, altura 150 pt a partir da base, `Image` com alpha 0 + `raycastTarget = true`. Legendas "TAP LEFT" / "TAP RIGHT" em Barlow 10/600 @35% tracking `.22em` só no tutorial.
- **Indicador de switch** — canto inf. esq., x 24, y 46. Três barras (12 × 20, 14 × 30, 12 × 20, gap 6): a do meio na cor accent com glow, as laterais @20%. Label "SWITCH · MID" Barlow 10/600 @55%.
- **FAB de item ativo** — canto inf. dir., x 18, y 34. `fab_violet` 76 pt de diâmetro + ícone 32 pt. Label acima do botão (não abaixo), Barlow 10/700 `#D5C6FF` tracking `.16em`. Desabilitado (alpha 40%) quando não há item.

**Fundo:** `bg_game_rails.png` é referência de composição; em produção o 3D renderiza atrás. O gradiente escuro do topo (34% da altura, `#04060C` 92% → 0%) deve existir como Image no Canvas para legibilidade do HUD.

---

### Game Over (`Rail Switch UI.dc.html` → `2b`)
Overlay sobre a GameScene com `Time.timeScale = 0`. Fundo do jogo com blur/dessaturação + scrim 72%.

- **Header** — y 96: "DEAD END" Barlow 12/700 `#FF4D5E` tracking `.40em`; "RUN OVER" Chakra 34/700 branco tracking `.08em`.
- **Painel de resultado** — `panel_lg`, x 24, y 184, padding 22×20, gap 18.
  - Distância: Chakra 58/700 `#22E3FF`, glow `0 0 30px rgba(34,227,255,.5)`; sufixo "M" Barlow 20/600 @55%.
  - Linha "BEST 5 263 M · TIER 2 REACHED" Barlow 11/600 @50% tracking `.18em`.
  - Divisor 1 px @12%.
  - Três cards lado a lado (flex 1, gap 10, radius 12, padding 11 vertical): **COINS** (fundo gold @10%, borda @32%), **BATTLE PASS XP** (violeta), **MISSIONS x/3** (verde). Valor Chakra 18/700, label Barlow 9/600 @50%.
  - Faixa de missão concluída: fundo accent @7%, borda tracejada accent @35%, ícone 34 pt, título Barlow 12/700, detalhe Barlow 11/500 @60%. Só renderiza se houve conclusão na run.
- **Continue por gem** — x 24, bottom 150, altura 62. Gradiente horizontal violeta 30% → 8%, borda `#A078FF` @55%, radius 16. Ícone de gem 26 pt, título Chakra 14/700, subtítulo Barlow 11/500 @60%, custo Chakra 18/700 à direita. Custo dobra a cada uso na mesma run (2 → 4 → 8).
- **RUN AGAIN** — `btn_primary`, x 24, bottom 76, altura 62, texto Chakra 24/700 `#05210D`.
- **Rodapé** — bottom 26: HOME · LEADERBOARD · SHARE, Barlow 11/600 @50% tracking `.18em`, distribuídos com space-between.

---

### Pause (`Rail Switch Screens.dc.html` → `S5`)
`Time.timeScale = 0`, mesmo tratamento de fundo do Game Over.
- Painel `panel_lg` central (x 24, y 150): "PAUSED" Barlow 11/600 @50% tracking `.40em`, distância Chakra 40/700, linha de moedas + tier.
- Sliders MUSIC / SFX (label 52 pt de largura, Barlow 11/600 @60%) e toggle HAPTICS.
- Faixa informativa com os power-ups ativos: "Active power-ups keep their timers while paused."
- **RESUME** `btn_primary` altura 62 (bottom 150); **RESTART** (`btn_ghost`) e **QUIT** (`btn_danger`) lado a lado, altura 52 (bottom 76).
- Aviso Barlow 11/500 @40% no rodapé: "Quitting loses this run — coins are kept."

---

### Warmup 3-2-1-GO
Overlay antes do primeiro input. Números Chakra 46/700 accent; "GO" Chakra 34/700 safe. Animação por batida: escala 1.4 → 1.0 em 200 ms (ease-out) + fade-out nos últimos 100 ms. Um beat por segundo. Input travado até o "GO".

---

### S1 · Splash / Loading
Só no cold start. Fundo `bg_meta_grid`. Logo composto (3 barras + wordmark Chakra 42/700 tracking `.05em` com glow accent) em y 246, tagline "READ TWO SWITCHES AHEAD" Barlow 11/600 accent @80% tracking `.40em`. Barra de progresso 6 pt (`bar_track` + `bar_fill_cyan`) em bottom 132, com "LOADING TRACKS" e a porcentagem. Card de dica `row_sm` em bottom 56 (label "TIP" gold + texto Barlow 13/500 @80%) — sorteia de uma lista. Versão/build Barlow 10/500 @30% no rodapé.

---

### S2 · Seleção de modo
Fundo `bg_meta_grid`. Header com back + "CHOOSE TRACK" + chip de moedas.
- **Card Classic** (`panel_lg` com borda accent @40% e glow) em y 86: label "CLASSIC" / "ALWAYS OPEN", título Chakra 24/700 em 2 linhas, slot de preview 96 pt, dois stats (BEST, RUNS) em cards `#FFFFFF` @5% radius 12, e **START RUN** (`btn_primary`, altura 56).
- **Card Daily** (borda violeta @42%) em bottom 110: label + contador "6h 12m LEFT" gold, título Chakra 19/700, três stats (YOUR BEST, RANK, ATTEMPTS), **PLAY DAILY** (`btn_premium`, altura 48).
- Bottom nav 5 slots (HOME ativo).

**Regra:** o Daily usa o mesmo seed para todos os jogadores no dia; tentativas limitadas (o mock mostra 3 — confirmar com design).

---

### S3 · Loja
Fundo `bg_meta_city`. Header com back + "STORE" + chips de coins e gems.
- **Tabs** (`tabs_track` + `tabs_thumb`) em y 70: DEALS · COINS · GEMS · ITEMS.
- **Daily Deal** (`panel_accent_gold`, y 130): label + "RESETS 6h 12m"; à esquerda `frame_legendary` 86 pt com o render do cosmético; à direita raridade+categoria (Barlow 8/700 tracking `.16em`), nome Chakra 18/700, preço antigo riscado + desconto verde; **CTA de compra** (`btn_purchase`, altura 52) com ícone de moeda + valor.
- **Pacotes** (grid 2 col, gap 12, y 390): coins (neutro) e gems (violeta com badge "BEST"). Ícone grande, quantidade Chakra 20/700, label, botão de preço em moeda real.
- **Bundle de itens** (`panel_md`, bottom 100): dois ícones de power-up 44 pt, nome Chakra 13/700, lista de conteúdo Barlow 11/500 @60%, `btn_sm_gold` com o preço.
- Bottom nav (STORE ativo, gold).

---

### S4 · Coleção
Fundo `bg_meta_city`. Header com back + "COLLECTION" + contador "37 / 96".
- **Preview do loadout equipado** (y 70, altura 196, radius 18, tracejado) — render do personagem + trail + tile skin, com o nome e a raridade abaixo.
- **Chips de categoria** (y 280, wrap, gap 7): CHARACTER · TRAIL · TILE · COIN · SWITCH · BG · GAME OVER FX · COLLECT FX. Ativo = fundo accent sólido com texto `#04121A`.
- **Grade 3 colunas** (y 360, gap 11): cada célula é `frame_[raridade]` 104 pt de altura com o render dentro. Overlays possíveis: badge "EQUIPPED" verde (canto sup. esq.), preço em pill escuro (canto inf. dir.), ou `frame_locked` com o requisito ("BATTLE PASS LV 22", "EVENT 24 TOKENS"). Nome abaixo, Barlow 10/600.
- **EQUIP SELECTED** (`btn_primary` altura 52, bottom 100). Desabilita para itens não possuídos (vira "BUY").
- Bottom nav (SKINS ativo).

---

### S6 · Battle Pass
Fundo `bg_meta_city`. Header com back, "SEASON 1" + "SWITCHYARD · 22 DAYS LEFT", e badge de nível à direita.
- **Barra de XP** (`panel_md`, y 74): "XP TO LEVEL 15", valor "340 / 500" Chakra 12/700 violeta, `bar_track` + `bar_fill_violet` 8 pt, nota "XP comes from daily and weekly missions only."
- **Trilha vertical** (y 172 até bottom 172): cabeçalho de colunas FREE / PREMIUM (Barlow 9/600 tracking `.20em`) recuado 52 pt. Cada linha: badge de nível 42 pt + célula free + célula premium (flex 1 cada, altura 60, gap 10).
  - **Reclamado:** fundo verde @8%, borda verde @35%.
  - **Atual:** badge com fundo `#C9B3FF` sólido e glow; célula premium com borda 2 px `#B48CFF`.
  - **Futuro:** opacidade decrescente (62% → 45% → 32% → 24%), última linha cortada com fade para `#0D0A1C`.
- **UNLOCK PREMIUM** (bottom 96): faixa violeta com título, subtítulo e botão de preço em gems.
- Bottom nav (MORE ativo).

---

### S7 · Missões
Fundo `bg_meta_grid`. Header com back + "MISSIONS" + "RESETS 6h 12m".
- **Tabs** DAILY · WEEKLY com contadores no rótulo ("DAILY · 1/3").
- **Lista** (y 130, gap 11):
  - **Concluída não coletada:** `panel_accent_green`, ícone 38 pt, título + "COMPLETE", `btn_sm_green` "CLAIM".
  - **Em progresso:** `panel_md`, título Barlow 13/600 + recompensa à direita (ícone + valor), `bar_track` + `bar_fill_cyan` 5 pt, texto "430 / 600" Barlow 10/600 @50%.
- **Preview semanal** (`panel_accent_violet`, y 474): duas linhas de missão com `bar_fill_violet` e as recompensas, com um "· · ·" no rodapé indicando continuação.
- **Baú de conclusão** (`panel_accent_gold`, bottom 100): ícone de baú 54 pt, título "ALL 3 DAILIES CHEST", barra gold 5 pt, lista de recompensas.
- Bottom nav (MORE ativo).

---

### S8 · Leaderboard
Fundo `bg_meta_grid`. Header com back + "LEADERBOARD" + contador.
- **Tabs** TODAY · WEEK · FRIENDS.
- **Pódio** (y 132): três colunas alinhadas pela base, radius 14 só no topo, sem borda inferior. 1º lugar mais alto (146 pt, gold, glow), 2º (112 pt) e 3º (96 pt) neutros. Avatar circular, nome, distância, posição.
- **Lista** (y 300 até bottom 186): linhas de 11 pt de padding, alternando fundo transparente / `#FFFFFF` @3%. Posição (30 pt), avatar 28 pt, nome (flex), distância Chakra 13/700. Termina com "· · ·".
- **Linha do jogador** — fixa em bottom 100, fundo accent @12%, borda accent @55%, glow. Posição, avatar, "You" + tentativas restantes, distância.
- Bottom nav (RANKS ativo).

---

### S9 · Perfil
Fundo `bg_meta_grid`. Header com back + "PROFILE" + botão de menu.
- **Card de identidade** (`panel_lg`, y 76): avatar 76 pt radius 22, nome Chakra 22/700, badge de nível + ID, barra de XP 5 pt.
- **Grid 2×2 de stats** (y 206, gap 11): BEST DISTANCE (accent), TOTAL RUNS (branco), COINS EARNED (gold), BEST TIER (violeta). Label Barlow 9/600 @45% tracking `.18em`, valor Chakra 24/700.
- **TOP 4 RUNS** (`panel_md`, y 406): linhas com posição, distância Chakra 15/700 e detalhe "TIER 4 · 312 coins" Barlow 11/500 @45%, separadas por tracejado. Lê do PlayerPrefs.
- **Atalhos** (bottom 100): ACHIEVEMENTS 9/24 (verde) e COLLECTION 37/96 (neutro).
- Bottom nav (MORE ativo).

---

### S10 · Settings
Fundo liso `#12172B → #05070F`. Header com back + "SETTINGS".
Seções com label Barlow 10/600 accent tracking `.26em` e um `panel_md` por seção:
- **AUDIO** — sliders MUSIC e SFX com valor numérico à direita (30 pt de largura).
- **GAMEPLAY** — toggle HAPTICS (com subtítulo), toggle HIGH CONTRAST RAILS, segmented CAMERA TILT (FLAT / 26° / 40°), segmented ACTIVE ITEM BUTTON (LEFT / RIGHT).
- **ACCOUNT** — lista de linhas com chevron: RESTORE PURCHASES, PRIVACY POLICY, RESET LOCAL PROGRESS (em `#FF8D99`, pede confirmação em modal).
- Rodapé com versão/build e "local save (PlayerPrefs)".

---

## Interactions & Behavior

### Input do jogo
- Toque na **metade esquerda** = switch −1 lane; **metade direita** = +1 lane. As zonas cobrem a altura toda, exceto o FAB de item ativo e o botão de pause, que consomem o toque.
- Hazard **Lane Swap** inverte esquerda/direita por 4 s — o banner deve avisar antes.
- Haptics: leve no switch, médio na coleta de moeda, forte na morte.

### Animações
| Elemento | Comportamento |
|---|---|
| Hazard banner | slide-in de cima 250 ms ease-out; triângulo pulsa alpha .35↔1 a cada 1.1 s; slide-out 200 ms |
| Puck de power-up | pop-in escala 0.8→1.0 em 150 ms; anel radial contínuo; pisca vermelho nos últimos 3 s |
| Contador do warmup | escala 1.4→1.0 200 ms ease-out + fade nos últimos 100 ms |
| Botão | pressed troca sprite + desloca 3 px em Y, 60 ms |
| Toast | slide-in de baixo 200 ms, permanece 2.5 s, fade-out 200 ms |
| Game Over | scrim fade 250 ms, painel escala 0.92→1.0 300 ms ease-out-back; números contam de 0 até o valor em 600 ms |
| Grid do fundo | offset V contínuo (material scroll), ~0.05 unidade/s |
| Coleta de moeda | ícone voa até o contador do HUD, 350 ms ease-in, + escala pulse no contador |

### Estados
- **Loading** (loja/leaderboard): skeleton com `panel_md` em alpha pulsante.
- **Erro de compra:** toast vermelho "NOT ENOUGH GEMS" + shake horizontal 3 px no chip da moeda.
- **Sem conexão** (leaderboard): painel central com mensagem e botão RETRY (`btn_ghost`).
- **Item bloqueado:** `frame_locked` + tooltip no long-press explicando o requisito.

---

## State Management

### Estado de run (memória, resetado a cada corrida)
`distanceMeters` (float) · `coinsThisRun` (int) · `currentTier` (int) · `laneIndex` (int) · `activePowerUps` (lista de {tipo, tempoRestante, duração}) · `activeItemCharges` (int) · `pendingHazard` ({tipo, tempoAtéImpacto}) · `reviveCount` (int) · `isPaused` (bool).

### Estado persistente (PlayerPrefs ou save local)
`bestDistance` · `totalRuns` · `coins` · `gems` · `eventTokens` · `playerLevel` / `xp` · `equipped[categoria]` · `owned[itemId]` · `battlePassLevel` / `bpXp` / `bpPremium` (bool) · `dailyMissions[]` e `weeklyMissions[]` ({id, progresso, alvo, coletada}) · `dailySeed` / `dailyAttempts` / `dailyBest` · `settings` ({music, sfx, haptics, highContrast, cameraTilt, itemButtonSide}) · `top4Runs[]` · `lastFreeChestUtc`.

### Transições principais
```
Splash → Home
Home → (PLAY) → Warmup → Run → GameOver → (RUN AGAIN → Warmup | HOME → Home)
Run → (pause) → Pause → (resume → Run | restart → Warmup | quit → Home)
Home ↔ Store / Collection / Leaderboard / Missions / BattlePass / Profile / Settings
GameOver → (continue por gem) → Run   [respawna no caminho seguro]
```

Timers de reset (daily 6 h no mock, weekly na segunda) devem usar **UTC do servidor** se houver backend; senão, UTC local com verificação de retrocesso de relógio.

---

## Assets
Todos em `assets/`, exportados a **3×** (multiplicar por 3 os valores de pt deste documento), PNG com transparência, **sem nenhum texto embutido** — todo rótulo, número e preço é TextMeshPro no Unity.

Exceção: `controls/btn_icon_*` (pause, ×, voltar, +, menu, chevron) — o glifo é o próprio ícone.

- `panels/` — containers, banners de hazard (3 cores), pill
- `buttons/` — CTAs e seus estados pressed
- `controls/` — botões de ícone 44 pt, toggle, tabs, slider, barras, FAB
- `economy/` — chips, ícones de moeda/gem/token, pucks (anel + tile), badges
- `frames/` — 4 raridades + locked
- `overlays/` — toasts, tooltip, dots, scrim
- `icons/` — 288 × 288: power-ups, hazards, navegação, baú
- `backgrounds/` — 1170 × 2532

O `assets/README.md` traz os valores de **border de 9-slice** de cada sprite e as configurações de import.

**Ainda faltam (não são UI):** renders dos personagens, trails, tile skins e ícones de cosméticos. Nos mocks estão como retângulos listrados.

## Files
- `Rail Switch UI.dc.html` — HUD (opção `2a` é a final), Game Over (`2b`), folha de ícones (`2c`) e as explorações anteriores
- `Rail Switch Screens.dc.html` — as 10 telas de meta-game (S1–S10)
- `Rail Switch UI Kit.dc.html` — tokens, componentes e specs de 9-slice
- `Rail Switch Export.dc.html` — folha de exportação (fonte dos PNGs; edite aqui para regerar assets)
- `assets/` — os 77 PNGs em 3× + README de import

Abra os `.dc.html` em qualquer navegador para ver os designs.

## Decisões em aberto (confirmar com o design antes de implementar)
1. Daily Challenge tem limite de tentativas? O mock assume 3.
2. Custo do revive: assumi 2 gems dobrando por uso na mesma run.
3. Missões semanais: 3 por semana, mesma mecânica das diárias?
4. Leaderboard tem backend? Se não, "FRIENDS" e "WEEK" ficam ocultos no MVP.
