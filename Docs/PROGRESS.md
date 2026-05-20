# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-20 (sessão grande — 3 ideas + PostMVP2.5 obstáculos + PostMVP2.4 pickups + D high score, tudo commitado e pushado)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)
**Tags:** `v0.1.0-mvp` (MVP1), `v0.2.0-mvp2` (MVP2)

---

## 🟢 Próxima sessão: começe AQUI

**Estado:** 3 ideas novas (Auto-follow PU + Trilhos coloridos + Warmup) implementadas.
Setup pendente na Editor. Pendências da sessão anterior (PostMVP2.3 setup
HUD) ainda valem.

### Passos da próxima sessão

**1. Setup pendente — junte tudo (~15-20 min):**

Detalhes em `Docs/PostMVP2_4_3Ideas.md`. Resumo:

| Componente | Setup mínimo |
|---|---|
| AutoFollow PU | `_AutoCriticalFollower` GameObject + `AutoFollowText` TMP_Text + ref no HUD |
| Trilhos coloridos | 2 materiais (verde, vermelho) + atribuir no `TrackTile_Prefab` (Mesh ref + 2 materials) |
| Warmup + countdown | `_WarmupController` GameObject + `CountdownText` TMP_Text (font ~160pt, centro) |
| **Pendente PostMVP2.3** | `TeleportText` + `ActiveItemText` se ainda faltavam |

**2. Validar (~15 min):**
- Play. Player deve estar em warmup: 5 rows single-center em 0.5x speed.
- Setas/Space não funcionam. Distance "0 m". Time fixo.
- Última warmup row → countdown "3" "2" "1" "GO!" no centro.
- Após GO, geração procedural normal. Trilhos coloridos (verde/vermelho).
- F1 → Grant AutoFollow → 5 tiles auto-pilot.
- Tudo da PostMVP2.3 também (TimeFreeze Space, Teleport Shift+arrow).

**3. Próximo trabalho — sugestões pra escolher:**
   Roadmap restante (já tudo na lista descartado pelo user): audio (C),
   polish visual (E), mobile (F), animações UI (G). Nada urgente.
   Pode parar aqui e jogar pra ver feel, ou pegar algum desses.

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
