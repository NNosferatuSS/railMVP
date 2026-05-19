# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-19 (após validação Iter 3, código Iter 4 pronto)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)

---

## Estou aqui agora

**Iteração 5 — Stress test (última do MVP).**

A última iteração do plano original. Stress test do gerador procedural pra
validar que:
1. **Headless test**: gerar ~10 000 linhas (sem cena, em Edit Mode) e verificar
   que critical path nunca fica vazio, sempre tem rota viável, e o algoritmo
   nunca trava (mesmo com tier changes simulados no meio).
2. **Profiler**: 60 fps mantidos em Play normal com 12 rows × 9 lanes ativas
   (Tier 5), incluindo despawn/spawn em loop.

### Próximo passo concreto

(A criar: `Docs/Iteracao5_StressTest.md` — guia da última iteração.)

---

## Concluído

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
