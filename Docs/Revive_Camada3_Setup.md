# Camada 3 — Continue após Morte (Revive): Setup no Unity

Guia de montagem da UI + wiring da Camada 3. O **código já está pronto**; aqui é só a
parte do editor (asset, cena, overlay) pra você validar.

Fluxo implementado: player morre → se ainda tem continue, o jogo **pausa** e aparece um
overlay (Ad / Coins / countdown) → ao continuar, reaparece num trilho do critical path
recuando ~15m, com invencibilidade por ~1.5s → se recusar/expirar, vai pro Game Over normal.

---

## Pré-requisitos (já no projeto, não precisa criar)
- `ReviveConfig.cs` (SO), `ReviveController.cs` — novos.
- `GameManager` (ConfirmGameOver), `LethalObstacle`, `PlayerRailRider` (RespawnAt) — já editados.
- `AdsManager` na cena (pro botão de Ad). Já existe.

---

## Passo 1 — Criar o asset ReviveConfig

1. Project → pasta `Assets/ScriptableObjects/RailSwitchMVP/` (ou onde ficam os outros configs).
2. **Create → RailSwitchMVP → Revive Config**. Nomeie `ReviveConfig_Default`.
3. Valores sugeridos (já são os defaults):

| Campo | Valor |
|---|---|
| Max Continues Per Run | 2 |
| Continue Cost 1 | 50 |
| Continue Cost 2 | 150 |
| Allow Ad For Continue | ✓ |
| Revive Setback Distance | 15 |
| Revive Grace Seconds | 1.5 |
| Offer Countdown Seconds | 5 |

---

## Passo 2 — Montar o overlay de continue (UI)

Na **GameScene** (`RailSwitchMVP.unity`), no Canvas que já tem o HUD/GameOver (ou crie um
Canvas novo `Screen Space - Overlay`). Monte esta hierarquia:

```
Canvas
└── ContinueOverlay            (GameObject vazio com Image de fundo) ← overlayPanel
    ├── Background             (Image, cor preta alpha ~0.6, stretch full screen)
    ├── Title                  (TMP_Text: "CONTINUAR?")
    ├── Countdown              (TMP_Text: "5")            ← countdownText
    ├── AdButton               (Button)                   ← adButton
    │   └── Text (TMP)         "Continuar (Ad)"           ← adButtonText
    ├── CoinsButton            (Button)                   ← coinsButton
    │   └── Text (TMP)         "Continuar (50 coins)"     ← coinsButtonText
    └── DeclineButton          (Button: "Desistir")       ← declineButton
```

Notas:
- **Deixe `ContinueOverlay` INATIVO** no editor (uncheck no topo do Inspector). O
  `ReviveController.Start` também desativa, mas começar inativo evita flash no load.
- Os textos dos botões (`adButtonText`/`coinsButtonText`) são **sobrescritos em runtime**
  (o custo entra no texto das coins). O que você digita é só placeholder.
- Garanta que existe um **EventSystem** na cena (geralmente já tem por causa dos outros
  botões). Sem ele, nenhum botão clica.
- O overlay aparece com o jogo **pausado** (`Time.timeScale = 0`) — por isso os botões e o
  countdown usam tempo real (unscaled). Não precisa configurar nada pra isso.

---

## Passo 3 — Adicionar o ReviveController e wirear

1. Na GameScene, crie um GameObject vazio `_ReviveController` (ou use um manager existente).
2. Add Component → **Revive Controller**.
3. Preencha as refs:

| Campo | Arraste |
|---|---|
| Config | o asset `ReviveConfig_Default` (Passo 1) |
| Rail Config | o `RailGenConfig` que os outros componentes usam (mesmo asset) |
| Player | o GameObject do player (PlayerRailRider) |
| Overlay Panel | `ContinueOverlay` |
| Ad Button / Ad Button Text | `AdButton` / o TMP dele |
| Coins Button / Coins Button Text | `CoinsButton` / o TMP dele |
| Decline Button | `DeclineButton` |
| Countdown Text | `Countdown` |

> Refs de UI são **opcionais** no código (null = ignora). Mas sem o `overlayPanel` e os
> botões, não há como o jogador escolher — então monte todos pra validar.

---

## Passo 4 — O botão de Ad (IMPORTANTE)

O botão de Ad usa o `AdsManager`. Hoje os **ads reais estão bloqueados** (backend sem
inventory — ver `Docs/Ads_BackendIssue.md`). Pra o botão funcionar no teste, use o
**Mock Mode**:

1. Selecione o GameObject do **AdsManager** na cena.
2. No Inspector, marque **`Use Mock Ads` = ✓** (header "Mock Mode (DEV ONLY)").
3. Opcional: `Mock Ad Duration` (1.5s) e `Mock Ad Always Succeeds = ✓`.

Com mock ON: o ad fica **sempre pronto**, e ao clicar simula 1.5s e chama o sucesso →
revive acontece. Sem mock e sem backend servindo, `IsRewardedReady` fica `false` e o
**botão de Ad some** (comportamento correto de produção — não dá revive grátis).

Como o overlay se comporta:
- **Botão Ad**: só aparece se `Allow Ad For Continue` está ligado E o ad está pronto
  (ou se não há AdsManager na cena = modo stub, que revive direto).
- **Botão Coins**: fica desabilitado (não clicável) se você não tem coins suficientes pro
  custo do continue atual (50 no 1º, 150 no 2º).
- **Desistir / countdown zera**: confirma o Game Over normal.

> Detalhe: o overlay calcula o estado do botão de Ad no instante em que abre. Em Mock Mode
> o ad está sempre pronto, então não tem problema. (Se um dia usar ads reais e quiser que o
> botão "apareça" caso o ad fique pronto durante o countdown, dá pra fazer o ReviveController
> escutar `AdsManager.OnRewardedReadyChanged` — hoje não escuta, pra manter simples.)

---

## Roteiro de teste

1. Mock Mode ON no AdsManager. Tenha algumas coins (use o debug de coins se precisar).
2. Jogue e **morra** (dead-end, out-of-bounds ou bater num obstáculo letal).
3. O jogo deve **pausar** e o overlay aparecer com o countdown contando.
4. **Continuar com Coins**: confirma que debitou 50, o player reaparece num trilho válido
   (critical path), e fica **invencível ~1.5s** (passe por um obstáculo logo após — não morre).
5. **Continuar com Ad**: clica, espera ~1.5s (mock), revive igual.
6. Morra de novo: agora o custo de coins deve ser **150** (2º continue).
7. Morra a **3ª vez**: não deve mais oferecer continue → vai direto pro Game Over.
8. **Desistir** / deixar o countdown zerar: vai pro Game Over normal (com a death cam).

---

## Pontos de atenção (decisões abertas)

1. **Score vs revive:** a distância salva usa a posição do player **no momento da morte
   final**. Como o revive recua ~15m, se você morrer logo após sem voltar a passar do ponto
   anterior, a distância pode ficar menor que o pico alcançado. Se incomodar na validação,
   a gente passa a rastrear o "máximo alcançado". Avise.
2. **Câmera no respawn:** a câmera faz lerp suave até a nova posição — pode "viajar" um
   pouco quando reposiciona pra trás. Se ficar estranho, troco pra teleporte instantâneo.

Quando validar, me diga como foi (principalmente os itens 4 e 7 do roteiro) — aí ajustamos
o que precisar e commitamos a Camada 3.
