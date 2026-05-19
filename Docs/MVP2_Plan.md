# RailMVP — Plano do MVP Parte 2

> **Contexto:** o MVP parte 1 foi fechado em 2026-05-19 (tag `v0.1.0-mvp`).
> Core gameloop validado (player + critical path procedural + dificuldade
> dinâmica + moedas + Game Over). Ver `PROGRESS.md` e `RailSwitchMVP_Spec.md`.
>
> **Objetivo do MVP2:** adicionar mecânicas de risco/recompensa e feedback
> visual mínimo (HUD + Game Over) sem polish artístico.
>
> **Como usar este doc:** cada Iteração abaixo é uma unidade entregável.
> Termina cada iteração com play test manual + commit. Atualize
> `PROGRESS.md` ao concluir cada uma.

---

## Sumário das decisões de design (alinhadas com o usuário)

| Tema | Decisão |
|---|---|
| **Obstáculos — escopo MVP2** | 1 tipo apenas (letal). Arquitetura suporta N. Barreira fica pra Iteração 4. |
| **Onde obstáculos spawnam** | Só em decoys. Critical path sempre limpo (reforça "moeda = caminho seguro"). |
| **Posição no tile** | Meio do tile. |
| **Visual obstáculo** | Cor saturada + forma identificável vista de cima (câmera é quase top-down, altura é irrelevante). |
| **Telegrafia obstáculo** | Primária: ausência de moedas no decoy. Secundária: o próprio obstáculo visível como elemento distinto à frente. UI warning explícito fica como follow-up pós-MVP2. |
| **Curva de obstáculos** | `obstacleChanceOnDecoy` no `DifficultyTier`: 0%, 15%, 25%, 35%, 45%, 55% (tiers 0→5). |
| **Power-ups — tipos no MVP2** | 4: `Shield`, `SlowDown`, `DifficultyReset`, `Magnet` (coleta moedas das lanes adjacentes). |
| **Onde power-ups spawnam** | Mais comum em critical path, mas também aparecem em decoys (cria dilema). Probabilidade configurável por tier. |
| **Duração power-ups** | Tile-based (decrementa ao transicionar entre tiles), não time-based. |
| **Stack power-ups** | Múltiplos podem estar ativos ao mesmo tempo. |
| **Cronômetro** | Crescente desde o Play, formato `mm:ss`, pausa em Game Over. |
| **Distância** | `PlayerRailRider.DistanceTraveled` arredondado pra metro inteiro (`487m`). |
| **HUD** | UGUI (Canvas tradicional). Top-left: tempo + distância + moedas. Top-right: tier atual. |
| **Game Over** | Tela com razão (`DeadEnd`/`OutOfBounds`/`HitObstacle`) + stats finais + botão Restart. |
| **Indicador power-up ativo** | Ícone + contador de tiles restantes. Aparece quando power-up ativo. |

---

## Iteração 1 (MVP2) — Obstáculos letais

**Objetivo:** introduzir mecânica de risco. Decoys agora podem matar não só
por dead-end, mas por colisão com obstáculo letal.

### Escopo
- `ObstacleSpawner` (componente do `TrackTile`, paralelo ao `CoinSpawner`).
- `ObstacleBase` (MonoBehaviour) com método `OnTriggerEnter` que mata o player.
  - Arquitetura preparada pra N tipos (futuro: barreira, móvel, etc.). No MVP2,
    apenas 1 implementação concreta: `LethalObstacle`.
- `GameOverReason.HitObstacle` adicionado ao enum.
- Novo campo no `DifficultyTier`: `obstacleChanceOnDecoy` (float 0–1).
  - Tiers 0→5 já existentes: 0, 0.15, 0.25, 0.35, 0.45, 0.55.
- `ProceduralRailGenerator` chama `ObstacleSpawner.Spawn(...)` apenas em decoys,
  com probabilidade do tier ativo. Critical path NUNCA recebe obstáculo.
- Prefab `Obstacle_Lethal_Prefab`: cubo vermelho saturado, escala visível em
  câmera quase top-down. Trigger collider, sem rigidbody.

### Critérios de validação
- [ ] Tier 0: nenhum obstáculo spawna (probabilidade 0%).
- [ ] Tier 1+: obstáculos aparecem só em decoys, nunca no critical path verde.
- [ ] Colisão com obstáculo → Game Over com `HitObstacle` no log.
- [ ] Player consegue ver o obstáculo à frente com pelo menos 1 tile de
      antecedência (sem precisar entrar no decoy pra descobrir).
- [ ] Stress test (`Tools → RailSwitchMVP → Run Stress Test (10k rows)`)
      continua passando.

### Commit
```
feat(mvp2-iter1): obstáculos letais em decoys com chance por tier
```

---

## Iteração 2 (MVP2) — HUD básico

**Objetivo:** dar feedback visual contínuo de progresso (tempo, distância,
moedas, tier).

