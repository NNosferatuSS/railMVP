# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-19 (MVP2 Iter 4 código pronto, falta setup Editor — última iteração do MVP2)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`) — milestone MVP1 tag `v0.1.0-mvp`

---

## Estou aqui agora

**MVP2 Iteração 4 — Power-ups + Barreira (código pronto, falta setup na Editor).**

Última iteração do MVP2. Adiciona 4 tipos de power-up (Shield, SlowDown,
Magnet, DifficultyReset) com duração em tiles e stack permitido. Adiciona
um segundo tipo de obstáculo (Barreira, absorvida por Shield). HUD ganha
3 indicadores de power-up ativo.

### Próximo passo concreto

Seguir `Docs/MVP2_Iteracao4_PowerUps.md`:
1. Criar 5 prefabs: Obstacle_Barrier (cubo amarelo+preto), PowerUp_Shield
   (esfera azul), PowerUp_SlowDown (cyan), PowerUp_Magnet (roxo),
   PowerUp_DifficultyReset (verde).
2. Atualizar `TrackTile_Prefab` adicionando PowerUpSpawner.
3. No `_RailManager → ProceduralRailGenerator`: atribuir os 5 novos prefabs.
4. Adicionar `_PowerUpManager` GameObject na cena.
5. Adicionar 3 TMP_Texts no HUD pra indicadores (Shield/Slow/Magnet) e atribuir refs no HUDController.
6. Play test com critérios do guide.
7. Commit final do MVP2.

Checklist Iter 4:

- [x] `Core/PowerUpManager.cs` — singleton, contadores Shield/Slow/Magnet, eventos, scan de coins.
- [x] `PowerUps/PowerUpBase.cs` + 4 pickups (Shield, SlowDown, Magnet, DifficultyReset).
- [x] `Track/PowerUpSpawner.cs`.
- [x] `Obstacles/BarrierObstacle.cs` + `ObstacleBase` consome shield no OnTriggerEnter.
- [x] `PlayerRailRider.OnTileEntered` event + SpeedMultiplier.
- [x] `DifficultyTier`: barrierChanceOnDecoy + powerUpChanceOnCritical/Decoy.
- [x] Generator: spawn de barriers e power-ups (gate XOR com obstáculo).
- [x] `HUDController`: 3 TMP_Text refs novos + events do PowerUpManager.
- [ ] **5 prefabs novos** criados.
- [ ] **`TrackTile_Prefab`** atualizado com PowerUpSpawner.
- [ ] **`_RailManager`** com 5 prefabs atribuídos.
- [ ] **`_PowerUpManager`** GameObject na cena.
- [ ] **3 TMP_Texts** novos no HUD com refs atribuídas.
- [ ] **Play test** com critérios do guide.
- [ ] Commit final do MVP2.

---

## MVP Parte 2 (em andamento — última iteração)

Plano e decisões de design em `Docs/MVP2_Plan.md`. Roadmap:

- **Iter 1 — Obstáculos letais** ✅ validada.
- **Iter 2 — HUD básico** ✅ validada.
- **Iter 3 — Tela de Game Over** ✅ validada.
- **Iter 4 — Power-ups + Barreira** (em andamento, última).

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
