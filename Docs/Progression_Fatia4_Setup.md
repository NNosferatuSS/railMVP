# Fatia 4 — Setup no Unity Editor (passo a passo)

> Códigos prontos: `CharacterCatalog`, `CharacterShopCardUI`,
> `ShopController`, `PlayerCharacterApplier`, `HomeScreenController`
> atualizado, `DebugPanelController` com seção Characters,
> `PlayerDataManager.DebugResetCharacters()`. Falta setup de cena.

**Tempo estimado:** 25–35 min (parte chata é montar 3 cards + popup
de confirma; resto é pluggar refs).

---

## Bloco 1 — ShopPanel na HomeScene

### 1.1 Backdrop sobreposto

1. Abra `HomeScene.unity`.
2. Hierarchy → no Canvas → clique direito → `UI → Panel` → renomeie
   `ShopPanel`.
3. Rect Transform: anchor `stretch+stretch` (Alt). Left/Top/Right/Bottom `0`.
4. Image: cor `#000000` alpha `200` (escurecimento mais forte que o
   LoginPopup pra distinguir).
5. Mantenha o GameObject **inativo** por padrão (desmarque a check no
   topo do Inspector). ShopController.Open() ativa.

### 1.2 Header

1. Filho do `ShopPanel` → `UI → Text - TextMeshPro` → `ShopTitle`.
   - Anchor: top-center. Pos Y `-100`. Width `600`. Height `80`.
   - Text: `Loja de Personagens`. Font Size: `48`. Bold. Alignment Center.

2. Filho do `ShopPanel` → `UI → Button - TextMeshPro` → `CloseButton`.
   - Anchor: top-right. Pos X `-50`, Pos Y `-50`. Width `80`, Height `80`.
   - Child Text: `X`. Font Size: `36`.

### 1.3 Container dos 3 cards

1. Filho do `ShopPanel` → `UI → Panel` → `CardsContainer`.
   - Anchor: middle center. Pos `0,0`. Width `1500`, Height `500`.
   - Image: alpha 0 (transparente).
   - Add Component → `Horizontal Layout Group`:
     - Padding 24 all. Spacing 32. Child Alignment Middle Center.
     - Control Child Size: Width ☑, Height ☑.
     - Child Force Expand: Width ☑, Height ☑.

---

## Bloco 2 — Criar 1 card e duplicar

### 2.1 Estrutura do card

1. Filho do `CardsContainer` → `UI → Panel` → `CharacterCard_Template`.
   - Width auto (vai ser dado pelo HorizontalLayout). Height `500`.
   - Image: cor `#1A1A2E` alpha `255`.
   - Add Component → `Vertical Layout Group`:
     - Padding 16. Spacing 12. Child Alignment Upper Center.
     - Control Child Size: Width ☑, Height ☐.
     - Child Force Expand: Width ☑, Height ☐.
   - Add Component → `Character Shop Card UI` (script novo).

### 2.2 Filhos do card

**1) NameText** (`UI → Text - TextMeshPro`):
- Renomeie `NameText`. Text: `Nome`. Font Size: `32`. Bold. Alignment Center.

**2) PreviewImage** (`UI → Image`):
- Renomeie `PreviewImage`. Layout Element → Preferred Height `200`.
- Source Image: pode deixar default (a cor sobrescreve no Bind).
- Color inicial: branco (placeholder).

**3) StatusText** (`UI → Text - TextMeshPro`):
- Renomeie `StatusText`. Text: `Status`. Font Size: `22`. Alignment Center.

**4) ActionButton** (`UI → Button - TextMeshPro`):
- Renomeie `ActionButton`. Layout Element → Preferred Height `64`.
- Filho `Text (TMP)` → renomeie `ActionText`. Font Size: `22`.

### 2.3 Wire refs do CharacterShopCardUI

Selecione o `CharacterCard_Template`. No componente:
- **Name Text** → `NameText`.
- **Status Text** → `StatusText`.
- **Preview Image** → `PreviewImage`.
- **Action Button** → `ActionButton`.
- **Action Text** → `ActionText` (filho do botão).

### 2.4 Duplicar pra 3 cards

1. Ctrl+D no `CharacterCard_Template` 2 vezes → vai ter `CharacterCard_Template (1)` e `(2)`.
2. Renomeie pra `Card0`, `Card1`, `Card2`.
3. (Opcional) Vire o `CharacterCard_Template` num prefab arrastando pro
   `Assets/Prefabs/UI/`. Útil pra futuro.

---

## Bloco 3 — Confirm popup

### 3.1 Painel de confirmação

1. Filho do `ShopPanel` → `UI → Panel` → `ConfirmPanel`.
2. Rect Transform: anchor stretch+stretch (Alt). Left/Top/Right/Bottom `0`.
3. Image: cor `#000000` alpha `220` (mais opaco que o ShopPanel pra
   sobrepor visualmente).
4. Mantenha **inativo** por padrão.

### 3.2 Card de confirma

1. Filho do `ConfirmPanel` → `UI → Panel` → `ConfirmCard`.
2. Anchor middle center. Pos `0,0`. Width `700`, Height `300`.
3. Vertical Layout Group: padding 24, spacing 16, Center alignment.

### 3.3 Filhos do ConfirmCard

**1) ConfirmText** (`UI → Text - TextMeshPro`):
- Text: `Comprar X por Y coins?` (placeholder).
- Font Size: `28`. Alignment Center. Wrap: enabled.

