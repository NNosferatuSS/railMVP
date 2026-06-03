# Login Streak Panel — Setup no Unity

> **Sessão 2026-06-02.** Código completo e commitado (`b9e16cc`).
> Setup no Unity **pendente** — ponto de entrada da próxima sessão.

## O que foi implementado (código)

| Arquivo | O que faz |
|---|---|
| `UI/LoginDayEntryUI.cs` | Card visual de 1 dia: dayLabel, coinsText, gemBonusText, completedOverlay, todayHighlight, background tint por status |
| `UI/LoginStreakPanelController.cs` | Painel com 7 cards, botão Reclamar e botão Fechar. Abre/fecha via `Open()`/`Close()`. Refresha automaticamente via `OnLoginClaimed`. |
| `UI/HomeScreenController.cs` | Novos campos `loginStreakButton` + `loginStreakPanel`; método `OpenLoginStreak()` |

---

## Hierarquia do painel (montar na HomeScene)

```
_LoginStreakPanel  ← LoginStreakPanelController aqui
├── Background         (Image — semitransparente, cobre a tela)
├── Card               (Panel branco/escuro centralizado)
│   ├── Title          (TMP_Text) → "Login Diário"
│   ├── DaysRow        (Horizontal Layout Group, Space Between)
│   │   ├── Day1       ← LoginDayEntryUI
│   │   ├── Day2       ← LoginDayEntryUI
│   │   ├── Day3       ← LoginDayEntryUI  (gem bonus dia 3)
│   │   ├── Day4       ← LoginDayEntryUI
│   │   ├── Day5       ← LoginDayEntryUI
│   │   ├── Day6       ← LoginDayEntryUI
│   │   └── Day7       ← LoginDayEntryUI  (gem bonus dia 7)
│   ├── ClaimButton    (Button + TMP_Text)
│   └── CloseButton    (Button)
```

---

## Cada card de dia (LoginDayEntryUI)

Estrutura sugerida para cada `DayN`:

```
DayN  ← LoginDayEntryUI aqui
├── Background         (Image) ← arrastar em "Background"
├── DayLabel           (TMP_Text) ← "Day Label"
├── CoinsText          (TMP_Text) ← "Coins Text"
├── GemBonusText       (TMP_Text) ← "Gem Bonus Text" (se vazio, campo fica inativo)
├── CompletedOverlay   (GameObject — ex: ícone ✓) ← "Completed Overlay"
└── TodayHighlight     (GameObject — ex: borda amarela) ← "Today Highlight"
```

### Cores de background por status (configuráveis no Inspector)

| Status | Cor default | Quando aparece |
|---|---|---|
| `Completed` | Cinza `(0.35, 0.35, 0.35)` | Dias anteriores do ciclo |
| `ClaimedToday` | Verde `(0.20, 0.70, 0.30)` | Hoje, já reclamado |
| `AvailableToday` | Amarelo `(1.00, 0.85, 0.10)` | Hoje, ainda não reclamado |
| `Upcoming` | Preto `(0.15, 0.15, 0.15)` | Dias futuros |

---

## Atribuições no LoginStreakPanelController

| Campo Inspector | O que arrastar |
|---|---|
| **Panel** | O próprio GameObject `_LoginStreakPanel` |
| **Title Text** | `Card/Title` (opcional) |
| **Day Entries [0]** | `Day1` |
| **Day Entries [1]** | `Day2` |
| **Day Entries [2]** | `Day3` |
| **Day Entries [3]** | `Day4` |
| **Day Entries [4]** | `Day5` |
| **Day Entries [5]** | `Day6` |
| **Day Entries [6]** | `Day7` |
| **Claim Button** | `Card/ClaimButton` |
| **Claim Button Text** | `Card/ClaimButton/Text` |
| **Close Button** | `Card/CloseButton` |

---

## Atribuições no HomeScreenController

| Campo Inspector | O que arrastar |
|---|---|
| **Login Streak Button** | Botão na Home que abre o painel (ex: "Ver Streak") |
| **Login Streak Panel** | `_LoginStreakPanel` |

O botão pode ser posicionado ao lado do popup de login diário ou na área de top-bar.

---

## Comportamento esperado

1. Player abre a Home → popup de claim aparece normalmente (sem mudança).
2. Player clica em "Ver Streak" → `_LoginStreakPanel` abre, mostra os 7 dias.
3. Dias 1..N-1 (já feitos) = cinza com ✓.
4. Dia N (hoje):
   - Amarelo se ainda não reclamou → botão **"Reclamar Dia N"** ativo.
   - Verde se já reclamou → botão **"Já reclamado hoje"** desativado.
5. Dias N+1..7 = preto, sem overlay.
6. Ao clicar Reclamar: `DailyLoginManager.ClaimLogin()` → `OnLoginClaimed` → cards
   atualizam na hora (dia N vira verde, botão desativa).
7. Dias com gem (3 e 7): `GemBonusText` aparece com `+2 gems` / `+5 gems`.

---

## Validação

- [ ] Abrir HomeScene, clicar em "Ver Streak" → painel aparece.
- [ ] 7 cards visíveis com labels Dia 1..7 e valores de coins corretos.
- [ ] Dias 3 e 7 mostram texto de gem bonus; outros não.
- [ ] Dia atual amarelo (ou verde se já reclamado).
- [ ] Clicar Reclamar → dia vira verde, botão desativa, coins creditados.
- [ ] Fechar e reabrir → estado persiste corretamente.
- [ ] Debug: F1 → "Daily Login → Force available" → reabrir painel → dia volta a amarelo.
