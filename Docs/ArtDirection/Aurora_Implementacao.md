# Aurora — implementação 3D (slice v1)

Direção escolhida: **01 Aurora** (`2026-10-15-exploracao-v1`). Este doc descreve o que foi
construído, onde mora e como ajustar. Gameplay e câmera não mudaram.

## Pipeline

```
Blender (Art/Source/RailAurora.blend)  ──FBX──▶  Assets/Graphics/Aurora/Models
                                                    │
Tools → RailSwitch → Art → Aurora — Apply All  ◀────┘  (materiais, preset, volume, prefabs, cena)
Tools → RailSwitch → Art → Aurora — Render Previews     (PNG 9:19.5 com 3 e 7 colunas)
```

- Modelagem 100% procedural via bpy (MCP do Blender). Convenção de export:
  Blender `(x, y, z)` → Unity `(x, z, y)` (`axis_forward='Z'`, `axis_up='Y'`, bake de transform).
  No Unity os importers usam `bakeAxisConversion` e não importam materiais.
- O wizard é idempotente: tudo que ele cria começa com `Aurora_`/`M_Aurora_` e é refeito a cada
  execução. Visuais antigos são **desativados**, não apagados.
- Batch (sem abrir o Editor):
  `unity run . -- -executeMethod RailSwitchMVP.EditorTools.AuroraArtSetup.BatchApplyAndPreview`

## Peças

| Peça | Mesh (FBX) | Onde é aplicada |
|---|---|---|
| Trilho (fita teal, ponta chanfrada, contorno ciano) | `Aurora_Track` | `TrackTile_Prefab/Aurora_Track` |
| Conector reto / diagonal (fita menta emissiva) | `Aurora_ConnectorStraight`, `Aurora_ConnectorDiag` | `TrackTile_Prefab/Switch/Aurora_Connectors/{Left,Middle,Right}` |
| Player (pérola + crescente) | `Aurora_Player` | `Player/Aurora_PlayerVisual` (cena) |
| Coin (losango âmbar) | `Aurora_Coin` | `Resource_CoinPrefab/Aurora_Visual` |
| Gem (cristal violeta) | `Aurora_Gem` | `Resource_Gem/Aurora_Visual` |
| Power-up (anel + ícone existente) | `Aurora_PowerUpRing` | `PowerUp*/Visual/Aurora_Visual` |
| Debuff (triângulo invertido + ícone) | `Aurora_DebuffFrame` | `DebuffVariant_*/Visual/Aurora_Visual` |
| Hazard letal (bloco serrilhado) / Barrier (parede listrada) | `Aurora_HazardLethal`, `Aurora_HazardBarrier` | `HazardVariant_*/Aurora_Visual` |
| Véus de aurora / arcos escuros | `Aurora_BGVeil`, `Aurora_BGArc` | `Aurora_Environment` (cena) |

Leitura por **forma**, não só cor: círculo = power-up, triângulo = debuff, bloco anguloso = hazard,
losango = coin, esfera + crescente = player.

## Conector

`SwitchVisualConfig` ganhou `animMode`:
- `HingeX` — ponte elevadiça (comportamento anterior).
- `ExtendZ` — a direção escolhida se **estende** do fim do trilho até a próxima linha; as outras
  recolhem pra 0 (renderer desligado). Preset: `Assets/Graphics/Aurora/Config/SwitchVisual_Aurora.asset`,
  ligado no `RailGenConfig_Default.switchVisualConfig`.

Cada slot do conector tem um filho `Mesh` com escala Z calculada a partir de `trackLength`, `rowGap`,
`laneSpacing` e dos StartPoint/EndPoint do prefab. Se esses valores mudarem, rode o Apply All de novo.

Cores: verde-menta = conectado, coral = aponta pro vazio (mantido do sistema de conectividade).

## Shaders (URP, sem textura)

| Shader | Uso |
|---|---|
| `Rail/Aurora/Surface` | Opaco flat de 2 tons pela main light + fresnel + emissão HDR + fog próprio (distância e profundidade). |
| `Rail/Aurora/Veil` | Cortina aditiva com raios animados (u ao longo, v na altura). |
| `Rail/Aurora/Sky` | Skybox gradiente. |
| `Rail/Aurora/Additive` | Rastro do player e partículas (faixa / radial / losango). |

O fog é global, publicado pelo `AuroraEnvironment` (`_AuroraFogColor`, `_AuroraFogParams`).

## Luz, bloom e post

