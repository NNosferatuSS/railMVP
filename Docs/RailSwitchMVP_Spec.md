# Rail Switch MVP — Especificação Técnica (v3.2)

**Projeto:** MVP de jogo top-down endless de trilhos com switches
**Engine:** Unity 6000.3.10f1 (Unity 6.3 LTS, URP, Forward)
**VCS:** Git (+ Git LFS para binários — veja `.gitattributes` na raiz)
**Objetivo deste doc:** Validar o CORE GAMELOOP antes de qualquer arte/polish.

> **Changelog v3.2 (2026-05-22):**
> - Adicionada **§17 — Pós-MVP1 (Estado Atual Consolidado)** sintetizando
>   MVP2 + todo o pós-MVP2 (power-ups passivos, active items, hazards
>   não-letais, 3 ideas de UX, high score, ferramentas de debug).
> - §15 "Reservas Arquiteturais" virou histórica — o "futuro" descrito lá
>   já foi implementado; ver §17 pra o estado real.
>
> **Changelog v3.1:**
> - Atualizado para **Unity 6.3 LTS** (era 2021.3.45f1).
> - Trocado VCS de Perforce para **Git** (com Git LFS para assets binários).
>
> **Changelog v3:**
> - Adicionado **Sistema de Dificuldade Dinâmica** (`DifficultyManager`) com tiers configuráveis.
> - Adicionado **Sistema de Moedas** que sinaliza o caminho seguro.
> - Grid agora pode **expandir/contrair em runtime** (3 → 5 → 7 lanes).
> - Reservada arquitetura para **obstáculos e power-ups** futuros (trilho como contêiner de spawn).
> - UI removida do MVP (mantemos apenas debug Gizmos).
>
> **Changelog v2:**
> - Geração procedural baseada em **Critical Path** com decoys.
> - Adicionada seção de **Visibilidade & Câmera** (zoom, inclinação, look-ahead).
> - Comportamento de borda **punitivo** (switch fora do grid = Game Over).
> - Switch visual: **seta única rotacionando**.

---

## 1. Conceito do Core Gameloop

O jogador controla um objeto (Player) que se move automaticamente para frente sobre uma malha procedural de trilhos retos, organizados em uma grade **Linhas × Lanes**. Entre cada linha existe um **gap** com um conector de 3 posições no final de cada trilho. O jogador troca o estado do conector com input lateral para escolher por qual lane da próxima linha vai prosseguir.

**Pilares de design:**

1. Existe sempre um **caminho válido** (critical path) entre cada par de linhas — mas NÃO necessariamente a partir do trilho onde o jogador está.
2. **Moedas indicam o caminho seguro** — distribuídas predominantemente sobre o critical path. O jogador aprende a "ler o tabuleiro" pelas moedas, não por marcação artificial.
3. **Dificuldade escala dinamicamente** durante o run: velocidade, zoom, quantidade de lanes e mínimo de trilhos por linha aumentam em tiers configuráveis. Pode ser **resetada** em pontos definidos (checkpoint, power-up, etc.).
4. **Erro é punição justa**: escolher um decoy sem saída ou estourar a borda = Game Over.

---

## 2. Sistema de Dificuldade Dinâmica

### 2.1 Filosofia

Em vez de fórmulas matemáticas escalando parâmetros, usamos uma **lista ordenada de Difficulty Tiers** num ScriptableObject. Cada tier é um snapshot completo de configuração — fácil de tunar manualmente, e o `DifficultyManager` faz **lerp** entre tiers conforme o progresso.

### 2.2 `DifficultyTier` (struct/SO entry)

```csharp
[System.Serializable]
public struct DifficultyTier
{
    [Header("Trigger")]
    public float triggerAtDistance; // distância acumulada para ativar este tier

    [Header("Generation")]
    public int maxLanes;              // 3, 5, 7, 9...
    public int minLanesPerRow;
    public int maxLanesPerRow;
    public int criticalPathsPerRow;
    [Range(0f, 1f)] public float lanePopulationChance;

    [Header("Player")]
    public float playerSpeed;

    [Header("Camera")]
    public float cameraZoomMin;
    public float cameraZoomMax;

    [Header("Coins")]
    public int coinsPerCriticalTile;  // moedas em tiles do critical path
    public int coinsPerDecoyTile;     // moedas em decoys (geralmente 0 ou 1)
}
```