### Escopo
- Canvas UGUI overlay novo: `HUD_Canvas`.
- `HUDController` (singleton ou MonoBehaviour ref-by-inspector) com Texts:
  - Top-left vertical stack:
    - `⏱ mm:ss` (tempo decorrido)
    - `📏 487m` (distância em metros inteiros)
    - `💰 42` (moedas — já existe `CoinManager.Total`)
  - Top-right:
    - `Tier 3` (lê de `DifficultyManager.CurrentTierIndex`)
- `GameTimer` (componente novo ou método no `GameManager`): conta tempo
  desde Play, pausa em Game Over.
- HUD se atualiza em `Update` (tempo + distância) e por eventos
  (`OnCoinsChanged`, `OnTierChanged`).
- Fonte default da Unity (LiberationSans), tamanho legível em 1080p. Sem
  estilo customizado — polish depois.

### Critérios de validação
- [ ] Tempo cresce em formato `mm:ss` durante Play.
- [ ] Tempo congela ao entrar em Game Over.
- [ ] Distância atualiza em metros inteiros conforme o player avança.
- [ ] Contador de moedas reflete `CoinManager.Total` instantaneamente.
- [ ] Tier atualiza ao trocar de tier (use debug `T` pra forçar tier up).

### Commit
```
feat(mvp2-iter2): HUD básico com tempo, distância, moedas e tier
```

---

## Iteração 3 (MVP2) — Tela de Game Over com Restart

**Objetivo:** fechar o loop morte → retry. Hoje o jogo só loga Game Over;
queremos uma tela mostrando o que aconteceu e botão pra recomeçar.

### Escopo
- Panel UGUI `GameOverPanel` (filho do `HUD_Canvas`, desativado por padrão).
- `GameOverController` exibe ao receber evento de `GameManager`.
- Conteúdo:
  - Título: "GAME OVER"
  - Razão (label): "Dead End", "Out of Bounds", ou "Hit Obstacle" (mapear
    do `GameOverReason`).
  - Stats:
    - "Time: mm:ss"
    - "Distance: 487m"
    - "Coins: 42"
    - "Best Tier: 3"
  - Botão "Restart" — recarrega a cena (`SceneManager.LoadScene(activeScene)`)
    ou reseta o estado manualmente (decisão do implementador — recarregar a
    cena é mais simples e robusto).
- Input alternativo: tecla `R` também reinicia (já existe no debug controller
  pra `ResetDifficulty`; aqui é distinto — reset COMPLETO da run).
- Pausa o tempo de jogo (`Time.timeScale = 0`) enquanto a tela está ativa.

### Critérios de validação
- [ ] Game Over por qualquer razão (`DeadEnd`/`OutOfBounds`/`HitObstacle`)
      mostra a tela com a razão correta.
- [ ] Stats finais corretos (mesmos valores que estavam no HUD no momento da morte).
- [ ] Botão Restart e tecla R reiniciam pra começo limpo (tier 0, distância 0,
      moedas 0, tempo 0).
- [ ] Tempo congelado durante tela de Game Over.

### Commit
```
feat(mvp2-iter3): tela de Game Over com stats e restart
```

---

## Iteração 4 (MVP2) — Power-ups (4 tipos) + obstáculo barreira

**Objetivo:** introduzir recompensas e ferramentas de mitigação. Permite
revisitar a barreira como obstáculo não-letal (drena shield).

### Escopo

#### 4a. Arquitetura de power-ups
- `PowerUpBase` (abstract MonoBehaviour) com:
  - `OnTriggerEnter` que coleta e ativa via `PowerUpManager`.
  - `int durationInTiles` (configurável por tipo/tier).
  - Métodos virtuais `OnActivate`, `OnTick(int tilesRemaining)`, `OnDeactivate`.
- `PowerUpManager` (singleton):
  - Lista de power-ups ativos (suporta stack).
  - Decrementa duração em cada transição de tile do player (hook em
    `PlayerRailRider.EnterTile` ou similar).
  - Evento `OnPowerUpActivated` e `OnPowerUpExpired` para HUD.
