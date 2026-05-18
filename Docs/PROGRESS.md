# Progresso do RailMVP

> **Como usar este doc:** marque as checkboxes conforme avança. A seção
> "Estou aqui agora" diz exatamente o próximo passo a executar. Quando
> mudar de iteração, mova a seção de "Próximo" para "Estou aqui agora"
> e atualize a data.

**Última atualização:** 2026-05-18
**Engine:** Unity 6000.3.10f1 (6.3 LTS)
**Remote:** https://github.com/NNosferatuSS/railMVP.git (`main`)

---

## Estou aqui agora

**Iteração 1 — Setup na Unity (código pronto, falta clicar na Editor).**

O código C# da Iteração 1 já está commitado e pushado. Falta abrir
o projeto na Unity e seguir o guia para criar os assets que dependem
da Editor (ScriptableObjects, prefab, cena montada).

### Próximo passo concreto

Abrir `Docs/Iteracao1_Setup.md` e executar a partir da **Seção 2**
(Seção 1 — estrutura de pastas — já está feita pelo commit inicial).

Checklist da Iteração 1:

- [x] Estrutura de pastas em `Assets/Scripts/RailSwitchMVP/` criada e
      populada com os 6 scripts.
- [x] `Assets/Prefabs/RailSwitchMVP/` e `Assets/ScriptableObjects/RailSwitchMVP/`
      criadas (vazias).
- [ ] Criar `RailGenConfig_Default` (ScriptableObject) em
      `Assets/ScriptableObjects/RailSwitchMVP/`.
- [ ] Criar `DifficultyConfig_Default` com 1 tier (valores na tabela
      do `Iteracao1_Setup.md` §2.2).
- [ ] Montar prefab `TrackTile_Prefab` em `Assets/Prefabs/RailSwitchMVP/`
      (Mesh cube 2×0.2×10, StartPoint Z=-5, EndPoint Z=+5).
- [ ] Criar cena `Assets/Scenes/RailSwitchMVP_Scene.unity`.
- [ ] Adicionar `_DifficultyManager` na cena com `DifficultyConfig_Default`.
- [ ] Instanciar 3 `TrackTile_Prefab` em Z=5, 17, 29 (Row 0/1/2, Lane 1).
- [ ] Adicionar Player (Capsule) com `PlayerRailRider` configurado.
- [ ] Adicionar MainCamera com `PlayerCameraRig` configurado.
- [ ] **Play test** — validar critérios da §5 do `Iteracao1_Setup.md`.
- [ ] Commit + push dos assets gerados (`.unity`, `.prefab`, `.asset`,
      `.meta`). Mensagem sugerida:
      `feat(iter1): scene + prefab + SOs configurados`

> **Atenção:** ao salvar a primeira vez na Unity, alguns arquivos em
> `ProjectSettings/` (ex: `ShaderGraphSettings.asset`) podem aparecer
> modificados — é esperado, pode commitar junto.

---

## Roadmap restante

### Iteração 2 — Switches + transição + moedas
- [ ] `SwitchController` (seta única rotacionada, estados Left/Middle/Right).
- [ ] `IDirectionalInput` + `KeyboardDirectionalInput` (←/→).
- [ ] Transição do player entre tiles via switch (lerp X durante `rowGap`).
- [ ] `CoinSpawner` no `TrackTile`.
- [ ] `CollectibleCoin` + `CoinManager` singleton.
- [ ] `GameManager` com estados Playing/GameOver.
- [ ] Game Over: `DeadEnd` e `OutOfBounds`.

### Iteração 3 — Geração procedural com Critical Path
- [ ] `RowData` (POCO).
- [ ] `ProceduralRailGenerator` com algoritmo da §4.2.
- [ ] `RailManager` com spawn ahead / despawn behind.
- [ ] Moedas distribuídas conforme `isCriticalPath`.
- [ ] Gizmos coloridos para validação visual do critical path.

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