### 2.3 `DifficultyConfig` (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "RailSwitchMVP/Difficulty Config")]
public class DifficultyConfig : ScriptableObject
{
    public List<DifficultyTier> tiers; // ordenado por triggerAtDistance crescente
    public bool interpolateBetweenTiers = true; // lerp suave ou snap entre tiers
}
```

### 2.4 Exemplo de Curva (default no MVP)

| Tier | Trigger (m) | maxLanes | minLanes | criticalPaths | playerSpeed | coinsPerCritical |
|---|---|---|---|---|---|---|
| 0 | 0 | 3 | 2 | 1 | 8 | 3 |
| 1 | 100 | 5 | 2 | 1 | 10 | 3 |
| 2 | 250 | 5 | 3 | 1 | 12 | 4 |
| 3 | 500 | 7 | 3 | 1 | 14 | 4 |
| 4 | 800 | 7 | 4 | 2 | 16 | 5 |
| 5 | 1200 | 9 | 4 | 2 | 18 | 5 |

### 2.5 `DifficultyManager`

```csharp
public class DifficultyManager : MonoBehaviour
{
    public DifficultyConfig config;
    public DifficultyTier CurrentTier { get; private set; }
    public event System.Action<DifficultyTier> OnTierChanged;

    private float _distanceTraveled;
    private int _currentTierIndex;

    public void ResetDifficulty()
    {
        _distanceTraveled = 0f;
        _currentTierIndex = 0;
        ApplyTier(config.tiers[0]);
    }

    public void UpdateDistance(float distance)
    {
        _distanceTraveled = distance;

        // Advance tier if eligible
        while (_currentTierIndex + 1 < config.tiers.Count &&
               _distanceTraveled >= config.tiers[_currentTierIndex + 1].triggerAtDistance)
        {
            _currentTierIndex++;
            ApplyTier(config.tiers[_currentTierIndex]);
        }
    }

    void ApplyTier(DifficultyTier tier)
    {
        CurrentTier = tier;
        OnTierChanged?.Invoke(tier);
    }
}
```

### 2.6 Mudança de `maxLanes` em Runtime — Cuidados

Quando o tier muda e `maxLanes` aumenta (ex: 3 → 5):
- **Novas lanes aparecem nas bordas** (lanes 3 e 4 passam a existir).
- O gerador deve **passar a poder spawnar tiles** nelas a partir da próxima linha gerada.
- Linhas já existentes (atrás e à frente que já estão spawnadas) **não mudam** — a transição é orgânica, conforme novas linhas entram.
- **Critical paths existentes não se quebram**: lanes 0–2 continuam válidas; o gerador apenas ganha mais "espaço" pra trabalhar.

Quando `maxLanes` diminui (raro, mas possível com `ResetDifficulty`):
- Mesma lógica: linhas já existentes continuam até saírem do scope; novas linhas usam o novo `maxLanes`.

**Posicionamento centralizado:** a fórmula de X muda conforme `maxLanes`:
```
position.x = (lane - (maxLanes - 1) / 2f) * laneSpacing
```
Isso significa que **a lane 0 não está na mesma posição mundial** quando `maxLanes` é 3 vs 5. Decisão: posições recalculadas por linha conforme `maxLanes` ATIVO no momento da geração daquela linha. Lanes mais antigas mantêm sua posição original. Como o gerador escolhe critical paths com offset ±1 entre linhas, a transição visual fica suave naturalmente.

### 2.7 Reset de Dificuldade

`DifficultyManager.ResetDifficulty()` pode ser chamado por:
- Evento de gameplay (checkpoint, power-up, fase de "calmaria").
- Debug button no inspector.

Reset volta ao tier 0 imediatamente. Aceleração subsequente recomeça do zero.

---

## 3. Sistema de Moedas

### 3.1 Filosofia

Moedas servem **dupla função**:
- **Reward:** contador de progresso para o jogador.
- **Sinalização do caminho seguro:** densidade de moedas é maior no critical path, então o jogador instintivamente segue onde tem mais brilho.

Isso é uma forma elegante de ensinar a mecânica sem tutorial: o jogador segue moedas, percebe que o caminho com moedas leva a mais moedas, e os trilhos sem moedas (decoys) frequentemente terminam em dead-end.

### 3.2 `CoinSpawner` (componente do `TrackTile`)

```csharp
public class CoinSpawner : MonoBehaviour
{
    public GameObject coinPrefab;
    public Transform startPoint;
    public Transform endPoint;

