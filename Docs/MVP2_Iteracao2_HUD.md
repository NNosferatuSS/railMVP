# MVP2 — Iteração 2: HUD Básico

**Objetivo:** dar feedback visual contínuo de progresso. Top-left: tempo,
distância, moedas. Top-right: tier atual.

Pré-requisitos: MVP2 Iter 1 validada (obstáculos funcionando). Ver
`Docs/MVP2_Plan.md` §"Iteração 2".

---

## 1. Importar TMP Essentials (se ainda não tem)

A primeira vez que você criar um Text - TextMeshPro na cena, a Unity
mostra um popup **"Import TMP Essentials"**. Clique **Import**.
Só precisa fazer uma vez por projeto. Cria a pasta `Assets/TextMesh Pro/`
com font/material default.

Se já fez antes, pode pular este passo.

---

## 2. Criar o Canvas

1. Hierarchy → clique direito → **UI → Canvas**.
2. Renomeie pra `_HUD_Canvas`.
3. No componente **Canvas**:
   - Render Mode: `Screen Space - Overlay`.
4. No componente **Canvas Scaler**:
   - UI Scale Mode: `Scale With Screen Size`.
   - Reference Resolution: `1920 × 1080`.
   - Match: `0.5` (balanceia largura/altura).

> A Unity também cria um GameObject `EventSystem` automaticamente — pode
> deixar.

---

## 3. Criar os 4 textos

**Para cada um dos 4 textos abaixo:**
- Right-click no `_HUD_Canvas` → **UI → Text - TextMeshPro**.
- Renomeie conforme tabela.
- Ajuste o componente `RectTransform` (anchor + position).
- Ajuste o componente `TextMeshPro - Text (UI)`:
  - Font Size: 36
  - Color: branco (#FFFFFF)
  - Alignment: alinhe conforme tabela.

### 3.1 Tempo (top-left, primeira linha)

| Campo | Valor |
|---|---|
| Nome | `TimeText` |
| Anchor preset | Top-Left (segure Alt para definir position junto) |
| Anchored Position | (24, -24) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Initial text | `Time 00:00` |

### 3.2 Distância (top-left, segunda linha)

| Campo | Valor |
|---|---|
| Nome | `DistanceText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -84) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Initial text | `Dist 0 m` |

### 3.3 Moedas (top-left, terceira linha)

| Campo | Valor |
|---|---|
| Nome | `CoinsText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -144) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Initial text | `Coins 0` |

### 3.4 Tier (top-right)

| Campo | Valor |
|---|---|
| Nome | `TierText` |
| Anchor preset | Top-Right |
| Anchored Position | (-24, -24) |
| Width × Height | 200 × 60 |
| Alignment | Top-Right |
| Initial text | `Tier 0` |

> **Dica anchor**: na barrinha de Anchor Presets, segure **Shift** pra
> alinhar pivot e **Alt** pra alinhar posição junto. Mais rápido.

---

## 4. Criar `_GameTimer` GameObject

1. Hierarchy → clique direito → **Create Empty**.
2. Renomeie pra `_GameTimer`.
3. Add Component → **Game Timer**
   (`RailSwitchMVP/Core/GameTimer.cs`).
4. Não precisa preencher campos — funciona standalone.

---

## 5. Criar `_HUD` GameObject + HUDController

1. Hierarchy → clique direito → **Create Empty**.
2. Renomeie pra `_HUD`.
3. Add Component → **HUD Controller**
   (`RailSwitchMVP/UI/HUDController.cs`).
4. Inspector → **HUD Controller**:
   - `Time Text` → arraste `_HUD_Canvas/TimeText` da hierarchy.
   - `Distance Text` → arraste `_HUD_Canvas/DistanceText`.
   - `Coins Text` → arraste `_HUD_Canvas/CoinsText`.
   - `Tier Text` → arraste `_HUD_Canvas/TierText`.
   - Demais campos (Timer, Player, Difficulty, Coin Manager) — **deixe vazios**.
     O script auto-resolve no Start via `Instance` singletons.

> **Por que o HUD_Controller fica num GameObject separado** (e não no
> Canvas)? Pra não acoplar lógica ao GameObject visual. Quando a tela de
> Game Over (Iter 3) entrar, ela é outro Canvas/Panel — fica mais limpo.

---

## 6. Play Test

Salve a cena e dê **Play**.

### 6.1 Tempo
- `TimeText` mostra `Time 00:00`, cresce a `00:01`, `00:02`, ...
- ✅ Validação: formato `mm:ss`, atualiza continuamente.

### 6.2 Distância
- `DistanceText` mostra `Dist 0 m` no início, vai pra `Dist 1 m`, `Dist 2 m`...
- Distância é a partir do START (não Z absoluto). Player começa exibindo 0m
  mesmo que esteja em world Z=5.
- ✅ Validação: cresce em metros inteiros, sem casas decimais.

### 6.3 Moedas
- `CoinsText` mostra `Coins 0` no início. Ao pegar uma moeda → `Coins 1`, etc.
- ✅ Validação: incrementa imediatamente ao tocar moeda.

### 6.4 Tier
- `TierText` mostra `Tier 0` no início.
- Aperte `T` → muda pra `Tier 1`. Aperte de novo → `Tier 2`. Etc.
- ✅ Validação: atualiza ao trocar tier (debug ou progressão natural).

### 6.5 Game Over congela tempo
- Cause um Game Over (entre em DeadEnd, OutOfBounds, ou bata num obstáculo).
- Player para. `TimeText` **congela** no valor da morte.
- ✅ Validação: tempo não passa enquanto player tá morto.

### 6.6 Reset (R) não muda o tempo
- Aperte `R` (debug reset de dificuldade). Tier volta pra 0.
- **Importante:** o tempo do `GameTimer` NÃO reseta com `R` — só com a tela
  de Restart da Iter 3 (que vamos fazer depois). `R` é só dificuldade.
- ✅ Validação: o tempo continua crescendo após `R`. Tier vai pra 0.

---

## 7. Commit

```
git add Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(mvp2-iter2): HUD com tempo, distância, moedas e tier"
```

---

## 8. Troubleshooting

**Texts aparecem com fonte rosa/magenta (sem material):**
- TMP Essentials não foi importado. Window → TextMeshPro → Import TMP
  Essential Resources.

**HUD não atualiza nenhum dos campos:**
- Confirme que os 4 refs estão arrastados no `HUDController` (Inspector
  do `_HUD`).
- Confirme que `_GameTimer`, `_DifficultyManager`, `_CoinManager`, `Player`
  estão ATIVOS na cena.

**Tempo não para no Game Over:**
- Confirme que `_GameManager` está ativo na cena. O `GameTimer.Start`
  subscreve no `GameManager.OnGameOver`.

**Distância começa com valor estranho (ex: 5 m em vez de 0 m):**
- Verifique que o `HUDController.LateUpdate` está rodando após o
  `PlayerRailRider.Update` (deveria — LateUpdate sempre vem depois).
- A primeira chamada captura o baseline. Se o baseline foi capturado
  antes do `PlayerRailRider.Start` posicionar o player, a distância vai
  ficar deslocada. Fix: dê Play e veja se atualiza corretamente em runtime.

**Anchors do TMP_Text saem fora da tela ou em posições erradas:**
- Garanta o Canvas Scaler com Reference Resolution 1920×1080. Posições
  do guide são pra essa resolução.

---

## 9. Próximo passo

Após validar, prossiga pra MVP2 Iter 3 — **Tela de Game Over com Restart**.
Ver `Docs/MVP2_Plan.md` §"Iteração 3".
