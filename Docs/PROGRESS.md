# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-18 (após validação Iter 1, código Iter 2 pronto)
**Engine:** Unity 6000.3.10f1 (6.3 LTS) — Input System: **New only** (`activeInputHandler=1`)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)

---

## Estou aqui agora

**Iteração 2 — Switches + transição + moedas + Game Over (código pronto, falta setup na Editor).**

Código C# da Iter 2 commitado. Falta:
1. Atualizar o prefab `TrackTile_Prefab` (adicionar Arrow + SwitchController + CoinSpawner).
2. Criar prefab `Coin_Prefab` (cylinder amarelo com trigger + CollectibleCoin).
3. Adicionar managers de cena (`_RailManager`, `_GameManager`, `_CoinManager`, `_Input`).
4. Refazer a cena com **3 linhas × 3 lanes**, omitindo `(Row=2, Lane=0)` pra demonstrar DeadEnd.
5. Play test: setas ←/→ mudam o switch, transição entre tiles, DeadEnd e OutOfBounds disparam Game Over, moedas coletadas incrementam `CoinManager`.

### Próximo passo concreto

Abrir `Docs/Iteracao2_Setup.md` e seguir do início.

Checklist da Iteração 2:

- [x] Scripts: `SwitchController`, `CoinSpawner`, `RowData`, `RailManager`,
      `GameManager`, `CollectibleCoin`, `CoinManager`, `IDirectionalInput`,
      `KeyboardDirectionalInput` criados.
- [x] `TrackTile.cs` atualizado (refs Switch+Coins + auto-registro no RailManager).
- [x] `PlayerRailRider.cs` reescrito (gap transition + game over + dispatch input).
- [ ] Tag `Coin` criada (Project Settings → Tags and Layers).
- [ ] Prefab `Coin_Prefab` (cylinder amarelo escala (0.3, 0.05, 0.3), `SphereCollider isTrigger`, `CollectibleCoin`).
- [ ] Atualizar `TrackTile_Prefab`: adicionar `Arrow` (Cone/Cube fino em EndPoint), `SwitchController` na raiz, `CoinSpawner` na raiz com refs.
- [ ] Adicionar `_RailManager`, `_GameManager`, `_CoinManager`, `_Input` na cena.
- [ ] Refazer cena com 3×3 (omitir Row=2/Lane=0).
- [ ] **Play test** — setas ←/→, DeadEnd, OutOfBounds, coleta moedas.
- [ ] Commit assets: `feat(iter2): scene + prefabs com switches e moedas`.

---

## Concluído

### Iteração 1 — Cena estática + câmera + difficulty
Validado em 2026-05-18.
- [x] Estrutura de pastas + 6 scripts.
- [x] `RailGenConfig_Default` SO.
- [x] `DifficultyConfig_Default` SO com 1 tier.
- [x] Prefab `TrackTile_Prefab` (Mesh 2×0.2×10, StartPoint Z=-5, EndPoint Z=+5).
- [x] Cena `RailSwitchMVP_Scene.unity` com 3 tiles + Player + Camera.
- [x] Play test: movimento forward, framing de câmera, zoom adaptativo OK.
- [x] Commits `fe850b7` (setup) e `b73070d` (push test).

---

## Roadmap restante

### Iteração 3 — Geração procedural com Critical Path
- [ ] `ProceduralRailGenerator` com algoritmo da §4.2.
- [ ] Trocar `RailManager` minimalista por uma versão com spawn ahead / despawn behind.
- [ ] Moedas distribuídas conforme `isCriticalPath`.
- [ ] Gizmos coloridos para validação visual do critical path.

> **Já feito antecipadamente na Iter 2 (pra evitar refactor):**
> `RowData` (POCO) e `RailManager` mínimo com `Dictionary<row, RowData>` + auto-registro de tiles.

### Iteração 4 — Dificuldade dinâmica
- [ ] `DifficultyConfig` populado com os 5–6 tiers da tabela §2.4.
- [ ] Geração respondendo a mudanças de `maxLanes` em runtime.
- [ ] Botão de debug para `ResetDifficulty()`.

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
