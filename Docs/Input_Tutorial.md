# Input inteligente + Tutorial V2

## Input — 3 formas ao mesmo tempo (`Input/SmartTouchInput.cs`)

Prioridade por toque:

1. **Toque no trilho**:
   - **Trilho da próxima linha** (ou ao lado): o conector do trilho em que o player está aponta pra ele.
   - **Trilho a 2+ linhas à frente** (até 5): o trilho da **sua rota** (conectores como estão)
     na linha logo antes dele passa a apontar pra ele — **um conector só**. Se ele está a mais de
     1 lane da rota, nada muda (moldura vermelha tremendo + tique grave). Comando `RouteTile` →
     `PlayerRailRider.ConnectAhead` → `SwitchController.SelectRemote`. Não conta como troca do
     trilho do player (não dispara `AnyNudged`/clutch). Mudar uma rota inteira fica pra um
     possível power-up.
   - **Feedback** (`UI/TapFeedbackUI.cs`, criado pelo SmartTouchInput): moldura dourada no trilho
     tocado, losango ciano no conector que trocou e uma linha ligando os dois (0,6 s).
   Mira assistida: o trilho mais próximo do dedo num raio de 9% da largura da tela, com desempate
   a favor da linha mais próxima. (Caso do playtest: tocar na pontinha agora funciona.)
2. **Tap na metade da tela**: se o toque não caiu perto de nenhum trilho (comportamento de antes).
3. **Swipe ← →**: se o dedo deslizar, o swipe **sobrescreve** o que o toque fez:
   `estado = estado no momento do toque ± 1`. Reconhecido ainda durante o gesto, sem esperar soltar.

Teclado (←/→/A/D) segue igual. Toques sobre UI (botão do Fever) não trocam o conector.
No Editor o mouse funciona como toque.

### Assistências (`Player/PlayerRailRider.cs`)
- **Buffer**: input feito durante a lacuna fica guardado por 0,35 s e vale pro próximo trilho
  (antes: teclado era perdido e toque ficava numa fila sem prazo).
- **Tolerância**: nos primeiros 0,12 s da lacuna, um input ainda corrige o destino a partir do
  trilho que acabou, e pode salvar de um dead end por toque atrasado.
- `SwitchController.Select(state)`: seleção absoluta por input (mesmos eventos de um Nudge).

## Tutorial V2 (`Gameplay/TutorialDirector.cs`)

Ensina dentro da corrida, sem pausar. Primeira run (`RailMVP.Tutorial.SeenV2`):

1. "TOQUE NA ESQUERDA OU NA DIREITA" (metades pulsando) — começa já no warmup.
2. "TAP THE TRACK YOU WANT" (moldura dourada num trilho alcançável da próxima linha).
3. "TAP A TRACK FURTHER AHEAD" / "the track before it connects to it" — moldura num trilho 2 linhas
   à frente que a rota ainda não usa. Avança quando um toque distante conecta (ou timeout).

**Ninguém perde no tutorial** (do início até 5 trilhos depois do "NICE!" final):
`RunModifiers.TutorialProtection` faz o PlayerRailRider virar o conector pra um trilho vizinho que
existe em vez de cair (com o aviso "WHOA! RED = FALL"), o GameManager ignora batida em obstáculo e o
gerador não cria hazards. A rede de segurança do tutorial também corrige antes, a 86% do trilho.
4. "OR SWIPE ← →".

Chave atual: `RailMVP.Tutorial.SeenV4` (bump pra quem já viu o V3). Botão REPLAY TUTORIAL no Pause.

Durante o tutorial: velocidade ×0,7 (`RunModifiers.AssistSpeedMultiplier`) e **rede de segurança**:
conector apontando pro vazio no fim do trilho é corrigido, com o aviso "VERMELHO = QUEDA" (depois a
instrução do passo volta). Dicas avulsas, uma vez cada: conector vermelho, Fever pronto, Ghost.
O painel V1 (que pausava o jogo) foi desativado.

Rever o tutorial: `TutorialDirector.ResetTutorial()` (apaga as chaves de PlayerPrefs).

## Ghost — feedback
Pérola tremulando e violeta, bolha violeta, rastro violeta e, enquanto atravessa o vazio, uma
**ponte fantasma** (rastro largo embaixo) + popup "FANTASMA!" + som. Evento
`PlayerRailRider.GhostFlightStarted`, estado `IsGhostFlying`.

## Fundo
Véus de aurora 80% mais discretos (intensidade 0,75→0,15 e 0,6→0,12 nos materiais).
