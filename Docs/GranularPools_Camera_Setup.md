# Pools Ponderados + Camera Dual-Mode — Setup + Testes

> Mudanças implementadas em 2026-05-26. Sessão cobriu duas frentes:
> (1) granularidade por tier nos hazards/power-ups via pools ponderados,
> (2) câmera com suporte simultâneo a perspective e orthographic +
> correções de proporção lookAhead/distance.

## O que mudou

### 1. Pools ponderados por tier (`DifficultyTier`)

Antes: cada tier tinha 5 chance-fields individuais pra hazards + 1 chance
única pra power-ups (que sorteava uniformemente em `GameObject[] powerUpPrefabs`).
Sem como dizer "Teleport só libera no tier 4+".

Agora:

- `hazardChanceOnDecoy` (float) + `hazardPool: List<HazardWeight>` —
  1 chance global, weighted pick no pool. Pool vazio = nenhum hazard.
- `powerUpChanceOnCritical/OnDecoy` (mantidas) + `powerUpPool: List<PowerUpWeight>` —
  mesma ideia, pool único compartilhado entre critical/decoy.
- `HazardWeight = { HazardKind kind, float weight }` e
  `PowerUpWeight = { PowerUpType type, float weight }` (ambos em
  `DifficultyConfig.cs`).
- Weight 0 = desabilitado (equivalente a omitir).

`ProceduralRailGenerator`:

- `powerUpPrefabs` virou `List<PowerUpPrefabBinding>` (mapping
  explícito `PowerUpType → prefab`) em vez de array flat.
- `ResolveHazardClassic`/`ResolvePowerUpClassic` agora fazem weighted
  pick + resolvem prefab via `GetHazardPrefab(kind)` / `GetPowerUpPrefab(type)`.
- Type/kind sem prefab registrado → no-op silencioso (não spawn).

`SpawnOverrideController` (debug F2): refatorado pra só atuar quando
`MasterEnabled = true`. Quando OFF, o gerador chama clássico direto,
removendo a duplicação anterior. `SnapshotFromTier` traduz pool
ponderado → chances per-prefab da UI.

### 2. Camera: zoom global + FOV + dual-mode

`RailGenConfig` ganhou:

- `cameraZoomGlobalMultiplier` (range 0.3–3, default 1) — em **perspective**,
  escala distance (Z) e altura (Y) proporcionalmente, preservando ângulo.
  Em **ortho**, multiplica `orthographicSize` (zoom real).
- `cameraFieldOfView` (range 20–100°, default 60°) — aplicado via
  `_cam.fieldOfView` a cada `LateUpdate`. Ignorado em ortho.
- `deathCamOrthoSizeDelta` (default 1.5) — quanto o `orthoSize` reduz no
  death cam (efeito zoom-in em ortho). Separado de `deathCamZoomDelta`
  (que continua afetando `cameraDistance` em ambos os modos).

`DifficultyTier` ganhou:

- `cameraOrthoSizeMin/Max` per-tier — paralelo a `cameraZoomMin/Max`,
  mas usado apenas em modo ortho. Interpolado pelo mesmo `speedFactor`.

`PlayerCameraRig`:

- Detecta `_cam.orthographic` e roteia pro caminho correto.
- **Perspective**: FOV setter + altura Y per-tier (como antes).
- **Orthographic**: `_cam.orthographicSize` = `Lerp(orthoMin, orthoMax, speedFactor) × globalZoom`.
- LookAhead + distance scaling funcionam idênticos nos dois modos.

### 3. LookAhead proporcional (bugfix)

`cameraLookAhead` (RailGenConfig) era um escalar fixo. Em tiers altos,
o player tinha metade do tempo de reação visual.

Correção: `effectiveLookAhead = cameraLookAhead × (playerSpeed / speedAtMinZoom)`.
Mantém horizonte temporal constante (~0.5s à frente em todos os tiers
com defaults `lookAhead=4, speedAtMinZoom=8`).

### 4. Distance scaling (bugfix do bugfix)

Escalar só lookAhead sem escalar distance fazia a câmera ultrapassar o
player (camZ = player + lookAhead − distance fica positivo quando
ratio é alto). Distance agora também escala por `speedRatio` —
invariante "câmera atrás do player na mesma proporção" preservada em
todos os tiers e nos dois modos.

## Setup no Editor

### 1. Configurar pools por tier

Abra `Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset`
no Inspector. Cada tier mostra agora:

- **Hazards** — `Hazard Chance On Decoy` (slider 0–1) + lista `Hazard Pool`.
  Add elementos com kind (enum dropdown) + weight (slider 0–10).
- **Power-ups** — `Power Up Chance On Critical/Decoy` + lista `Power Up Pool`.

Default já vem com progressão: tier 0 só Shield, tier 5 todos os tipos
com pesos ponderados.

### 2. Power-up bindings no generator

O `ProceduralRailGenerator` na scene tem agora uma lista
`Power Up Prefabs` de `{Type, Prefab}`. Já vem populada com 10 bindings
(Shield, SlowDown, Magnet, DifficultyReset, DoubleCoins, Ghost,
LanePreview, CoinRadar, Teleport, AutoCriticalFollow). Pra ativar um
type novo, adicione binding aqui.

