# Pools como SO + Validation Warnings — Setup + Próximos Passos

> Refactor de tooling implementado em 2026-05-26 (sessão à noite). Não muda
> comportamento de runtime — só estrutura de dados e UX de edição.
> Motivação: antecipação de dor — `DifficultyTier` ia ficar inviável de
> manter conforme escala (lista de structs com listas aninhadas, sem
> reuso, sem validação visível).

## Pirâmide de soluções discutida

Antes de codar, alinhamos a sequência por ROI (do mais barato/estrutural
pro mais caro):

1. **Refatorar o dado** — pools como SOs separados (✅ feito)
2. **OnValidate + warnings visuais** — HelpBox no topo do Inspector (✅ feito)
3. **NaughtyAttributes** (free) — `[ShowIf]`, `[InfoBox]`, `[CurveRange]`. Adiado.
4. **Custom EditorWindow** (tier table view + gráficos). Adiado.
5. **Hot-reload em play mode** via DebugPanel. Adiado.

Regra do polegar acordada: **só constrói ferramenta quando a mesma edição
manual doeu 3+ vezes.** Layers 3/4 só entram se a dor reaparecer.

## O que mudou (Layer 1 — pools como SO)

### Novos tipos

| Arquivo | Conteúdo |
|---|---|
| `Config/HazardPool.cs` | `ScriptableObject` com `entries: List<HazardWeight>`, `Count`, `GetValidationWarnings()`, `OnValidate` que clampa pesos negativos. `[CreateAssetMenu]` em `RailSwitchMVP/Hazard Pool`. |
| `Config/PowerUpPool.cs` | Mesmo padrão para `List<PowerUpWeight>`. Menu em `RailSwitchMVP/PowerUp Pool`. |
| `Config/IValidatedConfig.cs` | Interface com `string GetValidationWarnings()`. Implementada por DifficultyConfig, RailGenConfig, HazardPool, PowerUpPool. Null/empty = sem warnings. |

### `DifficultyTier` (struct em `Config/DifficultyConfig.cs`)

Antes:
```csharp
public List<HazardWeight> hazardPool;
public List<PowerUpWeight> powerUpPool;
```

Depois:
```csharp
public HazardPool hazardPool;     // SO ref (null = sem hazards)
public PowerUpPool powerUpPool;   // SO ref (null = sem power-ups)
```

`HazardWeight` e `PowerUpWeight` permanecem como structs serializáveis —
agora vivem **dentro** do SO do pool (em `.entries`).

### Pool assets pré-criados

Em `Assets/ScriptableObjects/RailSwitchMVP/Pools/`:

| Asset | Conteúdo (kind/type: weight) |
|---|---|
| HazardPool_Tier1 | Lethal:0.15, SpeedUp:0.08 |
| HazardPool_Tier2 | Lethal:0.25, Barrier:0.1, SpeedUp:0.12, LaneSwap:0.05 |
| HazardPool_Tier3 | + Vortex:0.08, pesos crescem |
| HazardPool_Tier4 | mais pesos |
| HazardPool_Tier5 | pesos máximos |
| PowerUpPool_Tier0 | Shield:1 (única) |
| PowerUpPool_Tier1 | Shield:1, Magnet:1 |
| PowerUpPool_Tier2 | + SlowDown:1, DoubleCoins:0.5 |
| PowerUpPool_Tier3 | + Ghost:0.7, LanePreview:0.7 |
| PowerUpPool_Tier4 | + CoinRadar:0.5, Teleport:0.4 |
| PowerUpPool_Tier5 | + AutoCriticalFollow:0.3, DifficultyReset:0.15 |

Tier 0 não tem hazardPool (null no asset — equivale ao `[]` que estava
inline antes). `DifficultyConfig_Default.asset` foi reescrito pra referenciar
esses pools via GUID.

