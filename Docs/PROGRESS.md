# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-06-02 (sessão 3) — **Sistema de Gems + Reorganização de prefabs.** Commits `b9e16cc` pushados na `main`. Feito nesta sessão: (1) **GemPickup.cs** novo script coletável; (2) spawn de gem na pista (critical path, `gemSpawnChance`=0.5%, `gemMinRowGap`=30, campo `gemPrefab` no gerador); (3) `Resource_Gem.prefab` + `Verde - Gem.mat` criados no Unity; (4) **Game Over** rastreia gems da run (pickups + 1 gem/1000 m de distância); (5) **Home** exibe saldo de gems (`gemsText`); (6) **Daily Login milestones**: dia 3 = +2 gems, dia 7 = +5 gems; (7) **Missões com gem reward**: daily "1000m" e "run sem power-up" = +1 gem; weekly "2500m" e "3 minutos" = +3 gems — `MissionDef.RewardCurrency` + `GrantReward()` + `MissionEntryUI` atualizado; (8) **Login streak panel**: `LoginDayEntryUI.cs` + `LoginStreakPanelController.cs` + wiring no `HomeScreenController` — **código pronto, setup no Unity pendente** (próxima sessão). Doc: `Docs/LoginStreak_Panel_Setup.md`. (9) Prefabs reorganizados de `RailSwitchMVP/` → `Spawnables/`; typo `Debugg_LaneSwap` → `Debuff_LaneSwap` corrigido; Layer Lab importado. **Anterior (2026-06-02, sessão 2):** Overhaul de power-ups + gating + morte no switch + QoL de editor. Commits `64f0f27` + `907efca` pushados na `main`. Feito nesta sessão: (1) **Inventário/active-item REMOVIDO** — `TimeFreeze` virou `PowerUpType` instantâneo, consumido na colisão; `ActiveItemSlot`/`ActiveItemType` deletados, botão USE/slot do HUD removidos (GameObject `Use` órfão deletado da cena). (2) **Sem stacking** — coletar power-up RENOVA (Shield = 1 carga; durações resetam). (3) **Gating de spawn** (power-ups E hazards): gap global (`RailGenConfig.powerUpMinRowGap`=3 / `hazardMinRowGap`=0) + cooldown por tipo (`PowerUpWeight.cooldownRows` / `HazardWeight.cooldownRows`). (4) **Shield+Barrier** = desaceleração da velocidade do player com decay (`PlayerRailRider.ApplyImpactSlowdown`); slow-mo global (`PlayerCameraRig.ImpactSlowmo`) mantido pra reuso. (5) **Game Over (dead-end/OOB) no FIM da transição do switch**, não no fim do trilho; player congela onde morre (`_deathPending`, sem snap-back). (6) **Editor:** botão "Open" em todo campo de SO (`ScriptableObjectFieldDrawer`); descrições de hazards/power-ups (`HazardPool.Describe`/`PowerUpPool.Describe` + InfoBox no inspector). (7) **Ângulo da seta do switch configurável** (`RailGenConfig.switchArrowDegreesPerStep`=45, Control Panel→Generation; só visual). Docs: `PowerUp_NoStack_SpawnGating_Setup.md`, `Editor_OpenSO_Button.md`, `PowerUpRandom_ShieldSlowmo_Setup.md`(§2). **Pendências de Unity:** setar `powerUpMinRowGap`/`hazardMinRowGap` e `cooldownRows` nos assets (vêm 0); bindar `TimeFreeze→PowerUp_TimeFreeze` no gerador + pôr nos pools pra spawnar; `decoyCoinChance` nos tiers.

**Anterior (2026-06-02, sessão 1):** **RailSwitch Control Panel** (EditorWindow Odin em `Tools → RailSwitch → Control Panel`, agrega os SOs de config: Difficulty/Generation/Revive/Pools; v1 esqueleto validado, v2 tabela de tiers + v3 Play Tools pendentes) + **override de tilt/FOV por tier** (`DifficultyTier.overrideCameraAngle`/`cameraTilt`/`cameraFieldOfView`, default off = usa global; `[ShowIf]` Odin runtime; header do tier agora mostra `zoom`/`FOV` no lugar de `pop %`). **TriInspector REMOVIDO** do manifest (projeto já tem Odin Inspector). Doc: `Docs/ControlPanel_CameraOverride_Setup.md`. Em planejamento antes: **fundação de economia** (CurrencyManager facade + gems com sync Supabase). Anterior (2026-05-29): Sessão grande. **Progressão Adaptativa** (Camadas 1+3 + rework de câmera + XP no sync Supabase), **leaderboard global** de best distance (aba no painel), e extras de gameplay: **Mystery Box** (power-up random), **shield slow-mo** de impacto, **decoyCoinChance** por tier. Commits até `b683f91` pushados; extras aguardando wiring no Unity. TriInspector REMOVIDO (projeto já tem Odin). **XP→Supabase ✅ e leaderboard global ✅ concluídos (2026-06-02).** **Pendente:** wiring dos extras (Mystery Box prefab/pool, `decoyCoinChance` nos tiers — user vai ajustar), validar Slot System no Unity, Camada 2, polish UI C3. Anterior (2026-05-28): **Sessão de difficulty tools.** `DifficultyTierDrawer`: foldouts com label `Tier N — X m | speed Y | A-B/row | pop Z%` no Inspector. Tier Lock (F3 configurável): trava no tier atual ao pressionar, destrava voltando ao auto-advance por distância; HUD exibe `tierText` em vermelho quando travado. `rowsAhead` por tier em `DifficultyTier` — RailManager usa tier > config como fallback; SpawnOverride ainda tem prioridade máxima. Defaults 6→18 rows no asset. Anterior: **Sessão de tuning tools.** Slot System (Slices 1+2) implementado: coins/hazards/power-ups num grid de slots discretos (default 5), coin spawner skipa reservados pra stride uniforme, coin count vira range `[min, max]` por tier. + `CoinPlacement` enum (UniformGrid/RandomFree, default RandomFree). + `PlayerCameraRig.followLateral` toggle (default false — câmera não segue lateralmente, experimento). + `DifficultyManager.CurrentTier` lê live do SO no Editor (`#if UNITY_EDITOR`) pra que edits no Inspector propaguem sem precisar transição de tier. + arquivos `HazardPool`/`PowerUpPool`/`IValidatedConfig`/`ValidatedConfigInspector` finalmente commitados (commit anterior `c139450` referenciava mas tinha esquecido os .cs). Detalhes em `Docs/SlotSystem_Refactor.md`. Anterior: Fatia 5 — Rewarded Ads ✅ VALIDADA.
**Engine:** Unity 6000.4.7f1 (6.4 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)
**Tags:** `v0.1.0-mvp` (MVP1), `v0.2.0-mvp2` (MVP2)

