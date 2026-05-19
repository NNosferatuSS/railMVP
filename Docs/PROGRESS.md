# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-19 (MVP2 Iter 1 código pronto, falta setup Editor)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`) — milestone MVP1 tag `v0.1.0-mvp`

---

## Estou aqui agora

**MVP2 Iteração 1 — Obstáculos letais (código pronto, falta setup na Editor).**

Plano completo do MVP2 em `Docs/MVP2_Plan.md` (4 iterações). Iteração 1
adiciona obstáculos letais que spawnam só em decoys, com probabilidade
escalando por tier (0% → 55%).

### Próximo passo concreto

Seguir `Docs/MVP2_Iteracao1_Obstaculos.md`:
1. Criar prefab `Obstacle_Lethal_Prefab` (cubo vermelho + BoxCollider trigger + LethalObstacle).
2. Adicionar `ObstacleSpawner` ao `TrackTile_Prefab` (refs StartPoint/EndPoint).
3. Atribuir o prefab no campo `Lethal Obstacle Prefab` do ProceduralRailGenerator.
4. Play test: confirmar que cubos vermelhos só aparecem em decoys, colisão = Game Over com `HitObstacle`.
5. Commit assets: `feat(mvp2-iter1): Obstacle_Lethal prefab + setup na cena`.

Checklist Iter 1:

- [x] Folder `Obstacles/` + `ObstacleBase.cs` (abstract) + `LethalObstacle.cs`.
- [x] `Track/ObstacleSpawner.cs` (paralelo ao CoinSpawner).
- [x] `TrackTile.cs` com novo campo `Obstacles` + auto-resolve.
- [x] `DifficultyTier.obstacleChanceOnDecoy` + 6 tiers populados.
- [x] `ProceduralRailGenerator.lethalObstaclePrefab` + spawn em decoys.
- [ ] **Prefab `Obstacle_Lethal_Prefab`** criado.
- [ ] **`TrackTile_Prefab`** atualizado com ObstacleSpawner.
- [ ] **`ProceduralRailGenerator`** com prefab atribuído.
- [ ] **Play test** com critérios da seção 6 do guide.
- [ ] Commit assets.

---

## MVP Parte 2 (em andamento)

Plano e decisões de design em `Docs/MVP2_Plan.md`. Roadmap:

- **Iter 1 — Obstáculos letais** (em andamento) — risco em decoys, critical
  path 100% limpo.
- **Iter 2 — HUD básico** — tempo, distância, moedas, tier (UGUI overlay).
- **Iter 3 — Tela de Game Over** — stats + botão Restart.
- **Iter 4 — Power-ups + Barreira** — 4 tipos (Shield, SlowDown, Reset, Magnet),
  duração em tiles, stack permitido. Barreira = obstáculo não-letal consumida
  por shield.

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