**GUIDs fixados** (não regenerar — quebra as refs no DifficultyConfig):
- `HazardPool.cs` → `f3a1b2c3d4e5f60718293a4b5c6d7e8f`
- `PowerUpPool.cs` → `a4b5c6d7e8f900112233445566778899`
- HazardPool_TierN.asset → `b5N00...0670N`
- PowerUpPool_TierN.asset → `c6N00...0671N`

### Callers atualizados

- `ProceduralRailGenerator.cs:541,554` — passa `tier.hazardPool.entries` /
  `.powerUpPool.entries` pros helpers (que continuam tomando `List<>`).
  Checks de null/Count usam as propriedades do SO.
- `SpawnOverrideController.cs:239-272` — cache local `puEntries`/`hazEntries`
  no início do snapshot, em vez de re-acessar o SO em cada loop.

## O que mudou (Layer 2 — warnings no Inspector)

### `Editor/ValidatedConfigInspector.cs`

Base class que renderiza um `EditorGUILayout.HelpBox` amarelo no topo
quando `target is IValidatedConfig` e `GetValidationWarnings()` retorna
não-vazio. Depois chama `DrawDefaultInspector()`.

Quatro inspectors concretos: `DifficultyConfigEditor`, `RailGenConfigEditor`,
`HazardPoolEditor`, `PowerUpPoolEditor` — só declaram `[CustomEditor]` e
herdam. Pra adicionar warnings em outro SO, basta implementar
`IValidatedConfig` e criar um inspector trivial.

### Regras implementadas

**DifficultyConfig** (`DifficultyConfig.cs:131-180`):
- Lista de tiers vazia
- `triggerAtDistance` não monotônico (tier N <= tier N-1)
- `maxLanes < 1`, `minLanesPerRow > maxLanesPerRow`, `maxLanesPerRow > maxLanes`
- `criticalPathsPerRow < 1` ou `> minLanesPerRow` (impossível garantir)
- `playerSpeed <= 0`
- `cameraZoomMin > cameraZoomMax` (perspective range invertido)
- `cameraOrthoSizeMin > cameraOrthoSizeMax`
- `hazardChanceOnDecoy > 0` mas `hazardPool` null/vazio
- `powerUpChance > 0` mas `powerUpPool` null/vazio

**RailGenConfig** (`RailGenConfig.cs`, no final):
- `laneSpacing/trackLength <= 0`, `rowGap < 0`, `globalMaxLanes < 1`
- `globalMaxLanes` par (convenção projeto: ímpar pra lane central)
- `rowsAhead < 1`, `rowsBehind < 0`
- `warmupSpeedMultiplier > 1` (warmup ficaria mais rápido que tier 0)
- `cameraDistance < 0`, `cameraZoomGlobalMultiplier <= 0`
- `cameraFieldOfView` fora de `[20, 100]`

**HazardPool / PowerUpPool**:
- Soma de pesos = 0 (pool efetivamente vazio)
- Duplicatas do mesmo kind/type (pesos somam, mas costuma ser erro)
- Entradas com peso ≤ 0 (informativo — ignoradas no sorteio)
- `HazardKind.None` no pool (geraria "sem hazard" aleatoriamente)

Não há cross-asset validation (ex: `RailGenConfig.globalMaxLanes >=
max(tier.maxLanes)`) — exigiria que um SO conhecesse o outro, invasivo.
Esse check continua em runtime.

## Checklist de validação pendente

A fazer ao abrir o Unity amanhã:

- [ ] Unity compila sem erro
- [ ] `DifficultyConfig_Default` mostra os 6 tiers com `hazardPool`/`powerUpPool`
      preenchidos com referência ao pool asset correto
- [ ] Tier 0: `hazardPool` = None
- [ ] Editor mostra `HelpBox` vazio (nenhuma warning na config default)
- [ ] Abrir `HazardPool_Tier3` em isolamento — inspector funciona, entries editáveis
- [ ] Spawn de hazards/power-ups em runtime continua igual:
  - Tier 1 (dist 100): só Lethal + SpeedUp aparecem
  - Tier 5 (dist 1200): pool completo de hazards + 10 power-ups
