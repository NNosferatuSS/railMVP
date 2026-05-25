# Fatia 10 — Tutorial Overlay Setup

> **Pré-requisitos:** nenhum especial. É uma fatia independente que só
> adiciona um overlay UI na cena Game (RailSwitchMVP.unity).
>
> **Códigos prontos:** `TutorialOverlayController.cs` (lê PlayerPrefs
> `RailMVP.Tutorial.SeenV1`, pausa via Time.timeScale=0, texto adaptado
> a mobile/desktop), `DebugPanelController` ganhou seção Game state com
> botão "Reset Tutorial".

**Tempo estimado:** 10-15min de setup UI.

---

## Conceito

Primeira run do jogador → overlay sobreposto na cena Game antes do warmup
começar. Mostra instruções de controle adaptadas à plataforma (toque para
mobile, setas para desktop). Pausa via `Time.timeScale = 0`. Dismiss salva
flag em PlayerPrefs — nunca aparece de novo (até reset via DebugPanel).

Versionamento: a key é `RailMVP.Tutorial.SeenV1`. Se um dia o tutorial
mudar materialmente (ex: adicionar explicação de power-ups), bump pra V2
e usuários antigos veem novamente.

---

## Passo 1 — Criar overlay no HUD Canvas da cena Game

1. **File → Open Scene** → `Assets/Scenes/RailSwitchMVP.unity` (a Game scene).
2. Hierarchy → identificar o **HUD Canvas** (o que tem o coinsText, GameOver panel, etc).
3. Right-click no Canvas → **UI → Panel** → renomear `TutorialOverlay`.

### 1.1 — Backdrop

1. Rect Transform do `TutorialOverlay`: anchor stretch/stretch. Left/Top/Right/Bottom `0`.
2. Image: cor `#000000` alpha `220` (escurecimento forte — usuário precisa parar pra ler).
3. **Importante:** deixar o GameObject **ATIVO** (NÃO desativar a checkbox). O controller decide visibilidade em Start() baseado nas Prefs. Se você desativar manualmente, o componente nunca Awakes.

### 1.2 — Título

1. Filho de `TutorialOverlay` → **UI → Text - TextMeshPro** → renomear `TutorialTitle`.
2. Rect Transform: anchor top-center. Pos Y `-200`. Width `800`. Height `100`.
3. Text: `BEM-VINDO!`. Font Size: `64`. Bold. Alignment Center. Color branco.

### 1.3 — Body text (instruções)

1. Filho → **UI → Text - TextMeshPro** → renomear `TutorialBody`.
2. Rect Transform: anchor middle-center. Pos `0,0`. Width `800`. Height `400`.
3. Text: deixa placeholder qualquer — controller sobrescreve em runtime baseado em plataforma.
4. Font Size: `36`. Alignment Center. Color branco. Word Wrap ON.

### 1.4 — Got It button

1. Filho → **UI → Button - TextMeshPro** → renomear `GotItButton`.
2. Rect Transform: anchor bottom-center. Pos Y `200`. Width `400`. Height `120`.
3. Image: cor primária do app (ex: verde `#4FC97A` ou azul `#4F7BC9`).
4. Child Text: `ENTENDI`. Font Size: `48`. Bold. Color branco.

### 1.5 — Component `TutorialOverlayController`

1. Selecionar `TutorialOverlay` root.
2. **Add Component** → `TutorialOverlayController` (`RailSwitchMVP.UI.TutorialOverlayController`).
3. Drag refs:
   - **Overlay Panel** ← `TutorialOverlay` (self).
   - **Body Text** ← `TutorialBody`.
   - **Got It Button** ← `GotItButton`.
   - **Mobile Text** / **Desktop Text**: deixar nos defaults (pode customizar se quiser).

**Como saber se deu certo:**
- Hierarchy mostra `TutorialOverlay` ativo no HUD Canvas, com 3 filhos.
- Inspector do TutorialOverlay tem todas as refs populadas.

