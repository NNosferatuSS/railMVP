# Camera Polish — Setup + Testes

> Mudanças implementadas em 2026-05-22. Pra testar na segunda 2026-05-25.

## O que mudou

1. **Tilt máximo subiu pra 90°** — `RailGenConfig.cs:47` agora `[Range(0f, 90f)]`.
2. **Smoothing de posição** — base position interpolada via Lerp;
   tunável em `cameraPositionSmoothing` (default 12 = quase instantâneo,
   reduzir pra mais cinematic).
3. **Camera Shake API** — `PlayerCameraRig.Instance.Shake(intensity, duration)`
   + presets `ShakeLight/Medium/Heavy`. Perlin noise com falloff linear.
4. **Death Cam** — em `OnGameOver`, slow-mo (`Time.timeScale = 0.3`) por
   1s + zoom-in (subtrai `deathCamZoomDelta` do distance, soma
   `deathCamTiltDelta` no tilt) com SmoothStep. Painel só aparece após.
5. **Shake automático em tier change** — `PlayerCameraRig` se inscreve
   em `DifficultyManager.OnTierChanged` e dispara `ShakeLight()`
   (skip do primeiro init).
6. **Shake automático em VortexObstacle e LaneSwapObstacle** —
   `ShakeMedium()` no `OnPlayerHit` desses.
7. **Shake automático em HitObstacle** — `PlayerCameraRig` se inscreve
   em `GameManager.OnGameOver` e dispara `ShakeHeavy()` quando reason
   == `HitObstacle`.

## Setup no Editor (opcional)

### Único passo obrigatório-ish

`GameOverController` ganhou um campo novo `Rail Config`. Se você quer
controle explícito, atribua `RailGenConfig_Default.asset` lá. Senão,
no `Start` ele tenta resolver via reflection a partir do
`PlayerCameraRig.Instance` (que já tem a ref serializada) — funciona
mas é gambiarra. Atribuir explicitamente é mais limpo.

1. Abre `RailSwitchMVP.unity`.
2. Seleciona `_GameOver` (ou o GameObject com `GameOverController`).
3. Slot novo **Rail Config** → arrasta
   `Assets/ScriptableObjects/RailSwitchMVP/RailGenConfig_Default.asset`.
4. Salve a cena.

### Tunables novos disponíveis

Abra `RailGenConfig_Default.asset` no Inspector — todos esses são
editáveis em runtime via Inspector durante Play (ScriptableObject):

- `Camera Tilt` — agora 0..90° (era 0..60°).
- `Camera Position Smoothing` — 0..30. **0 = teleporta (sem smoothing)**.
  Default 12. Reduzir pra ver câmera mais cinematic.
- `Shake Light/Medium/Heavy Intensity` + `Duration`.
- `Death Cam Duration / Slow Mo / Zoom Delta / Tilt Delta`.

## Critérios de teste (segunda)

- [ ] **1.** Câmera mais "smooth" durante lane switches (não teleporta
   no instante do input).
- [ ] **2.** Tier mudou → tela treme leve (`ShakeLight`). Tier 0 inicial
   NÃO treme.
- [ ] **3.** Hit Vortex → tela treme médio + switch redireciona.
- [ ] **4.** Hit LaneSwap → tela treme médio + debuff aplica.
- [ ] **5.** Hit Lethal Obstacle → shake forte + slow-mo 1s + câmera
   se aproxima e inclina mais + painel aparece DEPOIS do segundo.
- [ ] **6.** DeadEnd ou OutOfBounds (sem obstáculo) → death cam roda
   sem shake (só slow-mo + zoom).
- [ ] **7.** Tilt no Inspector vai de 0 até 90°. Mexer durante Play
   altera a câmera em tempo real (porque é ScriptableObject — cuidado,
   persiste após sair do Play!).

## Tuning recommendations

- **Smoothing 12** é responsivo. Pra MAIS cinematic, tente 6-8. Pra
  competição/responsivo, 18-20.
- **Shake Heavy** default `0.6` intensity. Pode ser muito; se incomodar,
  tente `0.4`.
- **Death Cam Duration 1.0s** é o sweet spot pra impacto sem virar lento.
  Subir pra 1.5s pra mais cinematic, descer pra 0.7s pra ação mais rápida.
- **Death Cam SlowMo 0.3** — bem dramatic. Subir pra 0.5 deixa menos
  óbvio. Descer pra 0.15 é Matrix.

## Troubleshooting

- **Painel do Game Over não aparece** → confere que a coroutine
  `DeathSequence` rodou. Console deve ter logs do GameManager normal,
  mas se trava antes do `Time.timeScale = 0`, é bug.
- **Death cam não roda visualmente** → falta o `PlayerCameraRig` na cena
  (ou seu Instance não está setado). Verifica no Console se aparece
  warning "Multiple instances".
- **Shake quebra o smoothing** → não deveria — shake é offset somado
  DEPOIS do Lerp da base position. Se notar drift, abre issue.
- **Tier change shake dispara no spawn inicial** → o `_seenInitialTier`
  flag deveria pular isso. Se não pulou, é race condition no Awake
  order — mover a inscrição pra Start (já está em Start).