- [ ] Testar warning: apagar entry no `HazardPool_Tier1` → HelpBox aparece
- [ ] Testar warning: setar `cameraZoomMin = 99` num tier → warning DifficultyConfig
- [ ] Testar warning: zerar peso de tudo no `PowerUpPool_Tier0` → "Soma = 0"
- [ ] StressTestRunner (Tools/RailSwitchMVP/Stress Test) ainda passa

Se algo der errado na importação do YAML (pouco provável mas escrito à mão):
```
git checkout -- Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset
```
e regerar via Unity manualmente.

## Próximos passos (não decidido)

Opções pra continuar amanhã, em ordem de ROI:

### A. Refatorar tier (cada tier vira SO próprio?)
Hoje `DifficultyConfig.tiers` é `List<DifficultyTier>` (lista de structs).
Se cada tier virasse um SO próprio, ganha-se: Ctrl+D pra duplicar, diff
limpo no git, possibilidade de A/B testing (DifficultyConfig_Easy vs
DifficultyConfig_Hard apontando pros mesmos tiers em ordens diferentes).
Custo similar ao que foi feito hoje.

### B. NaughtyAttributes
Install via Package Manager → git URL `https://github.com/dbrizov/NaughtyAttributes.git`.
Wins concretos:
- `[ShowIf("isPerspective")]` escondendo campos ortho quando não relevantes
- `[InfoBox]` substitui o tooltip por banner persistente
- `[ReorderableList]` melhora drag-reorder das entries dos pools
- `[CurveRange]` desenha minicurva inline pra ramps

### C. DebugPanel com sliders pro tier ativo
Em vez de tunar o SO e dar play, expandir o DebugPanelController (F1)
com sliders pros campos do tier ativo. Mexe-se durante play e sente-se
na hora. Muito mais ROI que UI bonita pra tunar curva de jogo.

### D. Custom EditorWindow (Difficulty Curve Window)
Janela com tiers como colunas (lado a lado) e curvas plotadas:
distance × playerSpeed, × hazardChance, × lanePopulationChance.
Maior payoff mas só vale construir quando já se sabe **o que** se quer
ver no gráfico. Construir cedo demais = refazer 3x.

Recomendação: **C** é o mais provável de dar dopamina rápido. **A** é
estrutural e barato. **B** entra se algum campo específico começar a
incomodar (ex: confusão entre perspective/ortho).

## Arquivos tocados

**Criados:**
- `Assets/Scripts/RailSwitchMVP/Config/HazardPool.cs` (+ .meta)
- `Assets/Scripts/RailSwitchMVP/Config/PowerUpPool.cs` (+ .meta)
- `Assets/Scripts/RailSwitchMVP/Config/IValidatedConfig.cs` (+ .meta)
- `Assets/Scripts/RailSwitchMVP/Editor/ValidatedConfigInspector.cs` (+ .meta)
- `Assets/ScriptableObjects/RailSwitchMVP/Pools.meta` (folder)
- `Assets/ScriptableObjects/RailSwitchMVP/Pools/HazardPool_Tier{1..5}.asset` (+ .meta)
- `Assets/ScriptableObjects/RailSwitchMVP/Pools/PowerUpPool_Tier{0..5}.asset` (+ .meta)

**Modificados:**
- `Assets/Scripts/RailSwitchMVP/Config/DifficultyConfig.cs` — SO refs + validation
- `Assets/Scripts/RailSwitchMVP/Config/RailGenConfig.cs` — validation
- `Assets/Scripts/RailSwitchMVP/Core/ProceduralRailGenerator.cs` — callers
- `Assets/Scripts/RailSwitchMVP/Core/SpawnOverrideController.cs` — callers
- `Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset` — YAML refs
