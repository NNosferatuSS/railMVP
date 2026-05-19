# MVP2 — Iteração 3: Tela de Game Over com Restart

**Objetivo:** fechar o loop **morte → retry**. Hoje o jogo só loga Game Over
no Console; agora aparece uma tela mostrando a razão (`DeadEnd`/`OutOfBounds`/
`HitObstacle`), stats finais, e botão Restart pra recomeçar do zero.

Pré-requisitos: MVP2 Iter 2 validada (HUD funcionando com TMP).

---

## 1. Estrutura do GameOverPanel

A tela é um **filho do `_HUD_Canvas`** (mesmo Canvas do HUD principal).
Vamos construir uma árvore assim:

```
_HUD_Canvas
├─ TimeText, DistanceText, CoinsText, TierText  (já existem)
└─ GameOverPanel                                 ← (vamos criar — desativado por padrão)
   ├─ Background  (Image preta semi-transparente full-screen)
   └─ Center
      ├─ Title       "GAME OVER"
      ├─ ReasonText  "Dead End"
      ├─ TimeText    "Time: 01:23"
      ├─ DistanceText "Distance: 487 m"
      ├─ CoinsText   "Coins: 42"
      ├─ BestTierText "Best Tier: 3"
      └─ RestartButton
         └─ Text (TMP)  "Restart"
```

---

## 2. Criar GameOverPanel + Background

1. Hierarchy → clique direito **em `_HUD_Canvas`** → **Create Empty** child.
2. Renomeie pra `GameOverPanel`.
3. No RectTransform: clique no Anchor Preset → segure **Alt** → clique no
   **Stretch / Stretch** (canto inferior direito). Isso faz o painel ocupar
   o Canvas todo.
4. Como filho de `GameOverPanel`, **UI → Image**. Renomeie pra `Background`.
   - Anchor: stretch / stretch (mesmo padrão).
   - **Source Image**: deixa vazio (None).
   - **Color**: preto com Alpha ~180 (`#00000088B4`). Cria o "véu" escuro.

---

## 3. Container central (com Vertical Layout)

1. Como filho de `GameOverPanel` (mesmo nível do Background), **Create Empty**.
   Renomeie pra `Center`.
2. RectTransform: Anchor preset = **Middle / Center**. Width 600, Height 500.
3. Add Component → **Vertical Layout Group**:
   - Padding: 20 / 20 / 20 / 20
   - Spacing: 12
   - Child Alignment: Upper Center
   - Control Child Size: Width ✅, Height ❌
   - Use Child Scale: ambos ❌
   - Child Force Expand: Width ✅, Height ❌
4. Add Component → **Content Size Fitter**:
   - Vertical Fit: **Preferred Size** (faz o Center se ajustar à altura
     do conteúdo).

> Vertical Layout + Content Size Fitter empilha os filhos automaticamente,
> sem você ter que ajustar Y de cada um.

---

## 4. Filhos do Center (Title, Reason, 4 stats, Button)

**Para cada um abaixo:**
- Right-click no `Center` → **UI → Text - TextMeshPro** (para os textos)
  ou **UI → Button - TextMeshPro** (pro botão).
- Renomeie, ajuste tamanho de fonte conforme tabela.
- Alignment: Center / Middle.

### 4.1 Textos

| Nome | Font Size | Initial Text | Notas |
|---|---|---|---|
| `Title` | 64 | `GAME OVER` | Cor: vermelho `#FF4040` ou branco — escolha |
| `ReasonText` | 36 | `Reason` | Cor branco |
| `TimeText` | 28 | `Time: 00:00` | branco |
| `DistanceText` | 28 | `Distance: 0 m` | branco |
| `CoinsText` | 28 | `Coins: 0` | branco |
| `BestTierText` | 28 | `Best Tier: 0` | branco |

> O initial text é só pra você ver o layout no Editor — no Play os valores
> são substituídos pelo GameOverController.

### 4.2 Botão Restart

1. Right-click `Center` → **UI → Button - TextMeshPro**. Renomeie pra `RestartButton`.
2. No RectTransform: Width 200, Height 60. Vertical Layout vai
   centralizar/expandir conforme as settings.
3. No filho `Text (TMP)` do botão:
   - Text: `Restart`
   - Font Size: 28
   - Alignment: Center / Middle

> Você pode customizar o `Image` do botão (cor, sprite). Default cinza
> claro funciona — polish depois.

### 4.3 Desativar o GameOverPanel

1. Selecione `GameOverPanel`.
2. No Inspector, **destique a checkbox ao lado do nome** (no topo, antes do
   nome do GameObject). Isso desativa o GameObject — ele NÃO aparece no Play
   até alguém ativar via script.

---

## 5. Criar `_GameOver` GameObject + GameOverController

1. Hierarchy → clique direito → **Create Empty** (na raiz da cena, NÃO
   filho do Canvas).
2. Renomeie pra `_GameOver`.
3. Add Component → **Game Over Controller**
   (`RailSwitchMVP/UI/GameOverController.cs`).
4. Inspector → preencha os refs:

