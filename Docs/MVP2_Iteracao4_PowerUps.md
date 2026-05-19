# MVP2 — Iteração 4: Power-ups + Barreira (última do MVP2)

**Objetivo:** introduzir 4 tipos de recompensa (Shield, SlowDown, Magnet,
DifficultyReset) e um segundo tipo de obstáculo (Barreira, absorvida por
Shield). HUD ganha 3 indicadores de power-up ativo.

Pré-requisitos: MVP2 Iter 3 validada.

---

## 1. Criar os 5 prefabs novos

Pra cada um: clique direito Hierarchy → **3D Object → Cube** (obstáculos)
ou **Sphere** (power-ups). Configure Collider + cor + componente. Arraste pra
`Assets/Prefabs/RailSwitchMVP/`. Delete da cena.

### 1.1 Obstacle_Barrier_Prefab
- Forma: **Cube** scale `(2, 0.3, 0.3)` (faixa horizontal, vê-se bem de cima).
- **BoxCollider** → `Is Trigger = ✅`.
- Material: amarelo `#FFD400`. *(Polish depois: textura listrada amarela/preta.)*
- Add Component → **Barrier Obstacle**.

### 1.2 PowerUp_Shield_Prefab
- Forma: **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`, Radius 0.5.
- Material: azul `#3399FF` com emission ativada (mesma cor, intensidade ~1.5).
- Add Component → **Shield Pickup**.

### 1.3 PowerUp_SlowDown_Prefab
- Forma: **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: cyan `#33CCCC` com emission.
- Add Component → **Slow Down Pickup**.
- Inspector: `Duration Tiles = 0` (usa o default 8 do manager). Override aqui se quiser.

### 1.4 PowerUp_Magnet_Prefab
- Forma: **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: roxo `#A050FF` com emission.
- Add Component → **Magnet Pickup**.
- Inspector: `Duration Tiles = 0` (usa default 6 do manager).

### 1.5 PowerUp_DifficultyReset_Prefab
- Forma: **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: verde `#33DD66` com emission.
- Add Component → **Difficulty Reset Pickup**.

---

## 2. Atualizar `TrackTile_Prefab`

Abra o prefab em modo edit:

1. Selecione a raiz.
2. Add Component → **Power Up Spawner**
   (`RailSwitchMVP/Track/PowerUpSpawner.cs`).
3. Configure:
   - `Start Point` → arraste `StartPoint`.
   - `End Point` → arraste `EndPoint`.
   - `Spawn Height = 0.8` (um pouco acima dos obstáculos pra distinção visual).
4. No componente **Track Tile**, o novo campo `Power Ups` agora aparece
   (auto-resolve via Awake — pode deixar vazio).
5. Salve.

---

## 3. Configurar `ProceduralRailGenerator`

No `_RailManager` (ou onde tá o `ProceduralRailGenerator`), preencha os
novos campos:

| Campo | Valor |
|---|---|
| `Barrier Obstacle Prefab` | `Obstacle_Barrier_Prefab` |
| `Power Up Prefabs` | Array de 4, na ordem: Shield, SlowDown, Magnet, DifficultyReset |

> Pode arrastar em qualquer ordem — o generator escolhe random uniforme do
> array. Se quiser drop rate diferente, duplique entradas (ex: 2x Shield +
> 1 de cada um dos outros = Shield aparece 2x mais).

---

## 4. Adicionar `_PowerUpManager` na cena

1. Hierarchy → clique direito → **Create Empty**.
2. Renomeie pra `_PowerUpManager`.
3. Add Component → **Power Up Manager** (`RailSwitchMVP/Core/PowerUpManager.cs`).
4. Inspector (defaults OK):
   - `Slow Down Speed Multiplier` = 0.7
   - `Magnet Radius` = 4
   - `Slow Down Default Tiles` = 8
   - `Magnet Default Tiles` = 6

> O manager auto-resolve o `PlayerRailRider` da cena e se subscreve no
> `OnTileEntered` event do player.

---

## 5. Adicionar 3 TMP_Texts no HUD

Como filhos do `_HUD_Canvas` (mesmo Canvas dos outros HUD texts):

### 5.1 ShieldText

| Campo | Valor |
|---|---|
| Nome | `ShieldText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -204) — abaixo do CoinsText (que estava em -144) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Font Size | 32 |
| Color | azul claro |
| Initial text | `Shield x0` |

### 5.2 SlowDownText

| Campo | Valor |
|---|---|
| Nome | `SlowDownText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -260) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Font Size | 32 |
| Color | cyan |
| Initial text | `Slow 0` |

### 5.3 MagnetText

