# Iteração 1 — Guia de Setup na Unity

**Objetivo desta iteração:** validar movimento forward + framing da câmera + escala visual com 3 trilhos hardcoded em sequência.

---

## 1. Estrutura de Pastas

Crie no Project Window:

```
Assets/
├── Scripts/RailSwitchMVP/
│   ├── Config/
│   │   ├── RailGenConfig.cs
│   │   └── DifficultyConfig.cs
│   ├── Core/
│   │   └── DifficultyManager.cs
│   ├── Track/
│   │   └── TrackTile.cs
│   └── Player/
│       ├── PlayerRailRider.cs
│       └── PlayerCameraRig.cs
├── Prefabs/RailSwitchMVP/
└── ScriptableObjects/RailSwitchMVP/
```

Copie os 6 scripts para `Assets/Scripts/RailSwitchMVP/` nas subpastas indicadas.

Faça `git add` (e `git commit`) para incluir tudo no repositório quando estiver pronto. Assets binários (texturas, modelos, áudio) são tratados automaticamente pelo Git LFS via `.gitattributes` na raiz do repo.

---

## 2. Criar os ScriptableObjects

### 2.1 RailGenConfig

1. Em `Assets/ScriptableObjects/RailSwitchMVP/`, clique direito → **Create → RailSwitchMVP → Rail Gen Config**.
2. Nomeie: `RailGenConfig_Default`.
3. Deixe os valores default (já estão calibrados como ponto de partida).
4. **Importante:** marque `Debug Draw Critical Path` = true para vermos os Gizmos durante o dev.

### 2.2 DifficultyConfig (apenas 1 tier para a Iteração 1)

1. Clique direito → **Create → RailSwitchMVP → Difficulty Config**.
2. Nomeie: `DifficultyConfig_Default`.
3. Expanda a lista `Tiers` e adicione **1 elemento**. Preencha:

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

> Na Iteração 1, só `Player Speed`, `Camera Zoom Min` e `Camera Zoom Max` são realmente lidos. Os outros campos serão usados nas iterações 2+.

---

## 3. Criar o Prefab do TrackTile

1. Na Hierarchy, crie um **GameObject vazio** chamado `TrackTile_Prefab`.
2. Como filho do prefab, crie:
   - **Cube** chamado `Mesh` — escale para `(2, 0.2, 10)` (largura, altura, comprimento do trilho).
     - Posicione em `(0, 0, 0)` local.
     - Crie um Material URP Lit qualquer (ex: cinza médio) e aplique.
   - **Empty GameObject** chamado `StartPoint` — posição local `(0, 0, -5)` (extremidade traseira do trilho).
   - **Empty GameObject** chamado `EndPoint` — posição local `(0, 0, 5)` (extremidade dianteira).

3. No GameObject raiz `TrackTile_Prefab`, adicione o componente **Track Tile**:
   - Arraste `StartPoint` e `EndPoint` para os campos correspondentes.
   - Arraste `RailGenConfig_Default` para o campo `Debug Config`.
   - Deixe `Row`, `Lane`, `Max Lanes At Spawn` zerados (serão sobrescritos pelo gerador depois).
   - Marque `Is On Critical Path` = true (apenas para visualização do gizmo verde nesta iteração).

4. Arraste o GameObject para `Assets/Prefabs/RailSwitchMVP/` para virar prefab.
5. Delete da cena após criar o prefab.

> A geometria `(2, 0.2, 10)` assume `trackLength = 10` no config. Se você mudar `trackLength`, vai precisar reescalar o mesh do prefab para bater visualmente.

---

## 4. Montar a Cena `RailSwitchMVP_Scene`

### 4.1 Criar a cena

1. **File → New Scene** → escolha o template **Basic (Built-in)** ou **3D (URP)** dependendo do que aparecer.
2. Salve como `Assets/Scenes/RailSwitchMVP_Scene.unity`.
3. Delete a Main Camera padrão (vamos criar a nossa).
4. Ajuste a Directional Light pra ter uma boa iluminação top-down (rotação Y -30, X 50 funciona bem).

### 4.2 GameObject "DifficultyManager"

1. Crie GameObject vazio chamado `_DifficultyManager` na raiz.
2. Adicione o componente **Difficulty Manager**.
3. Arraste `DifficultyConfig_Default` para o campo `Config`.

### 4.3 Hardcode dos 3 trilhos

1. Crie GameObject vazio chamado `_Tracks` (organização).
2. Como filhos, instancie **3 prefabs `TrackTile_Prefab`** posicionados em sequência:

| Tile | Position (X, Y, Z) | Row | Lane |
|---|---|---|---|
| Tile_0 | (0, 0, 5) | 0 | 1 |
| Tile_1 | (0, 0, 17) | 1 | 1 |
| Tile_2 | (0, 0, 29) | 2 | 1 |