---

## 🟢 Próxima sessão: começe AQUI

**Estado: sistema de gems completo (código), login streak panel (código pronto, Unity pendente), missões com gem reward, prefabs organizados em `Spawnables/` — tudo commitado (`b9e16cc`).**

### ⏳ Passo 1 — Montar o Login Streak Panel no Unity (começe aqui)

Guia completo em `Docs/LoginStreak_Panel_Setup.md`. Resumo:

1. Na **HomeScene**, criar `_LoginStreakPanel` com hierarquia:
   `Background → Card → Title + DaysRow (7× LoginDayEntryUI) + ClaimButton + CloseButton`
2. Adicionar `LoginStreakPanelController` no `_LoginStreakPanel` e arrastar os 7 cards + botões.
3. Criar botão "Ver Streak" na Home e arrastar no `HomeScreenController → Login Streak Button/Panel`.
4. Testar: abrir painel, ver 7 dias coloridos, reclamar, ver dia virar verde.

### ⏳ Passo 2 — Pendências de Unity de sessões anteriores (após streak panel)

- `RailGenConfig_Default`: setar `powerUpMinRowGap` (asset vem 0, recomendado 3) e `hazardMinRowGap`.
- `DifficultyConfig_Default`: setar `decoyCoinChance` nos 6 tiers (vem 0 = decoys sem moedas).
- Bindar `TimeFreeze → PowerUp_TimeFreeze` no `ProceduralRailGenerator` + colocar nos pools.
- `gemPrefab` no `ProceduralRailGenerator` → arrastar `Resource_Gem`.
- `cooldownRows` nas entradas de `HazardPool`/`PowerUpPool` (vêm 0, opcional).

### ⏳ Próximas frentes (decidir após pendências)

1. **Camada 2 — Head Start** — pular pro tier mais alto antes da run (coins/ad).
2. **Control Panel v2** — tabela curada de tiers via `[TableList]` Odin.
3. **Polish UI** — fade/scale do overlay de revive, animação countdown, player piscando no grace.

### ✅ Progressão Adaptativa — Camadas 1 e 3 + câmera (2026-05-29)

Spec em `Docs/RailSwitch_AdaptiveProgression.md` (3 camadas; objetivo: matar o
"re-onboarding tax" — veterano não recomeça sempre do tier lento e fácil).

- ✅ **Camada 1 — Speed Floor por account level** (`f74ac78`). XP/level no
  `PlayerDataManager` (`AddXP`/`ComputeLevelFromXP`/`XpForNextLevel` + debug L/K/J),
  `StartingTierRule`+`GetStartingTierIndex` no DifficultyConfig, rampa de starting tier
  pós-warmup no DifficultyManager (1º degrau imediato; ao atingir o piso, adianta a
  distância pro trigger). `+XP`/`LEVEL UP` no GameOver, `Lv. N` na Home. XP por run =
  `floor(dist/10)+coins+missões*50`. Fix: distância do warmup não conta mais no score.
- ✅ **Rework de câmera** (`f74ac78`). `PlayerCameraRig` mira no player (player travado
  na tela em qualquer zoom/tier); zoom = distância única por tier
  (`DifficultyTier.cameraZoom`); `cameraLookAhead` virou fator de compensação (offset
  escala com o zoom); **ortográfica removida**.
- ✅ **Camada 3 — Continue após Morte (revive)** (`235549f`). `ReviveController`
  intercepta o game over (`GameManager.TryOfferContinue`/`ConfirmGameOver`), overlay
  ad/coins + countdown; revive recua metros pro critical path + grace de invencibilidade
  (cobre letal/dead-end/oob). `ReviveConfig` SO. Setup: `Docs/Revive_Camada3_Setup.md`.

### ✅ XP / account level → Supabase (2026-06-02)

Concluído. Coluna `account_xp` criada em `public.players` e código integrado:
`PlayerRemoteState.account_xp`, `CopyToRemoteState` (push) + `ApplyRemoteState`
(pull com recálculo de level via `ComputeLevelFromXP`, last-write-wins igual coins).
Doc: `Docs/XP_Supabase_Sync_Setup.md`.

### ✅ Leaderboard global — best distance (validado, funcionando)

Aba no painel existente reusa `players.best_distance` via RPCs. SQL rodado e
wiring feito.

### ✅ Inventário/Active Item REMOVIDO (2026-06-02)

Diretriz do user: **todo power-up é consumido na colisão**, sem slot pra usar depois.
- `TimeFreeze` virou `PowerUpType` normal (instantâneo via `PowerUpManager.GrantTimeFreeze`
  → `TimeFreezeController.TryActivate`). Spawnável por pool e MysteryBox.
- **Deletados:** `ActiveItemSlot.cs` (+ enum `ActiveItemType`). Removido o input de Space,
  o slot do HUD (`activeItemText`), o botão USE mobile e a seção "Active item slot" do debug.
- `ActiveItemInputHandler` mantido **só pro Teleport** (Shift+←/→, power-up passivo).
- **Ajustes Unity pendentes:** (1) deletar GameObject `_ActiveItemSlot ` da cena (script
  faltando); (2) bindar `TimeFreeze → PowerUp_TimeFreeze` no ProceduralRailGenerator +
  pôr nos pools pra ele spawnar; (3) opcional: apagar o TMP do slot no HUD e o botão USE mobile.

### ✅ Power-ups: sem stack + gating de spawn (2026-06-02)

Diretriz do user. Doc: `Docs/PowerUp_NoStack_SpawnGating_Setup.md`.
- **Sem stack:** coletar power-up ativo apenas RENOVA. Shield = 1 carga (HUD sem `x{n}`);
  duração reseta (`= tiles`, era `+= tiles`). Debuffs inalterados (hazards).
