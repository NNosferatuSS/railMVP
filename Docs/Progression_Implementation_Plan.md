# Plano de Implementação — Camada de Progressão

> Companheiro de `RailSwitch_ProgressionDesign.md` (que tem a spec completa).
> Este doc define **ordem de execução** e **fatias verticais** pra entregar a
> camada meta sem virar refactor de tudo. Cada fatia é jogável/testável
> isoladamente.

**Status:** Fatias 1-4 ✅ validadas e commitadas em 2026-05-22.
Próxima sessão decide entre Fatia 5 (Rewarded Ads) ou skip pra spec §11
(Daily Challenge → Supabase → Leaderboard).

---

## Princípio guia

Quase tudo na spec passa pelo `PlayerDataManager` (coins, best, runs,
personagens, claims de missão/login/chest). Atacar feature isolada (ex: só
missões) força stubar metade do PDM e refatorar depois. Por isso fazemos
**fundação primeiro, features depois**.

Trade-off aceito: adiar playtesting puro de calibração do gameplay até a
Fatia 2 estar pronta (loop de retenção é a feature de maior valor pendente).

---

## Fatia 1 — Fundação + scene flow (~1 sessão)

**Objetivo:** `Coins do run persistem no PlayerPrefs e aparecem na Home.`
Sem isso, nada da spec funciona.

### Entregáveis

- [ ] `Assets/Scripts/RailSwitchMVP/Meta/PlayerDataManager.cs`
  - Singleton `DontDestroyOnLoad` com proteção de duplicata.
  - Estado: `Coins`, `BestDistance`, `BestCoins`, `BestTier`, `BestTime`,
    `TotalRuns`, `OwnedChars` (CSV), `EquippedChar`, `PlayerName`.
  - API: `AddCoins(int)`, `SpendCoins(int):bool`, `UpdateBests(...)`,
    `IncrementTotalRuns()`, `UnlockCharacter(int)`, `EquipCharacter(int)`,
    `IsCharacterOwned(int)`, `Save()`, `Load()`.
  - Auto-`Save()` em `OnApplicationPause(true)` e `OnApplicationFocus(false)`.
  - Funciona se instanciado direto na GameScene (sem passar pela Home).
- [ ] Migrar `HighScoreManager` → `PlayerDataManager`.
  - PDM absorve os 4 bests + `UpdateBests` retornando `RecordResult`.
  - Manter mesmas chaves PlayerPrefs (`RailMVP.BestX`) pra não perder records
    salvos. Adicionar chaves novas (`RailMVP.Coins`, etc) com defaults.
  - Apagar `HighScoreManager.cs` e referência na cena depois que GameOver e
    DebugPanel forem migrados.
- [ ] Refatorar `GameOverController`:
  - Substituir `HighScoreManager.Instance` por `PlayerDataManager.Instance`.
  - No "Restart": chamar `PlayerDataManager.AddCoins(runCoins)` +
    `IncrementTotalRuns()` antes de recarregar cena.
  - Adicionar botão "HOME" que chama `PlayerDataManager.AddCoins(...)` e
    carrega `HomeScene`.
- [ ] Criar `Assets/Scenes/HomeScene.unity` minimalista:
  - Canvas Screen Space Overlay 1920×1080.
  - 3 TMP_Text: nome (placeholder "Player"), coins, best distance.
  - Botão "JOGAR" → carrega `RailSwitchMVP` scene.
  - Placeholders desabilitados de Loja/Leaderboard/Perfil (botões inertes).
- [ ] `Assets/Scripts/RailSwitchMVP/UI/HomeScreenController.cs`:
  - Lê PDM no `OnEnable` (não só Start) — spec §7.2.
  - Botão Jogar → `SceneManager.LoadScene("RailSwitchMVP")`.
- [ ] DebugPanel: trocar "Reset Best Scores" → "Reset All Player Data"
  (chama `PlayerDataManager.WipeAll()`).
- [ ] Atualizar Build Settings: incluir HomeScene como cena 0,
  RailSwitchMVP como cena 1.

### Critérios de validação (Fatia 1)

1. Abrir o app → HomeScene aparece com `Coins: 0`, `Best: 0m`.
2. Clicar JOGAR → carrega RailSwitchMVP, jogo roda normal.
3. Morrer → GameOver mostra stats e botão HOME.
4. Clicar HOME → volta pra HomeScene com `Coins: N` (coins do run somadas)
   e `Best: Xm` (se bateu record).
5. Fechar o app e reabrir → coins e best persistem.
6. `Reset All Player Data` no DebugPanel zera tudo.

### Notas de implementação

- `PlayerName` no MVP é placeholder fixo "Player" — input UI fica pra depois.
- A spec menciona `MaterialPropertyBlock` por personagem mas no MVP a Home
  só guarda o índice; aplicação visual é Fatia 4.
- Não criar AdMob/UnityAds ainda — botão "2x coins" stubado dá coins
  direto na Fatia 1, viramos pra rewarded ad real só na Fatia 5.

---

## Fatia 2 — MissionTracker + UI de missões (~2 sessões)

**Objetivo:** Loop diário de retenção funcionando.

### Entregáveis

- [ ] `Assets/Scripts/RailSwitchMVP/Meta/MissionTracker.cs` (spec §10).
  - Singleton `DontDestroyOnLoad`.
  - Pool de 20 diárias (§3.2) + 10 semanais (§4.2) em arrays serializados.
  - Geração por `DayOfYear % 20` (e `WeekOfYear % 10`) — §3.5/§4.3.
  - Suporte aos 9 tipos de tracking (§3.3): `single_run_coins`,
    `single_run_distance`, `single_run_time`, `total_runs`,
    `daily_total_coins`, `use_powerup`, `reach_tier`, `no_powerup_run`,
    `tiles_with_coins`.
  - `CommitProgress()` salva PlayerPrefs.