- Volume: `Assets/Graphics/Aurora/Config/Aurora_VolumeProfile.asset` — Bloom (threshold 0.95,
  intensidade 1.3, scatter 0.68), **sem tonemapping** (o Neutral escurecia os tons médios e
  engolia o bloom; a paleta é LDR e as emissões viram núcleo + halo), Color Adjustments leve, Vignette.
- Bloom seletivo por regra de emissão: só passa do threshold o que deve chamar atenção
  (conector, crescente, coin, anel, moldura). Contorno do trilho fica abaixo de 1 de propósito.
- Câmera: `renderPostProcessing` ligado (antes estava desligado). URP assets: HDR on, MSAA 4x.
- A "luz" do shader Surface é de arte: `AuroraEnvironment.lightEuler` publica `_AuroraLightDir`
  (independe das luzes da cena). Sem sombras.
- Cores dos materiais são gravadas em gamma (`Lin()` no wizard): o Unity lineariza propriedades
  Color na GPU, então passar valor já linear escurecia tudo (bug corrigido).

## Animação e VFX (runtime, `Scripts/RailSwitchMVP/Visual`)

| Script | O que faz |
|---|---|
| `AuroraEnvironment` | Fog global; véus acompanham a câmera; arcos ficam no mundo e são reciclados pra frente (passam pela tela = sensação de velocidade). |
| `AuroraMotion` | Bob / giro / pulso / wobble / billboard procedurais. |
| `AuroraGroundSnap` | Assenta o visual na pista (hazards no Start; visual do player todo frame, porque a altura do root do Player muda em jogo). |
| `AuroraFX` | Burst de coleta (coin, gem, power-up) com pool, via eventos estáticos `Collected` adicionados em `CollectibleCoin`, `GemPickup` e `PowerUpBase`. |
| `TrailRenderer` no player | Rastro curto (0.16s) ciano. |

Nada disso altera tempo, câmera, velocidade ou regras.

## Juice (v2)

| Efeito | Onde |
|---|---|
| Bloom forte (threshold 1.0, intensidade 2.1) + aberração cromática sutil | `Aurora_VolumeProfile` |
| Pulso de tela claro (power-up, gem, tier, revive; leve em coin) | Volume `PulseFlash` (peso animado) |
| Pulso de impacto (CA, vinheta vermelha, lens distortion) em shake/morte | Volume `PulseHit` |
| Poça de luz sob o player e onda de choque nos trilhos a cada pouso/evento | shader Surface (`_PlayerGlow`) + `AuroraJuice` |
| Trilhos se montam subindo do vazio ao longe e afundam atrás (só visual) | `AuroraTileJuice` |
| Flash no conector ao trocar direção | `AuroraSwitchJuice` (`_Flash` via MPB) |
| Squash no pouso, stretch na lacuna, inclinação lateral, faíscas | `AuroraPlayerJuice` |
| Bolha de escudo / ghost, anel de ímã, crescente dourado no 2x | `AuroraPlayerJuice` + shader `Rail/Aurora/Fresnel` |
| Explosões maiores + anel de choque; estilhaço na morte | `AuroraFX` |
| Partículas de velocidade (densidade pela velocidade) | `AuroraJuice` |
| Aurora intensifica a cada tier | `AuroraJuice` (MPB nos véus) |

Ganchos usados: `OnTileEntered`, `OnGameOver`, `OnRevived`, `OnTierChanged`, `OnPowerUpActivated`,
`SwitchController.OnStateChanged`, eventos estáticos `Collected` e `PlayerCameraRig.ShakeRequested`
(adicionado, só notifica). Todos os valores ficam serializados nos componentes pra tunar.

## Notas

- Cor do personagem (`PlayerCharacterApplier`) agora tinge o **crescente**; a pérola fica sempre
  clara pra ler sobre o trilho teal.
- Spawners instanciam com `Quaternion.identity` — a rotação salva no root do coin/gem é ignorada
  em jogo; o visual só compensa a escala.
- Pra controlar o Editor pela CLI foi adicionado `com.unity.pipeline` (+ `com.unity.nuget.newtonsoft-json`,
  dependência que o pacote não declara). Pode remover do manifest se não for usar.
- Previews do wizard: `Docs/ArtDirection/2026-10-15-aurora-unity/`.

## Pendente / próximos passos

- Validação no aparelho (iPhone 14 Pro Max / Galaxy A55): fps com bloom + MSAA 4x.
- VFX de morte, revive, ativação de power-up (shield ativo, magnet etc.) e troca de tier.
- HomeScene ainda no visual antigo.
- Ajuste fino de cores e tamanhos após jogar.