    public void Spawn(int coinCount, bool isCriticalPath)
    {
        if (coinCount <= 0) return;

        // Distribuição equidistante ao longo do tile
        // padding interno para não nascer colado nas bordas
        float padding = 0.1f; // 10% de cada lado
        for (int i = 0; i < coinCount; i++)
        {
            float t = Mathf.Lerp(padding, 1f - padding, (i + 0.5f) / coinCount);
            Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, t);
            pos.y += 0.5f; // elevada para visual
            Instantiate(coinPrefab, pos, Quaternion.identity, transform);
        }
    }
}
```

### 3.3 `CollectibleCoin`

```csharp
public class CollectibleCoin : MonoBehaviour
{
    public int value = 1;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CoinManager.Instance.AddCoins(value);
            Destroy(gameObject);
        }
    }
}
```

- Trigger collider esférico pequeno.
- Sem física, sem rigidbody (kinematic se precisar).
- Visual: MVP usa cylinder/sphere com material amarelo. Polish depois.

### 3.4 `CoinManager`

Singleton simples acumulando o total. Sem UI no MVP, mas o dado existe e pode ser logado.

```csharp
public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }
    public int Total { get; private set; }
    public event System.Action<int> OnCoinsChanged;

    public void AddCoins(int amount)
    {
        Total += amount;
        OnCoinsChanged?.Invoke(Total);
    }

    public void Reset() { Total = 0; OnCoinsChanged?.Invoke(0); }
}
```

### 3.5 Integração com Geração

No `ProceduralRailGenerator`, ao instanciar cada tile:

```csharp
var tile = Instantiate(tilePrefab, position, Quaternion.identity);
tile.IsOnCriticalPath = isCriticalPath;
int coinCount = isCriticalPath
    ? difficulty.CurrentTier.coinsPerCriticalTile
    : difficulty.CurrentTier.coinsPerDecoyTile;
tile.GetComponent<CoinSpawner>().Spawn(coinCount, isCriticalPath);
```

### 3.6 Tuning de Sinalização

- **Critical path:** 3–5 moedas por tile (forte sinalização).
- **Decoy:** 0 moedas por tile no MVP (sinalização limpa). Em tiers mais altos, pode-se ter 1 moeda em alguns decoys para gerar tentação/dilema (escolher caminho seguro com menos moedas, ou arriscar pelo decoy com 1 moeda e potencialmente cair no dead-end).

---

## 4. Conceito de Critical Path (preservado da v2)

### 4.1 Definição

Um **critical path** é uma sequência de lanes `[L0, L1, L2, ...]` onde:
- `Li` é a lane do tile do critical path na linha `i`.
- `|Li - Li-1| <= 1` (sempre alcançável por um switch).

O gerador mantém uma **lane atual do critical path** e, a cada nova linha, decide pra qual lane vizinha ele vai (-1, 0 ou +1), com clamp nas bordas de acordo com `maxLanes` **ativo no momento**.

Quando `criticalPathsPerRow > 1`, mantém múltiplos critical paths em paralelo.

### 4.2 Algoritmo de geração da próxima linha

```
INPUT: previousCriticalLanes, currentMaxLanes (vindo do DifficultyManager)
OUTPUT: nextRow com critical lanes computadas

Passo 1: Avançar cada critical path
    nextCriticalLanes = []
    para cada lane L em previousCriticalLanes:
        // Se a lane atual é >= novo maxLanes (shrink raro), clamp pra borda
        L = Clamp(L, 0, currentMaxLanes - 1)
        offset = Random.Range(-1, 2)
        newL = Clamp(L + offset, 0, currentMaxLanes - 1)
        nextCriticalLanes.Add(newL)
    nextCriticalLanes = Distinct(nextCriticalLanes)

Passo 2: Garantir quantidade de critical paths
    enquanto Count < criticalPathsPerRow:
        // adiciona um novo critical path em lane aleatória
        addLane = Random.Range(0, currentMaxLanes)
        se !nextCriticalLanes.Contains(addLane):
            nextCriticalLanes.Add(addLane)

Passo 3: Marcar lanes garantidas
    nextLanes = new bool[currentMaxLanes]
    para cada lane em nextCriticalLanes:
        nextLanes[lane] = true

Passo 4: Popular decoys
    totalCount = nextCriticalLanes.Count
    para cada lane L de 0 a currentMaxLanes-1:
        se nextLanes[L] já é true: continue
        se Random.value < lanePopulationChance E totalCount < maxLanesPerRow:
            nextLanes[L] = true
            totalCount++

Passo 5: Garantir mínimo total
    enquanto totalCount < minLanesPerRow:
        L = lane aleatória ainda vazia
        nextLanes[L] = true
        totalCount++

Passo 6: Instanciar tiles + spawnar moedas
    para cada lane onde nextLanes[L] == true:
        instanciar TrackTile na posição correta (com currentMaxLanes)
        marcar IsOnCriticalPath se L está em nextCriticalLanes
        sortear SwitchState inicial random
        spawnar moedas via CoinSpawner conforme tier
```

### 4.3 Garantia Matemática

**Invariante:** dado que cada critical path avança com `offset ∈ {-1, 0, +1}` e clamp em `[0, currentMaxLanes-1]`, então da lane `Li` na linha N **sempre** existe uma posição de switch (`Left`, `Middle` ou `Right`) que alcança `Li+1` na linha N+1 — **enquanto `currentMaxLanes` não muda entre N e N+1**.

**Caso especial — tier mudou entre N e N+1:** se `maxLanes` cresceu, não há problema (o critical path continua válido). Se diminuiu, o clamp do Passo 1 reposiciona o critical path dentro do novo range, e o jogador continua com pelo menos uma rota válida.

### 4.4 Comportamento de Borda

Quando o jogador está em **lane 0** com switch em `Left`, `TargetLane = -1` → **Game Over (OutOfBounds)**. Mesma coisa para a lane máxima com `Right`. A seta visualmente aponta pra fora — responsabilidade do jogador.

---

## 5. Modelo de Dados

### `TrackTile`
```csharp
public class TrackTile : MonoBehaviour
{
    public int Row;
    public int Lane;
    public int MaxLanesAtSpawn; // para posicionamento consistente
    public SwitchController Switch;
    public Transform StartPoint;
    public Transform EndPoint;
    public CoinSpawner Coins;
    public bool IsOnCriticalPath;

