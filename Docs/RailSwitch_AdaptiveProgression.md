# Rail Switch — Sistema de Progressão Adaptativa
## Spec para implementação (Claude Code)

**Problema que resolve:** o "re-onboarding tax" — todo run força o jogador veterano a
recomeçar do início lento e fácil, projetado para novatos. Quanto melhor o jogador fica,
mais frustrante isso se torna, porque a parte interessante do jogo (alta velocidade, caos
estratégico) fica cada vez mais longe do botão "play again".

**Solução:** três camadas independentes e combináveis que ajustam o ponto de partida e a
recuperação do jogador conforme ele progride, sem punir o novato.

---

## Visão Geral das 3 Camadas

| Camada | Nome | O que faz | Quando atua |
|---|---|---|---|
| 1 | Speed Floor por Progressão | Sobe a velocidade/tier inicial conforme o account level | Automática, sempre |
| 2 | Head Start Opcional | Permite pular direto para um tier mais alto (coin/ad) | Opcional, antes do run |
| 3 | Continue após Morte | Revive no ponto onde morreu (coin/ad) | Opcional, após morte |

As três funcionam juntas. A Camada 1 é o fundo invisível; a 2 e a 3 são escolhas do jogador.

---

## Dependências e Pré-Requisitos

Esta spec assume que já existem:
- `PlayerDataManager` (coins, best distance, account level/XP)
- `DifficultyManager` (tiers, CurrentTierIndex, UpdateDistance)
- `DifficultyConfig` (lista de tiers com triggerAtDistance, playerSpeed, etc.)
- `PlayerRailRider` (movimento, DistanceTraveled)
- `GameOverController` (tela de morte)
- Sistema de Rewarded Ads (Unity Ads) — para opções de ad

Se `PlayerDataManager` ainda não tem `AccountLevel`, adicionar primeiro (ver seção da Camada 1).

---

# CAMADA 1 — Speed Floor por Progressão

## 1.1 Conceito

A velocidade inicial de cada run não é sempre o Tier 0. Ela escala com o **account level**
do jogador, até um teto. Um jogador iniciante começa no Tier 0 (lento, didático). Um jogador
experiente começa num tier mais alto, porque já provou que domina o básico.

A escala é baseada em **account level** (tempo/engajamento acumulado), não em best distance
(skill puro). Isso é intencional: é mais previsível, mais suave, e não joga o jogador num
tier que ele talvez não domine só porque teve uma run de sorte.

## 1.2 Pré-requisito: Account Level no PlayerDataManager

Se ainda não existe, adicionar ao `PlayerDataManager`:

```
Chaves PlayerPrefs:
"account_xp"     → int (default 0) — XP acumulado lifetime
"account_level"  → int (default 1) — nível derivado do XP
```

XP ganho ao fim de cada run:
```
runXP = floor(distance / 10) + coinsCollected + (missionsCompletedThisRun * 50)
```

Curva de nível (simples, previsível):
```
xpParaProximoNivel(level) = 100 * level * 1.5

Exemplos:
Nível 1 → 2: 150 XP
Nível 2 → 3: 300 XP
Nível 3 → 4: 450 XP
Nível 10 → 11: 1500 XP
```

Método para recalcular o nível a partir do XP total:
```csharp
public int ComputeLevelFromXP(int totalXP)
{
    int level = 1;
    int xpRemaining = totalXP;
    while (xpRemaining >= XpForNextLevel(level))
    {
        xpRemaining -= XpForNextLevel(level);
        level++;
    }
    return level;
}

int XpForNextLevel(int level) => Mathf.RoundToInt(100 * level * 1.5f);
```

## 1.3 Mapeamento Account Level → Starting Tier

Configurável via `DifficultyConfig` (adicionar uma nova lista). A ideia é:

```
Account Level  →  Starting Tier (índice do tier inicial do run)
1 – 4          →  Tier 0
5 – 9          →  Tier 1
10 – 19        →  Tier 2
20 – 34        →  Tier 3
35+            →  Tier 4 (teto inicial)
```

**Importante — o teto.** O starting tier NUNCA deve ser o último tier disponível. Sempre
deixar pelo menos 1–2 tiers acima do starting tier para o jogo ainda ter pra onde acelerar
durante o run. Se `DifficultyConfig` tem 6 tiers (0–5), o starting tier máximo é o Tier 4.