- `PowerUpSpawner` (componente do `TrackTile`, paralelo a Coin/Obstacle).
- 4 implementações concretas:
  - **`ShieldPowerUp`**: dura "1 hit" (sem timer de tiles). Próximo evento
    letal é absorvido em vez de matar. Stack: shields adicionais se acumulam.
  - **`SlowDownPowerUp`**: reduz `playerSpeed` em ~30% por N tiles (sugestão: 8).
    Sobreescreve speed do tier durante a duração. Multiplica no caso de stack
    (cuidado pra não ficar parado).
  - **`DifficultyResetPowerUp`**: instantâneo. Chama `DifficultyManager.ResetDifficulty()`.
    Sem duração nem ícone persistente. Sem stack (não tem por quê).
  - **`MagnetPowerUp`**: por N tiles (sugestão: 6), coleta automaticamente
    moedas das lanes ADJACENTES (`Lane ±1`) à do player. Implementação
    sugerida: ampliar o trigger de coleta do player ou enviar broadcast nas
    moedas das rows próximas. Stack: cada magnet ativo amplia o raio ou
    apenas estende duração (decisão do implementador — começar com "estende
    duração" é mais simples).

#### 4b. Barreira (segundo tipo de obstáculo)
- Nova subclasse de `ObstacleBase`: `BarrierObstacle`.
- Comportamento:
  - Se player tem Shield ativo: consome 1 shield, passa sem morrer.
  - Se NÃO tem Shield: Game Over com `HitObstacle` (mesma razão do letal).
- Visual: faixa amarela/preta listrada vista de cima, distinguível do letal.
- Spawn: também só em decoys. Adicionar `barrierChanceOnDecoy` no `DifficultyTier`
  (curva sugerida: 0/0/0.1/0.2/0.25/0.3). Probabilidade INDEPENDENTE de
  `obstacleChanceOnDecoy` (decoys podem ter um, outro, ou nenhum — nunca os
  dois no mesmo tile).

#### 4c. Spawn de power-ups
- Novos campos no `DifficultyTier`:
  - `powerUpChanceOnCritical` (float 0–1) — sugestão de curva: 0.08 em todos os tiers, ajustável.
  - `powerUpChanceOnDecoy` (float 0–1) — sugestão: 0.03.
  - Pesos relativos por tipo (4 floats que somam 1, ou config simples como
    "uniform random entre 4"). Implementador decide.
- Em `ProceduralRailGenerator`, após decidir spawn de moedas/obstáculo, rola
  pra power-up. Tile não recebe power-up E obstáculo no mesmo lugar.

#### 4d. HUD — indicador de power-up ativo
- Nova área no HUD (sugestão: top-center ou abaixo do bloco top-left).
- Para cada power-up ativo, mostra ícone + contador de tiles restantes
  (ex: `🛡 ∞` pro shield, `🐌 5`, `🧲 3`).
- Atualiza por eventos `OnPowerUpActivated` / `OnPowerUpExpired`.
- Reset Difficulty não aparece como ativo (efeito instantâneo).

### Critérios de validação
- [ ] Cada um dos 4 power-ups coleta corretamente e ativa o efeito.
- [ ] Shield absorve obstáculo letal e barreira sem morrer.
- [ ] Slow-down reduz visível a velocidade pela duração.
- [ ] Difficulty Reset volta ao tier 0 e dispara a transição semeada
      (mesmo comportamento da tecla R do debug — reusar o caminho existente).
- [ ] Magnet coleta moedas das lanes adjacentes sem o player passar por elas.
- [ ] Múltiplos power-ups simultâneos funcionam (ex: shield + magnet).
- [ ] Barreira mata sem shield, é consumida com shield.
- [ ] HUD mostra todos os ativos com contador de tiles correto.
- [ ] Stress test continua passando (geração não deve quebrar com os novos campos).

### Commit
```
feat(mvp2-iter4): 4 power-ups + obstáculo barreira + indicador no HUD
```

---

## Pontos de implementação a deixar pro Claude Code decidir

Esses pontos NÃO precisam ser pré-acordados — o implementador escolhe o
caminho mais limpo:
- Forma exata de hookar transição de tile no `PowerUpManager` (evento no
  `PlayerRailRider` vs polling do `currentTile`).
- Stack do Shield: lista de "cargas" vs único int contador.
- Stack do Magnet/SlowDown: "estende duração" (recomendado) vs "amplia efeito".
- Layout exato dos elementos no Canvas (anchor/padding/font size).
- Restart na Iteração 3: `SceneManager.LoadScene` (recomendado, mais robusto)
  vs reset manual de estado.
- Distribuição de probabilidade entre os 4 tipos de power-up (pesos relativos).

## Pontos que NÃO entram no MVP2 (follow-ups)

Anotar em `PROGRESS.md` como pós-MVP2:
- UI warning explícito (ícone ⚠ flutuante) acima de decoys com obstáculo.
- Polish visual de obstáculos/power-ups (modelos de verdade no lugar de
  cubos/esferas).
- SFX de coleta de power-up, ativação, expiração, morte por obstáculo.
- Animação de UI (fade-in do Game Over, pulse nos ícones de power-up).
- High score persistente (`PlayerPrefs`).
- Pooling de obstáculos e power-ups (mesma preocupação do tile pooling
  já listada no follow-up do MVP1).
- Mais tipos de obstáculo (móvel, oscilante).
- Mais tipos de power-up (2x coins, ghost-mode, etc).

---

## Antes de começar a Iteração 1

1. Ler `Docs/RailSwitchMVP_Spec.md` (especialmente §15 — Reservas Arquiteturais).
2. Ler `Docs/PROGRESS.md` (estado atual + commits de cada iteração do MVP1).
3. Confirmar que `Tools → RailSwitchMVP → Run Stress Test (10k rows)` passa
   no estado atual (baseline).
4. Criar branch `mvp2` a partir de `main` (ou trabalhar direto em `main` —
   decisão do usuário no momento).

Boa implementação!
