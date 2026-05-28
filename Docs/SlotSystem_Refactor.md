# Slot System Refactor

> Refactor da geração procedural pra dar granularidade de balanço de coins +
> resolver overlap visual entre coin/hazard/power-up. Implementado em 2026-05-28.

## Problema original

Antes do refactor, cada tipo de spawnable tinha seu próprio modelo de posição:

| Componente | Posicionamento |
|---|---|
| `CoinSpawner.Spawn(N, crit)` | N moedas equidistantes via `Lerp(padding, 1-padding, (i+0.5)/N)` |
| `ObstacleSpawner.Spawn(prefab)` | Sempre `t=0.5` (centro do tile) |
| `PowerUpSpawner.Spawn(prefab)` | Sempre `t=0.5` (centro do tile) |

**Sintomas:**
- Coin no slot central podia ficar exatamente em cima de hazard/power-up.
- Posição das moedas mudava com N — sem controle visual previsível.
- Sem range min/max de moedas; só int fixo por tier (`coinsPerCriticalTile`,
  `coinsPerDecoyTile`).

## Conceito

Tile tem **N slots discretos** ao longo do comprimento (default 5). Slot `i`
em `i ∈ [0, N-1]` corresponde a:

```
t = Lerp(padding, 1 - padding, i / (N - 1))   // para N > 1
t = 0.5                                        // para N == 1
```

Coins, hazards e power-ups todos consomem do MESMO grid de slots. Quem reserva
um índice impede que outro spawne em cima. **Não-overlap garantido por
construção.**

## Implementação

### Slice 1 — Slot foundation ✅ implementado (falta validar Unity)

**`Config/RailGenConfig.cs`** — adiciona campos globais:

```csharp
public enum SlotPlacement { CenterSlot, RandomFree }
public enum CoinPlacement { UniformGrid, RandomFree }

public int coinSlotsPerTile = 5;          // [1, 15]
public float coinSlotPadding = 0.1f;      // [0, 0.45)
public SlotPlacement hazardSlotStrategy = SlotPlacement.CenterSlot;
public SlotPlacement powerUpSlotStrategy = SlotPlacement.CenterSlot;
public CoinPlacement coinSlotStrategy = CoinPlacement.RandomFree;
```

**`Track/TrackTile.cs`** — helper de posição centralizado:

```csharp
public Vector3 GetSlotPosition(int slotIndex, int totalSlots, float padding, float heightOffset)
```

Calcula o Lerp entre `StartPoint` e `EndPoint`. Antes esse cálculo estava
duplicado em CoinSpawner/ObstacleSpawner/PowerUpSpawner.

**`Track/CoinSpawner.cs`** — assinatura nova:

```csharp
public void Spawn(int targetCount, int totalSlots, float padding,
                  HashSet<int> reservedSlots, CoinPlacement strategy)
```

Duas estratégias:

**UniformGrid** (determinístico):
1. `clampedCount = min(targetCount, totalSlots)`.
2. Para cada `i ∈ [0, clampedCount)`, computa slot-alvo no grid COMPLETO:
   `slot = round(i * (totalSlots - 1) / (clampedCount - 1))`.
3. Se o slot-alvo está em `reservedSlots`, **skipa** essa coin.
4. Caso especial `clampedCount == 1` → centro do grid (`totalSlots / 2`).

Stride sempre uniforme; visual previsível mas repetitivo (3 coins → 0,2,4
em todo tile).

**RandomFree** (atual default):
1. Coleta `free[]` = slots não reservados.
2. `actualCount = min(targetCount, free.Count)`.
3. Fisher-Yates parcial: shuffle os primeiros `actualCount` elementos de `free[]`
   e usa esses slots.

Stride varia tile a tile (mais natural), sem repetição na mesma row.

**Campos removidos do CoinSpawner** (movidos pra config global ou obsoletos):
- `padding` → `RailGenConfig.coinSlotPadding`
- `startPoint`, `endPoint` → usa `TrackTile.StartPoint`/`EndPoint`
- `spawnOnStartCount`, `isCriticalPath` → dead code da Iter 2 manual
- `Awake`, `Start`, `ResolvePointsFromTile` → não mais necessários

**`Track/ObstacleSpawner.cs` / `PowerUpSpawner.cs`** — assinatura nova:

```csharp
public GameObject Spawn(GameObject prefab, int slotIndex, int totalSlots, float padding)
```

Posiciona via `Tile.GetSlotPosition`. Removidos `startPoint`/`endPoint`/
`Awake`/`ResolvePointsFromTile`.

**`Core/ProceduralRailGenerator.cs`** — orquestração em `GenerateRow`:

```csharp
int totalSlots = config.coinSlotsPerTile;
float slotPadding = config.coinSlotPadding;
HashSet<int> reservedSlots = null;

// 1. Hazard primeiro (mais raro, define âncora)
if (hazardDecision.prefab != null) {
    int hazardSlot = PickSlot(config.hazardSlotStrategy, totalSlots, reservedSlots);
    reservedSlots ??= new HashSet<int>();
    reservedSlots.Add(hazardSlot);
    tile.Obstacles.Spawn(hz.prefab, hazardSlot, totalSlots, slotPadding);
}

// 2. Power-up (skip se tem hazard, regra antiga preservada)
if (puPrefab != null) {
    int puSlot = PickSlot(config.powerUpSlotStrategy, totalSlots, reservedSlots);
    reservedSlots ??= new HashSet<int>();
    reservedSlots.Add(puSlot);
    tile.PowerUps.Spawn(puPrefab, puSlot, totalSlots, slotPadding);
}

// 3. Coins por último, evitando reservados
int coinCount = tile.IsOnCriticalPath ? tier.coinsPerCriticalTile : tier.coinsPerDecoyTile;
if (coinCount > 0)
    tile.Coins.Spawn(coinCount, totalSlots, slotPadding, reservedSlots);
```