- **Gap global:** `RailGenConfig.powerUpMinRowGap` (default 3) — após qualquer power-up,
  N rows sem nenhum. Control Panel → Generation.
- **Cooldown por tipo:** `PowerUpWeight.cooldownRows` em cada entrada de `PowerUpPool`
  (default 0). Control Panel → Pools → Power-ups. Filtra o tipo do sorteio por N rows.
- Gerador: `_lastPowerUpRow` + `_lastRowByType` + `IsPowerUpOnCooldown`. Override F2
  respeita gap global, ignora cooldown por tipo.
- **MESMO gating pra HAZARDS:** `RailGenConfig.hazardMinRowGap` (default **0** = sem
  mudança; subir deixa mais fácil) + `HazardWeight.cooldownRows` por entrada de `HazardPool`.
  Gerador: `_lastHazardRow` + `_lastRowByHazard` + `IsHazardOnCooldown`.
- **Validar Unity:** abrir `RailGenConfig_Default` e setar `powerUpMinRowGap` (asset vem 0)
  e, se quiser, `hazardMinRowGap`.

### ✅ Shield impact (decay) + descrições de pools + botão Open SO (2026-06-02)

- **Shield+Barrier:** trocado o slow-mo GLOBAL (timeScale) por desaceleração da
  **velocidade do player com decay** (`PlayerRailRider.ApplyImpactSlowdown`, SmoothStep).
  Tunables em `RailGenConfig` ("Shield impact — player slowdown"): `shieldImpactSpeedFactor`
  (0.3) + `shieldImpactRecoverSeconds` (1.0). `PlayerCameraRig.ImpactSlowmo` mantido pra reuso.
- **Descrições de hazards e power-ups:** `HazardPool.Describe` / `PowerUpPool.Describe`
  (fonte única) + InfoBox `[OnInspectorGUI]` em cada pool listando o que cada tipo faz
  (aparece no Control Panel).
- **Botão "Open" em campos de SO:** `ScriptableObjectFieldDrawer` global — todo campo de SO
  ganha botão que abre as properties numa janela. Caveat: Control Panel (Odin) não pega.

### ✅ Game Over no fim da transição do switch (2026-06-02)

Antes a morte (DeadEnd/OutOfBounds) disparava no instante em que o player cruzava o
`EndPoint` do tile (fim do trilho reto), em `TryEnterGap`. Agora o player **percorre o
gap seguindo a direção do switch** (rumo a onde o tile deveria estar / pra fora da pista)
e o game over dispara **no fim dessa transição** (`TickGap` quando `gapProgress >= 1`).
Leitura visual: você vê o switch levar pro vazio antes de morrer.
- `PlayerRailRider`: `_gapIsFatal`/`_gapFatalReason` + `BeginFatalGap(lane, reason)`
  (usa `TrackTile.ComputeWorldPosition` pro destino-fantasma, igual ghost flight).
- Vale pra DeadEnd E OutOfBounds (borda lateral: anda pra fora e cai).
- Ghost/grace pós-revive continuam interceptando antes (sobrevivem). Distância do trecho
  fatal **não conta** no score (congelada durante o gap fatal).
- **Fix snap-back:** flag `_deathPending` congela o player onde morreu (sem voltar pro fim
  do trilho durante a oferta de revive, que mantém o state em Playing). Limpo no `RespawnAt`.

### ✅ Ângulo da seta do switch configurável (2026-06-02, `907efca`)

`RailGenConfig.switchArrowDegreesPerStep` (default 45, range 0–90, header "Switch (visual)",
Control Panel → Generation). `SwitchController` lê o ângulo do config injetado pelo gerador
no spawn (`SetConfig`), fallback 45°. **Só visual** — `TargetLane` usa o estado inteiro, não
o ângulo. Doc/diretriz: todo param novo funciona no Control Panel.

### ⏳ Próximos passos (ordem combinada)

1. **Camada 2 — Head Start.** Pular pra tier mais alto antes do run (coins/ad),
   desbloqueio por best distance ≥ 500. `HeadStartController` + UI pré-run.
   `DifficultyManager.StartRunWithAdaptiveTier(level, headStartOverride)` já aceita o override.
2. **Polish de UI da Camada 3.** Fade/scale do overlay, animação do countdown,
   feedback visual do grace (player piscar).

> ⚠️ **Pré-launch:** `AdsManager.useMockAds` está **true** (backend sem inventory) —
> desligar mock + `testMode` antes de build de produção. Ver `Docs/Pre_Launch_Checklist.md`.

---

_As seções abaixo são de sessões anteriores — setup/validação no Unity ainda pendente
em alguns itens (slot system, mobile input, `_SpawnOverride`)._

### Slot System — Slices 1 + 2 ⏳ implementados 2026-05-28, falta validar

Refactor da geração procedural pra usar **slots discretos** ao longo do tile
(default 5, configurável em `RailGenConfig.coinSlotsPerTile`). Coins, hazards
e power-ups agora ocupam slots — quem reserva um índice impede que outro
spawne em cima. **Resolve o problema de overlap visual** entre moeda no centro
e hazard/power-up no centro.

**Arquivos tocados:**

- `Config/RailGenConfig.cs` — + enum `SlotPlacement` (CenterSlot/RandomFree),
  + `coinSlotsPerTile` (5), `coinSlotPadding` (0.1), `hazardSlotStrategy`,
  `powerUpSlotStrategy`. + validações.
- `Track/TrackTile.cs` — + helper `GetSlotPosition(slotIndex, totalSlots,
  padding, height)` que centraliza o Lerp antes duplicado nos 3 spawners.
- `Track/CoinSpawner.cs` — reescrito. Nova assinatura
  `Spawn(targetCount, totalSlots, padding, reservedSlots)`. Distribuição
  uniforme entre slots livres (endpoints inclusos com count > 1). Removidos
  campos legacy: `padding`, `startPoint`, `endPoint`, `spawnOnStartCount`,
  `isCriticalPath`, `Awake`/`Start`/`ResolvePointsFromTile`.