    // Futuro: ObstacleSpawner, PowerUpSpawner
}
```

### `SwitchState` (enum)
```csharp
public enum SwitchState { Left = -1, Middle = 0, Right = 1 }
```

### `SwitchController`
```csharp
public class SwitchController : MonoBehaviour
{
    public SwitchState State;
    public int TargetLane => OwnerTile.Lane + (int)State;
    public Transform ArrowVisual;

    public void Nudge(int dir)
    {
        int next = Mathf.Clamp((int)State + dir, -1, 1);
        State = (SwitchState)next;
        UpdateArrowRotation();
    }

    void UpdateArrowRotation()
    {
        float angle = (int)State * 45f; // -45°, 0°, +45°
        ArrowVisual.localEulerAngles = new Vector3(0, angle, 0);
    }
}
```

### `RowData`
```csharp
public class RowData
{
    public int RowIndex;
    public int MaxLanesAtSpawn;
    public TrackTile[] Tiles; // indexado por lane, null = lane vazia
    public int[] CriticalLanes;
    public bool HasTile(int lane) => lane >= 0 && lane < Tiles.Length && Tiles[lane] != null;
}
```

---

## 6. Lógica de Conexão e Game Over

```csharp
void OnReachSwitchPoint()
{
    int targetLane = currentTile.Switch.TargetLane;
    RowData nextRow = railManager.GetRow(currentTile.Row + 1);

    // Out of bounds — baseado no maxLanes da PRÓXIMA linha
    if (targetLane < 0 || targetLane >= nextRow.MaxLanesAtSpawn)
    {
        gameManager.TriggerGameOver(GameOverReason.OutOfBounds);
        return;
    }

    if (nextRow.HasTile(targetLane))
    {
        TransitionToTile(nextRow.Tiles[targetLane]);
    }
    else
    {
        gameManager.TriggerGameOver(GameOverReason.DeadEnd);
    }
}
```

`GameOverReason` ajuda a debugar tuning.

A transição durante o gap: lerp de posição X durante o tempo equivalente a atravessar `rowGap`. Z avança normalmente.

---

## 7. Player Movement

```csharp
public class PlayerRailRider : MonoBehaviour
{
    public float Speed; // setado pelo DifficultyManager
    private TrackTile currentTile;
    private bool inGap;
    private Vector3 gapStartPos, gapEndPos;
    private float gapProgress;

    void Update()
    {
        // Speed sempre lido do DifficultyManager
        Speed = difficultyManager.CurrentTier.playerSpeed;

        if (inGap)
        {
            gapProgress += (Speed * Time.deltaTime) / config.rowGap;
            transform.position = Vector3.Lerp(gapStartPos, gapEndPos, gapProgress);
            if (gapProgress >= 1f) ExitGap();
        }
        else
        {
            transform.position += Vector3.forward * (Speed * Time.deltaTime);
            if (transform.position.z >= currentTile.EndPoint.position.z)
                EnterGap();
        }

        // Atualiza dificuldade com distância
        difficultyManager.UpdateDistance(transform.position.z);
    }
}
```

---

## 8. Câmera, Visibilidade e Zoom

### 8.1 Arquitetura

A câmera **não é filha do player**. Rig dedicado:

```csharp
public class PlayerCameraRig : MonoBehaviour
{
    public Transform player;
    public RailGenConfig config;
    public DifficultyManager difficulty;
    private float currentZoom;

