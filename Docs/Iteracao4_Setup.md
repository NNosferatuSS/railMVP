# Iteração 4 — Guia de Setup na Unity

**Objetivo desta iteração:** validar a progressão dinâmica de dificuldade —
speed/zoom/maxLanes/criticalPaths/coinsPerTile escalam conforme a distância
percorrida; tecla `R` reseta tudo de volta ao tier 0.

Pré-requisitos: Iter 3 validada. Cena gera linhas proceduralmente.

> **Nota:** quase todo o trabalho desta iteração é **no Editor** (popular o
> ScriptableObject com tiers). O código já existia desde a Iter 1 e
> foi finalizado nessa iteração com:
> - log de mudança de tier no Console
> - `[ContextMenu]` para `ResetDifficulty` e `ForceNextTier`
> - reset com offset de distância (pra não reanimar o tier no próximo frame)
> - reset do critical path acumulado no generator
> - `DifficultyDebugController` com tecla R (reset) e T (forçar próximo tier)

---

## 1. Popular `DifficultyConfig_Default` com 5 tiers adicionais

Abra `Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default`.

Você já tem 1 tier (o Tier 0 da Iter 1). Adicione **5 elementos** à lista
`Tiers` (clique no `+` do array) e preencha conforme a tabela abaixo
(da spec §2.4 + §3.6):

### Tier 0 (já existe — confira os valores)

| Campo | Valor |
|---|---|
| Trigger At Distance | 0 |
| Max Lanes | 3 |
| Min Lanes Per Row | 2 |
| Max Lanes Per Row | 3 |
| Critical Paths Per Row | 1 |
| Lane Population Chance | 0.6 |
| Player Speed | 8 |
| Camera Zoom Min | 12 |
| Camera Zoom Max | 18 |
| Coins Per Critical Tile | 3 |
| Coins Per Decoy Tile | 0 |

### Tier 1 — ativa em 100m

| Campo | Valor |
|---|---|
| Trigger At Distance | 100 |
| Max Lanes | 5 |
| Min Lanes Per Row | 2 |
| Max Lanes Per Row | 4 |
| Critical Paths Per Row | 1 |
| Lane Population Chance | 0.55 |
| Player Speed | 10 |
| Camera Zoom Min | 14 |
| Camera Zoom Max | 20 |
| Coins Per Critical Tile | 3 |
| Coins Per Decoy Tile | 0 |

### Tier 2 — ativa em 250m

| Campo | Valor |
|---|---|
| Trigger At Distance | 250 |
| Max Lanes | 5 |
| Min Lanes Per Row | 3 |
| Max Lanes Per Row | 4 |
| Critical Paths Per Row | 1 |
| Lane Population Chance | 0.55 |
| Player Speed | 12 |
| Camera Zoom Min | 15 |
| Camera Zoom Max | 22 |
| Coins Per Critical Tile | 4 |
| Coins Per Decoy Tile | 0 |

### Tier 3 — ativa em 500m

| Campo | Valor |
|---|---|
| Trigger At Distance | 500 |
| Max Lanes | 7 |
| Min Lanes Per Row | 3 |
| Max Lanes Per Row | 5 |
| Critical Paths Per Row | 1 |
| Lane Population Chance | 0.5 |
| Player Speed | 14 |
| Camera Zoom Min | 17 |
| Camera Zoom Max | 24 |
| Coins Per Critical Tile | 4 |
| Coins Per Decoy Tile | 0 |

### Tier 4 — ativa em 800m

| Campo | Valor |
|---|---|
| Trigger At Distance | 800 |
| Max Lanes | 7 |
| Min Lanes Per Row | 4 |
| Max Lanes Per Row | 6 |
| Critical Paths Per Row | 2 |
| Lane Population Chance | 0.5 |
| Player Speed | 16 |
| Camera Zoom Min | 19 |
| Camera Zoom Max | 26 |
| Coins Per Critical Tile | 5 |
| Coins Per Decoy Tile | 0 |

### Tier 5 — ativa em 1200m

| Campo | Valor |
|---|---|
| Trigger At Distance | 1200 |
| Max Lanes | 9 |
| Min Lanes Per Row | 4 |
| Max Lanes Per Row | 7 |
| Critical Paths Per Row | 2 |
| Lane Population Chance | 0.45 |
| Player Speed | 18 |
| Camera Zoom Min | 21 |
| Camera Zoom Max | 28 |
| Coins Per Critical Tile | 5 |
| Coins Per Decoy Tile | 0 |

> Os valores de **Camera Zoom Min/Max** e **Lane Population Chance** são
> sugestões iniciais — tune conforme o feel durante o playtest. A coluna
> "Max Lanes Per Row" representa quantos tiles a linha pode ter no MÁXIMO
> (limita decoys); deixe sempre `>= Min Lanes Per Row` e `<= Max Lanes`.

Salve o asset (Ctrl+S).

---

## 2. Adicionar o `_DebugController` na cena

1. Na hierarchy, crie um GameObject vazio chamado `_DebugController`.
2. Add Component → **Difficulty Debug Controller**.
3. (Opcional) marque `Restrict To Debug Builds` se você quiser que os atalhos
   só funcionem no Editor. No MVP deixe `false`.

---

## 3. Play Test

### 3.1 Progressão automática