- `Track/ObstacleSpawner.cs` / `PowerUpSpawner.cs` — nova assinatura
  `Spawn(prefab, slotIndex, totalSlots, padding)`. Removidos `startPoint`,
  `endPoint`, `Awake`/`ResolvePointsFromTile`.
- `Core/ProceduralRailGenerator.cs` — em `GenerateRow`, **ordem invertida**:
  hazard → reserva slot → power-up → reserva → coins com `reservedSlots`.
  + helper `PickSlot(strategy, totalSlots, reserved)` com fallback gracioso
  (busca livre mais próximo do centro pra CenterSlot, sample aleatório pra
  RandomFree).
- `RailGenConfig_Default.asset` — + 4 campos novos.

**Decisões fechadas com user:**
1. Slot count: GLOBAL no RailGenConfig (não per-tier nem per-prefab).
2. Strategy: CenterSlot/RandomFree configurável desde o slice 1 (default Center
   pra preservar o visual atual).
3. Coin min/max: 2 ints separados (`criticalCoinsMin/Max`) — vem no Slice 2.

**Próxima sessão: validar Slice 1 no Unity**

1. Abrir Unity, abrir `TrackTile_Prefab`. Unity vai warnar sobre os campos
   antigos (`spawnOnStartCount`, `isCriticalPath`, `startPoint`, `endPoint`,
   `padding`) nos 3 spawners — só salvar (Ctrl+S) e os orphans somem.
2. Selecionar `RailGenConfig_Default` → conferir que os 4 campos novos
   aparecem com `coinSlotsPerTile=5`, `coinSlotPadding=0.1`,
   `hazardSlotStrategy=CenterSlot`, `powerUpSlotStrategy=CenterSlot`.
3. Rodar uma run. Em Tier 1+ (decoys com hazards), verificar visualmente
   que **moeda + hazard não mais ocupam o mesmo X central**.
4. Com 3 coins no critical sem powerup: devem aparecer nos slots `0, 2, 4`.
5. Com 3 coins + powerup no centro (slot 2): coins vão pros slots `0, 4`
   (slot 2 skipado — stride uniforme com o powerup, total 2 coins + 1 PU).

**Slice 2 ✅ código implementado:** `DifficultyTier` agora tem
`criticalCoinsMin/Max` + `decoyCoinsMin/Max`. Sample por tile via
`Random.Range(min, max+1)` no generator. `FormerlySerializedAs` preserva
o valor antigo no Min; `OnValidate` auto-copia min→max na primeira
abertura do `DifficultyConfig_Default`. Log do `DifficultyManager` ajustado
pra mostrar `coins=[min-max]`. **Validar Unity:** abrir o asset uma vez
pra disparar OnValidate, depois ajustar os Max nos 6 tiers (default vem
igual ao min após migração).

**Slice 3 (opcional):** UI no `SpawnOverrideController` (F2) pra trocar slot
strategy + coin range em runtime. Só se for útil pro playtest.

---

### Backlog anterior (intacto)

Estado: gameplay completo (MVP1+MVP2+pós-MVP2 todo) + camada de PROGRESSÃO
(Fatias 1-5 fechadas). Detalhes em `Docs/Progression_Implementation_Plan.md`.

### Próxima sessão: setup Ads + testar Fatia 5

**Fluxo:**
1. Setup conta Unity Ads no dashboard + linkar Project (`Docs/AdsFatia5_Setup.md` §"Setup conta").
2. Criar `_AdsManager` GameObject na HomeScene + preencher Game IDs.
3. Adicionar botão "Watch Ad +N coins" no `_GameOver` panel da RailSwitchMVP scene.
4. Testar fluxo chest + GameOver com Test Mode = true (Editor).
5. Rebuild Android + validar device com test ads.

**Depois disso, opções pro próximo passo:**
- **Spec §11** — Daily Challenge → Supabase backend → Leaderboard.
- **Gameplay polish** — audio, modelos visuais, animações UI.
- **Mais features de meta** — input UI pra PlayerName, novos personagens, etc.

### Fatia 5 — Rewarded Ads ✅ VALIDADA 2026-05-24

- `com.unity.ads` 4.4.2 adicionado ao manifest. Escolhido **classic Unity Ads**
  (não Mediation/LevelPlay) pra MVP — migração futura é localizada.
- `Meta/AdsManager.cs` singleton DontDestroyOnLoad. Wrapper Unity Ads:
  `TryShowRewarded(onSuccess, onFailed)` + `IsRewardedReady` +
  `OnRewardedReadyChanged` event. Reload automático após show.
- `DailyLoginManager.ClaimChest()` — removido stub interno. Caller decide
  se passa por ad.
- `HomeScreenController.ClaimChest()` — orquestra ad → `dl.ClaimChest`.
  `RefreshChestButton` esconde botão se ad não pronto (spec §5.2).
  Sem AdsManager na cena = modo stub (fallback Editor).
- `GameOverController` — botão NOVO "Watch Ad +N coins" (não existia stub).
  1 uso por GameOver via `_doubleCoinsClaimed`. Credita +runCoins extra
  (dobra total).
- Bug fix: enum era `UnityAdsShowCompletionState` (não `UnityAdsCompletionState`).
- Doc completo passo-a-passo (incluindo dashboard, scene setup, validação
  Editor + Android) em `Docs/AdsFatia5_Setup.md`.
- Checklist de pre-launch (keystore, Privacy Policy, GDPR, Play Console) em
  `Docs/Pre_Launch_Checklist.md` — consultar quando for shipar.

### Mobile Performance ✅ passada inicial 2026-05-24 (commit `5428c00`)

Iteração após primeiro build Android mostrar fps abaixo do esperado:

- **`Core/PerformanceBootstrapper.cs`** — `RuntimeInitializeOnLoadMethod` antes
  da cena. Trava `targetFrameRate = 60`, `vSyncCount = 0`,
  `Screen.sleepTimeout = NeverSleep` em mobile. Não precisa ser anexado a GO.
- **Fix CollectibleCoin.Collect()** — cache estático do PlayerRailRider.
  Antes, cada moeda coletada chamava `FindFirstObjectByType` (scene scan).
  Com Magnet ativo, era chamado várias vezes/seg.

