# PostMVP2.4 — 3 Ideas (Auto-follow + Trilhos coloridos + Warmup)

3 features implementadas em batch nessa session. Cada uma requer setup
mínimo na Editor pra funcionar.

---

## Idea 3 — Auto-follow critical path power-up

**O que é:** power-up passive tile-based. Por N tiles (default 5), o
jogo segue critical path sozinho. Manual input ainda funciona mas é
sobrescrito a cada tile.

### Setup na Editor

1. GameObject vazio na cena → `_AutoCriticalFollower`.
2. Add Component → **Auto Critical Follower**.
3. (Opcional) Marcar checkbox `Debug Force Active` no Inspector se quiser
   testar sem power-up.

4. No `_HUD_Canvas`, novo TMP_Text:
   - Nome: `AutoFollowText`
   - Anchor preset: Top-Left
   - Anchored Position: (24, -652) (abaixo de TeleportText, se já tiver)
   - Width × Height: 300 × 60
   - Font Size: 32
   - Initial text: vazio (`""`)

5. No `_HUD → HUD Controller → Auto Follow Text` → arrastar `AutoFollowText`.

### Testar via Debug Panel

- F1 → Power-ups section → **Grant AutoFollow**.
- HUD: `AutoFollow 5`.
- Player segue critical sozinho por 5 tiles. Após expirar, manual de volta.

> **Nota:** o toggle "Auto-follow critical path (debug)" na seção Auto-test
> agora age sobre o `AutoCriticalFollower.Instance.DebugForceActive`. Mesmo
> efeito, mas vem do componente em vez de uma flag local no DebugPanel.

---

## Idea 2 — Trilhos coloridos por conectividade

**O que é:** tile fica VERDE se tem ao menos 1 lane vizinha (±1) com
tile na próxima row. VERMELHO se todas estão vazias (= dead-end garantido).

### Setup na Editor

1. Criar 2 materiais em `Assets/Prefabs/`:
   - `TrackConnected.mat` — Base Color verde (#22BB44 ou similar). URP Lit.
   - `TrackDisconnected.mat` — Base Color vermelho (#CC2233 ou similar).

2. Abrir `TrackTile_Prefab` em modo edição:
   - Selecionar a raiz do prefab.
   - No componente **Track Tile** → seção "Connectivity visuals":
     - **`Connectivity Renderer`** → arrastar o `MeshRenderer` **do Arrow**
       (filho do prefab — é o cubo/cone que rotaciona indicando o switch).
       **NÃO** arraste do Mesh do trilho — assim só o connector muda
       de cor, não o trilho inteiro.
     - `Connected Material` → arrastar `TrackConnected.mat`.
     - `Disconnected Material` → arrastar `TrackDisconnected.mat`.
   - Salvar prefab.

> **Por que o Arrow e não o Mesh do trilho?** O Arrow já representa o
> SWITCH/CONNECTOR. Pintar ele combina temática ("este switch leva a
> algum lugar / não leva") + mantém o visual do trilho consistente.

> **Refs faltando = warning no console:** se você esquecer de atribuir,
> o console mostra `[TrackTile] connectivityRenderer NÃO atribuído...`
> ou `[TrackTile] connectedMaterial NÃO atribuído...`. Aparece UMA vez
> por session pra não spammar.

### Testar

- Play. Anda alguns metros.
- Decoys que NÃO levam a lugar nenhum aparecem VERMELHOS.
- Critical path e decoys conectados ficam VERDES.
- Última row à frente fica verde (default — ainda não tem next pra checar).

### Trade-off de balance

Pode trivializar — player só segue verde. Se quiser balance:

- Limitar visibilidade: só mostra cor em rows próximas (~5 à frente).
- Gate por tier: vermelho ativo só Tier 2+.

Por ora, **implementação pura** sem gate. Playtest depois decide.

---

## Idea 1 — Warmup + countdown

**O que é:** as 5 primeiras rows do jogo são "calmaria" — single tile
center, sem hazards/coins/power-ups, em 0.5x speed. Ao entrar na ÚLTIMA
warmup row, countdown 3-2-1-GO! aparece. Após GO, geração procedural
normal começa.

### Setup na Editor

1. GameObject vazio na cena → `_WarmupController`.
2. Add Component → **Warmup Controller**.
3. Inspector:
   - `Config` → `RailGenConfig_Default`.
   - `Countdown Text` → (atribuir depois, próximo passo).
   - `Countdown Step Seconds` = 1
   - `Go Display Seconds` = 0.8

4. No `_HUD_Canvas`, novo TMP_Text grande pro countdown:
   - Nome: `CountdownText`
   - Anchor preset: Center / Middle (overlay no centro da tela).
   - Anchored Position: (0, 0)
   - Width × Height: 600 × 300
   - Font Size: **160** (grande, dramático).
   - Color: branco com possivelmente outline preto.
   - Alignment: Center / Middle.
   - Initial text: `""` (vazio).
   - Inicialmente DESATIVE o GameObject (checkbox no topo do Inspector
     ao lado do nome). O WarmupController reativa em runtime.

5. No `_WarmupController → Countdown Text` → arrastar `CountdownText`.

### Tunables

No `RailGenConfig_Default`:
- `Warmup Row Count` = 5 (mude pra 3 ou 8 se quiser ajustar).
- `Warmup Speed Multiplier` = 0.5 (0.3 = mais lento, 1.0 = sem efeito).

### Testar

- Play. Player nasce no centro da row 0.
- Primeiras 5 rows: single tile, atravessa em ~3s cada a 0.5x speed.
- HUD distance fica em "Dist 0 m". Time não conta. Tier 0 fixo.
- Setas/Space/Shift+arrow não fazem nada (input lockado).
- Ao entrar na row 4 (última warmup), aparece "3" grande no centro da tela.
- "3" → "2" → "1" (cada um por 1s).
- "GO!" aparece, state vira Playing, geração procedural começa nas próximas rows.
- 0.8s depois, GO! some.
- Player agora em speed normal (1.0x tier 0), pode dar setas pra switch.

### Edge cases

**Restart durante warmup:** se player apertar R no Game Over screen
durante warmup (estranho mas possível), scene reload → começa novo warmup.

**Game Over durante warmup:** durante warmup não tem hazards, decoys, ou
DeadEnd (todos rows são single center connected). Único game over teórico
seria OutOfBounds, mas switch está locked em Middle. Praticamente
impossível morrer no warmup. ✓

---

## Próximos passos pós-3 ideas

Setup todos os 3 acima + validar. Depois discutimos próximas direções
(audio, polish visual, mobile, etc.).
