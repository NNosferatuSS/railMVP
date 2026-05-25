# Fatia 9 — Profile Panel Setup

> **Pré-requisitos:** Fatias 7A + 7B + 8 commitadas. Você precisa de PDM
> + Sync funcionando — schema da tabela `players` já tem `player_name`
> desde a Fatia 7A.
>
> **Códigos prontos:** `ProfilePanelController.cs`, `PlayerDataManager`
> ganhou `SetPlayerName(string)` + `OnPlayerNameChanged` event,
> `HomeScreenController` wire do profileButton. Falta: panel UI na cena.

**Tempo estimado:** 10-15min de setup.

---

## O que isto resolve

- Botão "Profile" da Home era stub (`interactable = false`). Agora abre um modal pra editar nome.
- Leaderboard mostra "Player" pra todo mundo. Após edit, próximos submits usam o nome novo.
- Foundation pra futuras features de profile (avatar, sound prefs, etc).

**Limitação:** rows já submetidas no `daily_results` ficam com o nome
antigo (`player_name` é desnormalizado lá). O `submit_daily_result` RPC
só atualiza com distance > existing. Pra MVP, aceitável.

---

## Passo 1 — Criar `ProfilePanel` modal na HomeScene

Padrão de modal igual ao LeaderboardPanel / ShopPanel.

### 1.1 — Backdrop

1. HomeScene → Canvas → Right-click → **UI → Panel** → renomear `ProfilePanel`.
2. Rect Transform: anchor stretch/stretch. Left/Top/Right/Bottom `0`.
3. Image: cor `#000000` alpha `200` (escurecimento).
4. **Desativar** a checkbox do GameObject no topo do Inspector (panel começa hidden).

### 1.2 — Header

1. Filho de `ProfilePanel` → **UI → Text - TextMeshPro** → `HeaderText`.
   - Anchor: top-center. Pos Y `-150`. Width `600`. Height `80`.
   - Text: `Editar Perfil`. Font Size: `48`. Bold. Alignment Center.

2. Filho → **UI → Button - TextMeshPro** → `CloseButton`.
   - Anchor: top-right. Pos X `-50`. Pos Y `-50`. Width `80`. Height `80`.
   - Child Text: `X`. Font Size: `36`.

### 1.3 — Label + Input field

1. Filho de `ProfilePanel` → **UI → Text - TextMeshPro** → `NameLabel`.
   - Anchor: middle center. Pos X `0`. Pos Y `60`. Width `500`. Height `60`.
   - Text: `Seu nome no leaderboard:`. Font Size: `28`. Alignment Center.

2. Filho → **UI → Input Field - TextMeshPro** → `NameInputField`.
   - Anchor: middle center. Pos X `0`. Pos Y `-20`. Width `500`. Height `80`.
   - **Inspector → TMP_InputField → Character Limit**: `12` (matching PDM.PlayerNameMaxLength).
   - **Content Type**: `Standard` (ou `Alphanumeric` se quiser bloquear símbolos no input).
   - Text Component (filho): Font Size `36`. Alignment Left, com padding.
   - Placeholder (filho): texto `Digite seu nome...`. Cor cinza claro.

### 1.4 — Texto de erro

1. Filho → **UI → Text - TextMeshPro** → `ErrorText`.
   - Anchor: middle center. Pos X `0`. Pos Y `-120`. Width `500`. Height `40`.
   - Text: `(erro placeholder)`. Font Size: `22`. Color **vermelho** (`#FF6B6B`). Alignment Center.
   - **Desativar** GameObject (controle visual fica com o controller).

### 1.5 — Save button

1. Filho → **UI → Button - TextMeshPro** → `SaveButton`.
   - Anchor: middle center. Pos X `0`. Pos Y `-220`. Width `300`. Height `90`.
   - Image: cor primária do app (ex: verde `#4FC97A`).
   - Child Text: `SALVAR`. Font Size: `36`. Bold. Color branco.

### 1.6 — Component `ProfilePanelController`

