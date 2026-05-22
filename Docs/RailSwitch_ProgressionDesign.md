# Rail Switch — Design de Progressão
## Spec completa para implementação

**Escopo:** tudo que o jogador faz FORA do gameplay pra progredir.
**Público:** Claude Code — cada seção é prescritiva o suficiente pra implementar sem decisão extra.

---

## Visão Geral do Loop Diário

O jogador típico abre o app e percorre este fluxo:

```
Abre o app
  → Login diário (popup automático, 1 clique, ganha coins)
  → Vê Home: coins, best distance, missões do dia
  → Joga Classic (3-8 runs)
      → Cada run: coleta coins in-game
      → Ao morrer: opção rewarded ad pra 2x coins
      → Coins do run somam ao total
      → Progresso das missões atualiza
  → Missões completadas: clica pra reclamar coins
  → Eventualmente gasta coins em personagem novo
  → Fecha o app
```

Tempo médio de sessão diária: 10-20 minutos.
Objetivo de retenção: jogador volta amanhã pelo login diário + missões novas.

---

## 1. Sistema de Coins

### 1.1 Fontes de Coins

| Fonte | Quantidade | Frequência |
|---|---|---|
| Coins coletadas no run | Variável (ver 1.2) | Por run |
| Rewarded ad "2x coins" | Dobra o run | 1x por run |
| Login diário | 50–500 (ver seção 2) | 1x por dia |
| Missão diária completa | 100–300 (ver seção 3) | Até 3x por dia |
| Missão semanal completa | 500–1000 | Até 3x por semana |

### 1.2 Coins In-Game por Run

Coins coletadas durante o run são transferidas para o total do jogador ao fim de cada run.
Quantidade depende da habilidade (seguir o critical path) e da sorte (quantas moedas spawnam).

Estimativa de coins por run por tier de dificuldade:

| Tier | Moedas por tile crítico | Tiles típicos/run | Coins estimadas/run |
|---|---|---|---|
| 0 (início) | 3 | 20–40 | 60–120 |
| 1 | 3 | 30–60 | 90–180 |
| 2 | 4 | 40–80 | 160–320 |
| 3 | 4 | 50–100 | 200–400 |
| 4–5 | 5 | 60–120 | 300–600 |

Run mediana (tier 1-2): ~150-200 coins.
Run excelente (tier 3+): 300-500 coins.

### 1.3 Gastos de Coins

| Gasto | Custo | Observação |
|---|---|---|
| Personagem 2 | 2.500 coins | ~15-20 runs medianas |
| Personagem 3 | 5.000 coins | ~30-40 runs medianas |
| (futuro) Power-up upgrade nível 2 | 500 coins | Pós-lançamento |
| (futuro) Power-up upgrade nível 3 | 1.500 coins | Pós-lançamento |

### 1.4 Economia Geral

Jogador casual (5 runs/dia):
- Ganha: ~750-1000 coins/dia (runs + missões + login)
- Personagem 2 desbloqueado em: ~3-4 dias
- Personagem 3 desbloqueado em: ~7-10 dias

Isso é intencional: o jogador engajado desbloqueia tudo em 1-2 semanas.
A retenção depois disso vem do Daily Challenge e missões, não da loja.

---

## 2. Login Diário

### 2.1 Regra

- O popup aparece automaticamente na abertura do app se ainda não foi reclamado hoje.
- Cooldown: 24 horas desde o último claim (não reseta à meia-noite — usa hora do claim).
- Sem streak: perder um dia não reseta nada, apenas pula aquele dia.
- O ciclo de 7 dias reinicia após o dia 7 ser completado.

### 2.2 Tabela de Recompensas

| Dia | Recompensa | Descrição |
|---|---|---|
| Dia 1 | 50 coins | Pequeno, acessível |
| Dia 2 | 100 coins | |
| Dia 3 | 150 coins | |
| Dia 4 | 200 coins | |
| Dia 5 | 300 coins | Começa a valer |
| Dia 6 | 400 coins | |
| Dia 7 | 500 coins | Recompensa do ciclo completo |