| Campo | Valor |
|---|---|
| Nome | `MagnetText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -316) |
| Width × Height | 300 × 60 |
| Alignment | Top-Left |
| Font Size | 32 |
| Color | roxo claro |
| Initial text | `Magnet 0` |

### 5.4 Atribuir no HUDController

No GameObject `_HUD` → componente **HUD Controller**:
- `Shield Text` → arraste `_HUD_Canvas/ShieldText`.
- `Slow Down Text` → arraste `_HUD_Canvas/SlowDownText`.
- `Magnet Text` → arraste `_HUD_Canvas/MagnetText`.
- `Power Up Manager` → deixe vazio (auto-resolve).

> Esses textos começam **escondidos** (o HUDController desativa no Start
> se não tem power-up ativo). Só aparecem quando ativos.

---

## 6. Play Test

Salve a cena. **Play**.

### 6.1 Shield
- Aperte `T` pra Tier 2 (decoys com obstáculos).
- Pegue uma esfera azul (Shield).
- HUD top-left mostra `Shield x1`.
- Force entrar num decoy com cubo vermelho — Shield absorve, cubo some,
  você continua vivo. `Shield x1` desaparece (= x0).
- ✅ Validação.

### 6.2 SlowDown
- Pegue esfera cyan (SlowDown).
- HUD mostra `Slow 8` (ou o default que está no manager).
- O player visivelmente fica mais lento (~30%) por 8 tiles.
- A cada novo tile, contador decrementa (`Slow 7`, `Slow 6`...).
- Quando chega em 0, velocidade volta ao normal e o texto some.
- ✅ Validação.

### 6.3 Magnet
- Pegue esfera roxa (Magnet).
- HUD mostra `Magnet 6`.
- Anda perto (mas SEM passar em cima) de uma moeda numa lane adjacente.
- A moeda some sozinha e o contador de Coins sobe.
- Após 6 tiles, magnet expira, magnet text some.
- ✅ Validação. Raio default = 4 unidades = pega lanes vizinhas + algumas
  forwards.

### 6.4 DifficultyReset
- Pegue esfera verde (DifficultyReset).
- Console: `[PowerUpManager] DifficultyReset!` + `[DifficultyManager] RESET → Tier 0`.
- Tier HUD vai pra 0. Mesma transição semeada do `R` do debug.
- O pickup some, **sem indicador no HUD** (efeito instantâneo).
- ✅ Validação.

### 6.5 Stack
- Pegue 2 Shields seguidos.
- HUD: `Shield x2`.
- Bata em 2 obstáculos diferentes — primeiro consome um shield, segundo
  consome outro, terceiro mata.
- ✅ Validação.

### 6.6 Barreira
- Em Tier 2+ (barrier chance > 0), procure faixa amarela em decoys.
- SEM shield: entrar = Game Over `HitObstacle`.
- COM shield: shield absorve, barreira some, continua vivo.
- ✅ Validação.

### 6.7 Stress test ainda passa
- Tools → RailSwitchMVP → Run Stress Test (10k rows).
- Continua ALL CHECKS PASSED. Os novos sistemas não afetam plan layout.
- ✅ Validação.

---

## 7. Commit final do MVP2

```
git add Assets/Prefabs/RailSwitchMVP/Obstacle_Barrier_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_Shield_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_SlowDown_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_Magnet_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_DifficultyReset_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab.prefab \
        Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(mvp2-iter4): power-ups + barreira + HUD indicators"
```

Considere tag de release do MVP2:

```
git tag -a v0.2.0-mvp2 -m "MVP2 closed: obstacles, HUD, restart, 4 power-ups"
```

---

## 8. Troubleshooting

**Pickup esférico não some ao tocar:**
- Confirme `SphereCollider → Is Trigger = ✅`.
- Confirme tag do Player como `Player`.

**Magnet não coleta moedas adjacentes:**
- Confirme `Magnet Radius` no `_PowerUpManager` (default 4 funciona).
- Confirme que Coins têm Collider (qualquer um, com Trigger ou não — magnet
  usa OverlapSphere que pega ambos).

**HUD não mostra Shield/Slow/Magnet quando ativo:**
- Confirme refs no `_HUD → HUD Controller → Power Up Manager` (pode deixar
  vazio que auto-resolve) e os 3 TMP_Texts atribuídos.
- Confirme `_PowerUpManager` ATIVO na cena.

**SlowDown não muda velocidade:**
- Confirme que `PlayerRailRider.Update` lê `PowerUpManager.SpeedMultiplier`.
  Está na linha que define `currentSpeed = baseSpeed * multiplier`. Se
  build antigo: rebuild.

**Difficulty Reset (esfera verde) não dispara transição:**
- Confirme `_PowerUpManager` e `_DifficultyManager` ambos ativos.
- Confirme o log `[PowerUpManager] DifficultyReset!` no Console.

**Stack do Magnet/SlowDown não estende duração:**
- Verifique no Console o log de Grant — deve mostrar valor crescente.
  Se não cresce, problem in GrantSlowDown/GrantMagnet.

---

## 9. MVP2 fechado

Se todos os critérios validaram, o MVP2 está **completo**:

- ✅ Iter 1 — obstáculos letais.
- ✅ Iter 2 — HUD.
- ✅ Iter 3 — Game Over screen + Restart.
- ✅ Iter 4 — power-ups + barrier.

Próximos passos pós-MVP2 (não estão no plano original — ver
`Docs/Iteracao5_StressTest.md §5` + `MVP2_Plan.md §"Pontos que NÃO entram"`):
- UI warning explícito acima de decoys perigosos.
- Polish visual / modelos de verdade.
- SFX + música.
- Animations no UI (fade, pulse).
- High score em PlayerPrefs.
- Pooling de tiles, obstáculos, power-ups.
- Tipos novos de obstáculo (móvel, oscilante).
- Power-ups extras (2x coins, ghost-mode, etc).
