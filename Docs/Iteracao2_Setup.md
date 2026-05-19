# Iteração 2 — Guia de Setup na Unity

**Objetivo desta iteração:** validar input loop (setas ←/→ mudam o switch),
transição entre tiles via switch, coleta de moedas, e Game Over distinguindo
`DeadEnd` de `OutOfBounds`.

Pré-requisitos: Iteração 1 validada. Cena `RailSwitchMVP_Scene.unity` já
existe com Player + Camera + DifficultyManager + 3 tiles em Lane 1.

---

## 1. Tag `Player` e Tag `Coin`

1. **Edit → Project Settings → Tags and Layers**.
2. Em **Tags**, garanta que existe `Player` (você já criou na Iter 1).
3. Adicione uma nova tag `Coin` (vai ser útil pra debug/filter, opcional para o MVP).
4. A capsule do `Player` na cena precisa estar com a tag `Player` (confira na Inspector).

---

## 2. Prefab `Coin_Prefab`

1. Hierarchy → clique direito → **3D Object → Cylinder**.
2. Renomeie para `Coin_Prefab`.
3. Inspector:
   - Transform → Scale `(0.3, 0.05, 0.3)` (moeda achatada).
   - **Capsule Collider** (gerado automaticamente) → remova.
   - Adicione **Sphere Collider** → marque `Is Trigger` = ✅, Radius `0.5`.
   - Material: crie um material URP Lit amarelo (`Coin_Mat`), atribua.
   - Adicione o componente **Collectible Coin**
     (`RailSwitchMVP/Collectibles/CollectibleCoin.cs`):
     - `Value` = 1
     - `Spin Speed` = 180
   - Tag (opcional): `Coin`.
4. Arraste o GameObject para `Assets/Prefabs/RailSwitchMVP/Coin_Prefab.prefab`.
5. Delete o GameObject da cena.

---

## 3. Atualizar `TrackTile_Prefab`

Abra o prefab `Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab` em modo de edição (duplo clique).

### 3.1 Adicionar o visual da seta

1. Como filho do prefab raiz, **Create → 3D Object → Cube** (ou Cone, se preferir).
2. Renomeie para `Arrow`.
3. Posição local: **na mesma posição do `EndPoint`** → `(0, 0.4, 5)`.
4. Escala local: `(0.4, 0.1, 1.2)` (cubo fino e comprido apontando "pra frente").
5. Material amarelo/laranja (crie um `Arrow_Mat` se quiser).
6. Mantenha rotação local `(0, 0, 0)` — o SwitchController vai rotacionar via script.

### 3.2 Adicionar `SwitchController`

1. Selecione a **raiz** do prefab (`TrackTile_Prefab`).
2. Add Component → **Switch Controller**.
3. Configure:
   - `State` = Middle (default).
   - `Owner Tile` → deixe vazio (o TrackTile preenche no Awake).
   - `Arrow Visual` → arraste o GameObject `Arrow` que você acabou de criar.

### 3.3 Adicionar `CoinSpawner`

1. Selecione a raiz do prefab.
2. Add Component → **Coin Spawner**.
3. Configure:
   - `Coin Prefab` → arraste `Coin_Prefab` (do passo 2).
   - `Start Point` → arraste `StartPoint` (filho do prefab).
   - `End Point` → arraste `EndPoint` (filho do prefab).
   - `Padding` = 0.1
   - `Coin Height` = 0.5
   - `Spawn On Start Count` = 3 (no MVP da Iter 2, cada tile spawna 3 moedas
     ao iniciar; Iter 3+ o gerador passa esse número conforme o tier).
   - `Is Critical Path` = true.

### 3.4 Backlinks no `TrackTile`

1. Selecione a raiz do prefab.
2. No componente **Track Tile**:
   - `Switch` → arraste o próprio prefab raiz (já tem SwitchController).
   - `Coins` → arraste o próprio prefab raiz (já tem CoinSpawner).
   - *(O Awake do TrackTile faz `GetComponentInChildren` como fallback, então
     se esses campos ficarem vazios ainda funciona — mas preencher é mais limpo.)*

3. Salve o prefab (botão **Save** no topo da prefab edit view).

---

## 4. Atualizar a Cena

Abra `Assets/Scenes/RailSwitchMVP_Scene.unity`.

### 4.1 Managers (GameObjects vazios na raiz)

Adicione, **ao lado de `_DifficultyManager` que já existe**:

| GameObject | Componente | Configuração |
|---|---|---|
| `_RailManager` | Rail Manager | (nenhum campo a configurar) |
| `_GameManager` | Game Manager | (nenhum campo a configurar) |
| `_CoinManager` | Coin Manager | (nenhum campo a configurar) |
| `_Input` | Keyboard Directional Input | (nenhum campo a configurar) |

> Os 4 são singletons que se auto-registram no Awake.

### 4.2 Refazer o grid de tiles (3 linhas × 3 lanes, com 1 buraco)

**Apague o grupo `_Tracks` da Iter 1** (ou só seus filhos) e refaça
seguindo a tabela abaixo. O cálculo de posição é o mesmo da Iter 1:
- Z do tile `Row R` = `R * (trackLength + rowGap) + trackLength/2` = `R*12 + 5`.
- X do tile `Lane L` com `maxLanes=3` = `(L - 1) * laneSpacing` = `(L - 1) * 2.5`.