Helper `PickSlot(strategy, totalSlots, reservedSlots)`:
- **CenterSlot**: tenta `totalSlots / 2`. Se reservado, busca o livre mais
  próximo do centro alternando ±1, ±2... (preferência simétrica).
- **RandomFree**: coleta livres e sorteia. Lista alocada por chamada (OK pq
  só roda quando há hazard/powerup).
- Fallback se TUDO reservado: retorna centro (não deveria ocorrer com slot
  count saudável).

**`RailGenConfig_Default.asset`** — atualizado com:
```yaml
coinSlotsPerTile: 5
coinSlotPadding: 0.1
hazardSlotStrategy: 0   # CenterSlot
powerUpSlotStrategy: 0  # CenterSlot
coinSlotStrategy: 1     # RandomFree
```

### Slice 2 — Coin min/max range ✅ implementado (falta validar Unity)

`DifficultyTier` agora tem 4 ints em vez de 2:

```csharp
[FormerlySerializedAs("coinsPerCriticalTile")] public int criticalCoinsMin;
public int criticalCoinsMax;
[FormerlySerializedAs("coinsPerDecoyTile")] public int decoyCoinsMin;
public int decoyCoinsMax;
```

Sample em runtime no `ProceduralRailGenerator.GenerateRow`:
`Random.Range(min, max + 1)`. Min=Max preserva comportamento clássico
de int fixo.

**Migração `.asset` (automática):**
- `FormerlySerializedAs` em `*Min` preserva o valor antigo.
- `*Max` fica 0 no carregamento → `OnValidate` em `DifficultyConfig`
  copia `min → max` na primeira abertura.
- Atualiza todos os 6 tiers do `DifficultyConfig_Default` no Editor
  sem perder valores.

**Validações em `DifficultyConfig.GetValidationWarnings()`:**
- `criticalCoinsMin >= 0` (idem decoy).
- `criticalCoinsMax >= criticalCoinsMin` (idem decoy).
- Não há warning de "max + hazard + powerup > slots" — CoinSpawner clampa
  com `Mathf.Min(targetCount, totalSlots)` e Skip dos reservados resolve.

### Slice 3 — Override UI (OPCIONAL)

Adicionar no `SpawnOverrideController` (F2 debug):
- Toggle pra forçar slot strategy.
- Sliders pra coin min/max override em runtime.

Só fazer se for útil pro playtest.

## Validação pós-implementação (próxima sessão)

1. Abrir `TrackTile_Prefab` no Unity. Os 3 spawners têm campos órfãos
   (`spawnOnStartCount`, `isCriticalPath`, `startPoint`, `endPoint`,
   `padding`) — Unity warna no carregamento, basta salvar o prefab
   (Ctrl+S) e os órfãos somem.
2. Selecionar `RailGenConfig_Default` → confirmar 4 campos novos.
3. Rodar uma run:
   - Tier 0 (crit, 3 coins, sem hazard): coins nos slots `0, 2, 4`.
   - Tier 1+ (decoy com hazard): hazard no centro (slot 2), coins nos slots
     `0` e `4` (slot 2 skipado — total 2 coins + 1 hazard, stride uniforme).
   - Power-up no critical com 3 coins: PU no slot 2, coins nos slots `0` e `4`
     (slot 2 skipado — total 2 coins + 1 powerup, stride uniforme).
4. Toggle `hazardSlotStrategy = RandomFree` → confirmar que hazards aparecem
   em slots variados.

## Notas de design

- **Padding visual**: com 5 slots e `padding=0.1`, slots ficam em
  `t = 0.1, 0.3, 0.5, 0.7, 0.9`. Slot 0 ≠ StartPoint exato (margem de 10%).
- **Distribuição endpoints-inclusive no grid completo**: com 3 coins em
  5 slots, picks são `0, 2, 4` (não `1, 2, 3`). Trade-off: cobertura total
  do tile mas pode parecer "denso nas pontas".
- **Skip em vez de redistribuir**: quando hazard/powerup ocupa um slot-alvo
  da coin, a coin é skipada (count efetivo cai). Isso preserva o stride
  uniforme do grid — preferimos um tile com menos coins do que com coins
  coladas no special.
- **Hazard antes de powerup**: hazard tem prioridade no slot central (default
  CenterSlot). Se ambos forem CenterSlot, powerup pega o vizinho do centro.
- **HazardWarning** (ícone flutuante) continua se prendendo ao `hazardGo` →
  segue o slot pra frente sem mudança no código.
- **PrefabPool + ResetForReuse**: a destruição de children dinâmicos do tile
  (`Destroy(child.gameObject)` em `ResetForReuse`) garante limpeza dos
  coins/hazards/powerups antigos antes da próxima passagem pelo Spawn().
  Sem mudança no pool.

## Arquivos modificados

```
Assets/Scripts/RailSwitchMVP/Config/RailGenConfig.cs
Assets/Scripts/RailSwitchMVP/Track/TrackTile.cs
Assets/Scripts/RailSwitchMVP/Track/CoinSpawner.cs
Assets/Scripts/RailSwitchMVP/Track/ObstacleSpawner.cs
Assets/Scripts/RailSwitchMVP/Track/PowerUpSpawner.cs
Assets/Scripts/RailSwitchMVP/Core/ProceduralRailGenerator.cs
Assets/ScriptableObjects/RailSwitchMVP/RailGenConfig_Default.asset
```