**Tunables não-código (Editor) sugeridos** — listados em ordem de impacto em
`Docs/Mobile_Performance.md`:
1. `Mobile_RPAsset` → Main Light Cast Shadows = OFF (maior ganho).
2. `Mobile_RPAsset` → HDR = OFF.
3. Render scale 0.7 se ainda pesado.
4. Profiler obrigatório antes de mais mudanças (não otimizar às cegas).

### Mobile Input ✅ implementado 2026-05-24 (pendente setup cena + build)

Adicionou camada de input touch + UI buttons sem mexer no keyboard. Editor
continua funcionando exatamente igual; Android ganha o equivalente em UI.

- `Input/TouchDirectionalInput.cs` — **tap zones por default** (tap em
  metade esq/dir = -1/+1). Modo Swipe alternativo via enum (testado, ruim
  em mobile típico). No-op em desktop (Touchscreen.current == null).
  Toques sobre UI são ignorados via EventSystem.RaycastAll.
- `Input/CompositeDirectionalInput.cs` — agrega N IDirectionalInput. Player
  aponta pra ele; ele tenta touch primeiro, fallback keyboard. Plug-and-play
  sem mexer no PlayerRailRider.
- `UI/MobileTouchUI.cs` — botão USE (Active Item) + 2 botões TELE ←/→.
  Auto-hide via `ActiveItemSlot.OnItemAcquired/Used` e `PowerUpManager.OnPowerUpTick/Expired`.
  `hideOnDesktop` flag opcional.
- `Core/MobileDebugButtons.cs` — 2 botões pra toggle F1/F2 em Android (onde
  não existe teclado). Auto-hide em release builds.
- `DebugPanelController.Toggle()` e `SpawnOverrideController.Toggle()` públicos
  pra serem ligados em Button.onClick.

**Setup pendente:** componentes na cena + UI buttons no HUD canvas + trocar
`inputSource` do PlayerRailRider de Keyboard → Composite. Passo-a-passo
completo + critérios de validação em `Docs/MobileInput_Setup.md`.

**Debug no Android:** Development Build em Build Settings, `MobileDebugButtons`
na cena, `adb logcat -s Unity` pra stream de logs. Detalhes no doc.

### Camera Polish ✅ implementado 2026-05-22 (pendente testar)

- Tilt range subiu 60° → 90° (`RailGenConfig.cs`).
- `cameraPositionSmoothing` tunável (default 12, separa base pos do shake).
- `PlayerCameraRig.Instance` agora singleton com `Shake(intensity, duration)`
  + presets `ShakeLight/Medium/Heavy`. Perlin noise + falloff.
- Death cam em `OnGameOver`: slow-mo `0.3x` por 1s + zoom-in com
  SmoothStep. Painel só aparece após. Shake heavy se reason == HitObstacle.
- Shake auto em tier change (light), Vortex/LaneSwap (medium),
  HitObstacle (heavy via death cam).
- Tunables todos no `RailGenConfig_Default.asset` — editáveis live.

**Setup pendente segunda:** atribuir `Rail Config` no `_GameOver` na
cena (opcional, tem fallback por reflection). Detalhes + critérios de
teste em `Docs/CameraPolish_Setup.md`.

### Backlog futuro de câmera (ainda não implementado)

- FOV dinâmico (sensação de velocidade).
- Side-bias antecipando switch.
- Warmup cam dedicada.
- Roll em lane change.

User pretende migrar pra Cinemachine eventualmente — esses ficam pra lá.

### Fatias anteriores (✅ commitadas)

### Fatia 4 — Personagens / Loja ✅ commit `125ece8` (2026-05-22)

- `CharacterCatalog` hardcoded com 3 personagens (Runner free, Neon 2500,
  Ember 5000). Spec §6.1.
- `ShopController` panel sobreposto na Home com 3 cards + popup confirma.
- `PlayerCharacterApplier` aplica cor via MaterialPropertyBlock no
  Renderer do Player (URP `_BaseColor` + Built-in `_Color`).
- `HomeScreenController.OpenShop()` + botão Loja agora ativo.
- DebugPanel: seção Characters + botões `+100`/`+1000 coins` na Player
  data (funcionam em qualquer cena).

### Fatia 3 — Daily Login + Chest stub ✅ commit `02f5920` (2026-05-22)

- `DailyLoginManager` com login (7 dias, 50→500 coins, sem streak
  penalizado) + Daily Ad Chest stub (+150/dia, ad real fica pra Fatia 5).
- Lógica de data UTC. Keys `RailMVP.DailyLogin.*` e `RailMVP.AdChest.LastDate`.
- HomeScreenController: popup automático no OnEnable se ShouldShowPopup,
  botão chest com label dinâmico.
- DebugPanel: seção Daily Login + Chest.

### Fatia 2 — MissionTracker ✅ commit `8eacb16` (2026-05-22)

- Pool de 20 daily + 10 weekly hardcoded. Rotação determinística
  `DayOfYear%20` e `ISOWeek%10`.
- 12 tipos de tracking. Auto-StartRun via SceneManager.sceneLoaded.
- Persistência via PlayerPrefs (`RailMVP.Missions.*`).
- `MissionEntryUI` + arrays no HomeScreenController.
- DebugPanel: Force/Claim, Cycle Daily/Weekly, Reset.

### Fatia 1 — Fundação ✅ commit `415656a` (2026-05-22)

- `PlayerDataManager` singleton DontDestroyOnLoad com coins, bests, runs,
  personagens. Reusa keys `RailMVP.BestX` do HighScoreManager.
- `HomeScene` placeholder com Coins/Best/Player + JOGAR + 3 stubs.
- `GameOverController` migrado + botão HOME + `CommitRunToPlayerData()`
  idempotente que credita coins e incrementa runs.
- `DebugPanelController` migrado pra seção "Player data" com Reset All.
- `HighScoreManager.cs` deletado.
- Build Settings: HomeScene index 0, RailSwitchMVP index 1.

### Backlog de gameplay (ainda válido)

- Playtesting puro pra calibrar tunables (descartado pelo user por ora).
- Setup do `_SpawnOverride` GameObject na cena (pendente).

### Resumo do que está pronto

