# Fatia 1 — Setup no Unity Editor (passo a passo)

> Códigos prontos: `PlayerDataManager`, `HomeScreenController`,
> `GameOverController` migrado, `DebugPanelController` migrado.
> Aqui é o setup manual no Editor.

**Tempo estimado:** 20–30 min.

Antes de começar: feche tudo o que estiver aberto, deixe só o Unity ativo.
Se o Editor ainda não recompilou os scripts novos, espere terminar (canto
inferior direito: ícone redondo girando).

---

## Bloco 1 — Criar `HomeScene`

### 1.1 Criar a cena

1. `File → New Scene`.
2. Pop-up "New Scene": escolha `Empty (Built-in)` (não Standard).
3. Clique `Create`.
4. `File → Save As…` → navegue até `Assets/Scenes/` → nome `HomeScene` →
   `Save`. Confirma se o arquivo `HomeScene.unity` apareceu na pasta.

### 1.2 Criar `_PlayerDataManager`

1. No painel `Hierarchy` (esquerda), clique direito no espaço vazio →
   `Create Empty`.
2. Renomeie pra `_PlayerDataManager` (F2 ou enter).
3. Com ele selecionado, vá no `Inspector` (direita) → `Add Component`.
4. Digite `PlayerDataManager` → enter pra adicionar.
5. Verificar: o componente aparece no Inspector, campos read-only (coins
   bestDistance etc) todos em 0.

### 1.3 Criar o Canvas

1. Hierarchy → clique direito → `UI → Canvas`.
   - Isso cria automaticamente `Canvas` + `EventSystem`. **Não delete o
     EventSystem**, ele é necessário pros botões responderem.
2. Selecione `Canvas` → Inspector:
   - `Canvas` component → Render Mode: `Screen Space - Overlay` (default).
   - `Canvas Scaler` component:
     - UI Scale Mode: `Scale With Screen Size`.
     - Reference Resolution: X `1920`, Y `1080`.
     - Screen Match Mode: `Match Width Or Height`.
     - Match: `0.5`.

### 1.4 Criar os 3 TMP_Texts

> **Importante:** se for a primeira vez que usa TextMeshPro nesse projeto,
> ao criar o primeiro TMP vai pedir pra importar "TMP Essentials". Clique
> `Import TMP Essentials`. Espere terminar.

**PlayerNameText:**

1. Clique direito no `Canvas` → `UI → Text - TextMeshPro`.
2. Renomeie pra `PlayerNameText`.
3. Selecione → Inspector → Rect Transform (canto esquerdo do anchor preset):
   - Clique no quadradinho do anchor preset → segure `Alt` (pra setar
     posição também) → escolha `top center`.
   - Pos X `0`, Pos Y `-100`.
   - Width `800`, Height `60`.
4. `TextMeshPro - Text (UI)` component:
   - Text Input: `Player`
   - Font Size: `48`
   - Alignment (3×3 grid): `Center` (horizontal) + `Middle` (vertical).

**CoinsText:**

1. Selecione `PlayerNameText` → Ctrl+D pra duplicar.
2. Renomeie a cópia pra `CoinsText`.
3. Pos Y muda pra `-180`.
4. Text: `Coins: 0`. Font Size: `36`.

**BestDistanceText:**

1. Duplique `CoinsText`. Renomeie `BestDistanceText`.
2. Pos Y: `-240`.
3. Text: `Best: 0 m`.

### 1.5 Criar `PlayButton`

1. Clique direito no `Canvas` → `UI → Button - TextMeshPro`.
2. Renomeie pra `PlayButton`.
3. Rect Transform anchor preset → `middle center` (com Alt).
   - Pos X `0`, Pos Y `0`.
   - Width `320`, Height `100`.
4. Expanda o `PlayButton` → tem um filho `Text (TMP)`. Selecione:
   - Text: `JOGAR`
   - Font Size: `48`
   - Alignment: Center / Middle.

### 1.6 Criar 3 botões stub (Loja / Leaderboard / Perfil)

Vão ficar visíveis mas desabilitados (controller já cuida do
`interactable = false`). Layout sugerido: 3 botões lado a lado embaixo.

1. Clique direito no `Canvas` → `UI → Button - TextMeshPro` → renomeie
   `ShopButton`.
2. Anchor preset: `bottom center` (com Alt).
   - Pos X `-260`, Pos Y `150`.
   - Width `220`, Height `60`.
3. Filho `Text (TMP)`: texto `Loja`, font size `28`.
4. Duplique `ShopButton` 2 vezes:
   - `LeaderboardButton` → Pos X `0`, texto `Leaderboard`.
   - `ProfileButton` → Pos X `260`, texto `Perfil`.

### 1.7 Criar `_Home` com HomeScreenController

1. Hierarchy → clique direito espaço vazio → `Create Empty` → `_Home`.
2. Inspector → `Add Component` → `HomeScreenController`.
3. Atribuir os 7 refs do componente:
   - **Player Name Text** → arrastar `PlayerNameText` da hierarchy.
   - **Coins Text** → `CoinsText`.
   - **Best Distance Text** → `BestDistanceText`.
   - **Play Button** → `PlayButton`.
   - **Shop Button** → `ShopButton`.
   - **Leaderboard Button** → `LeaderboardButton`.
   - **Profile Button** → `ProfileButton`.

### 1.8 Salvar a cena