**2) Row de botões** (`UI → Panel` → `ButtonRow`):
- Layout Element → Preferred Height `80`.
- Horizontal Layout Group: spacing 24, Middle Center, expand width.
- Filhos:
  - `ConfirmYesButton` (UI → Button-TMP). Text `Sim`. Font Size `24`.
  - `ConfirmNoButton` (UI → Button-TMP). Text `Não`. Font Size `24`.

---

## Bloco 4 — Criar `_Shop` GameObject + wire ShopController

1. Hierarchy → clique direito → `Create Empty` → `_Shop`.
2. Add Component → `Shop Controller`.
3. Refs:
   - **Shop Panel** → `ShopPanel` (root).
   - **Cards** (size 3) → arraste `Card0`, `Card1`, `Card2`.
   - **Close Button** → `CloseButton`.
   - **Confirm Panel** → `ConfirmPanel`.
   - **Confirm Text** → `ConfirmText`.
   - **Confirm Yes Button** → `ConfirmYesButton`.
   - **Confirm No Button** → `ConfirmNoButton`.

---

## Bloco 5 — Habilitar botão Loja no `_Home`

1. Selecione `_Home`.
2. `HomeScreenController` agora tem 2 slots novos:
   - **Shop Button** → arraste o `ShopButton` que já existia na Home
     (era stub).
   - **Shop Controller** → arraste `_Shop` (o GameObject criado no Bloco 4).
3. Salve a HomeScene.

---

## Bloco 6 — Player aplica cor na GameScene

1. Abra `RailSwitchMVP.unity`.
2. Hierarchy → encontre o GameObject do Player (que tem o
   `PlayerRailRider`).
3. Add Component → `Player Character Applier`.
4. Slot `Target Renderer` → arraste o Renderer do player (geralmente
   o MeshRenderer no próprio GameObject ou num filho).
   - Se deixar vazio, o script faz `GetComponentInChildren<Renderer>()`
     no Start. Funciona, mas atribuir é mais explícito.
5. Salve a cena.

> **Cor do player:** o material atual provavelmente tem uma cor base.
> A aplicação por `MaterialPropertyBlock` sobrescreve essa cor em
> runtime sem criar instância nova de material — preserva batching.

---

## Bloco 7 — Validar (6 critérios)

### Preparação:
- F1 (DebugPanel) → seção Player Data → `Reset All Player Data`
  (zera coins + chars). Você vai precisar de coins pra testar compras.
- Joga uma run, completa missões + login + chest pra acumular alguns
  milhares de coins. Ou use o debug `+100 coins` várias vezes pra
  encurtar.

### Critérios:

- [ ] **1.** Na Home, clica o botão `Loja` → ShopPanel abre. 3 cards:
   - **Runner** (Card0): Status "Equipado", botão "Equipado" disabled.
   - **Neon** (Card1): Status "2500 coins", botão "Comprar" se você
     tem ≥2500, ou "Insuficiente" disabled.
   - **Ember** (Card2): Status "5000 coins", idem.
   - Cor do preview de cada card bate com a do personagem (branco, azul,
     laranja).
- [ ] **2.** Tenta comprar com coins insuficientes → botão disabled, sem
   clique possível.
- [ ] **3.** Com coins suficientes → clica `Comprar` → ConfirmPanel
   sobrepõe → "Comprar Neon por 2500 coins?". Clica `Sim` → coins debitam
   2500, Neon vira equipado, card de Neon mostra "Equipado", Runner
   volta a mostrar "Equipar".
- [ ] **4.** Fecha shop (X), clica `JOGAR` → roda o jogo → cor do player
   bate com o personagem equipado (azul pra Neon, laranja pra Ember).
- [ ] **5.** Volta pra Home, troca pra outro personagem owned → joga →
   cor muda na próxima run.
- [ ] **6.** Fecha Play e reabre → owned chars + equipped persistem.

### Debug rápido:
- F1 → seção `Characters` → `Unlock All` desbloqueia os 3 sem custo.
- `Equip Neon/Ember/Runner` força o equipado.
- `Reset Chars` volta a ter só Runner.

---

## Bloco 8 — Commit

Quando os 6 critérios passarem, commita Fatia 4.

---

## Troubleshooting

- **Cor do player não muda no jogo** → o `PlayerCharacterApplier` não
  está no GameObject do Player, ou o Renderer não foi achado. Adicione
  o componente. Se o player usa shader custom, talvez a property name
  não seja `_BaseColor` nem `_Color` — vê no shader e adiciona.
- **Botões dos cards ficam todos disabled** → confira no Inspector de
  cada card que o `Action Button` e `Action Text` estão atribuídos.
- **ConfirmPanel não aparece ao clicar Comprar** → ref `Confirm Panel`
  no ShopController não atribuída (Bloco 4.3).
- **Comprou mas coins não debitaram** → checa que `PlayerDataManager.
  Instance` existe na HomeScene. Sem isso, `SpendCoins` falha silencioso.
- **Shop não abre ao clicar botão Loja** → `Shop Controller` ref no
  HomeScreenController não atribuída (Bloco 5.2).
- **Player tem cor errada (preta/rosa)** → shader não tem `_BaseColor`
  nem `_Color`. Verifique o shader do material do player e adicione a
  property correta no PlayerCharacterApplier.