---

## Passo 2 — Validar no Editor

### 2.1 — Primeira run (overlay aparece)

1. Garante que tu não jogou ainda hoje OU faz reset:
   - Em Play (qualquer cena), F1 → seção "Game state" → **Reset Tutorial**.
   - Stop o Play.
2. Volta pra HomeScene → **Play** → click **JOGAR**.
3. Game scene carrega. **Overlay aparece** imediatamente sobre o jogo.
4. Texto mostra: `BEM-VINDO! Use as setas ← → (ou A/D) pra trocar de trilho...` (desktop mode no Editor).
5. **Pause confirmado:** countdown não roda, player não anda, world está congelado.
6. Click **ENTENDI**.
7. Console:
   ```
   [Tutorial] Dismissed. Prefs saved.
   ```
8. Overlay some, `Time.timeScale` volta a 1, player começa a andar normal, warmup 3-2-1-GO acontece.

### 2.2 — Segunda run (overlay NÃO aparece)

9. Morre/Restart → Game scene recarrega.
10. **Sem overlay**. Warmup começa direto.

### 2.3 — Reset + re-aparece

11. F1 → Game state → **Reset Tutorial**.
12. Console: `[Tutorial] DEBUG: SeenV1 flag cleared.`.
13. GameOver → Restart → Game scene recarrega.
14. **Overlay aparece novamente** (flag foi resetada).

### 2.4 — Build Android (mobile text)

15. Build Android, instala, joga.
16. **Texto agora mostra:** `Toque na ESQUERDA ou DIREITA da tela pra trocar de trilho...` (mobile detectado).
17. Logo após dismissal, no `adb logcat`:
    ```
    [Tutorial] Dismissed. Prefs saved.
    ```

---

## Critérios de validação

- [ ] TutorialOverlay no HUD Canvas da cena RailSwitchMVP, GameObject ATIVO no Inspector.
- [ ] Component TutorialOverlayController com todas as refs populadas.
- [ ] Primeira run → overlay aparece + game congelado.
- [ ] Click ENTENDI → game continua normal, overlay some.
- [ ] Segunda run → sem overlay.
- [ ] DebugPanel "Reset Tutorial" → próxima run mostra overlay novamente.
- [ ] Texto correto por plataforma (desktop no Editor, mobile no Android build).
- [ ] `Time.timeScale` corretamente restaurado (warmup roda).

---

## Edge cases conhecidos

### 1. User mata o app durante overlay
Time.timeScale fica em 0 no PlayerPrefs? Não — timeScale é runtime, não persistido.
Próxima abertura do app: Game scene não está aberta (Home é primeira), tutorial está
intacto, dismissal funciona normal.

### 2. User abre Profile/Leaderboard durante warmup pós-tutorial
Esses panels têm o próprio sistema de pausa (`Time.timeScale = 0` via outros
caminhos? Não — eles usam SetActive). Esses panels NÃO pausam o jogo —
overlay são UI canvas overrides. Player continua andando. Aceitável.

### 3. Tutorial mostrado em runs offline
Se o user joga sem internet, tutorial funciona normalmente (puro client-side,
PlayerPrefs local). Não depende de Auth/Sync.

### 4. Resetar PDM (Wipe All) não reseta tutorial
PDM.WipeAll só apaga keys de `RailMVP.Coins`, etc. A key do tutorial
(`RailMVP.Tutorial.SeenV1`) fica intacta. Pra reset completo, usar o
botão "Reset Tutorial" do DebugPanel ou estender PDM.WipeAll pra incluir.
Pra MVP, separation OK — você pode querer wipe data sem repetir tutorial.

---

## Próximas fatias possíveis

- Tutorial multi-step (próxima slide com obstáculos, power-ups).
- Onboarding na Home (modal "Bem-vindo, [nome]" no 1º login).
- Pre-launch checklist (keystore + signed APK + privacy policy).