## 1.4 Estrutura no DifficultyConfig

Adicionar ao `DifficultyConfig.cs`:

```csharp
[System.Serializable]
public struct StartingTierRule
{
    [Tooltip("Account level mínimo para esta regra se aplicar")]
    public int minAccountLevel;

    [Tooltip("Índice do tier em que o run começa (0-based)")]
    public int startingTierIndex;
}

[Header("Adaptive Start (Camada 1)")]
[Tooltip("Regras de starting tier por account level, em ordem crescente de minAccountLevel")]
public List<StartingTierRule> startingTierRules = new List<StartingTierRule>();

[Tooltip("Teto absoluto de starting tier. Nunca começar acima disto, "
       + "garantindo que sempre haja tiers acima para acelerar.")]
public int maxStartingTierIndex = 4;
```

Valores default para preencher no inspector:
```
startingTierRules:
  [0] minAccountLevel=1,  startingTierIndex=0
  [1] minAccountLevel=5,  startingTierIndex=1
  [2] minAccountLevel=10, startingTierIndex=2
  [3] minAccountLevel=20, startingTierIndex=3
  [4] minAccountLevel=35, startingTierIndex=4
maxStartingTierIndex: 4
```

## 1.5 Método de Resolução do Starting Tier

Adicionar ao `DifficultyConfig.cs`:

```csharp
public int GetStartingTierIndex(int accountLevel)
{
    if (startingTierRules == null || startingTierRules.Count == 0)
        return 0;

    int result = 0;
    foreach (var rule in startingTierRules)
    {
        if (accountLevel >= rule.minAccountLevel)
            result = rule.startingTierIndex;
        else
            break; // lista está em ordem crescente
    }

    // Aplica o teto e nunca passa do penúltimo tier disponível
    int safeCeiling = Mathf.Min(maxStartingTierIndex, tiers.Count - 2);
    safeCeiling = Mathf.Max(0, safeCeiling); // proteção se houver poucos tiers
    return Mathf.Min(result, safeCeiling);
}
```

## 1.6 Modificação no DifficultyManager

O `DifficultyManager` precisa começar o run no tier resolvido, não sempre no 0.

Adicionar/modificar:

```csharp
public void StartRunWithAdaptiveTier(int accountLevel, float headStartTierOverride = -1)
{
    int startTier;

    if (headStartTierOverride >= 0)
    {
        // Camada 2 sobrepõe a Camada 1
        startTier = Mathf.RoundToInt(headStartTierOverride);
    }
    else
    {
        startTier = config.GetStartingTierIndex(accountLevel);
    }

    startTier = Mathf.Clamp(startTier, 0, config.tiers.Count - 1);

    currentTierIndex = startTier;
    currentTier = config.tiers[startTier];

    // A distância inicial deve refletir o tier inicial, para que UpdateDistance
    // não rebaixe imediatamente o tier no primeiro frame.
    distanceTraveled = config.tiers[startTier].triggerAtDistance;

    OnTierChanged?.Invoke(currentTier);
}
```

**Atenção crítica (geração procedural):** se o run começa no Tier 3 com `maxLanes = 7`,
o `ProceduralRailGenerator` deve gerar as primeiras linhas já com 7 lanes — não começar
com 3 e expandir. Garantir que o gerador lê `DifficultyManager.CurrentTier.maxLanes` desde
a primeira linha do run.

## 1.7 Fluxo de Início de Run (Camada 1 ativa)

```
Jogador clica PLAY
  → int level = PlayerDataManager.Instance.AccountLevel
  → DifficultyManager.Instance.StartRunWithAdaptiveTier(level)
  → ProceduralRailGenerator gera primeiras linhas com CurrentTier.maxLanes
  → PlayerRailRider começa com CurrentTier.playerSpeed
```

## 1.8 Cuidado com a Distância e o Leaderboard

Se o run começa no Tier 3 com `distanceTraveled = triggerAtDistance do Tier 3`, isso
**não pode contar como distância "grátis"** no score do jogador.

Separar dois conceitos:
- `distanceTraveled` (interno, usado para tier progression) — começa no triggerAtDistance do starting tier
- `scoreDistance` (exibido e salvo) — começa SEMPRE em 0, conta só o que o jogador realmente percorreu

