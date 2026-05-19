# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-19 (MVP fechado)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)

---

## 🎉 MVP FECHADO

Todos os 12 critérios de sucesso da spec §14 atendidos. Core gameloop
validado. Próximos passos abaixo são opcionais (pós-MVP).

**Sugestão:** tag o commit final como `v0.1.0-mvp` pra marcar o milestone:
```
git tag -a v0.1.0-mvp -m "MVP fechado: core gameloop validado em 5 iterações"
git push origin v0.1.0-mvp
```

---

## Pós-MVP — Opções de direção

Sem ordem obrigatória. Ver `Docs/Iteracao5_StressTest.md §5` pra lista
completa de follow-ups. Categorias:

### Polish do core (opcional)
- Pooling de tiles (substituir Instantiate/Destroy — economiza GC).
- Buffer reuse no generator (evitar `new bool[]` e `new HashSet<>` por linha).

### Mecânica nova (spec §15)
- **Obstáculos** (§15.1): `ObstacleSpawner` no TrackTile, colisão = `GameOverReason.HitObstacle`.
- **Power-ups** (§15.2): Slow-down e Difficulty Reset como mecânica de gameplay (não só debug R).

### Apresentação
- **UI/HUD**: contador de moedas, distância, tier indicator, tela de Game Over.
- **Modelo visual**: trocar Capsule do player + cube esticado da seta por modelos de verdade.
- **Audio**: música ambiente, SFX (switch, coin, death).

### Persistência
- `PlayerPrefs.HighScore` simples.

### Mobile / outros inputs
- Implementar `TouchDirectionalInput` (interface `IDirectionalInput` já tá pronta).

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