| Sistema | Status |
|---|---|
| MVP1 core + MVP2 (obstacles/power-ups/HUD/Game Over) | ✅ Validado (tags) |
| Post-MVP2 polish (UI warning, pooling, debug tool, idea 1/2/3, ghost flyover) | ✅ Validado pelo user |
| PostMVP2.5 obstáculos (SpeedUp, LaneSwap, Vortex) | ⏳ Implementado, falta playtest |
| PostMVP2.4 pickup scripts (TimeFreeze, Teleport, AutoFollow) | ⏳ Implementado + prefabs criados |
| D — High score persistente | ⏳ Implementado, falta validar persistência entre sessões |
| Debug spawn buttons (botão Spawn ao lado de cada Grant) | ✅ Implementado, user atribuiu prefabs |
| Debug panel posição (lado direito) | ✅ |
| **Spawn Override (F2, in-runtime)** | ⏳ Implementado, falta criar GameObject `_SpawnOverride` na cena |

### Prefabs no repo (organizados PowerUp_/Debuff_)

- PowerUp_Shield, PowerUp_SlowDown (era "SlowDown"), PowerUp_Magnet, PowerUp_DifficultyReset
- PowerUp_DoubleCoins (era "2XCoins"), PowerUp_Ghost, PowerUp_LanePreview, PowerUp_CoinRadar
- PowerUp_Teleport, PowerUp_TimeFreeze, PowerUp_AutoFollow (novo)
- Obstacle_Lethal, Obstacle_Barrier (era "Barrier")
- Debuff_SpeedUp_Zone, Debuff_LaneSwap (era "LaneSwap"), Debuff_Vortex

Atribuídos nos slots do `_DebugPanel` pra os botões Spawn funcionarem.

### Próximas direções (descartadas pelo user agora, podem voltar)

- **C** — Audio (música + SFX).
- **E** — Polish visual (modelos vs primitives).
- **F** — Mobile / TouchDirectionalInput.
- **G** — Animações UI (fade-in, pulse).

Nenhuma feature nova pendente. Decide na próxima sessão.

---

## 🎉 MVP2 FECHADO

Adicionou risco/recompensa (obstáculos + 4 power-ups) e feedback visual
(HUD + Game Over). 4 iterações completas, todas validadas.

### O que ficou no jogo após MVP2

- **2 tipos de obstáculo** (Lethal + Barrier) que só spawnam em decoys.
- **4 power-ups** (Shield, SlowDown, Magnet, DifficultyReset) que spawnam
  com chances diferentes em critical vs decoy.
- **Shield absorve** qualquer obstáculo (no MVP2 implementado uniforme —
  ver discussão pós-MVP2 se quiser diferenciação Lethal vs Barrier).
- **HUD persistente**: tempo (mm:ss), distância (m), moedas, tier, +
  indicadores Shield/Slow/Magnet quando ativos.
- **Game Over screen** com razão, stats finais e botão Restart (também `R`).
- **Sistema de pickups extensível** — `PowerUpBase` abstrato facilita
  novos tipos sem mexer no manager.

### Sugestão imediata

Push das tags via GitHub Desktop (`git push --tags` ou incluir tags na
próxima sync do Desktop).

---

## Pós-MVP2 — Roadmap em andamento

Ordem combinada com user:
1. ✅ **Diferenciar Lethal vs Barrier** — commit `7d010f7`. Validado.
2. ✅ **UI warning sobre decoys perigosos** — commit `33aefd5`. Versão atual
   na mesma row. **Upgrade pendente:** mover pra 3 rows à frente, alinhado
   na lane do hazard.
3. ✅ **Pooling de tiles** — commit `108fb6a` + fix `1d72996`.
4. ✅ **Brainstorm de obstáculos/power-ups** — discutido com user.
   - **Confirmados:** SpeedUp zone, Lane Swap (F), Vortex safe (E),
     2x Coins (G), Ghost (H), Lane Preview (J), Coin Radar (K),
     Time Freeze (I) + Teleport (novo) via sistema de inventário/active items.
   - **Dropados:** Mover (A), Slow zone (D), Double Switch (L).
   - **Backlog:** Spike Strip (C).
5. ✅ **Debug tool** — commit `<próximo>`. `DebugPanelController` OnGUI F1
   toggle: power-ups grant, tier shortcuts, trigger game over, add coins.

### Próximas iterações (ordem)
- ✅ **PostMVP2.1:** UI Warning upgrade (3 rows à frente) — commit `269b947`.
- ✅ **Debug.AutoFollow:** auto-follow critical path toggle — commit `ad60c0b`.
- ✅ **PostMVP2.2:** Passive power-ups batch — commits `5152906` + Ghost fixes.
  - 2x Coins, Ghost, Lane Preview, Coin Radar.
  - CollectibleCoin aplica CoinMultiplier + scale pulse no Radar.
  - ObstacleBase skip se Ghost ativo.
  - HUDController com 4 indicadores novos + lógica de direção pro Lane Preview.
  - Debug panel com 4 grant buttons.
  - **Ghost expandido** (commits `84972b5` + `ec3903e` + `53a24f9` + `88b0b95`):
    - Atravessa OutOfBounds (clamp pra current lane = segue reto).
    - Atravessa DeadEnd (voa sobre rows vazias até encontrar tile).
    - Rescue land com lerp 0.2s se Ghost expira mid-flight.
  - Falta: criar 4 prefabs novos + 4 TMP_Texts no HUD (ver `Docs/PostMVP2_2_PassivePowerUps.md`).
- ✅ **PostMVP2.3:** Active items system — commit `<próximo>`.
  - ActiveItemSlot singleton (1 slot, substitui se pegar novo).
  - TimeFreezeController (timeScale=0 por 3s unscaled).
  - TeleportController (lateral ±1 lane via switch state).
  - ActiveItemInputHandler (Space = use).
  - PlayerRailRider.TeleportToAdjacent (preserva Z, muda só X).
  - HUDController.activeItemText (mostra "Item: TimeFreeze (Space)" ou "-").
  - DebugPanel seção "Active item slot" (Grant + Use buttons).
  - Falha não consome: Teleport sem direção, lane vazia, slot vazio = no-op silencioso.
  - Falta: setup na Editor (4 GameObjects + 1 TMP_Text). Ver `Docs/PostMVP2_3_ActiveItems.md`.
