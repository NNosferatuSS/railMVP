# Power-ups: sem stack + gating de spawn (gap global + cooldown por tipo)

> Sessão 2026-06-02. Duas mudanças relacionadas a power-ups.

## 1. Sem stacking — coletar RENOVA (não acumula)

Diretriz do user: coletar um power-up que já está ativo apenas o **renova**, não empilha.

- **Shield:** vira **1 carga** fixa (era `charges++`, mostrava `x2`/`x3`). HUD agora mostra
  só "Shield" (sem `x{n}`). Coletar outro com shield ativo = continua 1.
- **Duração (SlowDown, Magnet, 2x Coins, Ghost, Lane Preview, Coin Radar, Teleport,
  AutoFollow):** coletar **reseta a duração pra cheia** (era `+= tiles`, agora `= tiles`).
  Não soma mais.
- **Debuffs (SpeedUp / LaneSwap):** **inalterados** — são hazards negativos, não power-ups.
  SpeedUp ainda soma duração, LaneSwap ainda reseta. (Se quiser mudar, é no `PowerUpManager`.)

Código: `PowerUpManager.cs` (cada `Grant*`), texto do Shield em `HUDController.cs`.
**Nenhum setup no Unity** — comportamento automático.

---

## 2. Gating de spawn — evita power-ups em rows coladas

Dois controles **combinados** que limitam quando um power-up pode spawnar:

### A) Gap GLOBAL (entre quaisquer power-ups)

**Onde:** `RailGenConfig_Default` → header **"Power-up spawn gating"** → campo
**`Power Up Min Row Gap`**. (Abre pelo **Control Panel → Generation**.)

Depois que QUALQUER power-up spawna numa row, as próximas N rows **não recebem nenhum**
power-up. Default **3**.

- `0` = sem gap (comportamento antigo).
- `3` = depois de um power-up, 3 rows limpas antes do próximo poder aparecer.

Isso sozinho já resolve o caso "shield numa row, shield na próxima, outro na próxima":
com gap ≥ 1, no máximo um power-up a cada `gap` rows.

### B) Cooldown POR TIPO (mesmo tipo não repete tão cedo)

**Onde:** em cada **`PowerUpPool`** (SO), cada entrada da lista `entries` agora tem um
campo **`Cooldown Rows`** ao lado do `weight`. (Abre pelo **Control Panel → Pools → Power-ups**.)

Depois que um tipo spawna, ele fica **fora do sorteio** por esse nº de rows — mas **outros
tipos podem** aparecer no intervalo (respeitando o gap global). Default **0** (sem CD próprio).

- Ex.: `Shield` com `cooldownRows = 8` → após um Shield, nenhum Shield novo por 8 rows,
  mesmo que o gap global já tenha liberado um power-up (pode vir um Magnet, p.ex.).

### Como os dois interagem

1. **Gap global** decide *se* esta row pode ter qualquer power-up.
2. Se pode, o **cooldown por tipo** filtra quais tipos estão elegíveis no sorteio ponderado.
3. Spawna → registra a row no gap global **e** no cooldown daquele tipo.

> O override de debug (F2 / SpawnOverrideController) **respeita o gap global** mas **ignora
> o cooldown por tipo** (é ferramenta de tuning).

### Tuning sugerido
- Comece com `powerUpMinRowGap = 3` e `cooldownRows = 0` em tudo.
- Se um power-up específico (ex. Shield) ainda aparece demais, dê a ele um `cooldownRows`
  (6–10) só na entrada dele nos pools.
- Gap global alto deixa power-ups raros; baixo + cooldowns por tipo dá densidade variada.

Código: `ProceduralRailGenerator.cs` (`_lastPowerUpRow`, `_lastRowByType`,
`IsPowerUpOnCooldown`, gating no bloco de spawn), `PowerUpWeight.cooldownRows`
(`DifficultyConfig.cs`), `RailGenConfig.powerUpMinRowGap`.

---

## 3. MESMO gating pra HAZARDS

A mesma lógica foi aplicada aos hazards (obstáculos: Lethal, Barrier, SpeedUp, LaneSwap,
Vortex), que antes spawnavam só por chance por-tile sem nenhum espaçamento.

- **Gap global:** `RailGenConfig_Default` → header **"Hazard spawn gating"** → **`Hazard Min Row Gap`**.
  Control Panel → Generation. Após qualquer hazard, N rows sem nenhum.
  **Default 0 = comportamento atual (sem mudança).** ⚠️ Valores altos deixam o jogo **mais fácil**
  (hazards são o desafio) — suba com cuidado.
- **Cooldown por tipo:** cada **`HazardPool`** (SO) → cada entrada ganhou **`Cooldown Rows`**
  ao lado do `weight`. Control Panel → Pools → Hazards. Ex.: Vortex com `cooldownRows = 6`
  não repete por 6 rows, mas Lethal/Barrier podem vir no meio.

Interação idêntica à dos power-ups (gap global decide *se*; cooldown por tipo filtra *quais*).
Override F2 respeita o gap global, ignora o cooldown por tipo.

Código: `ProceduralRailGenerator.cs` (`_lastHazardRow`, `_lastRowByHazard`,
`IsHazardOnCooldown`), `HazardWeight.cooldownRows` (`DifficultyConfig.cs`),
`RailGenConfig.hazardMinRowGap`.

> **Diferença de default vs power-ups:** power-up gap default = 3 (clustering é só questão de
> feel); hazard gap default = 0 (mexer nisso altera a curva de dificuldade — opt-in).

### ⚠️ Validar no Unity
- `cooldownRows` é campo novo no struct serializado → vem **0** nas entradas existentes
  (= sem CD por tipo, ok). O `powerUpMinRowGap` tem default 3 no código, mas assets já
  serializados vêm **0** — abra o `RailGenConfig_Default` e ajuste pra 3 (ou o valor que quiser).