Salve a cena e dê **Play**. Anda forward (sem precisar mover lateralmente):

- Em ~12.5s o player passou ~100m → **Console**: `[DifficultyManager] ↑ Tier 1 @ 100.X m`
  - Speed pula de 8 → 10, câmera afasta um pouco.
  - As próximas linhas geradas (5 ou 6 linhas à frente) já têm `maxLanes=5`.
- Em ~12s adicionais (250m total) → **Tier 2**: speed=12, lanes ainda 5 mas minLanesPerRow=3.
- E assim por diante até Tier 5.

### 3.2 Reset com tecla R

Em qualquer ponto, aperte **R**:
- Console: `[DifficultyManager] RESET → Tier 0 (speed=8, maxLanes=3)`.
- Speed cai pra 8, câmera reaproxima.
- Próximas linhas geradas voltam a ter `maxLanes=3`.
- Tiles antigos atrás (com maxLanes maior) continuam onde estavam — a
  transição é orgânica.

### 3.3 Forçar próximo tier com tecla T

Atalho útil pra testar tiers superiores sem andar 1200m:
- Aperte **T** → avança 1 tier imediatamente.
- Console: `[DifficultyManager] ⏭ FORCED Tier N`.

### 3.4 Reset via Inspector (alternativa sem keyboard)

Durante o Play, selecione `_DifficultyManager` na hierarchy →
no componente Difficulty Manager, clique no ícone de **três pontinhos** (⋮)
→ **Reset Difficulty** (ou **Force Next Tier**). Mesma coisa que `R`/`T`.

### 3.5 Checklist de validação

- [ ] Console mostra `↑ Tier 1` em ~100m, `↑ Tier 2` em ~250m, etc.
- [ ] Speed do player aumenta visivelmente entre tiers.
- [ ] Câmera afasta entre tiers (zoom Y maior).
- [ ] No tier 1+ você vê linhas com 5 lanes (mais largas no Scene View).
- [ ] No tier 3+ você vê linhas com 7 lanes.
- [ ] No tier 4+ você vê 2 critical paths em paralelo (2 gizmos verdes por linha).
- [ ] Coleta de moedas escala — tier 2 dá 4 por tile crítico, tier 4 dá 5.
- [ ] Aperta R → speed volta pra 8, lanes voltam pra 3 nas próximas linhas.
- [ ] Aperta T várias vezes → sobe rápido até o tier 5.
- [ ] FPS estável em 60+ mesmo com `maxLanes=9` (12 rows × 9 lanes).

---

## 4. Commit

```
git add Assets/ScriptableObjects/RailSwitchMVP/DifficultyConfig_Default.asset \
        Assets/Scenes/RailSwitchMVP_Scene.unity
git commit -m "feat(iter4): tiers populados + debug controller"
```

---

## 5. Troubleshooting

**Console não mostra log de mudança de tier:**
- Confirme que você adicionou mais de 1 tier no `DifficultyConfig_Default`.
- Confirme que o `Trigger At Distance` dos tiers > 0 está crescente.
- O log aparece quando `distanceTraveled >= triggerAtDistance` do próximo tier
  — se você está parado, não aparece.

**Speed não muda mesmo após o log do tier:**
- Confirme que o `PlayerRailRider` está lendo do `DifficultyManager` (deveria,
  desde a Iter 1). Selecione o Player na hierarchy durante Play e veja o
  campo `Current Speed` no Inspector.

**Zoom da câmera não muda:**
- Tier deve ter `Camera Zoom Min/Max` diferentes do anterior. Se ambos
  forem 12 e 18 em todos os tiers, não vai mudar visualmente.

**Tile aparece "saltando" lateralmente quando maxLanes muda:**
- ~~Esperado pela spec §2.6.~~ **CORRIGIDO** com Fix A (commit pós-Iter 4):
  agora `RailGenConfig.globalMaxLanes = 9` define um range fixo de posições
  X no mundo, e os tiers ativam apenas um subset centrado deste range
  (Tier 0 = lanes [3,4,5], Tier 5 = lanes [0..8]). Como a fórmula de X
  agora usa `globalMaxLanes` em vez do `maxLanes` do tier, lane N tem
  sempre o mesmo X mundial — o switch ±1 sempre move exatamente
  1 laneSpacing visualmente.
- Se você reabrir o `RailGenConfig_Default` deve aparecer o novo campo
  `Global Max Lanes = 9`. Não mude esse valor a não ser que o tier mais
  alto tenha `maxLanes > 9`.

**Tecla R/T não funciona:**
- Confirme que adicionou `_DebugController` na cena.
- Confirme `DifficultyDebugController.Restrict To Debug Builds = false`.
- Confirme `_DifficultyManager` está ativo na cena.

**Após reset, tier sobe imediatamente de novo:**
- Não deveria mais (fix de offset implementado nesta iteração). Se
  aparecer, verifique que `_lastRawDistance` e `_distanceOffset` estão
  sendo atualizados corretamente. Confirme que você está rodando o build
  mais recente do código.

---

## 6. Próximos Passos (Iteração 5 — última)

Stress test:
- Modo headless: gerar 10k linhas e validar que o critical path nunca quebra
  (mesmo com tier changes simulados no meio).
- Profiler: confirmar 60fps com 12 rows × 9 lanes ativos.