```csharp
// No PlayerRailRider:
float scoreDistance = transform.position.z - runStartZ; // distância real percorrida
```

O leaderboard e o best distance usam `scoreDistance`, nunca `distanceTraveled`.
Isso garante que dois jogadores em tiers iniciais diferentes sejam comparados de forma justa.

---

# CAMADA 2 — Head Start Opcional

## 2.1 Conceito

Antes de iniciar o run, o jogador pode optar por **começar adiantado** — pulando direto
para um tier mais alto que o seu starting tier natural (Camada 1). Custa coins ou um
rewarded ad. Dá agência ao veterano que quer ir direto ao desafio, e gera receita/sink de coins.

## 2.2 Regra de Desbloqueio (proteção do novato)

O Head Start só fica disponível depois que o jogador prova que passou da fase de aprendizado:

```
Head Start destrava quando: best_distance >= 500 (configurável)
```

Antes disso, o botão não aparece. Isso impede que um novato pule a fase didática e morra
frustrado em 3 segundos.

## 2.3 Opções de Head Start

Oferecer 1–2 níveis de head start acima do starting tier natural:

```
Starting tier natural (Camada 1) = T
Head Start opção A: começar no Tier T+1 — custa 200 coins OU 1 rewarded ad
Head Start opção B: começar no Tier T+2 — custa 500 coins (sem opção de ad)
```

Limitar o head start ao teto seguro (mesma regra da Camada 1: nunca o último tier).

## 2.4 Estrutura de Configuração

Adicionar ao `RailGenConfig` ou criar um pequeno SO `HeadStartConfig`:

```csharp
[Header("Head Start (Camada 2)")]
[Tooltip("Best distance mínimo para destravar o Head Start")]
public float headStartUnlockDistance = 500f;

[Tooltip("Custo em coins para pular +1 tier")]
public int headStartCostPlus1 = 200;

[Tooltip("Custo em coins para pular +2 tiers")]
public int headStartCostPlus2 = 500;

[Tooltip("Permitir rewarded ad como alternativa ao custo +1")]
public bool headStartAllowAdForPlus1 = true;
```

## 2.5 Novo Script: HeadStartController

```
Namespace: RailSwitchMVP.Meta
Path:      Assets/Scripts/RailSwitchMVP/Meta/HeadStartController.cs
Tipo:      MonoBehaviour, na HomeScene (ou tela pré-run)
```

Responsabilidades:
- Verificar se o head start está desbloqueado (`best_distance >= headStartUnlockDistance`).
- Calcular o starting tier natural do jogador (via DifficultyConfig.GetStartingTierIndex).
- Exibir as opções de head start (+1, +2) com seus custos.
- Ao escolher: debitar coins (via PlayerDataManager.SpendCoins) ou disparar rewarded ad.
- Passar o tier escolhido para o DifficultyManager via StartRunWithAdaptiveTier(level, override).

API:
```csharp
public bool IsHeadStartUnlocked();
public int GetNaturalStartTier();       // da Camada 1
public int GetMaxHeadStartTier();        // respeitando o teto seguro
public bool TryPurchaseHeadStart(int targetTier); // debita coins, retorna sucesso
public void WatchAdForHeadStart(int targetTier);  // dispara ad, no callback inicia run
```

## 2.6 Fluxo de Head Start

```
Tela pré-run (ou Home):
  → HeadStartController.IsHeadStartUnlocked()?
      → Não: esconder UI de head start, PLAY normal (Camada 1 only)
      → Sim: mostrar opções

  Jogador escolhe "Começar no Tier T+1 (200 coins)":
      → PlayerDataManager.SpendCoins(200)
          → sucesso: DifficultyManager.StartRunWithAdaptiveTier(level, T+1)
          → falha (coins insuficientes): mostrar "coins insuficientes" + oferecer ad

  Jogador escolhe "Assistir ad para Tier T+1":
      → dispara rewarded ad
      → callback de sucesso: DifficultyManager.StartRunWithAdaptiveTier(level, T+1)
      → callback de falha/skip: não inicia, volta pra escolha

  Jogador escolhe PLAY normal:
      → DifficultyManager.StartRunWithAdaptiveTier(level)  // sem override
```

## 2.7 Transação e Log

