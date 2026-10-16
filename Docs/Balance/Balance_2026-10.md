# Análise de progressão e câmera — 2026-10

Fontes: `DifficultyConfig_Default`, `RailGenConfig_Default`, pools em `ScriptableObjects/Pools`,
`ProceduralRailGenerator`, `DifficultyManager`, `PlayerRailRider`, `PlayerCameraRig`.
Modelo numérico: `Docs/Balance/balance_model.py` (rode `python balance_model.py 0.6`).
Passe aplicado: **Tools → RailSwitch → Balance → Apply 2026-10 pass** (`Editor/Balance/BalancePass202610.cs`, reaplicável).

## 1. Como a progressão funciona hoje

- 11 tiers por **distância**. Cada tier define largura do grid (maxLanes), velocidade, zoom da
  câmera, densidade de tiles, coins, hazards e power-ups.
- Toda row tem um **caminho crítico garantido** (random walk ±1 lane); o resto são **decoys**.
  Morre-se por *dead end*: o conector aponta pra uma lane vazia na próxima row.
- Hazards nascem **só em decoys**; coins quase só no caminho crítico.
- Contas com level alto pulam o começo (rampa até o tier 2–5).

## 2. Achados

### Bugs / inconsistências de dados
| # | Achado | Efeito |
|---|---|---|
| 1 | Tier 10 com `maxLanes 10` > `globalMaxLanes 9` | clampado em silêncio |
| 2 | Tiers 9 e 10 com o **mesmo trigger (3200)** | tier 9 nunca existia; salto direto 22→30 |
| 3 | Tiers 5–10 apontam todos pro `HazardPool_Tier5` (**só Barrier**) | `HazardPool_Tier6` (Lethal) órfão; **Lethal e os 3 debuffs nunca apareciam** |
| 4 | Power-ups param no Tier5 | SlowDown, LanePreview, CoinRadar, TimeFreeze só via Mystery Box |
| 5 | `decoyCoinChance = 0` em todos os tiers | `decoyCoinsMax` era ignorado — **zero risco/recompensa** |
| 6 | `rowsAhead 18` nos tiers 5–6 | gera ~250 u à frente pra ~60 u visíveis |
| 7 | Velocidade trocava **instantaneamente** na troca de tier | "tranco" (16→22 = +37%) |
| 8 | (meu) montagem visual dos trilhos começava a 42 u fixos | nos tiers altos escondia 1–2 rows visíveis |

### Curva (antes)
| tier | dist | lanes | vel | s/row | leitura à frente | player fora da tela (9:19.5) |
|---|---|---|---|---|---|---|
| 0 | 0 | 3 | 5 | 2.80 | 7.6 s | — |
| 2 | 100 | 4 | 7 | 2.00 | 6.5 s | 0.7 u |
| 5 | 650 | 6 | 13 | 1.08 | 4.5 s | **2.0 u** |
| 7 | 1600 | 8 | **22** | 0.64 | 3.1 s | **3.8 u** |
| 10 | 3200 | 9 | **30** | **0.47** | 2.8 s | 1.8 u |

- **Começo lento**: os primeiros ~35 s têm 3–4 lanes, 2–3 s por row e 6–7 s de leitura. Pouca
  pressão (amortecido pela rampa de account level pra quem já jogou).
- **Muro no fim**: 30 u/s = uma row a cada 0,47 s. Está no limite humano; as mortes ali parecem
  injustas, não erro do jogador.
- **Platô de conteúdo**: a partir de ~1 min 20 s (tier 5) nada novo aparece — só fica mais rápido.

### Câmera
- Tilt 50°, FOV vertical 50°. Em retrato **9:19.5** (iPhone 14 Pro Max) o FOV horizontal é
  só **~24°**. A meia-largura visível na profundidade do player ≈ `0,495 × cameraZoom`.
