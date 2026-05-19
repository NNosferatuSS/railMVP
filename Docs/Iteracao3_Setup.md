# Iteração 3 — Guia de Setup na Unity

**Objetivo desta iteração:** validar a geração procedural infinita baseada
em Critical Path. Tiles deixam de ser hardcoded na cena — o `RailManager`
+ `ProceduralRailGenerator` instanciam linhas conforme o player avança e
removem linhas antigas atrás dele.

Pré-requisitos: Iter 2 validada. Cena `RailSwitchMVP_Scene.unity` existe.

---

## 1. Limpar a cena (remover tiles hardcoded)

1. Na hierarchy da cena `RailSwitchMVP_Scene`, **delete todos os 8 tiles**
   que estavam dentro de `_Tracks` (ou o grupo que você usou).
2. Você pode também deletar o GameObject vazio `_Tracks` se ele só servia
   pra organizar os tiles antigos.

Ao fim deste passo a cena deve ter apenas:
- `_DifficultyManager`, `_RailManager`, `_GameManager`, `_CoinManager`, `_Input`
- `Player`
- `MainCamera`
- Directional Light (padrão)

> Não delete `_RailManager`. A gente vai reaproveitar/reconfigurar ele.

---

## 2. Adicionar o ProceduralRailGenerator

Você tem 2 opções equivalentes:

**Opção A — gerador no mesmo GameObject do RailManager (recomendado):**
1. Selecione `_RailManager` na hierarchy.
2. Add Component → **Procedural Rail Generator**.

**Opção B — GameObject separado:**
1. Crie um GameObject vazio `_Generator`.
2. Add Component → **Procedural Rail Generator**.
3. No `_RailManager`, no campo `Generator`, arraste `_Generator`.

Em ambas as opções, no componente **Procedural Rail Generator** preencha:
- `Config` → `RailGenConfig_Default` (asset).
- `Tile Prefab` → `TrackTile_Prefab` (asset em `Assets/Prefabs/RailSwitchMVP/`).

---

## 3. Configurar o RailManager

No componente **Rail Manager** do `_RailManager`:

| Campo | Valor |
|---|---|
| `Config` | `RailGenConfig_Default` (asset) |
| `Generator` | o componente Procedural Rail Generator (mesmo GameObject ou `_Generator`) |
| `Difficulty` | `_DifficultyManager` GameObject |
| `Player` | `Player` GameObject |
| `Tiles Parent` | (opcional) um GameObject vazio chamado `_Tracks` pra organizar |

> Se quiser usar `Tiles Parent`: crie um GameObject vazio `_Tracks` na raiz
> da cena e arraste pro campo. Não é obrigatório — sem ele, os tiles vão
> direto na raiz da cena.

---

## 4. Ajustar o Player

No componente **Player Rail Rider** do `Player`:
- `Start Tile` → **deixe VAZIO** (None). O `RailManager` vai preencher em
  runtime, escolhendo um tile do critical path da linha 0.
- Restante dos campos: como estava na Iter 2.

A posição inicial do `Player` no Inspector (Transform → Position) será
sobrescrita pelo `RailManager` ao começar o Play. Mas mantenha Y > 0 (ex:
`Y = 1`) pra evitar problemas com o chão visual.

---

## 5. Verificar o TrackTile_Prefab

Abra `Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab` e confira:

- Componente **Coin Spawner** → campo `Spawn On Start Count`:
  - **AGORA deve ser `0`** (era `3` na Iter 2). O gerador procedural passa
    a contagem por argumento conforme o tier.
  - Se ainda estiver em 3, mude pra 0 e salve o prefab. Senão você vai ter
    moedas duplicadas (3 do auto-spawn + as do gerador).

- `Coin Prefab`, `Start Point`, `End Point` continuam preenchidos como na Iter 2.

---

## 6. Play Test

Salve a cena e dê **Play**.

### 6.1 Comportamento esperado

- O player começa em algum tile da linha 0 (que o RailManager escolheu,
  preferindo um do critical path).
