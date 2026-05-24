# Setup — Mobile Input (Touch + UI buttons + Android debug)

> Commit `<próximo>`. Adicionou input touch (swipe), botões UI pra Active Item/Teleport
> e botões UI pra toggle F1/F2 em Android. **Keyboard continua intacto** — Editor
> testa exatamente como antes.

## Componentes novos

| Script | Função |
|---|---|
| `Input/TouchDirectionalInput.cs` | **TapZones (default):** tap em qualquer ponto da metade esq/dir = switch. Tem modo Swipe alternativo. |
| `Input/CompositeDirectionalInput.cs` | Agrega N IDirectionalInput. PlayerRailRider aponta pra ele. |
| `UI/MobileTouchUI.cs` | Botões on-screen: USE (Active Item) + TELE ←/→. Auto-hide via eventos. |
| `Core/MobileDebugButtons.cs` | 2 botões pra toggle F1 (DebugPanel) e F2 (SpawnOverride). |

Também adicionado: `DebugPanelController.Toggle()` e `SpawnOverrideController.Toggle()` públicos.

---

## Setup na cena RailSwitchMVP

### 1. Composite input

1. Achar o GameObject onde está hoje o `KeyboardDirectionalInput` (ou criar um `_Input` GO se preferir).
2. Adicionar componente **TouchDirectionalInput** no mesmo GO.
3. Adicionar componente **CompositeDirectionalInput** no mesmo GO.
4. No `CompositeDirectionalInput`, no array `Sources`, arrastar (nessa ordem):
   - Slot 0: `TouchDirectionalInput` (mobile primeiro — silencioso em desktop)
   - Slot 1: `KeyboardDirectionalInput`
5. No **PlayerRailRider** (Player GO), trocar o slot `Input Source` de `KeyboardDirectionalInput` → `CompositeDirectionalInput`.

Sanity check: em Editor, setas devem continuar funcionando exatamente como antes (Composite cai no Keyboard porque Touch retorna 0).

### 2. Touch UI buttons (HUD canvas)

No `_HUD_Canvas` (Screen Space Overlay 1920×1080):

1. Criar parent `MobileTouchUI_Group` (RectTransform, anchor bottom-stretch).
2. Filho **USE Button**:
   - UGUI Button + TMP_Text filho "USE".
   - Anchor bottom-center, ~200×150px, deslocado um pouco pra cima da borda.
3. Filho **Teleport Group** (parent vazio com `HorizontalLayoutGroup` opcional):
   - **Teleport Left Button**: TMP_Text "← TELE", anchor bottom-left.
   - **Teleport Right Button**: TMP_Text "TELE →", anchor bottom-right.
4. Adicionar componente **MobileTouchUI** no `MobileTouchUI_Group`:
   - `Use Item Button` → o botão USE.
   - `Use Item Label` (opcional) → TMP_Text dentro do USE (mostra "TimeFreeze").
   - `Teleport Left/Right Button` → os 2 botões.
   - `Teleport Group` → parent dos 2 (esconde ambos juntos).
   - `Hide On Desktop` → **false** (deixe ligado em Editor pra testar com mouse).

Sanity check em Editor: pegar pickup TimeFreeze → botão USE aparece. Clicar → usa. Pegar Teleport → 2 botões aparecem por N tiles.

### 3. Mobile Debug Buttons (Android-only F1/F2 alternative)

Mesmo canvas (ou sub-canvas dedicado):

1. Criar parent `MobileDebug_Group` (anchor top-right ou top-left).
2. 2 botões pequenos (ex: 80×80px):
   - **DBG button** (texto "DBG" ou "F1") → toggle DebugPanelController.
   - **SPW button** (texto "SPW" ou "F2") → toggle SpawnOverrideController.
3. Adicionar **MobileDebugButtons** no `MobileDebug_Group`:
   - `Debug Panel Toggle Button` → DBG.
   - `Spawn Override Toggle Button` → SPW.
   - `Debug Panel` / `Spawn Override` (deixar vazio → auto-resolve no Awake).
   - `Restrict To Debug Builds` → **true** (botões somem em release builds).

Sanity check em Editor: clicar DBG = mesma coisa que F1. Clicar SPW = mesma coisa que F2.

---

## Build Android — passo-a-passo

1. **Unity Hub → Installs → seu Unity 6.3 LTS → Add modules**: marcar `Android Build Support` (com OpenJDK + Android SDK & NDK). Aguardar download.
2. Reabrir o projeto.
3. **File → Build Settings**:
   - Platform: selecionar **Android** → Switch Platform.
   - Marcar **Development Build** ✅ (necessário pra `Debug.isDebugBuild == true`, que ativa F1/F2 e os botões mobile de debug).
   - **Run Device**: conecte um device USB (com USB Debugging ativado nas Developer Options) ou use emulador.