Total do ciclo completo: 1.700 coins (~10 runs medianas de bonus grátis).

### 2.3 Estado Salvo (PlayerPrefs)

```
"daily_login_day"          → int   (1-7, qual dia do ciclo)
"daily_login_last_claim"   → string (ISO date: "2026-05-22", data do último claim)
```

### 2.4 Lógica de Verificação

```
Ao abrir o app:
  last = PlayerPrefs["daily_login_last_claim"]
  today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")

  se last == today:
    → já reclamou hoje, não mostrar popup

  se last == ontem (today - 1 dia):
    → streak continua, avança dia (+1, wrap em 7)
    → mostrar popup com recompensa do próximo dia

  se last < ontem (pulou dias):
    → sem penalidade, apenas avança dia (+1, wrap em 7)
    → mostrar popup com recompensa do próximo dia

  se last == "" (primeira vez):
    → dia = 1
    → mostrar popup com recompensa do dia 1
```

---

## 3. Missões Diárias

### 3.1 Regra Geral

- 3 missões ativas por dia, geradas uma vez por dia.
- Resetam à meia-noite UTC.
- Cada missão tem 1 objetivo e 1 recompensa em coins.
- Progresso acumula durante o dia (múltiplos runs contam).
- Missão completa: aparece badge na tela Home. Jogador clica para reclamar.
- Coins não expiram se não reclamadas (reclamar no dia seguinte ainda funciona,
  mas a missão some quando as do novo dia são geradas).

### 3.2 Pool de Missões Diárias (20 missões, rotação pseudo-aleatória)

Implementação de rotação: `missionIndex = (dayOfYear % 20)` define o "grupo" do dia.
As 3 missões do dia são os índices `[baseIndex, baseIndex+1, baseIndex+2]` do pool abaixo,
com wrap. Isso garante que todo jogador no mesmo dia tem as mesmas 3 missões
(necessário para validação futura e social sharing).

| ID | Descrição | Tipo | Target | Recompensa |
|---|---|---|---|---|
| 0 | Colete 100 moedas em uma única run | single_run_coins | 100 | 100 |
| 1 | Colete 200 moedas em uma única run | single_run_coins | 200 | 150 |
| 2 | Colete 300 moedas em uma única run | single_run_coins | 300 | 200 |
| 3 | Chegue a 300m em uma run | single_run_distance | 300 | 100 |
| 4 | Chegue a 600m em uma run | single_run_distance | 600 | 150 |
| 5 | Chegue a 1000m em uma run | single_run_distance | 1000 | 200 |
| 6 | Sobreviva 30 segundos em uma run | single_run_time | 30 | 100 |
| 7 | Sobreviva 60 segundos em uma run | single_run_time | 60 | 150 |
| 8 | Sobreviva 90 segundos em uma run | single_run_time | 90 | 200 |
| 9 | Complete 2 runs | total_runs | 2 | 100 |
| 10 | Complete 5 runs | total_runs | 5 | 200 |
| 11 | Colete 500 moedas no total do dia | daily_total_coins | 500 | 150 |
| 12 | Colete 1000 moedas no total do dia | daily_total_coins | 1000 | 300 |
| 13 | Use um Shield em uma run | use_powerup | shield | 100 |
| 14 | Use um Magnet em uma run | use_powerup | magnet | 100 |
| 15 | Use um SlowDown em uma run | use_powerup | slowdown | 100 |
| 16 | Atinja o Tier 2 de dificuldade em uma run | reach_tier | 2 | 150 |
| 17 | Atinja o Tier 3 de dificuldade em uma run | reach_tier | 3 | 200 |
| 18 | Complete uma run sem usar nenhum power-up | no_powerup_run | 1 | 200 |
| 19 | Colete moedas em 10 tiles diferentes numa run | tiles_with_coins | 10 | 150 |

### 3.3 Tipos de Missão (para o MissionTracker)

Cada tipo requer rastreamento diferente. O `MissionTracker` precisa:

**`single_run_coins`** — resetar ao início de cada run, incrementar em `CoinManager.AddCoins()`.
Completa se atingir target em uma única run. Não acumula entre runs.

