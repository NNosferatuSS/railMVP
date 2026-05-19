# Pós-MVP2 — Pooling de Tiles

**Objetivo:** reduzir GC alloc substituindo `Instantiate`/`Destroy` de tiles
por reuso via pool. Ganho mais relevante em builds mobile.

**Escopo:** apenas TILES nesta etapa. Coins/obstáculos/power-ups continuam
Instantiate/Destroy (rare lifecycle ou totalmente destruídos, pooling
adicional pode entrar depois).

---

## 1. Setup na cena

1. Hierarchy → clique direito → **Create Empty**.
2. Renomeie pra `_PrefabPool`.
3. Add Component → **Prefab Pool** (`RailSwitchMVP/Core/PrefabPool.cs`).
4. Inspector (defaults OK):
   - `Default Capacity` = 16
   - `Max Size` = 200

Pronto. Não precisa atribuir nada — o `ProceduralRailGenerator` e
`RailManager` detectam o singleton automaticamente.

> Se o GameObject `_PrefabPool` não existir, o sistema cai pro
> `Instantiate`/`Destroy` antigo (fallback transparente). Pode ser
> removido em tempo de debug pra comparar performance.

---

## 2. Como funciona

**Reaproveitamento:**
- `RailManager.DespawnRow` → `PrefabPool.Release(tile)` desativa o tile e
  guarda no pool (filho de `_PrefabPool`).
- `ProceduralRailGenerator.GenerateRow` → `PrefabPool.Spawn(tilePrefab)` pega
  uma instância reusada (se existir) ou cria nova.
- Após pegar, chama `tile.ResetForReuse()` que:
  - Reseta o switch pro `Middle`.
  - Destrói todos os children dinâmicos (coins, obstacle, barrier,
    power-up, warning icon) — comparando com snapshot dos children que
    vieram do prefab original.

**Snapshot inteligente:**
- `TrackTile.Awake` captura `_initialChildren = Transform[]` (Mesh,
  StartPoint, EndPoint, Arrow — qualquer coisa que estava no prefab).
- ResetForReuse compara children atuais contra esse set. O que sobrar é
  dinâmico → destroy.
- Não precisa configurar manualmente quais children são "permanentes".

---

## 3. Play test

### 3.1 Funcional
- Play normal. Anda 30s.
- ✅ Tudo funciona idêntico à versão anterior (geração, switches, moedas,
  hazards, power-ups, game over).

### 3.2 Hierarchy
- Pause no meio do Play. Veja a hierarchy.
- `_PrefabPool` deve ter vários filhos com nome `TrackTile_Prefab(Clone)`
  inativos (= reservas no pool).
- `_Tracks` (se você usa esse GameObject) deve ter os tiles ativos da rota.
- Quando o player atravessa um tile e ele despawna, ele migra de `_Tracks`
  pra `_PrefabPool` (visível na hierarchy).

### 3.3 Stress test
- Rode **Tools → RailSwitchMVP → Run Stress Test (10k rows)**.
- ✅ Continua passando — pool não afeta plan layout, só a instanciação.

### 3.4 Profiler (opcional)
- Profiler → CPU Usage → Memory.
- Sem pool: GC alloc por frame ~5-10KB com tile spawn/despawn.
- Com pool: deve cair pra <1KB em steady state (sem novas instâncias após
  o pool estar "quente").

---

## 4. Commit

```
git add Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(post-mvp2): _PrefabPool GameObject na cena"
```

---

## 5. Troubleshooting

**Tiles ficam com sobras de coins/obstáculos do uso anterior:**
- O `_initialChildren` snapshot pode ter capturado children errados. Verifique
  que `TrackTile.Awake` roda ANTES de qualquer spawner adicionar children.
  Em prefabs simples (estrutura estática), isso é garantido.

**Performance pior com pool:**
- Verifique no Profiler se a recursão SetParent/SetActive tá tomando muito
  tempo. Se sim, aumente `Max Size` no `_PrefabPool` pra reduzir Instantiate
  durante o início do jogo.

**`_PrefabPool` não detectado:**
- Confirme que o GameObject está ATIVO na cena.
- Console deve mostrar fallback silencioso (sem erro) — pool ausente é OK.