- ✅ **PostMVP2.4:** Prefabs scripts pra TimeFreeze + Teleport — commit `a4956a4`.
  - `PowerUps/TimeFreezePickup.cs` (coloca no slot).
  - `PowerUps/TeleportPickup.cs` (chama GrantTeleport tile-based).
  - Prefabs `PowerUp_TimeFreeze` + `PowerUp_Teleport` criados pelo user.
- ✅ **PostMVP2.5:** Novos obstáculos — commit `fd37105`.
  - SpeedUp Zone (debuff: +50% speed por 6 tiles, stack adiciona).
  - Lane Swap (debuff: inverte ←/→ por 2 tiles, stack reseta).
  - Vortex (instant: redireciona switch pra direção válida, modes
    OppositeOfSwitch ou PureRandom).
  - DifficultyConfig populado com curva escalada (Tier 0 = 0%, Tier 5 = 20/15/15%).
  - Scripts + prefabs + spawn paths no generator. HUD com indicadores.
- ✅ **Idea 1, 2, 3 (3 ideas):** commits `0ac24a2`, `3f63cb8` + fixes `dbbb78f` + `12a0ad1`, `922fdf2`.
  - Idea 3: Auto-follow critical path como power-up tile-based (default 5 tiles).
  - Idea 2: Trilhos coloridos por switch state (Arrow verde/vermelho em
    tempo real conforme switch aponta pra tile/vazio na próxima row).
  - Idea 1: Warmup 5 rows single-center em 0.5x speed + countdown
    "3-2-1-GO!" + input liberado pra player errar/aprender. Commit
    `1ab5ce0` removeu o input lock após primeiro feedback.
- ✅ **D — High Score persistente:** commit `a060d64`.
  - `HighScoreManager` singleton, 4 bests salvos via PlayerPrefs
    (dist/coins/tier/time).
  - `GameOverController` mostra "Current ★ (Best: X)" inline + opcional
    NewRecordText overlay "★ NEW RECORD! Distance Coins...".
  - DebugPanel button "Reset Best Scores".
- ✅ **Spawn Override (debug runtime):** commit `<próximo>`.
  - `SpawnOverrideController` singleton, OnGUI toggle **F2**, painel à direita.
  - Master toggle (OFF = comportamento clássico, ON = override).
  - Por hazard (Lethal/Barrier/SpeedUp/LaneSwap/Vortex) e por power-up
    individualmente: enabled/chance/location (Crit/Decoy/Both) + botão `solo`.
  - Global multiplier × em todas as chances.
  - Botão "Snapshot from current tier" copia valores do tier ativo.
  - `ProceduralRailGenerator` roteia decisões via `SpawnOverrideController.Instance`
    se presente, senão usa `ResolveHazardClassic`/`ResolvePowerUpClassic` (mantém
    comportamento idêntico ao anterior pra runs sem o controller).
  - **rowsAhead override** (toggle separado, slider 1–20): reduz a janela
    de streaming pra mudanças nos sliders aparecerem em poucos tiles. Quando
    reduzido, `RailManager` auto-despawna o excedente à frente e chama
    `ProceduralRailGenerator.SetPreviousCriticalLanes(...)` pra re-seed o
    gerador na row sobrevivente (sem isso, critical path drifftaria errado).
  - **Tier lock** (toggle + slider 0..tierCount-1): força e mantém o
    `DifficultyManager` num tier fixo (bypassa auto-advance). `UpdateDistance`
    checa `SpawnOverrideController.TryGetLockedTier(...)` antes do loop normal
    de progressão. Ao destravar, retoma auto-advance pela distância atual.
  - **Setup pendente:** criar GameObject `_SpawnOverride` na cena com o
    componente. Não precisa atribuir prefabs — controller descobre a lista
    de power-ups via `ProceduralRailGenerator.PowerUpPrefabs` no Start.

Ver follow-ups originais em `Docs/MVP2_Plan.md §"Pontos que NÃO entram"`.

### Pendências descartadas pelo user (não fazer agora)
- **C — Audio**: música + SFX.
- **E — Polish visual**: modelos no lugar de primitivos.
- **F — Mobile**: TouchDirectionalInput.
- **G — Animações UI**: fade-in Game Over, pulse nos ícones.

User pode pegar qualquer um se quiser depois. Mas próximo passo provavelmente
é **playtesting puro** (jogar bastante) pra calibrar tunables.

---

## MVP Parte 2 (concluído)

Plano e decisões em `Docs/MVP2_Plan.md`. Iterações:

- **Iter 1 — Obstáculos letais** ✅ validada.
- **Iter 2 — HUD básico** ✅ validada.
- **Iter 3 — Tela de Game Over** ✅ validada.
- **Iter 4 — Power-ups + Barreira** ✅ validada.

### MVP2 Iter 4 — Power-ups + Barreira
Validada em 2026-05-19.
- [x] PowerUpManager singleton com Shield/SlowDown/Magnet, OverlapSphere
      pra magnet, events pro HUD.
- [x] PowerUpBase abstract + 4 pickups concretos.
- [x] Track/PowerUpSpawner paralelo a Coin/ObstacleSpawner.
- [x] BarrierObstacle + ObstacleBase consome shield no OnTriggerEnter.
- [x] PlayerRailRider.OnTileEntered event + SpeedMultiplier.
- [x] DifficultyTier: barrier + powerUp chances em todos 6 tiers.
- [x] Generator: spawn ordem Lethal→Barrier→PowerUp (no máximo 1 por tile).
- [x] HUDController: 3 indicadores Shield/SlowDown/Magnet.
- [x] 5 prefabs novos (Barrier + 4 pickups) com materiais coloridos.
- [x] _PowerUpManager + 3 TMP_Texts no HUD.
- [x] Play test: cada power-up funciona, shield absorve, slow visível,
      magnet coleta adjacentes, stack ok, stress test ainda passa.
- [x] Commits `312f702` (código) + `380f8b5` (assets finais).
- [x] Tag `v0.2.0-mvp2`.

