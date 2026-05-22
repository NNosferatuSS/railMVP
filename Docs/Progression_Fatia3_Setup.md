# Fatia 3 — Setup no Unity Editor (passo a passo)

> Códigos prontos: `DailyLoginManager` (login + chest no mesmo arquivo),
> `HomeScreenController` atualizado com popup + botão chest,
> `DebugPanelController` com seção Daily Login + Chest. Falta o setup
> de cena.

**Tempo estimado:** 15–20 min.

---

## Bloco 1 — Adicionar `_DailyLoginManager` na HomeScene

1. Abra `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → clique direito → `Create Empty` → renomeie
   `_DailyLoginManager`.
3. Inspector → `Add Component` → `DailyLoginManager`.
4. Não precisa atribuir refs. Carrega state do PlayerPrefs no Awake.
5. Salve a cena.

---

## Bloco 2 — Criar Login Popup

Vamos criar um Panel que cobre a Home com escurecimento + uma "card"
com a recompensa do dia.

### 2.1 Backdrop

1. Hierarchy → no Canvas → clique direito → `UI → Panel` → renomeie
   `LoginPopupPanel`.
2. Rect Transform: anchor preset `stretch + stretch` (Alt). Garante que
   cobre a Home toda.
   - Left/Top/Right/Bottom: `0`.
3. Image: cor `#000000` com alpha `180` (escurecimento).
4. Adicione um `Canvas Group` (Add Component). Não é estritamente
   necessário, mas útil pra controlar interatividade.

### 2.2 Card central

1. Filho do `LoginPopupPanel` → clique direito → `UI → Panel` → renomeie
   `LoginCard`.
2. Rect Transform: anchor `middle center`. Pos `0,0`. Width `700`,
   Height `400`.
3. Image: cor `#1A1A2E` alpha `255` (sólido).
4. Add Component → `Vertical Layout Group`:
   - Padding 24 all. Spacing 16. Child Alignment Middle Center.
   - Control Child Size: Width ☑, Height ☐.
   - Child Force Expand: Width ☑, Height ☐.

### 2.3 Conteúdo do card

Filhos do `LoginCard`, na ordem:

**1) Título** (`UI → Text - TextMeshPro`):
- Renomeie `LoginTitle`. Text: `Login Diário`. Font Size: `40`. Bold.
  Alignment: Center.

**2) DayText** (`UI → Text - TextMeshPro`):
- Renomeie `LoginDayText`. Text: `Dia 1 de 7` (placeholder).
  Font Size: `28`. Alignment: Center.

**3) RewardText** (`UI → Text - TextMeshPro`):
- Renomeie `LoginRewardText`. Text: `+50 coins` (placeholder).
  Font Size: `36`. Bold. Color amarelo `#FFD54F`. Alignment: Center.

**4) ClaimButton** (`UI → Button - TextMeshPro`):
- Renomeie `LoginClaimButton`. Filho `Text (TMP)` → text `Reclamar`.
  Font Size: `28`.

**5) (Opcional) CloseButton** (`UI → Button - TextMeshPro`):
- Renomeie `LoginCloseButton`. Filho text `Mais tarde`. Font Size: `20`.
- Você pode pular esse — o user só não vai conseguir fechar sem
  reclamar, o que é OK (popup volta na próxima abertura mesmo).

### 2.4 Desativar o popup no início

1. Selecione `LoginPopupPanel` (o root).
2. Inspector → desmarque a checkbox no topo (deactivate o GameObject).
3. O HomeScreenController ativa via `SetActive(true)` se `ShouldShowPopup()`.

---

## Bloco 3 — Criar botão Daily Chest

1. Hierarchy no Canvas → clique direito → `UI → Button - TextMeshPro`
   → renomeie `ChestButton`.
2. Posição: ao lado dos 3 botões stub (Loja/Leaderboard/Perfil) ou
   acima/abaixo do PlayButton — escolha onde fica legível.
3. Width `280`, Height `80`. Font Size: `22`.
4. Renomeie o filho `Text (TMP)` pra `ChestButtonText`. Placeholder
   text: `Baú Grátis +150`.

---

## Bloco 4 — Wire refs no `_Home`

1. Selecione `_Home` na HomeScene.
2. No `HomeScreenController`, atribua:
   - **Daily Login popup:**
     - Login Popup Panel → `LoginPopupPanel`
     - Login Day Text → `LoginDayText`
     - Login Reward Text → `LoginRewardText`
     - Login Claim Button → `LoginClaimButton`
     - Login Close Button → `LoginCloseButton` (ou deixe vazio)
   - **Daily Ad Chest:**
     - Chest Button → `ChestButton`
     - Chest Button Text → `ChestButtonText`
3. Salve a cena.

---

## Bloco 5 — Validar (6 critérios)

### Antes de começar:
- F1 → seção `Daily Login + Chest`. Clica `Reset Login+Chest` pra
  garantir estado virgem.

### Critérios:

- [ ] **1.** Reabre Play partindo da HomeScene → popup aparece com
   "Dia 1 de 7" + "+50 coins". Clica `Reclamar` → coins do PDM sobem 50
   + popup fecha.
- [ ] **2.** Sem fechar o Play, sai pro game e volta (JOGAR → morre →
   HOME) → popup NÃO aparece (já reclamou hoje).
- [ ] **3.** F1 → `Show Login` (force) → fecha o Play e abre de novo →
   popup volta com "Dia 2 de 7" + "+100 coins". Claim → +100 coins.
- [ ] **4.** Repete passo 3 mais 5 vezes → você passa por Dia 3, 4, 5
   (+200), 6 (+300), 7 (+500). Depois cicla de volta pra Dia 1 (+50).
   Sem reset — pula dias só avança +1.
- [ ] **5.** Chest: botão `Baú Grátis +150` visível → clica → +150 coins
   → botão vira "Baú já reclamado hoje" e desabilita. F1 → `Avail Chest`
   → volta ao estado disponível.
- [ ] **6.** Fecha Play e reabre → estado persiste (last_claim, day,
   chest_date no PlayerPrefs).

---

## Troubleshooting

- **Popup não aparece na primeira abertura** → confirma que o
  `LoginPopupPanel` começa `inactive` (passo 2.4). HomeScreenController
  ativa via `SetActive(true)` se `ShouldShowPopup()` é true. Se já
  reclamou hoje (mesmo em sessão anterior), não aparece.
- **Day fica em 1 mesmo após Force Login várias vezes** → você precisa
  clicar `Reclamar` pro ciclo avançar. `Show Login` só zera o
  last_claim — quem incrementa Day é o ClaimLogin.
- **Popup aparece mas botão Reclamar não funciona** → ref do
  `Login Claim Button` no `_Home` não foi atribuído. Refazer Bloco 4.
- **Chest sempre mostra "já reclamado"** → ChestLastDate persistiu de
  uma sessão anterior. F1 → `Avail Chest` ou `Reset Login+Chest`.
- **Coins não sobem ao reclamar** → falta o `_PlayerDataManager` na
  HomeScene. Bloco 1 da Fatia 1.