Toda compra de head start registra no log de transações (se houver) ou em PlayerPrefs:
```
type: "head_start"
amount: -200 (ou -500)
detail: { target_tier: T+1, account_level: level }
```

---

# CAMADA 3 — Continue após Morte

## 3.1 Conceito

Quando o jogador morre, em vez de ir direto pra tela de resultado, ele pode **reviver** e
continuar a run do ponto onde estava. Isso preserva o progresso e o momento emocional de
pico (o jogador acabou de fazer uma run boa e não quer perdê-la).

## 3.2 Regras

- Máximo de **2 continues por run** (configurável). Depois disso, sem mais revives.
- 1º continue: 1 rewarded ad OU 50 coins.
- 2º continue: 1 rewarded ad OU 150 coins (custo escala).
- Ao reviver: o jogador reaparece **alguns metros antes** do ponto de morte, em um trilho
  válido do critical path, com 1–2 segundos de invencibilidade/grace period.
- A velocidade/tier no momento da morte é **mantida** (não reseta).

## 3.3 Estrutura de Configuração

```csharp
[Header("Continue / Revive (Camada 3)")]
[Tooltip("Máximo de continues permitidos por run")]
public int maxContinuesPerRun = 2;

[Tooltip("Custo em coins do primeiro continue")]
public int continueCost1 = 50;

[Tooltip("Custo em coins do segundo continue")]
public int continueCost2 = 150;

[Tooltip("Quantos metros antes do ponto de morte o player reaparece")]
public float reviveSetbackDistance = 15f;

[Tooltip("Segundos de invencibilidade após reviver")]
public float reviveGraceSeconds = 1.5f;
```

## 3.4 Lógica de Revive

O ponto mais delicado: ao reviver, o jogador precisa reaparecer num **trilho válido**.
Como a morte pode ter sido por DeadEnd ou OutOfBounds, não dá pra simplesmente "voltar":
o trilho onde ele estava pode não ter conexão.

Procedimento de revive:
```
1. Pausar o estado de game over (não mostrar a tela ainda)
2. Recuar a posição Z do player em reviveSetbackDistance
3. Encontrar o critical path tile mais próximo nessa região (RailManager já sabe quais são)
4. Reposicionar o player nesse tile do critical path (lane segura garantida)
5. Resetar o switch desse tile para uma posição que conecta (Middle, tipicamente)
6. Ativar grace period (invencibilidade) por reviveGraceSeconds
7. Retomar Time.timeScale = 1
8. Incrementar continuesUsedThisRun
```

**Por que recuar e colocar no critical path:** garante que o jogador revive num lugar de
onde é possível continuar, evitando reviver e morrer no mesmo instante (experiência terrível).

## 3.5 Modificação no GameOverController

O fluxo de morte passa a ter um passo intermediário:

```
GameManager.TriggerGameOver(reason)
  → se continuesUsedThisRun < maxContinuesPerRun:
      → ReviveController.OfferContinue(reason)
          → Time.timeScale = 0
          → mostrar overlay de continue:
              "Continuar? [Assistir Ad]  ou  [50 coins]"
              countdown de ~5s (se não escolher, vai pra game over real)
          → se escolher ad: dispara rewarded ad → callback sucesso → ExecuteRevive()
          → se escolher coins: SpendCoins(custo) → ExecuteRevive()
          → se countdown zera ou recusa: GameOverController.ShowFinal()
  → senão (já usou todos os continues):
      → GameOverController.ShowFinal() direto
```

## 3.6 Novo Script: ReviveController

```
Namespace: RailSwitchMVP.Core
Path:      Assets/Scripts/RailSwitchMVP/Core/ReviveController.cs
Tipo:      MonoBehaviour, na GameScene
```

API:
```csharp
public int ContinuesUsedThisRun { get; private set; }
public bool CanContinue();                 // continuesUsedThisRun < max
public int GetCurrentContinueCost();       // 50 ou 150 conforme o número
public void OfferContinue(GameOverReason reason); // mostra overlay
public void ExecuteReviveWithCoins();
public void ExecuteReviveWithAd();
public void DeclineContinue();             // vai pra game over real
public void ResetForNewRun();              // zera ContinuesUsedThisRun
```

## 3.7 Reset entre Runs

`ReviveController.ResetForNewRun()` deve ser chamado no início de cada run (junto com
`CoinManager.Reset()` e `MissionTracker.StartRun()`).