### MVP2 Iter 3 — Tela de Game Over com Restart
Validada em 2026-05-19.
- [x] UI/GameOverController (subscribe OnGameOver, popula 5 stats, Time.timeScale=0, Restart via SceneManager.LoadScene).
- [x] DifficultyDebugController gated em IsPlaying.
- [x] GameOverPanel filho do _HUD_Canvas, Vertical Layout + Content Size Fitter, 6 TMP_Texts + RestartButton.
- [x] _GameOver GameObject + refs assigned.
- [x] Play test: 3 razões mostram label correto, stats batem com HUD, R/Button restartam, R durante jogo ainda reseta tier.
- [x] Commit `27d6101`.

### MVP2 Iter 2 — HUD básico
Validada em 2026-05-19.
- [x] Core/GameTimer (singleton, pausa em OnGameOver, FormatMMSS).
- [x] UI/HUDController (4 TMP_Text refs, LateUpdate + eventos).
- [x] Canvas Screen Space Overlay 1920×1080 com 4 TextMeshProUGUI.
- [x] _GameTimer e _HUD GameObjects + refs.
- [x] Play test: tempo cresce em mm:ss e congela em GameOver, distância em metros, moedas e tier atualizam.
- [x] Bug encontrado durante setup: faltava IsTrigger no Coin's collider; usuário também adicionou Rigidbody no Player (kinematic) pra triggers serem confiáveis.
- [x] Commit `4b4a882`.

### MVP2 Iter 1 — Obstáculos letais
Validada em 2026-05-19.
- [x] Folder `Obstacles/` + ObstacleBase (abstract) + LethalObstacle.
- [x] Track/ObstacleSpawner.
- [x] TrackTile.Obstacles + auto-resolve.
- [x] DifficultyTier.obstacleChanceOnDecoy + 6 tiers populados (0..0.55).
- [x] Generator spawn em decoys, gate `!IsOnCriticalPath`.
- [x] Prefab Obstacle_Lethal_Prefab, TrackTile_Prefab atualizado, generator com refs.
- [x] Play test: spawn rate por tier OK, colisão dispara HitObstacle, critical limpo.
- [x] Commit `e564323`.

---

## 🎉 MVP1 FECHADO (tag `v0.1.0-mvp`)

Todos os 12 critérios de sucesso da spec §14 atendidos. Core gameloop
validado em 5 iterações.

Follow-ups pós-MVP1 originais (`Docs/Iteracao5_StressTest.md §5`) parcialmente
migrados pro MVP2 Plan. Restantes (polish/performance/persistência) ficam pra
depois do MVP2.

---

## Concluído

### Iteração 5 — Stress test
Validado em 2026-05-19.
- [x] `PlanRow` extraído de `GenerateRow` (planejamento puro sem Instantiate).
- [x] Editor/StressTestRunner com menu `Tools → RailSwitchMVP → Run Stress Test`.
- [x] 10k rows passou em **7ms** com 0 falhas. 100k rows em **66ms**.
- [x] Profiler manual confirmado pelo user (quick test).
- [x] Commit `8af72eb`.

### Iteração 4 — Dificuldade dinâmica
Validado em 2026-05-19.
- [x] DifficultyDebugController + ContextMenus + logs de tier.
- [x] DifficultyConfig_Default populado com 6 tiers (0 a 5).
- [x] Bug fix: ResetDifficulty com offset (não reanima tier no próximo frame).
- [x] Fix A: globalMaxLanes — X stable across tiers, no lateral skip on tier change.
- [x] Option B: seeded transition após reset — critical path drifta 1 lane/row pro
      centro canônico depois de R, evitando DeadEnd injusto quando o player
      reseta longe do centro.
- [x] Commits `e7ec487` (debug controller), `548457c` (Fix A), `<próximo>` (Option B).

### Iteração 3 — Geração procedural com Critical Path
Validado em 2026-05-19.
- [x] `ProceduralRailGenerator` com algoritmo §4.2.
- [x] `RailManager` reescrito (streaming spawn/despawn + bootstrap + StartTile auto).
- [x] `CoinSpawner.Awake` + spawnOnStartCount default 0.
- [x] Player aceita StartTile assigned em runtime via `SetStartTile`.
- [x] Play test: geração infinita, critical path verde, decoys laranja, FPS OK.
- [x] Commit `e96a995`.

### Iteração 2 — Switches + transição + moedas + Game Over
Validado em 2026-05-19.
- [x] 9 scripts novos (SwitchController, CoinSpawner, RowData, RailManager mínimo, GameManager, CollectibleCoin, CoinManager, IDirectionalInput, KeyboardDirectionalInput).
- [x] TrackTile + PlayerRailRider atualizados.
- [x] Cena com 3×3 (R2L0 omitido), prefab Coin_Prefab, prefab TrackTile_Prefab atualizado.
- [x] Play test: setas funcionam, transição suave, DeadEnd em R2L0, OutOfBounds nas bordas, moedas somam.
- [x] Commit `e18f208`.

### Iteração 1 — Cena estática + câmera + difficulty
Validado em 2026-05-18.
- [x] Estrutura de pastas + 6 scripts.
- [x] `RailGenConfig_Default` SO + `DifficultyConfig_Default` SO com 1 tier.
- [x] Prefab `TrackTile_Prefab` + cena com 3 tiles + Player + Camera.
- [x] Play test: movimento forward, framing de câmera, zoom adaptativo OK.
- [x] Commits `fe850b7` (setup) e `b73070d` (push test).

---

## Roadmap restante

### Iteração 5 — Stress test
- [ ] Headless: gerar 10000 linhas, validar critical path sempre existe.
- [ ] Profiler: 60fps com 12 rows × 9 lanes.

---

## Decisões tomadas (registro rápido)

- **Engine:** Unity 6.3 LTS (era 2021.3.45f1 na spec original).
- **VCS:** Git + Git LFS (era Perforce na spec original).
- **Branch default:** `main`.
- **Layout de pastas:** segue §12 da spec literal.
- **Moedas em decoys no MVP:** 0 (sinalização limpa) — §16 ponto 3.

---

## Pontos em aberto (da spec §16)

1. Curva de tiers default — calibrar depois de jogar.
2. Reset de dificuldade — feedback visual? Decidir na Iteração 4.
3. Moedas em decoys (0 vs 1) — começamos com 0.
4. Tempo de reação alvo (~2.5s) — calibrar tier a tier.
