# Setup: Power-up Random (Mystery Box) + Shield Slow-mo + Decoy Coin Chance

Três features implementadas em 2026-05-29. Código pronto; aqui o wiring/tuning no Unity.

---

## 1. Power-up Random (Mystery Box) 🎁

Pickup "? box" que, ao coletar, **sorteia um power-up aleatório** (ponderado) e concede.
Exclui a própria MysteryBox e os debuffs do sorteio. O feedback de qual saiu vem dos
indicadores normais do HUD.

**De onde sai o conteúdo:** do **`mysteryBoxPool` dedicado** do tier (você escolhe o que
pode sair); se esse campo estiver vazio, cai no `powerUpPool` normal do tier.

**Código (pronto):** `PowerUpType.MysteryBox` (enum), `PowerUpManager.GrantByType(type)`,
`MysteryBoxPickup : PowerUpBase`, `DifficultyTier.mysteryBoxPool`.

**Wiring no Unity:**
1. **Criar o prefab** `Prefab_MysteryBox` (espelhe um pickup existente tipo
   `PowerUp_Shield`): mesh/visual da caixinha + `Collider` com **IsTrigger = true** +
   componente **MysteryBoxPickup**. Tag não importa (o trigger checa a tag do player).
2. **Registrar no gerador:** no `ProceduralRailGenerator` (componente na cena), no array
   **Power Up Prefabs**, adicione um binding: `Type = MysteryBox`, `Prefab = Prefab_MysteryBox`.
3. **Fazer a caixa SPAWNAR:** nos `PowerUpPool` (SO) dos tiers onde quer que ela apareça,
   adicione uma entrada `MysteryBox` com um peso (ex 1–2). Compete com os outros power-ups.
4. **Escolher o CONTEÚDO da caixa (controle):** crie um `PowerUpPool`
   (Create → RailSwitchMVP → PowerUp Pool), ex `PowerUpPool_MysteryBox`, com **só os tipos
   que você quer que saiam** + pesos. Arraste-o no campo **Mystery Box Pool** de cada tier
   (o mesmo SO em todos = controle global; SOs diferentes = varia por tier). Deixar vazio =
   a caixa usa o `powerUpPool` normal do tier.

> Importante: **não** coloque `MysteryBox` dentro do `mysteryBoxPool` (já é filtrada no
> sorteio, mas evite confusão). Debuffs também são ignorados se aparecerem.

**Testar:** num tier com MysteryBox no pool, colete a caixinha → um power-up aleatório do
pool é concedido (aparece no HUD). Repita — deve variar.

---

## 2. Shield Slow-mo de impacto

Quando o **Shield absorve uma barreira** (que mataria o player), dá um **slow-mo breve +
shake** pra dar peso ao "escapei", e volta à velocidade normal.

**Código (pronto):** `PlayerCameraRig.ImpactSlowmo()`, chamado de `BarrierObstacle` quando
`ConsumeShield()` tem sucesso.

**Wiring:** nenhum — automático. Requer `PlayerCameraRig.Instance` na cena (já está).

**Tunáveis (RailGenConfig_Default, header "Camera — Shield impact slow-mo"):**
| Campo | Default | O quê |
|---|---|---|
| Shield Impact Slow Mo | 0.35 | `timeScale` no pico (menor = mais lento/dramático) |
| Shield Impact Duration | 0.45 | duração total em segundos reais (segura + volta) |

**Testar:** pegue um Shield, bata numa Barrier → o jogo desacelera por ~0.45s com shake e
volta ao normal; a barreira é destruída e você passa. (Não dispara contra obstáculo Lethal,
que mata mesmo com shield — por design.)

---

## 3. Decoy Coin Chance (% de coins em decoys por tier)

Controle granular: probabilidade de um tile **decoy** receber moedas, **por tier**.
Critical path sempre recebe.

**Código (pronto):** `DifficultyTier.decoyCoinChance` (0–1) + gate no gerador.

### ⚠️ ATENÇÃO — vem 0 por default
Como é um campo novo num struct serializado, ele aparece como **0 nos tiers existentes** —
o que significa **decoys sem moedas** até você setar. **Abra o `DifficultyConfig_Default` e
ajuste `decoyCoinChance` nos 6 tiers:**
- `1.0` = todo decoy recebe (comportamento antigo).
- `0.5` = metade dos decoys recebe.
- `0.0` = decoys nunca recebem (mesmo com `decoyCoinsMin/Max > 0`).

**Testar:** com `decoyCoinChance = 0.3` num tier, a maioria dos decoys fica sem moedas; o
critical path continua com coins normalmente.
