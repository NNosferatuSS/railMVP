# MVP2 — Iteração 1: Obstáculos Letais

**Objetivo:** decoys agora podem matar por colisão com obstáculo letal, não só
por dead-end. Critical path continua 100% seguro (decisão de design: reforça
"moeda = caminho seguro").

Pré-requisitos: MVP1 fechado (`v0.1.0-mvp`). Ver `Docs/MVP2_Plan.md` §"Iteração 1".

---

## 1. Confirmar baseline

Rode **Tools → RailSwitchMVP → Run Stress Test (10k rows)**.
Deve passar como antes do MVP2 — o novo campo `obstacleChanceOnDecoy` não
afeta o stress test (que só valida lane planning, não obstáculos).

---

## 2. Criar `Obstacle_Lethal_Prefab`

1. Hierarchy → clique direito → **3D Object → Cube**.
2. Renomeie para `Obstacle_Lethal_Prefab`.
3. Inspector:
   - Transform → **Scale**: `(1.5, 1.0, 1.5)` (mais largo que alto — câmera é
     quase top-down, então largura/profundidade é o que importa pra ler).
   - **BoxCollider** (gerado automaticamente) → marque `Is Trigger` = ✅.
   - Material: crie um material URP Lit `Obstacle_Red_Mat` com Base Color
     vermelho saturado (ex: `#E63946`). Atribua.
   - Add Component → **Lethal Obstacle**
     (`RailSwitchMVP/Obstacles/LethalObstacle.cs`).
4. Arraste o GameObject para `Assets/Prefabs/RailSwitchMVP/Obstacle_Lethal_Prefab.prefab`.
5. Delete o GameObject da cena.

> Dica visual: se quiser distinguir mais do trilho cinza, ative
> **Emission** no material com a mesma cor vermelha em intensidade baixa
> (1-2). Não é obrigatório.

---

## 3. Atualizar `TrackTile_Prefab`

Abra `Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab` em modo edição.

1. Selecione a **raiz** do prefab.
2. Add Component → **Obstacle Spawner**.
3. Configure:
   - `Start Point` → arraste `StartPoint` (filho do prefab).
   - `End Point` → arraste `EndPoint` (filho do prefab).
   - `Obstacle Height` = 0.5 (mesmo padrão das moedas).
4. No componente **Track Tile**, o campo `Obstacles` deve aparecer (novo).
   Arraste o próprio prefab raiz (que agora tem ObstacleSpawner).
   *(O Awake já faz `GetComponentInChildren` como fallback, então pode
   deixar vazio se quiser — mas atribuir é mais limpo.)*
5. Salve o prefab.

---

## 4. Configurar `_RailManager` / ProceduralRailGenerator

No GameObject que tem o componente **Procedural Rail Generator** (geralmente
`_RailManager` na cena), preencha o **novo campo**:

- `Lethal Obstacle Prefab` → arraste `Obstacle_Lethal_Prefab` do passo 2.

Se ficar vazio, obstáculos não spawnam (no-op seguro — não dá crash).

---

## 5. Verificar `DifficultyConfig_Default`

Abra `Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default`.
Cada um dos 6 tiers agora deve mostrar o campo **`Obstacle Chance On Decoy`**
com estes valores (já populados no commit deste setup):

| Tier | Trigger (m) | Obstacle Chance On Decoy |
|---|---|---|
| 0 | 0 | 0.00 |
| 1 | 100 | 0.15 |
| 2 | 250 | 0.25 |
| 3 | 500 | 0.35 |
| 4 | 800 | 0.45 |
| 5 | 1200 | 0.55 |

Se algum aparecer 0 (default), confirme que o arquivo `.asset` tem o valor —
ou edite no Inspector e salve.

---

## 6. Play Test

### 6.1 Tier 0 — sem obstáculos
- Dê Play. Permaneça no Tier 0 (não aperte `T`).
- Anda por uns 30 segundos. **Nenhum obstáculo deve aparecer** em decoys ou
  critical paths.
- ✅ Validação: spawn rate = 0% no Tier 0.

### 6.2 Tier 2 — obstáculos visíveis
- Restart Play. Aperte `T` 2x para pular pra Tier 2 (25% de chance em decoys).
- Observe que **a cada ~4 decoys, 1 tem cubo vermelho no meio**.
- ✅ Validação: cubos vermelhos só aparecem em tiles laranjas (decoy gizmo),
  nunca nos verdes (critical path gizmo) — confira no Scene View.

### 6.3 Colisão letal
- Em algum tier ≥ 1, entre num decoy com obstáculo (siga uma seta apontando
  pra ele propositalmente).
- ✅ Validação: ao colidir, Console mostra
  `[GameManager] GAME OVER — HitObstacle`. Player para.

### 6.4 Telegrafia
- Obstáculos aparecem ~5 linhas à frente do player conforme ele anda.
- Decoys com obstáculo nunca têm moedas (sem-moeda já era o sinal "decoy").
- Cubo vermelho é visível de longe — player consegue planejar evitar.
- ✅ Validação: você consegue ver o cubo no Tile_R+1 antes de ter que decidir
  o switch no Tile_R atual.

### 6.5 Stress test
- Rode **Tools → RailSwitchMVP → Run Stress Test (10k rows)** de novo.
- ✅ Validação: continua passando. O teste valida apenas planejamento de lanes,
  então adicionar obstáculos não afeta — mas confirmar que a alteração no
  generator não introduziu regressão.

---

## 7. Commit

```
git add Assets/Prefabs/RailSwitchMVP/Obstacle_Lethal_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab.prefab \
        Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(mvp2-iter1): Obstacle_Lethal prefab + setup na cena"
```

---

## 8. Troubleshooting

**Obstáculos spawnam em critical path:**
- Bug. O `if (!tile.IsOnCriticalPath ...)` no generator deveria barrar.
  Verifique se você não modificou a linha (`Assets/Scripts/RailSwitchMVP/Core/ProceduralRailGenerator.cs`).

**Obstáculos não spawnam nem em Tier 5:**
- Confirme `Lethal Obstacle Prefab` preenchido no ProceduralRailGenerator.
- Confirme `obstacleChanceOnDecoy` > 0 no tier ativo.
- No Console, log `[Generator] ↑ Tier N` deve aparecer — confirma que o
  tier mudou.

**Player atravessa o obstáculo sem morrer:**
- Confirme que o obstáculo tem **BoxCollider com `Is Trigger = true`**.
- Confirme que o Player tem **Capsule Collider** (sem trigger — colider físico).
- Confirme que a tag do Player é exatamente `Player`.

**Obstáculo flutuando muito alto / baixo:**
- Ajuste `Obstacle Height` no componente Obstacle Spawner do `TrackTile_Prefab`.

**Console: `[LethalObstacle] Player hit but no GameManager.Instance`:**
- O `_GameManager` GameObject não está na cena. Veja `Docs/Iteracao2_Setup.md`
  seção 4.1 para adicionar.

---

## 9. Próximo passo

Após validar tudo, prossiga pra MVP2 Iter 2 — **HUD básico**
(tempo, distância, moedas, tier). Ver `Docs/MVP2_Plan.md` §"Iteração 2".