1. Selecionar `ProfilePanel` root.
2. **Add Component** → `ProfilePanelController` (`RailSwitchMVP.UI.ProfilePanelController`).
3. Drag refs:
   - **Profile Panel** ← `ProfilePanel` (self).
   - **Close Button** ← `CloseButton`.
   - **Name Input Field** ← `NameInputField`.
   - **Save Button** ← `SaveButton`.
   - **Error Text** ← `ErrorText`.
   - **Error Too Short / Too Long / Empty**: deixar nos defaults (`{min}` e `{max}` são substituídos em runtime).

**Como saber se deu certo:**
- Selecionar `ProfilePanel`, todos os 5 slots populados.
- Panel está desativado na Hierarchy (não aparece em Play boot).

---

## Passo 2 — Wire o `profileButton` no `HomeScreenController`

1. Hierarchy → selecionar o GameObject que tem `HomeScreenController`.
2. Inspector → **Profile (Fatia 9)**:
   - **Profile Button**: arrastar o botão Profile que já existe na Home (era stub).
   - **Profile Controller**: arrastar `ProfilePanel` (que tem o `ProfilePanelController`).
3. **Ctrl+S**.

**Como saber se deu certo:** Em Play, o botão Profile vira clicável (não mais inerte). Clicar abre o panel.

---

## Passo 3 — Testar no Editor

### 3.1 — Editar nome

1. **Play**. Aguarda boot (auth + sync OK).
2. Click no botão **Profile** na Home.
3. Panel abre. Input mostra o nome atual (default "Player").
4. Apaga e digita um novo nome — ex: `Artur`.
5. Click **SALVAR**.
6. Panel fecha. Console:
   ```
   [PDM] PlayerName set to 'Artur'.
   ```
7. Após ~2s (debounce da Fatia 7B):
   ```
   [Sync] Push OK — updated_at=<novo timestamp>
   ```
8. Dashboard Supabase → Table Editor → `players` → tua row → `player_name` agora é `Artur`.

### 3.2 — Validação

9. Abre Profile de novo → input mostra "Artur".
10. Apaga até ficar `Ar` (2 chars).
11. Click SALVAR. Erro vermelho: `Nome muito curto (mínimo 3).`. Panel não fecha.
12. Digita um nome muito longo (16+ chars). Note: TMP_InputField com `Character Limit = 12` corta automaticamente em 12. Mesmo assim o save valida e aceita.
13. Apaga tudo. Click SALVAR. Erro: `Digite um nome.`.

### 3.3 — UI live update

14. Apaga input, digita `Rogue`. SALVAR.
15. Volta pra Home. **Texto do nome no topo deve mostrar `Rogue`** (live update via `OnPlayerNameChanged`).

### 3.4 — Leaderboard reflete novo nome

16. Roda Daily Challenge nova com nome `Rogue` ativo. Morre com novo best.
17. Console: `[LB] Submit OK — distance=...`.
18. Abre Leaderboard → tua row mostra **`Rogue`** (não mais `Player`).
19. **Submits anteriores** (com nome `Player`) **não retroactivam** — o `submit_daily_result` só atualiza com distance > existing. Aceitável.

---

## Critérios de validação

- [ ] ProfilePanel + ProfilePanelController criados, slots populados.
- [ ] Botão Profile na Home wireado.
- [ ] Editar nome → console mostra `[PDM] PlayerName set to '...'`.
- [ ] Após ~2s, dashboard `players.player_name` atualizado.
- [ ] Nome na Home atualiza live ao fechar panel.
- [ ] Validação rejeita <3 chars + >12 chars (via Character Limit) + vazio.
- [ ] Novo submit ao leaderboard usa o nome novo.
- [ ] Reabrir panel mostra nome atual (não stale).

---

## Próximas fatias possíveis

- Adicionar avatar/cor preferida no profile.
- Settings (som on/off, vibração).
- Pre-launch checklist (keystore, privacy policy, store assets).
- IAP (§11.4 — só após ter jogadores reais).