**`single_run_distance`** — checar ao fim da run (`PlayerRailRider.DistanceTraveled`).
Completa se a distância da run >= target.

**`single_run_time`** — checar ao fim da run (`GameTimer.ElapsedSeconds`).
Completa se o tempo da run >= target.

**`total_runs`** — incrementar +1 ao fim de cada run (independente do resultado).
Acumula entre runs durante o dia.

**`daily_total_coins`** — soma de todas as coins coletadas em todas as runs do dia.
Incrementar em cada `CoinManager.AddCoins()` em um contador separado.

**`use_powerup`** — checar se o power-up específico foi usado na run.
Completa se qualquer run do dia usar o power-up indicado.

**`reach_tier`** — checar o tier máximo atingido na run (`DifficultyManager.CurrentTierIndex`).
Completa se tier >= target em qualquer run do dia.

**`no_powerup_run`** — flag que começa `true` ao início do run, vira `false` ao usar qualquer power-up.
Completa se a flag for `true` ao fim da run.

**`tiles_with_coins`** — contador de tiles onde o jogador coletou pelo menos 1 moeda.
Incrementar em `CollectibleCoin.OnTriggerEnter`.

### 3.4 Estado Salvo (PlayerPrefs)

```
"missions_date"           → string  (data das missões ativas: "2026-05-22")
"mission_0_id"            → int     (ID da missão no slot 0)
"mission_0_progress"      → float   (progresso atual)
"mission_0_claimed"       → int     (0 ou 1)
"mission_1_id"            → int
"mission_1_progress"      → float
"mission_1_claimed"       → int
"mission_2_id"            → int
"mission_2_progress"      → float
"mission_2_claimed"       → int
```

### 3.5 Lógica de Geração Diária

```
Ao carregar as missões:
  savedDate = PlayerPrefs["missions_date"]
  today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd")

  se savedDate != today:
    → novo dia: gerar 3 novas missões
    baseIndex = (DateTime.UtcNow.DayOfYear % 20)  // 0-19
    slot0 = baseIndex % 20
    slot1 = (baseIndex + 1) % 20
    slot2 = (baseIndex + 2) % 20
    → salvar IDs, resetar progress e claimed para 0
    → salvar missions_date = today
  
  senão:
    → carregar do PlayerPrefs normalmente
```

---

## 4. Missões Semanais

### 4.1 Regra Geral

- 3 missões ativas por semana (segunda a domingo UTC).
- Mais difíceis que as diárias, recompensa maior.
- Mesma lógica de reclamação: badge na Home, clica pra receber.

### 4.2 Pool de Missões Semanais (10 missões)

| ID | Descrição | Tipo | Target | Recompensa |
|---|---|---|---|---|
| 0 | Colete 3.000 moedas no total da semana | weekly_total_coins | 3000 | 500 |
| 1 | Colete 6.000 moedas no total da semana | weekly_total_coins | 6000 | 800 |
| 2 | Complete 20 runs na semana | weekly_total_runs | 20 | 500 |
| 3 | Complete 40 runs na semana | weekly_total_runs | 40 | 800 |
| 4 | Chegue a 1500m em uma run | single_run_distance | 1500 | 600 |
| 5 | Chegue a 2500m em uma run | single_run_distance | 2500 | 1000 |
| 6 | Atinja o Tier 4 em uma run | reach_tier | 4 | 700 |
| 7 | Sobreviva 3 minutos em uma run | single_run_time | 180 | 800 |
| 8 | Complete todas as 3 missões diárias em 3 dias diferentes | daily_missions_complete | 3 | 600 |
| 9 | Colete moedas em 50 tiles numa única run | tiles_with_coins | 50 | 700 |

### 4.3 Rotação Semanal

```
baseIndex = (weekOfYear % 10)  // 0-9
slot0 = baseIndex % 10
slot1 = (baseIndex + 1) % 10
slot2 = (baseIndex + 2) % 10
```

### 4.4 Estado Salvo (PlayerPrefs)