**Removidos da lista durante a migração:**

- `LaneSwap.prefab` e `Debuff_Vortex.prefab` — são hazards (carregam
  scripts `*Obstacle`), nunca foram power-ups. Já estão nos campos
  `laneSwapObstaclePrefab`/`vortexObstaclePrefab` separados.
- `PowerUp_TimeFreeze.prefab` — usa sistema `ActiveItemSlot`, não está
  no enum `PowerUpType`. Se quiser uniformizar, adicione `TimeFreeze`
  ao enum em `PowerUpManager.cs:8` e binding aqui.

### 3. Alternar perspective ↔ orthographic

Na scene, selecione `Main Camera` → component **Camera** → checkbox
**Orthographic**. Liga/desliga. O `PlayerCameraRig` detecta em
runtime — sem rebuild.

### Tunables novos disponíveis

`RailGenConfig_Default.asset`:

- `Camera Zoom Global Multiplier` — 0.3..3, default 1.
- `Camera Field Of View` — 20..100°, default 60 (só perspective).
- `Death Cam Ortho Size Delta` — só ortho.

`DifficultyConfig_Default.asset`, por tier:

- `Camera Ortho Size Min/Max` — só ortho. Defaults variam 4→12.
- `Hazard Pool` — weighted list de kinds.
- `Power Up Pool` — weighted list de types.

## Critérios de teste

- [ ] **1.** Setando weight 0 no `Shield` no tier 0 → Shield não spawna
   nas primeiras rows. Volta pra 1 → spawna.
- [ ] **2.** Removendo `Teleport` do `powerUpPool` dos tiers 0-3, e
   adicionando no tier 4 → Teleport só aparece após distance ≥ 800.
- [ ] **3.** Setando `hazardChanceOnDecoy = 0` no tier 0 → zero hazards
   antes do tier 1 (100m).
- [ ] **4.** Alternando Camera.orthographic ON/OFF em runtime →
   transição visual (perspective: depth perception; ortho: 2D feel).
- [ ] **5.** Tier 0 → tier 5 em ambos os modos: player sempre visível
   na tela, mesma posição relativa no quadro.
- [ ] **6.** Em perspective, mexer `cameraFieldOfView` no Inspector
   durante Play → FOV atualiza no frame seguinte.
- [ ] **7.** Em ortho, mexer `cameraOrthoSizeMin/Max` de algum tier →
   `_cam.orthographicSize` atualiza com smoothing
   (controlado por `cameraZoomSpeed`).
- [ ] **8.** Death cam em ortho: orthoSize encolhe (zoom-in visual)
   durante a sequência, restaura no restart.

## Tuning recommendations

**Pools:**

- Weights são **relativos**, não absolutos. `{Shield: 1, Magnet: 1}` é
  o mesmo que `{Shield: 5, Magnet: 5}` — 50/50. Use weights pra
  diferenciar (`{Shield: 2, DifficultyReset: 0.5}` = Shield 4x mais comum).
- Quer manter um type "raro mas presente"? Weight 0.1–0.3.
- Pro hazardPool, lembre que `hazardChanceOnDecoy` é a chance GLOBAL.
  Vale ~0.2 no tier 1 e ~0.8 no tier 5 nos defaults.

**Camera:**

- `cameraZoomGlobalMultiplier` 0.7 = ~30% mais zoom in, 1.5 = ~50% out.
- `cameraFieldOfView` ~40° = lente tele (perspectiva achatada).
  ~80° = grande angular (distorce nas bordas).
- Em ortho, comece com `orthoSizeMin/Max` que mostre 1–2 lanes além das
  populáveis. Ex: tier 5 com 9 lanes spaced 2.5 = 20 unidades de largura;
  com 16:9 → orthoSize ~5.6 mostra exatamente; 8 mostra com folga.

## Troubleshooting

- **Power-up referenciado no pool não spawna** → confere se há binding
  `{type, prefab}` correspondente no `powerUpPrefabs` do generator.
  Sem binding, `GetPowerUpPrefab(type)` retorna null e o spawn é skipado.
- **Hazard kind referenciado no pool não spawna** → confere o campo
  específico (`speedUpZonePrefab`, `laneSwapObstaclePrefab`, etc.)
  está atribuído no generator. Empty = `GetHazardPrefab` retorna null.
- **Player sai do quadro entre tiers** → checa se `speedAtMinZoom` no
  `PlayerCameraRig` está alinhado com o `playerSpeed` do tier 0 (default 8).
  Desalinhamento desbalanceia o `speedRatio` que escala lookAhead/distance.
- **Em ortho, mudanças nos tier values do `cameraZoomMin/Max` não fazem
  zoom** → comportamento esperado. Em ortho, esses valores só posicionam
  a câmera (altura Y), não afetam zoom. Use `cameraOrthoSizeMin/Max`.
- **F2 spawn override não funciona como antes** → master OFF agora é
  no-op (gerador usa tier config direto). Ative `MasterEnabled` no
  toggle pra rodar override.