    void LateUpdate()
    {
        Vector3 target = player.position + Vector3.forward * config.cameraLookAhead;
        Vector3 desiredPos = target
            + Vector3.back * config.cameraDistance
            + Vector3.up * currentZoom;

        transform.position = desiredPos;
        transform.rotation = Quaternion.Euler(config.cameraTilt, 0, 0);

        // Zoom adaptativo dirigido pelo tier
        var tier = difficulty.CurrentTier;
        float speedFactor = Mathf.InverseLerp(8f, 20f, tier.playerSpeed);
        float targetZoom = Mathf.Lerp(tier.cameraZoomMin, tier.cameraZoomMax, speedFactor);
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * config.cameraZoomSpeed);
    }
}
```

### 8.2 Visibilidade

`rowsAhead = 12` (default) cobre folgadamente o que a câmera mostra com zoom máximo. Quando o tier sobe e zoom aumenta, mais linhas precisam estar visíveis simultaneamente — mas como `rowsAhead` é fixo no MVP, basta deixá-lo alto. Se necessário, no futuro vinculamos `rowsAhead` ao tier.

### 8.3 Inclinação

`cameraTilt` configurável em runtime via inspector. Recomendação: 30–40°.

---

## 9. Parâmetros Globais (`RailGenConfig`)

Parâmetros que **não** variam com dificuldade ficam aqui:

| Parâmetro | Tipo | Default | Descrição |
|---|---|---|---|
| `laneSpacing` | float | 2.5f | Distância lateral (X) entre lanes |
| `trackLength` | float | 10f | Comprimento (Z) de cada trilho |
| `rowGap` | float | 2f | Espaço entre o fim de uma linha e o início da próxima |
| `rowsAhead` | int | 12 | Quantas linhas spawnar à frente |
| `rowsBehind` | int | 2 | Quantas linhas manter atrás |
| `cameraTilt` | float | 35f | Inclinação da câmera (graus) |
| `cameraDistance` | float | 6f | Offset Z |
| `cameraLookAhead` | float | 4f | Offset target pra frente |
| `cameraZoomSpeed` | float | 8f | Velocidade de transição de zoom |

Parâmetros que **variam com dificuldade** vivem nos tiers do `DifficultyConfig`.

---

## 10. Input

`InputManager` simples:
- `Input.GetKeyDown(KeyCode.LeftArrow)` → `currentTile.Switch.Nudge(-1)`
- `Input.GetKeyDown(KeyCode.RightArrow)` → `currentTile.Switch.Nudge(+1)`

Mobile (futuro): tap nas zonas esquerda/direita. Abstrair via `IDirectionalInput`.

O jogador pode trocar o switch do tile atual a qualquer momento enquanto estiver nele.

---

## 11. Streaming de Linhas (Spawn/Despawn)

```csharp
void Update()
{
    int playerRow = player.CurrentTile.Row;

    while (highestSpawnedRow < playerRow + rowsAhead)
    {
        int currentMaxLanes = difficulty.CurrentTier.maxLanes;
        var newRow = generator.GenerateNextRow(rows[highestSpawnedRow], highestSpawnedRow + 1, currentMaxLanes);
        rows[++highestSpawnedRow] = newRow;
    }

    while (lowestSpawnedRow < playerRow - rowsBehind)
    {
        DestroyRow(rows[lowestSpawnedRow]);
        rows.Remove(lowestSpawnedRow);
        lowestSpawnedRow++;
    }
}
```

Pooling fica para depois do MVP.

---

## 12. Arquitetura de Scripts

```
Assets/Scripts/RailSwitchMVP/
├── Config/
│   ├── RailGenConfig.cs           [ScriptableObject — parâmetros globais]
│   └── DifficultyConfig.cs        [ScriptableObject — lista de tiers]
├── Core/
│   ├── GameManager.cs             [singleton, estados Playing/GameOver]
│   ├── DifficultyManager.cs       [tier atual, eventos de mudança]
│   ├── RailManager.cs             [gerencia rows, spawn/despawn]
│   └── ProceduralRailGenerator.cs [algoritmo de critical path]
├── Track/
│   ├── TrackTile.cs
│   ├── SwitchController.cs
│   ├── RowData.cs                 [POCO]
│   └── CoinSpawner.cs
├── Collectibles/
│   ├── CollectibleCoin.cs
│   └── CoinManager.cs             [singleton]
├── Player/
│   ├── PlayerRailRider.cs
│   └── PlayerCameraRig.cs
└── Input/
    ├── IDirectionalInput.cs
    └── KeyboardDirectionalInput.cs