---

# Integração das 3 Camadas no Fluxo de Run

## Início de Run (atualizado)

```
Jogador clica PLAY (ou escolhe Head Start na tela pré-run)
  → CoinManager.Reset()
  → MissionTracker.StartRun()
  → ReviveController.ResetForNewRun()
  → int level = PlayerDataManager.Instance.AccountLevel

  Se Head Start escolhido (Camada 2):
    → DifficultyManager.StartRunWithAdaptiveTier(level, chosenTier)
  Senão (Camada 1 automática):
    → DifficultyManager.StartRunWithAdaptiveTier(level)

  → ProceduralRailGenerator gera primeiras linhas com CurrentTier.maxLanes
  → PlayerRailRider.runStartZ = transform.position.z  // para scoreDistance
```

## Durante o Run

```
PlayerRailRider:
  → scoreDistance = transform.position.z - runStartZ
  → DifficultyManager.UpdateDistance(distanceTraveled interno)
  → (tier sobe normalmente a partir do starting tier)
```

## Morte (atualizado, Camada 3)

```
GameManager.TriggerGameOver(reason)
  → ReviveController.CanContinue()?
      → Sim: ReviveController.OfferContinue(reason)  // overlay de continue
      → Não: prosseguir para Game Over final
```

## Game Over Final

```
GameOverController.ShowFinal()
  → runXP = floor(scoreDistance/10) + coinsCollected + missionsCompleted*50
  → PlayerDataManager.AddXP(runXP)        // pode subir account level → afeta Camada 1 no próximo run
  → PlayerDataManager.AddCoins(CoinManager.Total)
  → PlayerDataManager.UpdateBestDistance(scoreDistance)
  → PlayerDataManager.IncrementTotalRuns()
  → MissionTracker.EndRun(scoreDistance, time, usedPowerUp)
  → PlayerDataManager.Save()
  → exibe stats + botões Play Again / Home
```

---

# Ordem de Implementação Sugerida

1. **Account Level no PlayerDataManager** (XP, level, ComputeLevelFromXP, AddXP)
   Testar: completar runs, ver XP subir, nível subir.

2. **Camada 1 — Speed Floor** (StartingTierRule no Config, GetStartingTierIndex,
   StartRunWithAdaptiveTier no DifficultyManager, separação scoreDistance vs distanceTraveled)
   Testar: forçar account level alto no inspector, ver run começar em tier mais alto;
   confirmar que scoreDistance começa em 0.

3. **Camada 3 — Continue** (ReviveController, modificação no GameOverController,
   lógica de revive no critical path com grace period)
   Testar: morrer, reviver com coins/ad, confirmar que reaparece em trilho válido.

4. **Camada 2 — Head Start** (HeadStartController, UI pré-run, desbloqueio por best distance)
   Testar: com best distance < 500 não aparece; com >= 500 aparece e funciona.

Implementar nesta ordem porque cada uma depende da anterior estar estável.
Camada 1 é a base (precisa do account level). Camada 3 é independente mas precisa do
fluxo de game over. Camada 2 é a última porque depende da Camada 1 (starting tier natural).

---

# Cuidados Críticos (não esquecer)

1. **scoreDistance ≠ distanceTraveled.** O leaderboard usa só o que o jogador realmente
   percorreu. Head start e adaptive tier NÃO dão distância grátis no score.

2. **Geração procedural respeita o starting tier desde a primeira linha.** Se começa no
   Tier 3, as primeiras linhas já têm maxLanes do Tier 3.

3. **Revive sempre num trilho do critical path** com switch conectável e grace period,
   nunca no trilho exato da morte (pode ser dead-end).

4. **Teto de starting tier** nunca é o último tier — sempre deixar espaço para acelerar.

5. **Head start protegido por best distance** para não jogar novato no caos.

6. **Pedir permissão de checkout no Perforce** antes de editar arquivos existentes
   (PlayerDataManager, DifficultyManager, DifficultyConfig, GameOverController, PlayerRailRider).

7. **Sem debug logs nem comentários nos scripts finais.** NRE guards e checagem de
   objeto ativo em coroutines, conforme padrão do projeto.

---

*Spec completa das 3 camadas. Cada seção é prescritiva.*
*Implementar na ordem indicada, testando cada camada antes da próxima.*