- Imediatamente já existem ~12 linhas spawnadas à frente (`rowsAhead = 12`
  no `RailGenConfig_Default`).
- O player anda forward, troca de lane com ←/→, coleta moedas.
- Linhas que ficam mais de `rowsBehind = 2` atrás são despawnadas (visível
  na hierarchy: GameObjects somem).
- Linhas novas spawnam à frente automaticamente conforme o player avança.

### 6.2 Critical Path

Com `RailGenConfig_Default.Debug Draw Critical Path = true`, no **Scene View**
você vê:
- **Cubos wireframe verdes** acima dos tiles do critical path.
- **Cubos wireframe laranjas** acima dos tiles decoy.

### 6.3 Game Over

- O player pode pegar moedas e seguir indefinidamente se sempre seguir
  o critical path (= linhas com mais moedas).
- Se entrar num decoy que vira beco sem saída na próxima linha, dispara
  `GameOver(DeadEnd)`.
- Switch apontando pra fora do grid (lane=0 com Left, ou lane=maxLanes-1
  com Right) dispara `GameOver(OutOfBounds)` — mas isso é raro porque
  o critical path normalmente fica no meio.

### 6.4 Checklist de validação

- [ ] Linhas spawnam sem interrupção à frente do player.
- [ ] Linhas atrás são despawnadas (FPS estável, sem leak de GameObjects).
- [ ] Tiles do critical path têm gizmo verde no Scene View.
- [ ] Decoys têm gizmo laranja.
- [ ] Critical path nunca "pula" mais de 1 lane entre linhas consecutivas.
- [ ] Coleta de moedas funciona — `_CoinManager.Total` cresce no Inspector.
- [ ] Decoys têm 0 moedas (`tier.coinsPerDecoyTile = 0`).
- [ ] FPS estável em 60+ com 12 rows × 3 lanes.

---

## 7. Commit

```
git add Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab.prefab \
        Assets/Scenes/RailSwitchMVP_Scene.unity
git commit -m "feat(iter3): cena procedural + critical path"
```

---

## 8. Troubleshooting

**Player não se move (fica parado no (0,0,0) ou no Inspector):**
- Confirme que `_RailManager → Player` aponta pro Player GameObject.
- Confirme que `_RailManager → Generator` está preenchido.
- Confirme que `_RailManager → Config` está preenchido.
- No Console deve aparecer `[RailManager] Generator/Config/Difficulty not assigned`
  se algum estiver vazio.

**Tiles spawnam mas o player não fica em cima:**
- Verifique a tag `Player` na capsule (necessária pra coleta de moedas).
- Verifique se o Y do Player no Inspector é > 0 (ex: 1).

**Moedas duplicadas em cada tile:**
- O `TrackTile_Prefab → CoinSpawner → Spawn On Start Count` ainda está em 3.
  Mude pra 0 e salve o prefab.

**Critical path "salta" mais de 1 lane:**
- Não é esperado. Verifique se você não alterou o ProceduralRailGenerator —
  o algoritmo só permite offset ∈ {-1, 0, +1}.

**Game Over instantâneo no início:**
- O player pode ter spawnado num tile e o switch inicial (aleatório) aponta
  pra fora do grid. Como o critical path começa no centro, isso é raro mas
  possível em `maxLanes = 3` com switch random Left. Aperta → assim que
  o Play começa pra contornar.

**Tiles vão pra raiz da cena (não pro `_Tracks`):**
- Você não preencheu `Tiles Parent` no `_RailManager`. Não é problema
  funcional, apenas organização visual da hierarchy.

---

## 9. Próximos Passos (Iteração 4)

- Popular `DifficultyConfig_Default` com 5–6 tiers (tabela §2.4 da spec).
- Validar que o gerador respeita `maxLanes` mutável (3 → 5 → 7).
- Botão de debug para `ResetDifficulty()`.
- Tunar curva de speed/zoom/lanes por tier.
