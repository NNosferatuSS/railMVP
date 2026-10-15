# Switch visual — ponte elevadiça (3 meshes)

Substitui o visual antigo (uma seta única girando em Y, snap instantâneo) por
três meshes pré-existentes, uma por direção, que giram no eixo **X local** como
uma ponte elevadiça vindo de baixo.

- Direção **selecionada** → `raisedAngleX` (default `0`, deitada/atravessável).
- Duas **não selecionadas** → `loweredAngleX` (default `-90`, penduradas pra baixo).

Exemplo do fluxo: selecionada = Middle → Left e Right em -90. Player aperta
direita → Middle desce pra -90, Right sobe pra 0, Left continua em -90.

## Nada mudou na gameplay

`SwitchState` (`Left = -1`, `Middle = 0`, `Right = 1`), `Nudge`, `TargetLane` e
`OnStateChanged` seguem idênticos. O visual só reflete o state — nunca o define.

## Arquivos

| Arquivo | Papel |
|---|---|
| `Track/SwitchVisual.cs` | Componente que guarda as 3 meshes e faz o tween em X. |
| `Config/SwitchVisualConfig.cs` | SO de preset: ângulos, durações, easing. |
| `Track/SwitchController.cs` | Delega o visual pro `SwitchVisual`; mantém a seta legada como fallback. |
| `Track/TrackTile.cs` | Cor de conectividade agora pinta só a mesh levantada. |
| `Core/ProceduralRailGenerator.cs` | Injeta o preset e seta o state inicial **sem animar**. |

## Wiring no prefab

No `TrackTile_Prefab`, no GameObject `Switch`:

1. Adicionar o componente **Switch Visual**.
2. Arrastar as 3 meshes (filhas de `Switch`) pros campos `Left Mesh`,
   `Middle Mesh`, `Right Mesh`. **Cada mesh precisa do pivot na dobradiça** — a
   rotação é aplicada direto no Transform referenciado, sem wrapper.
3. `Config` pode ficar vazio se o preset for injetado central (ver abaixo);
   senão, arrastar um `SwitchVisualConfig`.
4. A seta antiga (`Arrow`) e o campo `Arrow Visual` do `SwitchController` podem
   ser removidos. Enquanto existirem, só têm efeito em prefabs **sem**
   `SwitchVisual`.

Se as meshes ficarem sem atribuir, o `SwitchVisual` simplesmente não move nada
(sem erro) — `HasAllMeshes` diz se está completo.

## Preset

Criar o asset via **Create → RailSwitchMVP → Switch Visual Config**.
Aparece no Control Panel em **Switch Visual**, e o Inspector mostra os warnings
de validação (via `IValidatedConfig`).

### Toggles de tweak

Os dois ficam no topo do `SwitchVisualConfig`, header **Tweak / decisão de design**
— um lugar só, visível no Control Panel:

| Toggle | OFF (default) | ON |
|---|---|---|
| `colorOnlyConnected` | Verde quando conectado, vermelho quando aponta pro vazio. | Só verde. Apontando pro vazio, a mesh volta pro material original do prefab — sem vermelho. |
| `sequenceLowerThenRaise` | As duas meshes se movem juntas, defasadas por `raiseDelay`. Rápido e responsivo. | A antiga desce **por completo** antes da nova começar a subir (espera `lowerDuration + raiseDelay`). Lê como mecanismo, mas mais lento. |

`TotalSwitchDuration` no SO mostra o tempo ponta a ponta de uma troca com os
valores atuais, e a validação avisa se passar de ~0.6s — vale conferir se cabe no
tempo que o player tem dentro do tile, senão a ponte ainda está subindo quando ele
chega no fim.

Nota: a cor troca no instante do input (`OnStateChanged`), não no fim da animação.
Com sequenciado + `colorOnlyConnected` existe uma janelinha em que nenhuma mesh
está verde (a antiga já despintou, a nova ainda não subiu).

### Demais campos

| Campo | O que faz |
|---|---|
| `raisedAngleX` / `loweredAngleX` | Ângulos das posições levantada e baixada. |
| `raiseDuration` / `lowerDuration` | Durações separadas — subir e descer não precisam ter o mesmo tempo. |
| `raiseDelay` | Atraso antes da nova direção subir. É o que dá o beat de "a antiga baixa, depois a nova sobe". Sem isso o olho lê como troca de sprite, não como mecanismo. |
| `raiseEase` / `lowerEase` | `Linear`, `EaseOut`, `PopBack`, `Elastic`, `Custom`. |
| `overshoot` | Intensidade do pop do `PopBack` (o `Elastic` tem amplitude própria). |
| `customRaiseCurve` / `customLowerCurve` | Usadas quando o preset é `Custom`. |

Default: sobe em `PopBack` 0.18s com overshoot 1.7, desce em `EaseOut` 0.12s,
`raiseDelay` 0.04s.

### Preset central vs. por prefab

`RailGenConfig.switchVisualConfig` (header *Switch (visual)*) é injetado em todo
switch no spawn. Se ficar vazio, cada prefab usa o preset atribuído nele mesmo.
Isso permite trocar o feel do jogo inteiro num campo só, sem tocar nos prefabs.

## Conectividade verde/vermelho

Só a mesh **levantada** (a direção escolhida) recebe `connectedMaterial` /
`disconnectedMaterial`. As duas baixadas voltam pro material original do prefab,
cacheado no primeiro acesso — senão ficariam guardando a cor de uma escolha
anterior. O `connectivityRenderer` do `TrackTile` só é usado quando não há
`SwitchVisual`.

Com `colorOnlyConnected` ligado, o caso desconectado não pinta nada. O toggle é
lido de `Switch.Visual.Config`, então precisa do componente `SwitchVisual` no
prefab (as 3 meshes podem estar vazias, mas o componente e o preset têm que
existir). Sem eles, vale o verde/vermelho clássico.

## Animar vs. não animar

`SwitchController.SetState(state, immediate)`:

- `immediate: true` → aplica o ângulo no mesmo frame. Usado no spawn
  (`ProceduralRailGenerator`), no reset de pool (`TrackTile.ResetForReuse`) e no
  respawn (`ReviveController`). Sem isso, todo tile reciclado animaria a ponte
  na cara do jogador ao entrar em cena.
- `immediate: false` (default) → anima. É o caminho do input do player
  (`Nudge`), do auto-play (`AutoCriticalFollower`) e do `VortexObstacle`.

O tile nasce já com uma direção levantada (aleatória, `Random.Range(-1, 2)` —
comportamento que já existia) e as outras duas baixadas, sem animação.

## Perf

Tween manual no `Update`, sem coroutines nem alocação por tile. O `Update` só
percorre 3 structs e sai cedo quando nenhuma mesh está animando.