- A câmera tinha **X travado em 0**. Com 4+ lanes o player nas lanes externas saía da tela
  (até 3,8 u no tier 7–8, mais de uma lane). Em 9:16 é menos grave, por isso passava despercebido
  no Editor.

## 3. O que foi alterado

**Câmera — follow lateral suave** (`RailGenConfig`, aparece no Control Panel):
- `cameraLateralFollow = 0.6`: a câmera acompanha 60% do X do player → mostra o player E o miolo.
- `cameraLateralSafeZone = 0.65`: garantia dura — o player nunca passa de 65% da meia-largura
  visível, calculada com o **aspect real** do aparelho.
- `cameraLateralSmoothTime = 0.28 s` (SmoothDamp).
- Resultado no modelo: **0 u de saída em todos os tiers**, nos dois aspects.

**Velocidade com rampa**: `speedRampPerSecond = 5` u/s² — a troca de tier acelera em ~0,5–1 s.

**Tiers (depois)**
| tier | dist | lanes | vel | zoom | s/row | leitura | hazards | power-ups |
|---|---|---|---|---|---|---|---|---|
| 0 | 0 | 3 | 5 | 10 | 2.80 | 7.6 s | — | — |
| 1 | 30 | 3 | 6 | 11 | 2.33 | 6.9 s | Barrier | Shield |
| 2 | 100 | 4 | 7.5 | 12 | 1.87 | 6.1 s | Barrier | +2x |
| 3 | 210 | 5 | 9 | 13 | 1.56 | 5.5 s | Barrier | +Magnet, Ghost |
| 4 | 400 | 5 | 10.5 | 13.5 | 1.33 | 4.9 s | Barrier | +Teleport |
| 5 | 650 | 6 | 12 | 15 | 1.17 | 4.7 s | Barrier | idem |
| 6 | 1000 | 7 | 14 | 16.5 | 1.00 | 4.5 s | **+Lethal** | **+SlowDown, LanePreview, CoinRadar, TimeFreeze** |
| 7 | 1500 | 7 | 16 | 17.5 | 0.88 | 4.1 s | Lethal/Barrier | idem |
| 8 | 2200 | 8 | 18 | 18.5 | 0.78 | 3.9 s | **+SpeedUp, LaneSwap, Vortex** | idem |
| 9 | 3000 | 9 | 20.5 | 19.5 | 0.68 | 3.6 s | idem | idem |
| 10 | 4000 | 9 | 23 | 20.5 | 0.61 | 3.4 s | idem | idem |
| 11 | 5200 | 9 | 25 | 21.5 | 0.56 | 3.3 s | idem | idem |

- Incremento de velocidade sempre ≤ ~20% por tier; teto 25 (antes 30). Leitura mínima 3,3 s.
- Tempo até o último tier: ~5 min 30 s (antes ~3 min 35 s, com o salto).
- **Decoys com isca**: `decoyCoinChance 0.3` (tiers 2+), `powerUpChanceOnDecoy` 0,04–0,06. Pegar
  coins/power-up num decoy agora é uma aposta (dead end ou hazard).
- `hazardChanceOnDecoy` 0,82 → 0,5–0,72 (com 0,82 todo decoy era vermelho = sinalizava o caminho
  certo; agora decoy pode ser seguro, perigoso ou isca).
- `rowsAhead` 4–8 (coerente com o que a câmera mostra).
- Tier 4 `minLanesPerRow` 1→2 (pedia 2 caminhos críticos com mínimo de 1 lane).
- Stress test 10k rows: **ALL CHECKS PASSED**.

Não mexi: tier 1 com `powerUpChanceOnCritical = 1.0` (garante o 1º Shield cedo — parece
intencional), regras de account level, economia de coins.

## 4. Pendências de validação

- Jogar no aparelho real (9:19.5) e sentir o follow da câmera e a rampa de velocidade.
- Debuffs (SpeedUp/LaneSwap/Vortex) entravam pela 1ª vez em pools: conferir se cada um é
  divertido e legível.
