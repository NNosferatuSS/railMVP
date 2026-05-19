# Iteração 5 — Stress Test (última do MVP)

**Objetivo:** validar que o sistema aguenta o uso pretendido — geração
infinita correta + 60 fps em runtime — antes de fechar o MVP.

Duas validações independentes:
- **Headless test (algorítmico):** gerar 10 000 linhas em memória e
  verificar invariantes do critical path. **Automatizado via menu.**
- **Profiler test (performance):** rodar o jogo no Tier 5 (`maxLanes=9`)
  e validar 60 fps no Window > Analysis > Profiler. **Manual.**

---

## 1. Headless test

### 1.1 Como rodar

Na Unity, no menu superior:

**Tools → RailSwitchMVP → Run Stress Test (10k rows)**

(Versão com 100 000 linhas no mesmo menu, mas só vale rodar se a de 10k
passar e você quiser stress maior.)

O teste:
1. Encontra o primeiro `RailGenConfig` e `DifficultyConfig` no projeto.
2. Cria um `ProceduralRailGenerator` temporário (não toca em GameObjects da cena).
3. Chama `PlanRow` 10 000 vezes em loop. **Não instancia nenhum tile.**
4. Simula progressão de tier — muda de tier a cada 1 500 linhas.
5. Valida 4 invariantes em cada linha gerada.
6. Reporta resultado no Console.

### 1.2 Invariantes validadas

| # | Invariante | Falha = |
|---|---|---|
| 1 | Toda linha tem **pelo menos 1 critical lane** | `Row N: NO critical lane.` |
| 2 | Critical lanes em `[0, globalMaxLanes)` | `Row N: critical lane X out of global bounds.` |
| 3 | Total de tiles por linha em `[minPerRow, maxPerRow]` (fora de transição) | `Row N: tile count X < minPerRow / > maxPerRow.` |
| 4 | Continuidade ±1 entre linhas consecutivas — pelo menos 1 critical da linha N+1 está a distância ≤ 1 de alguma critical da linha N | `Row N: no critical reachable from previous.` |

### 1.3 Output esperado

```
[StressTest] Starting — config=RailGenConfig_Default, difficulty=DifficultyConfig_Default (6 tiers), 10000 rows.
[StressTest] Completed in 47 ms
  Rows generated   : 10000
  Tier transitions : 5
  Rows w/ critical : 10000
  Transition rows  : 0
  Failures         : 0
[StressTest] === ALL CHECKS PASSED ===
```

Se algo falhar, o Console mostra o `LogError` com a lista das primeiras
~4KB de falhas (suficientes pra diagnosticar; o resto é truncado pra
não estourar o Console).

### 1.4 Checklist

- [ ] `Run Stress Test (10k rows)` reporta **ALL CHECKS PASSED**.
- [ ] (Opcional) `Run Stress Test (100k rows)` também passa.
- [ ] Tempo total < 1s (geralmente <100ms — se demorar mais que isso,
      tem algo errado).

---

## 2. Profiler test

### 2.1 Como rodar

1. **Window → Analysis → Profiler** (`Ctrl+7`).
2. Na cena `RailSwitchMVP_Scene.unity`, dê **Play**.
3. No Profiler, certifique que **Record** está ativo (botão círculo
   vermelho no topo).
4. Aperte **T** 5 vezes no Game view pra pular direto pro Tier 5
   (`maxLanes=9`).
5. Anda por uns 30 segundos coletando moedas. Maior cobertura de spawn
   + despawn + coin trigger + lerp da câmera + lerp do gap.
6. Pause o Play. No Profiler, examine o range gravado.

### 2.2 Critérios

| Métrica | Target | Onde olhar |
|---|---|---|
| FPS médio | ≥ 60 (= ≤ 16.67ms/frame) | Profiler → CPU Usage → Selection média |
| Pico worst-case | ≤ 33ms (= ≥ 30fps mínimo) | Profiler → CPU Usage → maior spike |
| Tile spawn/despawn não causa hitch | sem spikes regulares | Comparar frame com tile spawn vs frame normal |
| GC alloc por frame em steady state | ≤ 1 KB | Profiler → Memory → "GC Alloc per frame" |

### 2.3 Se falhar

**FPS abaixo de 60:**
- Confira se está em Editor Play (mais lento que build). Build standalone
  pra teste real.
- Reduza `rowsAhead` no `RailGenConfig_Default` (12 → 8).
- Profile a parte mais lenta (geralmente Instantiate + SetupComponents).

**Hitches regulares ao spawnar tile:**
- Considerar pooling de tiles em vez de Instantiate/Destroy. **Fora do
  escopo do MVP** — anotar como follow-up.

**GC alloc alto:**
- Provavelmente `new bool[globalMax]` e `new HashSet<int>()` em cada
  GenerateRow. Reuso de buffers seria a otimização. **Fora do escopo
  do MVP** — só observar.

### 2.4 Checklist

- [ ] FPS médio ≥ 60 com 12 rows × 9 lanes ativas (Tier 5).
- [ ] Sem hitches > 33ms.
- [ ] Tile spawn/despawn não introduz spike notável.

---

## 3. Critérios de Sucesso do MVP

Da spec §14, ticked após validação manual + stress test:

- [x] Player se move infinitamente para frente.
- [x] Critical path sempre existe entre linhas consecutivas (validado headless §1).
- [x] Decoys podem ser becos sem saída.
- [x] Moedas spawnam predominantemente no critical path.
- [x] Player coleta moedas ao passar.
- [x] `CoinManager.Total` reflete corretamente.
- [x] Dificuldade avança em tiers conforme distância.
- [x] `ResetDifficulty()` funciona — com transição semeada pra evitar DeadEnd.
- [x] `maxLanes` muda em runtime sem quebrar geração nem critical path.
- [x] Jogador vê 5–7 linhas à frente com tempo confortável em todos os tiers.
- [x] Game Over distingue `DeadEnd` de `OutOfBounds`.
- [ ] FPS 60+ no editor com 12 rows × 9 lanes — **validar agora**.

Quando os 12 estiverem ✅, o MVP está concluído. 🎉

---

## 4. Commit

Depois de rodar os dois testes:

```
git commit -m "test(iter5): stress test passa em 10k+ rows e profiler 60fps"
```

---

## 5. Follow-ups (pós-MVP, fora do escopo)

Itens identificados durante o desenvolvimento que ficam pra depois:

- **Pooling de tiles** (em vez de Instantiate/Destroy) — economiza GC e
  reduz hitches em builds mobile.
- **Buffer reuse no generator** — `new bool[]` e `new HashSet<>` por linha
  geram GC. Pooling de arrays internos.
- **Obstáculos** (spec §15.1) — `ObstacleSpawner` no TrackTile, colisão = Game Over.
- **Power-ups** (spec §15.2) — `PowerUpSpawner`, `Slow-down`, `Difficulty Reset`
  como mecânica de gameplay (não só debug).
- **UI/HUD** — contador de moedas, distância, indicador de tier, telinha de
  Game Over. Removido do MVP por design (§16).
- **Pulo / esquiva** — se quiser variedade vertical em vez de só lateral.
- **Polish visual** — modelo de seta de verdade no lugar do cubo esticado,
  shader/material dos trilhos, efeitos de coleta de moeda.
- **Audio** — música, SFX de switch/coin/death.
- **Save de high score** — `PlayerPrefs` simples no início.