**Cálculo:** com `trackLength = 10` e `rowGap = 2`, cada tile fica a 12 unidades de distância um do outro. O Z indicado é o **centro do tile**. O StartPoint do Tile_0 fica em Z=0, EndPoint em Z=10, depois um gap até Z=12, e assim por diante.

Para cada tile, no componente `TrackTile`, preencha `Row` e `Lane` conforme a tabela. `MaxLanesAtSpawn` pode ficar = 3.

### 4.4 Player

1. Crie um **Capsule** na raiz chamado `Player`.
   - Posição inicial: `(0, 1, 0)`.
   - Escale para algo visível (default já funciona, mas pode reduzir para `(0.5, 0.5, 0.5)` se preferir).
   - Tag: `Player` (cria se não existir — usaremos isso para coleta de moedas na Iteração 2).
2. Adicione o componente **Player Rail Rider**:
   - `Config` → `RailGenConfig_Default`
   - `Difficulty` → arraste o GameObject `_DifficultyManager`
   - `Start Tile` → arraste `Tile_0` da hierarchy

### 4.5 Câmera

1. Crie um **Camera** na raiz chamado `MainCamera`.
   - Tag: `MainCamera` (Unity exige isso pra renderizar como câmera principal).
2. Adicione o componente **Player Camera Rig**:
   - `Player` → arraste o GameObject `Player`
   - `Config` → `RailGenConfig_Default`
   - `Difficulty` → arraste o GameObject `_DifficultyManager`
   - `Speed At Min Zoom` = 8, `Speed At Max Zoom` = 20 (defaults)

> Como o `PlayerCameraRig` tem `[RequireComponent(typeof(Camera))]`, ele garante que tem Camera no GameObject. Se Unity reclamar, é só deixar a Camera no mesmo GameObject que o script.

---

## 5. Teste

1. Salve a cena.
2. Play.

**O que esperar:**
- Player se move para frente em velocidade constante (8 unidades/s).
- Câmera segue por trás e acima, levemente inclinada (35°).
- Câmera olha um pouco à frente do player (look-ahead = 4).
- Você vê os 3 trilhos um após o outro, mas como ainda não há geração procedural, depois do Tile_2 o player "voa" pra frente sem trilho. **Isso é esperado nesta iteração.**
- Gizmos verdes (wireframe) aparecem acima dos trilhos no Scene View (apenas no editor).
- Esferas ciano (StartPoint) e amarelo (EndPoint) também visíveis no Scene View.

**Validações:**
- [ ] Player se move suave em linha reta.
- [ ] Câmera mantém posição relativa estável.
- [ ] Você consegue **ver os 3 trilhos no início** do play (não tem que correr atrás de nada).
- [ ] Mudando `cameraTilt` no `RailGenConfig_Default` durante runtime, a câmera responde imediatamente no próximo LateUpdate.
- [ ] Mudando `playerSpeed` no tier 0 do `DifficultyConfig_Default` durante runtime, o player muda velocidade e a câmera ajusta o zoom suavemente.

---

## 6. Tuning Sugerido

Brinque com estes valores durante o play pra calibrar feel:

- **`cameraTilt`** (RailGenConfig): 25° (mais cinematográfico, perde leitura) vs 45° (mais legível, perde profundidade). 35° é meu palpite, mas ajuste pra você.
- **`cameraLookAhead`** (RailGenConfig): 4 mostra mais do que vem. 0 fica centrado no player. 8 talvez seja excessivo.
- **`Camera Zoom Min/Max`** (DifficultyConfig tier 0): testar combinações entre 10 e 25. O zoom adaptativo entra em ação quando você muda `playerSpeed` em runtime.

---

## 7. Próximos Passos (Iteração 2)

Após validar a Iteração 1, partimos para:
- `SwitchController` (seta única rotacionada).
- `InputManager` (setas ←/→ no teclado).
- Transição do player entre tiles via switch.
- `CoinSpawner` + `CollectibleCoin` + `CoinManager`.
- Game Over: `DeadEnd` e `OutOfBounds`.

---

## 8. Troubleshooting

**Player não aparece:**
- Confira se a câmera está olhando na direção certa (rotação X = 35°, Y = 0).
- Confira se a posição da câmera está atrás do player (Z negativo relativo ao player).

**Gizmos não aparecem:**
- No Scene View, verifique se os Gizmos estão habilitados (botão no topo direito).
- Confira se `RailGenConfig_Default.Debug Draw Critical Path` = true.
- Confira se o campo `Debug Config` está atribuído em cada TrackTile.

**Erro "DifficultyConfig is not assigned":**
- Verifique no GameObject `_DifficultyManager` se o campo `Config` está apontando pro asset.

**Player não se move:**
- Verifique se o GameObject `_DifficultyManager` está ATIVO na cena.
- Verifique se o tier 0 tem `Player Speed > 0`.
- Verifique se `Difficulty` está atribuído no `PlayerRailRider`.

---

*Quando essa iteração estiver rodando e validada, me dê feedback e partimos para a Iteração 2.*
