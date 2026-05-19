# PostMVP2.2 — 4 Passive Power-ups

**Adiciona:** 2x Coins, Ghost, Lane Preview, Coin Radar. Todos passivos
(efeito automático ao coletar), duração em tiles, stack estende duração.

Pré-requisitos: PostMVP2.1 validada (debug panel + UI warning 3 rows).

---

## Resumo mecânico

| Power-up | Efeito | Duração default | Stack |
|---|---|---|---|
| **2x Coins** | Todas as moedas valem 2× | 10 tiles | estende |
| **Ghost** | Atravessa qualquer obstáculo sem dano | 5 tiles | estende |
| **Lane Preview** | HUD mostra direção pro critical da próxima row | 12 tiles | estende |
| **Coin Radar** | Moedas pulsam de escala (mais visíveis de longe) | 12 tiles | estende |

---

## 1. Criar os 4 prefabs

Mesmo padrão dos pickups existentes (Sphere + SphereCollider trigger +
material + componente Pickup).

### 1.1 PowerUp_DoubleCoins_Prefab
- **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: laranja `#FF9933` com emission (sugere "ouro/moeda").
- Add Component → **Double Coins Pickup**.
- `Duration Tiles = 0` (usa default 10 do manager).

### 1.2 PowerUp_Ghost_Prefab
- **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: branco `#FFFFFF` com Alpha 0.6 (semi-transparente — pede shader
  Transparent). Alternativa: branco opaco com emission alta — fica
  "brilhante", efeito mais simples sem mexer em shader.
- Add Component → **Ghost Pickup**.

### 1.3 PowerUp_LanePreview_Prefab
- **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: magenta `#FF55CC` com emission.
- Add Component → **Lane Preview Pickup**.

### 1.4 PowerUp_CoinRadar_Prefab
- **Sphere** scale `(0.6, 0.6, 0.6)`.
- **SphereCollider** → `Is Trigger = ✅`.
- Material: amarelo claro `#FFEE66` com emission.
- Add Component → **Coin Radar Pickup**.

---

## 2. Atribuir no Generator

No `_RailManager → ProceduralRailGenerator → Power Up Prefabs`, **adicione
os 4 prefabs** (array expanda pra 8 elementos: Shield, SlowDown, Magnet,
DifficultyReset + os 4 novos).

Generator escolhe random uniforme — 8 prefabs = ~12.5% chance cada quando
o power-up roll vence.

> Drop rate ajustável duplicando entradas (ex: 2× Shield no array faz
> Shield aparecer 2× mais que os outros).

---

## 3. Adicionar 4 TMP_Texts no HUD

Como filhos do `_HUD_Canvas`, abaixo dos textos existentes (Shield/Slow/Magnet).

| Nome | Anchor | Anchored Position | Tamanho | Cor inicial |
|---|---|---|---|---|
| `DoubleCoinsText` | Top-Left | (24, -372) | 300×60 | laranja `#FF9933` |
| `GhostText` | Top-Left | (24, -428) | 300×60 | branco |
| `LanePreviewText` | Top-Left | (24, -484) | 300×60 | magenta |
| `CoinRadarText` | Top-Left | (24, -540) | 300×60 | amarelo |

Font Size 32, Alignment Top-Left, initial text vazio (ou `"PowerUpName 0"`).

Atribua os 4 refs no `_HUD → HUD Controller` (campos novos sob
"Power-ups indicators (PostMVP2.2)").

> Esses TMP_Texts começam DESATIVADOS — só aparecem quando o power-up
> respectivo é ativo.

---

## 4. Testar via Debug Panel (recomendado)

Sem precisar coletar pickups na cena:

1. Play, F1 abre painel.
2. Clique nos botões novos:
   - **Grant 2x Coins** → contador `2xCoins 10` aparece.
     Colete uma moeda → ver `Coins +2` no HUD principal.
   - **Grant Ghost** → contador `Ghost 5`. Force entrar num cubo
     vermelho — atravessa sem morrer.
   - **Grant Lane Preview** → indicador `Next: <- LEFT (12)` (ou outra direção)
     aparece. Anda pra próxima row — direção atualiza.
   - **Grant Coin Radar** → todas as moedas começam a pulsar de tamanho.

---

## 5. Critérios de validação

- [ ] 4 prefabs criados e arrastados no Generator.
- [ ] 4 TMP_Texts no HUD com refs.
- [ ] 2x Coins: coleta dobra os coins. HUD mostra `2x Coins N`.
- [ ] Ghost: cubo vermelho NÃO mata; barreira amarela também NÃO consome shield.
- [ ] Lane Preview: HUD mostra direção pro critical da próxima row.
      Atualiza ao mudar de tile.
- [ ] Coin Radar: moedas pulsam de escala (visível, especialmente em tiers altos).
- [ ] Pickups orgânicos: andando pela cena, esfera laranja/branca/magenta/
      amarela aparece em decoy ou critical com chance do tier.
- [ ] Stack: 2 Ghosts seguidos = duração somada.
- [ ] Stress test continua passando.

---

## 6. Commit

```
git add Assets/Prefabs/RailSwitchMVP/PowerUp_DoubleCoins_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_Ghost_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_LanePreview_Prefab.prefab \
        Assets/Prefabs/RailSwitchMVP/PowerUp_CoinRadar_Prefab.prefab \
        Assets/Scenes/RailSwitchMVP.unity
git commit -m "feat(post-mvp2.2): 4 prefabs de power-ups passivos + setup na cena"
```

---

## 7. Troubleshooting

**Coleta DoubleCoins mas Coins não dobra:**
- Confirme que `CollectibleCoin.Collect()` foi atualizado (este commit).
- Verifique log `[CoinManager] +2 → N` no Console em vez de `+1`.

**Ghost ativo mas LethalObstacle ainda mata:**
- Confirme que `ObstacleBase.OnTriggerEnter` faz `if (Ghost) return;`
  ANTES de chamar `OnPlayerHit`. Build atualizado.

**Lane Preview não mostra direção:**
- Confirme que o `_HUD → Player` ref existe (auto-resolve no Start).
- O Lane Preview precisa do `OnTileEntered` event do player — esse já existe
  desde a Iter 4 do MVP2.

**Coin Radar não faz nada visualmente:**
- O pulse é via `transform.localScale`. Se as moedas têm escala muito pequena
  (~0.05), o pulse pode ser imperceptível. Ajuste `Radar Pulse Amplitude`
  no Inspector da `CollectibleCoin` (no Coin_Prefab — vai aplicar a todas
  as novas).

**HUD não mostra os 4 novos indicadores:**
- Confirme refs no `_HUD → HUD Controller` (4 novos campos sob "PostMVP2.2").
