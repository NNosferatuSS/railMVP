# PostMVP2.5 — 3 Novos Obstáculos

| Obstáculo | Efeito | Mata? | Cor sugerida |
|---|---|---|---|
| **SpeedUp Zone** | Acelera player 1.5x por 6 tiles. Stack ADICIONA duração. | ❌ | Laranja |
| **Lane Swap** | Inverte inputs ←/→ por 2 tiles. Stack RESETA duração. | ❌ | Rosa/magenta |
| **Vortex** | Force switch pra outra direção válida (não pra void). | ❌ | Cyan |

Todos só spawnam em decoys, com chances escalando por tier (ver tabela na DifficultyConfig_Default).

---

## Setup na Editor

### 1. Criar os 3 prefabs

Pra cada um, criar como os obstáculos existentes:

#### SpeedUp_Zone_Prefab
- **3D Object → Cube** scale `(1.5, 0.2, 0.8)` (zona achatada cobrindo a lane).
- Material **laranja vibrante** (`#FF8800`).
- **BoxCollider** → `Is Trigger = ✅`.
- Add Component → **Speed Up Zone Obstacle**.
- (Opcional) `Duration Tiles = 0` (usa default 6 do PowerUpManager).

#### LaneSwap_Prefab
- **3D Object → Cube** scale `(1.0, 0.3, 1.0)`.
- Material **rosa/magenta** (`#FF55CC`).
- **BoxCollider** → `Is Trigger = ✅`.
- Add Component → **Lane Swap Obstacle**.

#### Vortex_Prefab
- **3D Object → Cylinder** scale `(0.6, 0.3, 0.6)` (achatado tipo redemoinho).
- Material **cyan** (`#22CCDD`).
- **CapsuleCollider** ou substitua por SphereCollider → `Is Trigger = ✅`.
- Add Component → **Vortex Obstacle**.
- Inspector: `Push Mode = OppositeOfSwitch` (default) ou `PureRandom`.

Arraste cada um pra `Assets/Prefabs/RailSwitchMVP/`. Delete da cena.

### 2. Atribuir no Generator

No `_RailManager → ProceduralRailGenerator`, novos campos:

| Campo | Prefab |
|---|---|
| `Speed Up Zone Prefab` | SpeedUp_Zone_Prefab |
| `Lane Swap Obstacle Prefab` | LaneSwap_Prefab |
| `Vortex Obstacle Prefab` | Vortex_Prefab |

### 3. HUD — 2 novos TMP_Texts

Pros indicadores dos debuffs. Mesmo padrão dos outros indicadores:

| Nome | Anchored Position | Font Size | Cor |
|---|---|---|---|
| `SpeedUpDebuffText` | (24, -708) | 32 | laranja |
| `LaneSwapDebuffText` | (24, -764) | 32 | rosa |

Atribua refs no `_HUD → HUD Controller → Speed Up Debuff Text / Lane Swap Debuff Text`.

(Posições -708/-764 assumem os indicadores anteriores até -652. Ajuste se sua sequência for diferente.)

---

## Testar via Debug Panel

F1 → seção Power-ups → "Debuffs (PostMVP2.5)":
- **Grant SpeedUp Debuff** → player começa a correr mais rápido. HUD mostra `⚡ Fast 6`.
- **Grant LaneSwap Debuff** → aperta `→`, switch vai pra Left. HUD `↔ Swap 2`.
- **Vortex** não tem botão direct (precisa do tile context). Pra testar, suba pra Tier 3+ via `T` repetido — Vortex começa a aparecer naturalmente em decoys.

---

## Curva de spawn (já populada no DifficultyConfig_Default)

| Tier | Lethal | Barrier | SpeedUp | LaneSwap | Vortex |
|---|---|---|---|---|---|
| 0 | 0 | 0 | 0 | 0 | 0 |
| 1 | 0.15 | 0 | 0.08 | 0 | 0 |
| 2 | 0.25 | 0.10 | 0.12 | 0.05 | 0 |
| 3 | 0.35 | 0.20 | 0.15 | 0.10 | 0.08 |
| 4 | 0.45 | 0.25 | 0.18 | 0.12 | 0.12 |
| 5 | 0.55 | 0.30 | 0.20 | 0.15 | 0.15 |

Order de roll: Lethal → Barrier → SpeedUp → LaneSwap → Vortex. Cada tile no máximo 1 hazard.

---

## Critérios de validação

- [ ] 3 prefabs criados + arrastados no generator.
- [ ] 2 TMP_Texts no HUD + refs.
- [ ] Tier 0: nenhum hazard novo aparece.
- [ ] Tier 2+: SpeedUp aparece (cubo laranja), tocar → HUD mostra Fast + speed acelera.
- [ ] Tier 2+: LaneSwap aparece, tocar → setas invertem (HUD `↔ Swap`).
- [ ] Tier 3+: Vortex aparece, tocar → switch state muda pra outra direção válida (player continua vivo).
- [ ] Ghost ativo: atravessa todos sem efeito (já funciona — ObstacleBase tem o gate).
- [ ] Stress test continua passando.

---

## Detalhes de balanceamento

- **SpeedUp + SlowDown ativos** = multiplicam (1.5 × 0.7 = ~1.05, quase normal). Player pode usar SlowDown power-up pra contrarrestar SpeedUp.
- **LaneSwap em warmup**: input livre + LaneSwap simultâneo = inversão acontece. Mas warmup row só tem center → setas qualquer ficam vermelhas → cor avisa.
- **Vortex sem alternativa válida**: se TODAS as direções vizinhas (≠ switch atual) levam a void/empty, vortex é no-op (mantém switch original). Não mata.
