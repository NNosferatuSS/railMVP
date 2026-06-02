# PostMVP2.3 — Active Items System

> ⚠️ **DESCONTINUADO 2026-06-02.** O sistema de inventário/slot foi **removido** —
> agora **todo power-up é consumido na colisão** (diretriz do user). O `TimeFreeze`
> virou um `PowerUpType` normal (instantâneo); `ActiveItemSlot`/`ActiveItemType` e o
> input de Space foram deletados. O Teleport continua (window passiva, Shift+←/→).
> Este doc fica como registro histórico. Ver seção no fim do `PROGRESS.md`.

**Adiciona:** inventário de 1 slot pra items ativos (Time Freeze, Teleport),
disparados pelo player com tecla **Space**.

**Diferença vs passive power-ups:** items passivos (Shield, Magnet, etc.)
auto-ativam ao coletar. Items ativos vão pro **slot** e o player decide
quando usar.

Pré-requisitos: PostMVP2.2 validada.

---

## 1. Conceito

| Aspecto | Detalhe |
|---|---|
| **Slots** | 1 (substitui se pegar outro item ativo) |
| **Hotkeys** | `Space` (non-direcional) + `Shift + ←/→` (direcional) |
| **Tipos no MVP** | TimeFreeze (não-direcional) + Teleport (direcional) |
| **TimeFreeze** | Slowdown (`Time.timeScale = 0.15` configurável) por 3s reais. Mundo move LENTÍSSIMO — player vê o board com tempo, sem parada total. **Ativa com Space.** |
| **Teleport** | Pulo lateral instantâneo de ±1 lane. **`Shift + ←` = esquerda, `Shift + →` = direita.** Direção vem do input direto, não do switch state. Switch normal continua funcionando (setas sem Shift). |
| **Falha não consome** | Se use falha (slot vazio, lane destino vazia, Teleport com Space), o item fica no slot. |

---

## 2. Adicionar 3 managers na cena

Crie 3 GameObjects vazios na raiz da cena (lugar onde estão os outros
`_PowerUpManager`, `_HUD`, etc.):

| GameObject | Componente | Configuração |
|---|---|---|
| `_ActiveItemSlot` | Active Item Slot | (zero campos) |
| `_TimeFreezeController` | Time Freeze Controller | `Duration Seconds = 3` |
| `_TeleportController` | Teleport Controller | (zero campos) |

E mais um pro input:

| GameObject | Componente | Configuração |
|---|---|---|
| `_ActiveItemInput` | Active Item Input Handler | (zero campos) |

> Pode bundlar os 3 controllers num único GameObject `_ActiveItemSystem`
> se preferir hierarchy menos poluída.

---

## 3. Adicionar texto no HUD

Como filho do `_HUD_Canvas`, abaixo dos indicadores de power-ups:

| Campo | Valor |
|---|---|
| Nome | `ActiveItemText` |
| Anchor preset | Top-Left |
| Anchored Position | (24, -596) — abaixo do CoinRadarText |
| Width × Height | 350 × 60 |
| Alignment | Top-Left |
| Font Size | 32 |
| Color | branco |
| Initial text | `Item: -` |

Atribua o ref no `_HUD → HUD Controller → Active Item Text`.

---

## 4. Testar via Debug Panel

Sem precisar criar prefabs de pickup ativo:

1. Play, F1.
2. Seção **"Active item slot"**:
   - `Slot: None` aparece quando vazio.
   - **Grant TimeFreeze** → HUD mostra `Item: TimeFreeze (Space)`.
   - Aperta `Space` → tudo congela por 3s, player pode pensar/ajustar switch.
   - Depois de 3s, retoma.
3. **Grant Teleport** → HUD mostra `Item: Teleport (Shift+←/→)`.
   - **Aperta `Shift + →`** → player teleporta INSTANTANEAMENTE 1 lane pra direita (mesma row, Z preservado).
   - **Aperta `Shift + ←`** → 1 lane pra esquerda.
   - Se lane destino vazia → "use cancelada" no console, item fica no slot.
   - Aperta `Space` com Teleport no slot → log explicativo, sem efeito.
   - Setas SEM Shift continuam controlando o switch normal.

---

## 5. Critérios de validação

- [ ] 4 GameObjects novos na cena (Slot, TimeFreezeController, TeleportController, InputHandler).
- [ ] `ActiveItemText` no HUD com ref atribuído.
- [ ] Debug Panel mostra slot e botões de grant + use.
- [ ] TimeFreeze ativa Time.timeScale=0 por 3s reais. Player não move, mas input de switch funciona.
- [ ] TimeFreeze expira automaticamente, mundo retoma.
- [ ] Teleport com switch Left/Right teleporta lateralmente, mantendo Z.
- [ ] Teleport com switch Middle ou lane vazia → use cancelada, item preservado.
- [ ] Grant novo item enquanto slot tem outro → substitui (log no console).
- [ ] Slot esvazia ao usar com sucesso.

---

## 6. Commit

```
git add Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(post-mvp2.3): _ActiveItemSlot + controllers + HUD slot na cena"
```

---

## 7. Próximo passo

PostMVP2.4 — criar os prefabs de pickup (`PowerUp_TimeFreeze_Prefab` e
`PowerUp_Teleport_Prefab`) com scripts que colocam no slot ao coletar.
Daí dá pra coletar organicamente sem usar o Debug Panel.

---

## 8. Troubleshooting

**Space não faz nada:**
- Confirme `_ActiveItemInput` na cena com componente Active Item Input Handler.
- Confirme `_ActiveItemSlot` ativo.
- Slot vazio = Space é no-op (sem feedback). Use o painel pra grant primeiro.

**Time Freeze trava o jogo permanentemente:**
- `TimeFreezeController` usa `Time.unscaledDeltaTime` pra decrementar. Se
  algum outro código também seta `timeScale=0` (Game Over screen),
  pode dar conflito. O controller já checa `GameManager.IsPlaying` antes
  de ativar — game over bloqueia TimeFreeze.

**Teleport tem Z weird:**
- O design preserva Z do player. Se você teleportar de uma posição
  "meio do tile", ele aparece "meio do novo tile". Se startPoint está
  longe demais no Z, ajuste no prefab.
- Se quiser snap pro StartPoint do destino, edite `TeleportToAdjacent`
  pra usar `tile.StartPoint.position` em vez de `transform.position`.

**HUD não mostra slot:**
- Confirme `Active Item Text` atribuído no HUDController.
- HUD se subscribe a `ActiveItemSlot.OnItemAcquired/Used` no Start.
  Se o slot foi criado DEPOIS do HUD Start, subscribes não conectam.
  Ordem de Awake/Start: slot deve estar na cena antes (HUDController.Start
  faz `if (ActiveItemSlot.Instance != null)` então só conecta se já existe).