4. **Player Settings**:
   - Company Name / Product Name conforme quiser.
   - Other Settings → **Package Name** algo como `com.artur.railmvp`.
   - Other Settings → **Minimum API Level** ≥ 26 (Android 8.0) recomendado.
   - Other Settings → **Target Architectures**: marcar `ARM64` (obrigatório pra Google Play; ARMv7 opcional).
   - Other Settings → **Scripting Backend**: IL2CPP (necessário com ARM64).
5. **Build And Run** (com device conectado) — gera APK e instala automaticamente.

Primeiro build leva ~5min (IL2CPP compila tudo). Subsequentes ~1-2min.

---

## Debugando no Android — usar F1/F2 panels

### 1. Toggle dos panels

- Em build com **Development Build** + `MobileDebugButtons` na cena: clicar nos 2 botões DBG/SPW na tela.
- Os panels OnGUI renderizam em cima do jogo. Touch funciona neles normalmente.

### 2. Stream de logs do device → seu PC

Com device conectado via USB Debugging:

```powershell
adb logcat -s Unity *:S
```

Filtra só logs do Unity. Os `Debug.Log` do jogo aparecem em tempo real.

Se `adb` não estiver no PATH: ele veio com o Android SDK do Unity, em
`%LOCALAPPDATA%\Unity\Cache\AndroidPlayer\platform-tools\adb.exe` (caminho varia).
Adicionar essa pasta ao PATH ou rodar com path absoluto.

### 3. Profile (opcional)

Com Development Build + **Autoconnect Profiler** ✅ em Build Settings, o Unity Profiler
do Editor recebe stats do device em wifi/USB. Cuidado: profiler adiciona overhead.

### 4. Errors em runtime sem logcat

Como fallback, o Unity Development Build mostra um overlay vermelho com stack trace
quando rola exception. Você consegue ler direto na tela sem precisar do PC.

---

## Critérios de validação

### Editor (mantém todo o fluxo antigo)
- [ ] Setas ←/→ continuam mudando lane normalmente.
- [ ] Space ainda usa Active Item.
- [ ] Shift+←/→ ainda teleporta.
- [ ] F1 abre Debug Panel; F2 abre Spawn Override.
- [ ] (Se `Hide On Desktop = false`) os botões UI aparecem e funcionam com mouse.

### Android device
- [ ] Swipe horizontal → switch lane.
- [ ] Tocar em USE button (quando aparece) → usa item.
- [ ] Tocar em TELE ←/→ (quando aparece) → teleporta.
- [ ] Tocar em DBG → painel F1 abre, é interagível.
- [ ] Tocar em SPW → painel F2 abre, é interagível.
- [ ] Swipe iniciado em cima de um botão NÃO dispara switch (ignored).
- [ ] Logs aparecem em `adb logcat -s Unity`.

---

## Tunables úteis em `TouchDirectionalInput`

**Mode (default `TapZones`):**

| Field | Default | Pra mudar |
|---|---|---|
| `mode` | TapZones | Trocar pra Swipe se quiser gesto em vez de tap. |
| `zoneSplit` | 0.5 | 0.5 = metade exata. 0.55 = direita maior (mais "permissiva"). |
| `deadZoneFraction` | 0 | 0.05 = 5% morta ao redor da divisória (evita tap ambíguo). |

**Swipe (só se `mode = Swipe`):**

| Field | Default | Pra mudar |
|---|---|---|
| `minSwipeFraction` | 0.06 | Aumentar se swipes acidentais; diminuir se swipes não detectam. |
| `minSwipePixels` | 40 | Piso absoluto. Subir se tela ≥ 1080p e ainda detecta micro-swipes. |
| `maxVerticalRatio` | 1.0 | Diminuir (ex: 0.5) pra exigir swipes mais horizontais. |
| `maxSwipeTime` | 0.6s | Diminuir pra forçar swipes rápidos; aumentar pra aceitar swipes lentos. |

### Por que swipe ficou ruim e tap zones é melhor pra esse jogo

Switch é input **binário** (esq/dir), e tap zone divide o espaço **binariamente**.
Não há ambiguidade — ou foi pra esquerda, ou pra direita. Swipe precisa de
distância mínima, velocidade, ângulo — qualquer um falha vira "não funciona".
Em jogos de switch (oposto de Subway Surfers que mistura swipe vertical pra
pulo/agachar), tap é mais direto e responsivo. Mantemos swipe via enum só pra
A/B test, mas o default é TapZones.