- [ ] Hooks de tracking nos sistemas existentes:
  - `CoinManager.AddCoins` → `MissionTracker.OnCoinsCollected(n)`.
  - `PowerUpManager` (ou cada pickup) → `OnPowerUpUsed(string)`.
  - `DifficultyManager` mudança de tier → `OnTierReached(int)`.
  - `CollectibleCoin.OnTriggerEnter` → `OnTileWithCoin()`.
  - `GameManager.TriggerGameOver` → `MissionTracker.EndRun(...)`.
- [ ] `HomeScreenController`: lista de 3 missões diárias (descrição +
  progresso + recompensa + botão Reclamar). Quando claimed → estilo cinza
  + "Reclamado". Idem semanais (collapsable).
- [ ] DebugPanel: botão "Force complete mission slot 0/1/2" e
  "Reset all missions" (debug puro).

### Critérios de validação (Fatia 2)

1. Abrir app no dia X → 3 diárias geradas com IDs `(dayOfYear%20)+0/1/2`.
2. Jogar runs → progresso dos tipos aplicáveis aumenta.
3. Completar uma missão → botão Reclamar funciona, coins entram no PDM.
4. Fechar/reabrir mesmo dia → missões e progresso persistem.
5. Mudar a data do sistema pra dia seguinte → novas missões geradas,
   antigas (reclamadas ou não) somem.

---

## Fatia 3 — Login diário + Daily Ad Chest stub (~1 sessão)

**Objetivo:** "Volta amanhã" tem incentivo.

### Entregáveis

- [ ] `Assets/Scripts/RailSwitchMVP/Meta/DailyLoginManager.cs`
  - Pode ser parte do PDM ou classe separada — escolha durante implementação.
  - Lógica §2.4 (last claim vs hoje vs ontem vs gap).
  - Tabela de 7 recompensas (50→100→150→200→300→400→500).
- [ ] `HomeScreenController`: popup automático no `OnEnable` se ainda não
  reclamou hoje. Botão "Reclamar +X coins".
- [ ] `HomeScreenController`: botão "Baú Grátis +150 coins" (uma vez/dia).
  Stub: dá coins direto sem ad. Comentário marcando ponto de integração
  com Unity Ads (Fatia 5).
- [ ] Estado salvo no PDM (chaves §2.3 + `ad_chest_date`).

### Critérios

1. Primeira abertura do dia → popup Login Day 1, claim → +50 coins.
2. Mesma sessão depois do claim → popup não aparece.
3. Reabrir no dia seguinte → popup Day 2.
4. Pular 2 dias → popup Day 3 (sem penalidade).
5. Chest grátis: 1 clique/dia, depois desabilita até virar o dia.

---

## Fatia 4 — Personagens / Loja (~1 sessão)

**Objetivo:** Coins têm pra onde gastar (sem que isso afete gameplay).

### Entregáveis

- [ ] `Assets/Scenes/ShopScene.unity` (ou panel sobreposto na Home).
- [ ] `Assets/Scripts/RailSwitchMVP/UI/ShopController.cs` com 3 personagens.
- [ ] `PlayerRailRider`: ler `PDM.EquippedChar` no Start, aplicar cor via
  `MaterialPropertyBlock` (sem novo material por instance).
- [ ] `CollectibleCoin`: idem para partículas de coleta (se houver).
- [ ] Botão "Loja" na Home ativo (até aqui era stub).

### Critérios

1. Comprar Neon com coins insuficientes → botão desabilitado.
2. Com coins ≥ 2500 → popup confirma → compra debita 2500 + desbloqueia +
   equipa + salva.
3. Voltar pra jogar → cor do player muda.
4. Reabrir o app → personagem equipado persiste.

---

## Fatia 5 — Rewarded Ads (Unity Ads SDK) (~1-2 sessões)

**Objetivo:** Substituir stubs por ads reais.

### Entregáveis

- [ ] Setup Unity Ads SDK (Package Manager + Game ID na config).
- [ ] `Assets/Scripts/RailSwitchMVP/Meta/AdsManager.cs` com:
  - Inicialização + carga de rewarded.
  - `ShowRewardedAd(callback)` API única.
  - Fallback §5.2: se não tem ad carregado, esconde botão (não dá grátis).
- [ ] Substituir stubs na Home (Chest) e no GameOver (2x coins).

### Critérios

1. Com internet: ad carrega, assiste 15-30s, callback dá recompensa.
2. Sem internet: botão some.
3. Sem regressão nos fluxos de coins/missões.

---

## Pontos abertos (decidir na hora)

- **Build target:** continuamos validando em Editor; mobile fica pra Fatia 5+
  (Unity Ads é Android/iOS).
- **PlayerName:** input UI ou só placeholder? Spec não decide — MVP fica
  placeholder, virar input se Fatia 4 sobrar tempo.
- **Migração de saves:** se um jogador tem `RailMVP.BestDistance` salvo da
  v0.2.0, Fatia 1 lê com o mesmo nome de chave pra não perder. Outras chaves
  novas começam zeradas — aceitável.

---

## Convenções

- Namespace novo: `RailSwitchMVP.Meta` (PDM, MissionTracker, DailyLogin, Ads).
- Pasta: `Assets/Scripts/RailSwitchMVP/Meta/`.
- PlayerPrefs key prefix: `RailMVP.` (mantém compat com HighScoreManager).
- Singletons seguem o padrão existente (`Instance` static, `Awake`-guarded,
  `OnDestroy` limpa).