```
"weekly_missions_week"     → int    (número da semana do ano)
"weekly_mission_0_id"      → int
"weekly_mission_0_progress"→ float
"weekly_mission_0_claimed" → int
(repetir para 1 e 2)
```

---

## 5. Sistema de Rewarded Ads

### 5.1 Dois pontos de rewarded ad

#### A. "2x Coins" — Após cada run

- Aparece na tela de Game Over junto com o resultado.
- Botão: "Dobrar Coins" (mostra ícone de ad).
- Jogador assiste ao ad (15-30s) → coins do run são dobradas.
- Só pode ser usado 1x por run. Após assistir, botão desaparece.
- Coins dobradas são as do run atual, antes de serem somadas ao total.
- Salvar flag `"ad_doubled_this_run"` = true para não oferecer duas vezes.
- Resetar flag no início de cada nova run.

Implementação:
```
runCoins = CoinManager.Instance.Total
→ mostrar Game Over com runCoins
→ jogador clica "Dobrar Coins"
→ aguardar ad completar (callback de sucesso do Unity Ads)
→ runCoins = runCoins * 2
→ atualizar display
→ esconder botão
→ (coins são transferidas ao PlayerDataManager no "Coletar")
```

#### B. "Daily Ad Chest" — Uma vez por dia na Home

- Botão visível na HomeScene: "Chest Grátis" (ícone de baú + ícone de ad).
- Assiste ad → ganha 150 coins.
- Disponível 1x por dia (reseta à meia-noite UTC).
- Estado: `PlayerPrefs["ad_chest_date"]` = data do último uso.

### 5.2 Fallback se Nenhum Ad Disponível

Se o Unity Ads não tiver ad carregado (sem conexão, quota atingida, etc):
- Esconder o botão completamente. Não mostrar botão desabilitado.
- Não dar coins grátis como fallback.

### 5.3 Limite de Ads por Dia

Unity Ads gerencia internamente. Não adicionar limite manual no cliente.

---

## 6. Personagens (Loja Inicial)

### 6.1 Três Personagens