| Tile | Position (X, Y, Z) | Row | Lane | Max Lanes At Spawn |
|---|---|---|---|---|
| Tile_R0L0 | (-2.5, 0, 5)  | 0 | 0 | 3 |
| Tile_R0L1 | (0, 0, 5)     | 0 | 1 | 3 |
| Tile_R0L2 | (2.5, 0, 5)   | 0 | 2 | 3 |
| Tile_R1L0 | (-2.5, 0, 17) | 1 | 0 | 3 |
| Tile_R1L1 | (0, 0, 17)    | 1 | 1 | 3 |
| Tile_R1L2 | (2.5, 0, 17)  | 1 | 2 | 3 |
| **(sem Tile R2L0)** — pra demonstrar DeadEnd |
| Tile_R2L1 | (0, 0, 29)    | 2 | 1 | 3 |
| Tile_R2L2 | (2.5, 0, 29)  | 2 | 2 | 3 |

> Cada instância de `TrackTile_Prefab`: depois de posicionar, ajuste `Row` e
> `Lane` no Inspector do componente Track Tile.
> `MaxLanesAtSpawn = 3` para todos.

### 4.3 Player

1. No GameObject `Player` (Capsule existente), no componente **Player Rail Rider**:
   - `Config` → `RailGenConfig_Default` (já estava).
   - `Difficulty` → `_DifficultyManager` (já estava).
   - `Input Source` → arraste `_Input` GameObject.
   - `Start Tile` → arraste `Tile_R0L1` (centro da linha 0).
2. **Verifique:** a tag do Player está `Player` (necessário para a coleta de moedas).

---

## 5. Play Test

Salve a cena e dê **Play**.

### 5.1 Movimento livre (controle)
- Player começa em `Tile_R0L1`, anda forward.
- **Seta direita** uma vez → arrow gira pra +45°, switch = `Right`.
  - No fim do tile, o player faz lerp X de (0, ..., 10) até (2.5, ..., 14) entrando em `Tile_R1L2`.
- **Seta esquerda** uma vez (no Tile_R1L2) → arrow vira pra -45°, switch = `Left`.
  - No fim de R1L2 (lane 2), TargetLane = 1 → entra em `Tile_R2L1`. **Coleta as 3 moedas.**

### 5.2 Game Over — DeadEnd
- Restart Play.
- No Tile_R0L1, aperte ←. No Tile_R1L0 (lane 0), aperte ← de novo → TargetLane = -1.
- **Espera:** Game Over com reason `OutOfBounds` (foi pra fora do grid).

### 5.3 Game Over — DeadEnd genuíno
- Restart.
- No Tile_R0L1, seta ← uma vez → no fim, vai pra Tile_R1L0.
- Em Tile_R1L0, deixe o switch em `Middle` (não mexer).
  - No fim, TargetLane = 0 → próxima linha é Row 2, mas `(R2, L0)` está
    vazio (não existe). **Espera:** Game Over `DeadEnd`.

### 5.4 Coleta de moedas
- Em qualquer rota válida, o player passa por cima das moedas spawnadas (3 por tile).
- **Espera:** Console mostra `[CoinManager] +1 → N` a cada moeda coletada.

### 5.5 Checklist de validação

- [ ] Setas ←/→ rotacionam a seta visual em -45°/0°/+45°.
- [ ] Player faz lerp suave de X durante o gap.
- [ ] Coleta de moedas incrementa o total no `_CoinManager` (visível no Inspector durante Play).
- [ ] Game Over `OutOfBounds` ao pular pra fora do grid (de R1L0 com switch Left).
- [ ] Game Over `DeadEnd` ao tentar ir pra (R2, L0) que não existe.
- [ ] Após Game Over, o player para de mover.
- [ ] FPS estável (60+).

---

## 6. Commit

Quando validado, commitar os assets:

```
git add Assets/Prefabs/RailSwitchMVP/Coin_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/Coin_Prefab.prefab.meta \
        Assets/Prefabs/RailSwitchMVP/TrackTile_Prefab.prefab \
        Assets/Scenes/RailSwitchMVP_Scene.unity \
        Assets/Settings  # se você criou novos materials
git commit -m "feat(iter2): scene + prefabs com switches e moedas"
```

---

## 7. Troubleshooting

**Player não responde a setas:**
- Confirme que o GameObject `_Input` existe na cena com `KeyboardDirectionalInput`.
- Confirme que `Player Rail Rider → Input Source` aponta para ele (ou deixe
  vazio que o script resolve via FindObjectsByType no Start).
- Confirme `activeInputHandler = 1` em ProjectSettings (já é o caso) e que
  o pacote **Input System** está instalado em Package Manager.

**Moedas não somem ao passar:**
- Confirme que o Player tem **Collider** (a Capsule vem com CapsuleCollider; deixe).
- Confirme que a tag do Player é exatamente `Player` (case-sensitive).
- Confirme que a moeda tem **Sphere Collider com `Is Trigger = true`**.

**Arrow não rotaciona:**
- Confirme que `SwitchController.ArrowVisual` está atribuído.
- Se o Arrow está como filho do prefab, ele precisa ter rotação local `(0,0,0)` inicial.

**Game Over não dispara:**
- Confirme que `_GameManager` e `_RailManager` existem na cena.
- Confirme `Row` e `Lane` corretos em cada `TrackTile` na Inspector
  (os auto-registros usam esses valores).

**Player atravessa o tile e cai no infinito antes de Game Over:**
- O `TryEnterGap` é chamado quando `position.z >= currentTile.EndPoint.position.z`.
- Se o `EndPoint` do tile estiver mal-posicionado, ele atinge tarde demais.
  Confirme que o `EndPoint` está em Z local `+5` dentro do prefab.

---

## 8. Próximos Passos (Iteração 3)

- `ProceduralRailGenerator` (algoritmo de critical path da spec §4.2).
- Substituir o RailManager mínimo por uma versão com spawn ahead / despawn behind.
- Tiles deixam de ser hardcoded na cena — são instanciados conforme o player avança.
- Moedas passam a usar `tier.coinsPerCriticalTile` / `tier.coinsPerDecoyTile` em vez de hardcoded.