| Campo | Valor (arrastar da hierarchy) |
|---|---|
| `Panel` | `_HUD_Canvas/GameOverPanel` |
| `Reason Text` | `_HUD_Canvas/GameOverPanel/Center/ReasonText` |
| `Time Text` | `_HUD_Canvas/GameOverPanel/Center/TimeText` |
| `Distance Text` | `_HUD_Canvas/GameOverPanel/Center/DistanceText` |
| `Coins Text` | `_HUD_Canvas/GameOverPanel/Center/CoinsText` |
| `Best Tier Text` | `_HUD_Canvas/GameOverPanel/Center/BestTierText` |
| `Restart Button` | `_HUD_Canvas/GameOverPanel/Center/RestartButton` |
| Singletons (Timer/Player/Difficulty/Coin Manager) | **deixar vazios** (auto-resolve) |

> O `Game Over Controller` no `_GameOver` chama `panel.SetActive(false)` no
> Start, então mesmo que você tenha esquecido de desativar o GameOverPanel
> manualmente, ele desativa em runtime.

---

## 6. Verificar Scenes In Build

1. **File → Build Profiles** (ou **Build Settings** dependendo da versão).
2. Em "Scenes In Build", garanta que `RailSwitchMVP_Scene` está adicionada.
   Se não, abra a cena e clique **Add Open Scenes**.

> Isso é necessário porque o Restart usa `SceneManager.LoadScene(buildIndex)`,
> e só funciona pra cenas listadas. Se sua cena não estiver, vai dar erro
> "Scene not loaded" no Console.

---

## 7. Play Test

Salve a cena e dê **Play**.

### 7.1 Game Over por DeadEnd
- Aperte `T` 1 ou 2 vezes pra ter mais decoys, e depois entre num decoy
  que vira beco sem saída.
- ✅ Tela aparece com `Reason: Dead End`.
- ✅ Stats batem com os do HUD no momento da morte (time, distance, coins, tier).
- ✅ HUD do canto fica congelado (player parou, tempo parado).

### 7.2 Game Over por OutOfBounds
- Force um switch que aponte pra fora do grid (em lane 0 com Switch Left).
- ✅ Tela com `Reason: Out of Bounds`.

### 7.3 Game Over por HitObstacle
- Em Tier 2+, entre num decoy com cubo vermelho propositalmente.
- ✅ Tela com `Reason: Hit Obstacle`.

### 7.4 Restart pelo botão
- Clique no botão **Restart**.
- ✅ Cena recarrega. Player começa do zero (Time 00:00, Dist 0m, Coins 0, Tier 0).

### 7.5 Restart pela tecla R
- Cause outro Game Over. Aperte **R** sem clicar no botão.
- ✅ Mesmo comportamento — cena recarrega.

### 7.6 R durante o jogo continua sendo Reset Difficulty
- Após Restart, jogue normalmente. Aperte `T` 3x pra ir pro Tier 3.
- Aperte `R` durante o jogo (player vivo).
- ✅ Tier volta pra 0 (NÃO recarrega cena). HUD mostra `Tier 0`.
- Isso confirma que o `DifficultyDebugController` ainda funciona enquanto
  joga, e o `GameOverController` só reage quando a tela está ativa.

### 7.7 Esc fecha o jogo (opcional, não implementamos ainda)
- Não tem ainda — `Esc` no Editor para o Play. Em build, não faz nada.

---

## 8. Commit

```
git add Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(mvp2-iter3): tela de Game Over com stats e restart"
```

---

## 9. Troubleshooting

**Tela aparece preta inteira sem texto:**
- Você não criou os filhos do `Center`, ou o Vertical Layout Group está
  configurado errado. Verifique que os 6 textos e o botão são filhos diretos
  de `Center` (não de `Background`).

**Tela aparece SEM o fundo escuro:**
- Verifique o `Image` do `Background` — a cor com Alpha > 0.

**Botão Restart não faz nada:**
- O `Restart Button` no `_GameOver` Inspector tá vazio?
- O componente Button no GameObject não foi adicionado? (Use UI → Button -
  TextMeshPro, não só TextMeshPro.)

**R durante Game Over reseta dificuldade em vez de recarregar:**
- O `DifficultyDebugController.Update` deve checar `GameManager.IsPlaying`.
  Confirme que você está rodando o build mais recente do código (commit
  desta iteração).

**Cena reload dá erro "Scene 'X' not loaded":**
- A cena não está em Build Settings. Veja seção 6.

**Player continua se movendo durante a tela:**
- Confirme que `Time.timeScale = 0` foi setado. Inspector do `_GameOver`,
  componente Game Over Controller, deve mostrar `Is Showing = true` quando
  a tela tá ativa. Se não tá, o `OnGameOver` event não chegou — verifique
  refs e GameManager na cena.

**Tempo continua passando no HUD:**
- O GameTimer já deveria estar pausado por causa do `GameManager.OnGameOver`
  (subscribe na Iter 2). Time.timeScale = 0 é redundância, mas adiciona
  pausa visual completa (coin spin, etc.).

---

## 10. Próximo passo

Após validar, partimos pra MVP2 Iter 4 — **Power-ups + Barreira**.
Ver `Docs/MVP2_Plan.md` §"Iteração 4".