`Ctrl+S`. Verifique no canto da janela do Editor que `HomeScene` não tem
asterisco (`HomeScene*` = não salvo).

---

## Bloco 2 — Atualizar `RailSwitchMVP.unity`

### 2.1 Abrir a GameScene

1. Project window → `Assets/Scenes/RailSwitchMVP.unity` → double-click.

### 2.2 Remover `_HighScoreManager`

1. Hierarchy → procure por `_HighScoreManager` (Ctrl+F na hierarchy ou
   visualmente).
2. Se existir → clique direito → `Delete` (ou tecla Delete).
3. Se não existir, segue em frente.

### 2.3 Adicionar `_PlayerDataManager`

1. Hierarchy → clique direito → `Create Empty` → renomeie
   `_PlayerDataManager`.
2. Add Component → `PlayerDataManager`.

> Por que dois (um na Home e um aqui)? O `Awake` do PDM tem proteção: se já
> existe uma Instance (a da Home, que sobreviveu via `DontDestroyOnLoad`),
> esse segundo se destrói sozinho. Tê-lo aqui só serve pra rodar a
> GameScene diretamente em Play Mode (sem passar pela Home) durante o dev.

### 2.4 Adicionar botão HOME no GameOverPanel

1. Hierarchy → procure por `GameOverPanel` (filho de `_HUD_Canvas`).
   Expanda.
2. Lá dentro tem `RestartButton`. Selecione e `Ctrl+D` pra duplicar.
3. Renomeie a cópia pra `HomeButton`.
4. Posicione: se o painel usa Vertical Layout Group, ele já se
   reposicionou. Se não, ajuste manualmente — pode ser logo abaixo do
   RestartButton.
5. Filho `Text (TMP)` do `HomeButton` → texto `HOME`.

### 2.5 Wire o HomeButton no GameOverController

1. Hierarchy → selecione `_GameOver` (o GameObject que tem o
   `GameOverController`).
2. Inspector → componente `Game Over Controller` → procure o slot
   `Home Button` (novo).
3. Arraste o `HomeButton` da hierarchy pro slot.

### 2.6 Salvar a cena

`Ctrl+S`.

---

## Bloco 3 — Build Settings

1. `File → Build Settings…` (ou `File → Build Profiles…` no Unity 6 — abre
   janela similar).
2. Topo: "Scenes In Build" / "Scene List".
3. Se `HomeScene` não está na lista: clique `Add Open Scenes` (com
   HomeScene aberta) ou arraste o `HomeScene.unity` do Project pra dentro
   da lista.
4. Arraste-reordene: `HomeScene` no topo (index `0`),
   `RailSwitchMVP` em seguida (index `1`).
5. Fecha a janela.

---

## Bloco 4 — Validar (8 critérios)

Play Mode partindo da `HomeScene` (abra ela e clique ▶).

- [ ] **1.** Home aparece com `Player`, `Coins: 0`, `Best: 0 m`.
   Botões Loja/Leaderboard/Perfil cinzas (disabled).
- [ ] **2.** Clica `JOGAR` → carrega GameScene, jogo roda normal.
- [ ] **3.** Morre (qualquer razão) → GameOver mostra stats, dois botões
   visíveis (`Restart` e `HOME`).
- [ ] **4.** Clica `HOME` → volta pra HomeScene. `Coins: N` (N = coins do
   run). `Best: Xm` se bateu record.
- [ ] **5.** Clica `JOGAR` de novo → outra run → morre → `Restart`
   recarrega GameScene direto.
- [ ] **6.** Fecha Play Mode, abre Play de novo a partir da Home → coins
   e best persistem.
- [ ] **7.** `F1` abre DebugPanel → seção "Player data" mostra
   `Coins: N`, `Runs: M`, `Best — Dist X m | Coins Y | Tier Z | Time MM:SS`.
- [ ] **8.** Clica `Reset All Player Data` no DebugPanel → tudo zera.
   Fecha Play e reabre → continua zerado.

---

## Bloco 5 — Limpeza final (depois de validar)

1. Project window → `Assets/Scripts/RailSwitchMVP/Core/HighScoreManager.cs`
   → clique direito → `Delete` (Unity remove .cs + .meta).
2. Confirma que não aparece "Missing Script" em nenhuma cena (Console
   limpo).
3. Commit da Fatia 1.

---

## Troubleshooting

- **"Missing Script" warning** → você deletou `HighScoreManager.cs` antes
  de remover o `_HighScoreManager` da cena. Cancele o delete, remova o
  GameObject da cena, delete o script de novo.
- **`JOGAR` faz erro `Scene 'RailSwitchMVP' couldn't be loaded`** →
  RailSwitchMVP não está nas Build Settings. Adicione.
- **Coins não persistem entre Play sessions** → não tem
  `_PlayerDataManager` na HomeScene, ou você está iniciando Play direto na
  GameScene sem o PDM lá. PlayerPrefs só persiste se `Save()` for chamado;
  o Save acontece em GameOver e em OnApplicationPause/Focus.
- **HomeButton clica e não acontece nada** → ref do `Home Button` no
  `GameOverController` não foi arrastada. Refazer o passo 2.5.
- **Botão `JOGAR` não responde no clique** → o EventSystem foi deletado.
  Hierarchy → clique direito → `UI → Event System`.
- **Posições da UI ficam estranhas em resoluções diferentes** → confira o
  `Canvas Scaler` (passo 1.3): Match `0.5`, Reference Resolution `1920×1080`.