| Index | Nome | Cor Primária | Custo | Como Obter |
|---|---|---|---|---|
| 0 | "Runner" | Branco / Prata | Grátis | Default |
| 1 | "Neon" | Azul Neon (#00BFFF) | 2.500 coins | Loja |
| 2 | "Ember" | Laranja/Vermelho (#FF4500) | 5.000 coins | Loja |

### 6.2 O que muda por personagem

Apenas visual, sem vantagem mecânica:
- Cor emissiva do Player (MaterialPropertyBlock).
- Cor das partículas de coleta de moeda.
- Cor do trail (se implementado).

### 6.3 Fluxo de Compra

```
Jogador abre loja na HomeScene
→ Vê 3 personagens: Runner (equipado), Neon (2.500 coins), Ember (5.000 coins)
→ Clica em Neon
→ Se coins >= 2.500:
    → Popup "Confirmar compra: 2.500 coins?"
    → Sim → PlayerDataManager.SpendCoins(2500)
           → PlayerDataManager.UnlockCharacter(1)
           → PlayerDataManager.EquipCharacter(1)
           → Save()
           → UI atualiza: Neon agora mostra "Equipado"
    → Não → fecha popup
→ Se coins < 2.500:
    → Botão desabilitado ou mostra "Precisa de X coins a mais"
    → Sem popup de compra
```

### 6.4 Estado Salvo (PlayerPrefs)

```
"owned_chars"     → string (CSV de índices: "0" ou "0,1" ou "0,1,2")
"equipped_char"   → int (0, 1 ou 2)
```

---

## 7. Tela Home — O Que Exibir

Placeholder de UI — sem arte, só TextMeshPro + Buttons.

### 7.1 Elementos obrigatórios

```
[Nome do Jogador]           ← PlayerDataManager.PlayerName
[Coins: 1.250]              ← PlayerDataManager.Coins (atualiza em tempo real)
[Best: 847m]                ← PlayerDataManager.BestDistance

[  JOGAR  ]                 ← SceneLoader.LoadGame()

--- Missões Diárias ---
[✓] Chegue a 300m       +100 coins  [RECLAMAR]
[ ] Colete 200 moedas   +150 coins   [47/200]
[ ] Complete 5 runs     +200 coins    [2/5]

--- Login Diário ---
[Dia 3 de 7 — Abrir: 150 coins]    ← se ainda não reclamou hoje

--- Ad Chest ---
[Baú Grátis — 150 coins]           ← se ainda não usou hoje

--- Botões de Navegação ---
[Loja]   [Leaderboard]   [Perfil]   ← stubs por enquanto
```

### 7.2 Atualização dos Dados

A Home deve **ler do PlayerDataManager** no `OnEnable` (não só no Start),
para que ao voltar do jogo os dados estejam atualizados sem precisar recarregar a cena.

---

## 8. Tela Game Over — O Que Exibir

```
--- GAME OVER ---

Razão: Dead End / Out of Bounds / Hit Obstacle

Distância:    847m         ← PlayerRailRider.DistanceTraveled
Tempo:        1m 23s       ← GameTimer.ElapsedSeconds
Moedas:       +124         ← CoinManager.Instance.Total (do run)
Tier máximo:  3            ← DifficultyManager.CurrentTierIndex

[Dobrar Moedas — Assistir Ad]    ← só se ad disponível e não usou ainda

Best Distance: 1.204m      ← PlayerDataManager.BestDistance (já atualizado)
               ★ NOVO RECORDE!   ← se bateu

[JOGAR NOVAMENTE]    [HOME]
```

---

## 9. Fluxo Completo de Dados em Uma Run

Este é o fluxo que o Claude Code deve garantir que funciona end-to-end:

```
1. Jogador clica JOGAR na Home
   → SceneLoader.LoadGame()
   → CoinManager.Reset()
   → MissionTracker.StartRun()    ← inicia contadores do run
   → GameManager.StartGame()

2. Durante o run
   → CoinManager.AddCoins(n)      → MissionTracker.OnCoinsCollected(n)
   → PowerUpManager.OnPickup(t)   → MissionTracker.OnPowerUpUsed(t)
   → DifficultyManager.OnTierChanged(t) → MissionTracker.OnTierReached(t)
   → CollectibleCoin.OnTrigger    → MissionTracker.OnTileWithCoin()

3. Jogador morre
   → GameManager.TriggerGameOver(reason)
   → MissionTracker.EndRun(distance, time, usedPowerUp)
   → GameOverController.Show(distance, time, coins, tier)

4. Game Over exibido
   → Opcionalmente: jogador assiste ad → CoinManager.DoubleTotal()
   → Jogador clica "Coletar" ou navega

5. Ao sair do Game Over (qualquer botão)
   → PlayerDataManager.AddCoins(CoinManager.Instance.Total)
   → PlayerDataManager.UpdateBestDistance(runDistance)
   → PlayerDataManager.IncrementTotalRuns()
   → MissionTracker.CommitProgress()  ← salva progresso das missões no PlayerPrefs
   → PlayerDataManager.Save()
   → SceneLoader.LoadHome() ou SceneLoader.ReloadGame()
```

---

## 10. Novo Script Necessário: `MissionTracker`

```
Namespace: RailSwitchMVP.Meta
Path:      Assets/Scripts/RailSwitchMVP/Meta/MissionTracker.cs
Tipo:      MonoBehaviour singleton, DontDestroyOnLoad
```

### Responsabilidades

- Carregar as 3 missões diárias e 3 semanais ativas do PlayerPrefs no startup.
- Gerar novas missões se a data mudou.
- Rastrear progresso de cada tipo durante o run.
- Expor `IsMissionComplete(slot)` e `ClaimMission(slot)`.
- Salvar progresso no PlayerPrefs via `CommitProgress()`.

### API Pública

```csharp
// Chamados durante o run
void StartRun();
void OnCoinsCollected(int amount);      // para single_run_coins e daily_total_coins
void OnPowerUpUsed(string powerUpType); // para use_powerup
void OnTierReached(int tier);           // para reach_tier
void OnTileWithCoin();                  // para tiles_with_coins
void EndRun(float distance, float seconds, bool usedAnyPowerUp);

// Chamados pela UI
MissionEntry GetDailyMission(int slot);  // retorna dados pra exibir
MissionEntry GetWeeklyMission(int slot);
bool IsDailyComplete(int slot);
bool IsDailyClaimed(int slot);
void ClaimDaily(int slot);               // credita coins + marca claimed
bool IsWeeklyComplete(int slot);
bool IsWeeklyClaimed(int slot);
void ClaimWeekly(int slot);

// Chamado ao fim do run
void CommitProgress();
```

### MissionEntry (struct)

```csharp
public struct MissionEntry
{
    public int Id;
    public string Description;
    public float Progress;
    public float Target;
    public int Reward;
    public bool IsComplete;
    public bool IsClaimed;
}
```

---

## 11. Macro-Visão: O Que Vem Depois (não implementar agora)

Depois que o loop acima estiver fechado e testado, a próxima camada é:

1. **Daily Challenge** — seed fixo do dia, leaderboard local (top score salvo no PlayerPrefs).
   Sem servidor ainda: leaderboard é só o seu próprio histórico de scores.

2. **Backend Supabase** — auth anônima + sync do PlayerDataManager com o servidor.
   O PlayerPrefs vira cache local; Supabase vira fonte da verdade.

3. **Leaderboard online** — daily challenge com ranking global.
   Só faz sentido depois do Supabase estar up.

4. **IAP** — Remove Ads + Coin Pack.
   Só faz sentido depois de ter jogadores reais.

5. **Battle Pass** — S2 ou S3.

Não entrar em nenhum desses pontos até o loop desta spec estar 100% funcional.

---

## 12. Checklist de Implementação

### PlayerDataManager
- [ ] Singleton DontDestroyOnLoad com proteção de duplicata
- [ ] Load() e Save() com todas as chaves da seção 1.4 + 6.4
- [ ] AddCoins(), SpendCoins(), UpdateBestDistance()
- [ ] UnlockCharacter(int), EquipCharacter(int), IsCharacterOwned(int)
- [ ] OnApplicationPause e OnApplicationFocus chamando Save()
- [ ] Funciona se instanciado direto na GameScene sem passar pela Home

### DailyLoginManager (pode ser parte do PlayerDataManager)
- [ ] Lógica de verificação de data (seção 2.4)
- [ ] Tabela de recompensas dos 7 dias
- [ ] Salva e carrega estado do PlayerPrefs

### MissionTracker
- [ ] Geração diária de 3 missões (seção 3.5)
- [ ] Geração semanal de 3 missões (seção 4.3)
- [ ] Todos os 9 tipos de rastreamento (seção 3.3)
- [ ] CommitProgress() salva no PlayerPrefs
- [ ] ClaimDaily/ClaimWeekly credita coins via PlayerDataManager

### HomeScreenController
- [ ] Exibe todos os elementos da seção 7.1
- [ ] Atualiza no OnEnable
- [ ] Botão de reclamação de missão chama MissionTracker.ClaimDaily()
- [ ] Botão de login diário chama DailyLoginManager.Claim()
- [ ] Botão de ad chest chama fluxo de rewarded ad

### GameOverController (modificar existente)
- [ ] Exibe todos os elementos da seção 8
- [ ] Integra com MissionTracker.EndRun()
- [ ] Integra com PlayerDataManager para salvar
- [ ] Botão "Dobrar Coins" chama rewarded ad

### Fluxo end-to-end (seção 9)
- [ ] CoinManager.Reset() ao iniciar run
- [ ] MissionTracker.StartRun() ao iniciar run
- [ ] Todos os eventos de tracking conectados
- [ ] Dados transferidos ao PlayerDataManager ao fim da run
- [ ] PlayerDataManager.Save() garantido antes de mudar de cena

---

*Documento de design — não há código de implementação aqui.*
*Cada seção é prescritiva: implementar exatamente como descrito.*
*Dúvidas de implementação devem voltar para este doc antes de inventar soluções.*