# Futuro (reservado, não implementado no MVP):
├── Obstacles/                     [ObstacleSpawner.cs, ObstacleBase.cs]
└── PowerUps/                      [PowerUpSpawner.cs, PowerUpBase.cs]
```

---

## 13. Plano de Implementação Iterativo

**Iteração 1 — Cena estática + câmera + difficulty:**
- Criar `RailGenConfig`, `DifficultyConfig` com 1 tier inicial.
- `DifficultyManager` lendo do SO.
- Hardcodar 3 linhas com layouts fixos.
- Player movendo-se forward, lendo speed do tier.
- `PlayerCameraRig` com tilt + look-ahead + zoom adaptativo.
- *Objetivo: validar movimento, framing, escala visual.*

**Iteração 2 — Switches + transição + moedas:**
- `SwitchController` com seta única rotacionando.
- Input ←/→.
- Transição do player entre tiles via switch.
- `CoinSpawner` espalhando moedas equidistantes nos tiles (hardcoded ainda).
- `CollectibleCoin` + `CoinManager`.
- Game Over `DeadEnd` e `OutOfBounds`.
- *Objetivo: validar input loop, coleta e Game Over.*

**Iteração 3 — Geração procedural com Critical Path:**
- `ProceduralRailGenerator` completo.
- `RailManager` com spawn ahead / despawn behind.
- Moedas spawnando conforme `isCriticalPath`.
- **Debug:** Gizmos coloridos no critical path para validação visual.
- *Objetivo: validar geração infinita e correta.*

**Iteração 4 — Dificuldade dinâmica:**
- `DifficultyConfig` populado com 5–6 tiers.
- `DifficultyManager` avançando tiers conforme distância.
- Geração respondendo ao `maxLanes` mutável.
- Speed e zoom escalando junto.
- Botão de debug para `ResetDifficulty()`.
- *Objetivo: validar progressão.*

**Iteração 5 — Stress test:**
- Headless: gerar 10000 linhas, validar que sempre há critical path mesmo com tier changes.
- Profiler: 60fps com 12 rows × até 9 lanes ativas.

---

## 14. Critérios de Sucesso do MVP

- [ ] Player se move infinitamente para frente.
- [ ] Critical path sempre existe entre linhas consecutivas (validar headless).
- [ ] Decoys podem ser becos sem saída.
- [ ] Moedas spawnam predominantemente no critical path.
- [ ] Player coleta moedas ao passar.
- [ ] `CoinManager.Total` reflete corretamente.
- [ ] Dificuldade avança em tiers conforme distância.
- [ ] `ResetDifficulty()` funciona.
- [ ] `maxLanes` muda em runtime sem quebrar geração nem critical path.
- [ ] Jogador vê 5–7 linhas à frente com tempo confortável em todos os tiers.
- [ ] Game Over distingue `DeadEnd` de `OutOfBounds`.
- [ ] FPS 60+ no editor com 12 rows × 9 lanes.

---

## 15. Reservas Arquiteturais para Futuro

### 15.1 Obstáculos

`TrackTile` terá futuramente um `ObstacleSpawner`. Quando ativo, spawna obstáculo na metade do tile (configurável). Colisão = Game Over (`HitObstacle`).

Decisão a tomar quando chegar lá: decoys têm mais obstáculos? Critical path tem menos? Probabilidade por tier?

### 15.2 Power-ups

`TrackTile` terá futuramente um `PowerUpSpawner`. Spawn raro, em qualquer tile (mais comum em decoys, talvez, pra criar dilema risco/recompensa).

Power-up provável: **Slow-down** ou **Difficulty Reset**, alinhado com o pilar de "tempos em tempos poder resetar a dificuldade".

### 15.3 Multiplicadores de moedas

Critical path com 5 moedas vs decoy com 1 moeda + power-up dá interessante dilema. Arquitetura já comporta.

---

## 16. Pontos em Aberto

1. **Curva de tiers default:** a tabela em 2.4 é um chute. Vamos calibrar depois de jogar.
2. **Reset durante o jogo:** o reset deve ter feedback visual (flash, sound)? Decidir quando implementarmos.
3. **Moedas em decoys:** começamos com 0 (sinalização limpa) ou com 1 (cria tentação)? Sugestão: **0 no MVP**, adicionamos tentação depois.
4. **Tempo de reação alvo:** ainda ~2.5s, ajustar tier a tier.

> **Status pós-MVP1:**
> - Ponto 2 (reset feedback) — endereçado via **transition seed**: o
>   critical path drifta 1 lane/row até o centro canônico do tier 0,
>   evitando DeadEnd injusto quando o player reseta longe do centro.
>   Feedback visual de flash/som ainda pendente.
> - Ponto 3 (moedas em decoys) — decidido: **0**, mantido até hoje.
> - Pontos 1 e 4 (curva de tiers e tempo de reação) — ainda em
>   calibração via playtesting. A `SpawnOverrideController` (§17.8.2)
>   foi adicionada justamente pra acelerar essa calibração.

---

## 17. Pós-MVP1 — Estado Atual Consolidado

> Esta seção sintetiza tudo que foi adicionado após o MVP1 fechar
> (tag `v0.1.0-mvp`). Cada bloco aponta pro doc detalhado em `Docs/`
> e pra tag de release quando aplicável. Para o estado vivo (o que
> foi feito hoje, o que falta), ver `PROGRESS.md`.

### 17.1 MVP2 — Risco/Recompensa (tag `v0.2.0-mvp2`)

Adiciona dilemas reais ao gameloop validado no MVP1. Quatro iterações:

- **Obstáculos letais** (`LethalObstacle`) só em decoys — reforça
  "moeda = caminho seguro". Critical path nunca recebe. Colisão =
  Game Over com razão `HitObstacle`. Detalhes em
  `MVP2_Iteracao1_Obstaculos.md`.
- **HUD básico** (`HUDController` + `GameTimer`): tempo (mm:ss),
  distância (m), moedas, tier, mais indicadores ativos pra power-ups.
  Detalhes em `MVP2_Iteracao2_HUD.md`.
- **Game Over screen** (`GameOverController`): razão, 5 stats finais,
  botão Restart (também tecla `R`). `Time.timeScale = 0` durante.
  Detalhes em `MVP2_Iteracao3_GameOver.md`.
- **Power-ups e Barreira:** `PowerUpManager` singleton com Shield,
  SlowDown, Magnet, DifficultyReset. `PowerUpBase` abstract +
  `PowerUpSpawner` paralelo aos demais spawners. `BarrierObstacle`
  consumida por Shield. Tile com hazard nunca recebe power-up.
  Detalhes em `MVP2_Iteracao4_PowerUps.md`.

Plano original e decisões descartadas em `MVP2_Plan.md`.

### 17.2 Pooling de tiles

`PrefabPool` singleton armazena tiles + hazards + power-ups
desativados em vez de destruí-los. `RailManager.DespawnRow` faz
`PrefabPool.Release` quando disponível, e `ProceduralRailGenerator`
usa `PrefabPool.Spawn` na geração. `TrackTile.ResetForReuse` zera
estado quando vem do pool. Reduz GC spikes em runs longas.
Detalhes em `PostMVP2_Pooling.md`.

### 17.3 Power-ups passivos (PostMVP2.2)

Quatro power-ups que rodam por N tiles ao serem coletados:

- **2x Coins** — `PowerUpManager.CoinMultiplier` aplicado em
  `CollectibleCoin.OnTriggerEnter`.
- **Ghost** — `ObstacleBase.OnTriggerEnter` skip se Ghost ativo.
  Estendido (commits `84972b5` + `ec3903e` + `53a24f9` + `88b0b95`)
  pra atravessar **OutOfBounds** (clamp pra current lane) e
  **DeadEnd** (player voa sobre rows vazias até encontrar tile).
  Rescue land com lerp 0.2s se Ghost expira mid-flight.
- **Lane Preview** — HUD mostra direção da próxima switch coerente
  com o critical path (←/→/center).
- **Coin Radar** — scale pulse em coins do critical path,
  acompanhado de cor mais saturada.

Detalhes em `PostMVP2_2_PassivePowerUps.md`.

### 17.4 Active items (PostMVP2.3)

Sistema de slot único (`ActiveItemSlot` singleton) que substitui
o item ao pegar novo. Disparado por **Space** via
`ActiveItemInputHandler`. Falha não consome (slot vazio = no-op).

- **TimeFreezeController** — `Time.timeScale = 0` por 3s unscaled.
- **TeleportController** — lateral ±1 lane via switch state.
  `PlayerRailRider.TeleportToAdjacent` preserva Z e muda só X.

HUD com `activeItemText` mostrando "Item: TimeFreeze (Space)" ou "-".
Detalhes em `PostMVP2_3_ActiveItems.md`.

### 17.5 Hazards não-letais (PostMVP2.5)

Três obstáculos que afetam mecânica sem matar:

- **SpeedUp zone** — `+50%` speed por 6 tiles, stacks somam.
- **Lane Swap** — inverte ←/→ por 2 tiles, stack reseta.
- **Vortex** — instant: redireciona switch pra direção válida.
  Modos `OppositeOfSwitch` ou `PureRandom`.

`DifficultyConfig` populado com curva escalada (Tier 0 = 0%,
Tier 5 = 20/15/15%). HUD com indicadores ativos.
Detalhes em `PostMVP2_5_NewObstacles.md`.

### 17.6 3 Ideas de UX (PostMVP2.4)

- **Idea 1 — Warmup 3-2-1-GO:** 5 rows single-center em 0.5× speed,
  countdown na tela, input liberado (player aprende fazendo).
  `WarmupController` + `RailGenConfig.warmupRowCount`. Tiers e
  distância lógica não avançam durante warmup.
- **Idea 2 — Trilhos coloridos por switch state:** seta colorida
  verde se a próxima row tem tile na direção apontada, vermelha
  se vai pra fora/vazio. Atualizado em tempo real via
  `TrackTile.UpdateConnectivityVisual`.
- **Idea 3 — Auto-follow critical (tile-based power-up):**
  `AutoCriticalFollower` sobrescreve switch state pra mirar
  critical na próxima row. Ativo via **debug toggle** ou
  **power-up coletado** (`PowerUpManager.HasAutoCriticalFollow`).
  Manual input ainda funciona (player pode "burlar" pra pegar
  decoy intencional).

Detalhes em `PostMVP2_4_3Ideas.md`.

### 17.7 High Score persistente (PostMVP2.D)

`HighScoreManager` singleton salva 4 bests via `PlayerPrefs`
(distância/coins/tier/tempo). `GameOverController` exibe
"Current ★ (Best: X)" inline e overlay opcional
"★ NEW RECORD! Distance Coins..." quando aplicável.
Botão "Reset Best Scores" no debug F1.
Detalhes em `PostMVP2_D_HighScore.md`.

### 17.8 Ferramentas de Debug

Duas tools OnGUI separadas, gated em `Application.isEditor ||
Debug.isDebugBuild`. Uma de cada vez (anchor à direita).

#### 17.8.1 `DebugPanelController` (toggle **F1**)

Ações **pontuais** durante o playtest:
- **Grant** de cada power-up (consome Inventory slot quando ativo).
- **Spawn** de cada hazard/power-up prefab N rows à frente do
  player (botão ao lado de cada Grant).
- Tier shortcuts (jump direto), force Game Over, add coins.
- Auto-follow critical (`AutoCriticalFollower.DebugForceActive`).
- Reset Best Scores.

#### 17.8.2 `SpawnOverrideController` (toggle **F2**)

Controle **contínuo** do spawn em runtime:
- **Master ON/OFF** — quando OFF, gerador usa `DifficultyTier`
  normalmente (zero impacto).
- **Por hazard** (Lethal, Barrier, SpeedUp, LaneSwap, Vortex)
  e **por power-up individualmente**: toggle enabled +
  slider de chance + location (Critical/Decoy/Both) + botão `solo`.
- **Global multiplier** (0–5×) em todas as chances.
- **Snapshot from current tier** — copia valores do tier ativo
  pros sliders.
- **rowsAhead override** (toggle separado + slider 1–20) — reduz
  janela de streaming. `RailManager` auto-despawna o excedente à
  frente e re-seed o gerador via
  `ProceduralRailGenerator.SetPreviousCriticalLanes(...)`. Permite
  ver mudanças de slider em ~3 tiles em vez de ~12.
- **Tier lock** (toggle + slider) — força e mantém o
  `DifficultyManager` num tier fixo (bypassa auto-advance).
- **Auto-follow critical** — mesmo state do F1, espelhado aqui pra
  playtest one-handed.

Integração no gerador: a cascata `if-else` de hazards e o
`Random.Range` uniforme de power-ups foram substituídos por
chamadas a `SpawnOverrideController.Instance.ResolveHazard(...)` e
`ResolvePowerUpPrefab(...)`. `ResolveHazardClassic` /
`ResolvePowerUpClassic` no próprio gerador servem de fallback
quando o controller não está na cena (preserva comportamento
exato do anterior).

### 17.9 Componentes adicionados (referência cruzada)

| Componente | Função | Adicionado em |
|---|---|---|
| `ObstacleBase` + `LethalObstacle` + `BarrierObstacle` | Hazards letais (Lethal) e absorvíveis por Shield (Barrier) | MVP2 Iter 1 + 4 |
| `PowerUpBase` + `PowerUpManager` + 4 pickups | Sistema de power-ups | MVP2 Iter 4 |
| `HUDController` + `GameTimer` | HUD persistente | MVP2 Iter 2 |
| `GameOverController` | Tela de Game Over com Restart | MVP2 Iter 3 |
| `PrefabPool` | Pooling de tiles/hazards/power-ups | PostMVP2 |
| `HazardWarning` | Ícone flutuante N rows antes do hazard | PostMVP2 |
| `ActiveItemSlot` + `TimeFreezeController` + `TeleportController` + `ActiveItemInputHandler` | Sistema de active item slot | PostMVP2.3 |
| `WarmupController` | Countdown 3-2-1-GO + warmup rows | PostMVP2.4 (Idea 1) |
| `AutoCriticalFollower` | Auto-follow do critical path | PostMVP2.4 (Idea 3) |
| `SpeedUpZone` + `LaneSwap` + `Vortex` | Hazards não-letais | PostMVP2.5 |
| `HighScoreManager` | 4 bests persistentes via PlayerPrefs | PostMVP2.D |
| `DebugPanelController` | Debug pontual (F1) | Pós-MVP2 |
| `SpawnOverrideController` | Override contínuo de spawn (F2) | 2026-05-22 |

### 17.10 Próximos passos (descartados pelo user, podem voltar)

Sequência foi: gameplay → polish técnico → power-ups → hazards →
UX → persistência → debug tooling. Restam pendentes (decididos
pelo user como não-prioritários por enquanto):

- **C — Audio**: música ambiente + SFX em coletas/hits/Game Over.
- **E — Polish visual**: modelos no lugar dos primitivos atuais.
- **F — Mobile**: `TouchDirectionalInput` para deploy mobile.
- **G — Animações UI**: fade-in Game Over, pulse em ícones HUD.

Próximo passo provável é **playtesting puro** pra calibrar
tunables — `SpawnOverrideController` (§17.8.2) foi adicionada
justamente pra acelerar isso.

---

*Fim do doc v3. Quando aprovado, partimos pra Iteração 1.*
