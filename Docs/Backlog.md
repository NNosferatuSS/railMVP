# Backlog — Ideias e Futuros Trabalhos

> Tracker de coisas que **não estão sendo feitas agora** mas valem registro
> pra retomada futura. Não é roadmap (não tem ordem/prazo) — é parking lot.
>
> Quando algo daqui virar fatia ativa, mover pra `Docs/Progression_FatiaN_Setup.md`
> ou similar e remover daqui.

---

## Progressão Adaptativa — Camada 2 (Head Start) [ADIADA 2026-05-29]

Terceira e última camada da spec `Docs/RailSwitch_AdaptiveProgression.md`. Camadas 1
(speed floor) e 3 (revive) já FEITAS. Camada 2 adiada a pedido do user — não é
prioridade agora.

**O que é:** opção *paga e opcional* (antes do run) de começar num tier **mais alto**
que o piso natural da Camada 1, com coins ou rewarded ad. Dá agência ao veterano +
sink de coins / receita.

**Regras (spec §2):**
- Desbloqueio: só aparece com `best_distance >= 500` (configurável) — protege o novato.
- Opções: **+1 tier** (200 coins OU 1 ad) / **+2 tiers** (500 coins, sem ad).
- Respeita o teto seguro da Camada 1 (nunca o último tier).

**O que falta implementar:**
- `HeadStartController` (Meta/) + UI pré-run (opções +1/+2, custos, botão ad).
- Desbloqueio por best distance; debitar coins / disparar ad.
- **Já pronto:** `DifficultyManager.StartRunWithAdaptiveTier(level, headStartOverride)`
  aceita o override. Falta só calcular o tier escolhido e passá-lo.

**Decisão de arquitetura pendente:** o Head Start é escolhido na Home, mas o
DifficultyManager vive na GameScene (recriado por cena). O tier escolhido precisa
atravessar Home→Game (static var ou manager DontDestroyOnLoad).

**Esforço estimado:** ~1 sessão (lógica + UI + wiring).

---

## Tutorial — Contextual Hints (alternativa ao single-step da Fatia 10)

**Premissa:** atual Fatia 10 mostra 1 overlay genérico no início da 1ª run.
Contextual hints mostrariam balões pequenos **na primeira vez que cada
conceito aparece em gameplay** — switch tile aparece à frente, lethal
obstacle visível, primeiro pickup de power-up, primeiro game over, etc.

### Por que faz sentido eventualmente

- Player aprende cada conceito **no momento que precisa**, com contexto visual ao vivo.
- Pular conceitos que o player não encontra (ex: nunca pegou Magnet → nunca vê hint dele).
- Pra audiência maior, reduz fricção do "wall of text" do tutorial genérico.

### Arquitetura proposta

```
HintManager (singleton DontDestroyOnLoad)
  ├ PlayerPrefs: RailMVP.Hint.{id} = 0|1 por hint individual
  ├ Queue<Hint> _pending — coalesce de múltiplos triggers simultâneos
  ├ TryShow(id) — public API: chama de qualquer lugar do game
  ├ ShowNext() — drena o queue um hint por vez
  └ events OnHintShown/Dismissed pra DebugPanel

HintPopupUI (prefab UI component)
  ├ Small floating panel (~400x150 px)
  ├ TMP title + body + close (X)
  ├ Optional auto-dismiss timer (8-10s)
  ├ Optional "Não mostrar de novo" toggle pra hints específicos
  └ Arrow opcional apontando pra GameObject relevante (via Camera.WorldToScreenPoint)

Trigger sites (callsites de TryShow no código existente):
  ├ RailManager.SpawnRow — detecta primeiro switch visível → "first-switch"
  ├ ProceduralRailGenerator hazard resolution — primeiro lethal/barrier → "first-lethal", "first-barrier"
  ├ PowerUpManager.Grant* (cada power-up tipo) → "first-shield", "first-magnet", etc.
  ├ GameManager.TriggerGameOver — primeiro game over ever → "first-death"
  ├ HomeScreenController.OnEnable — primeiro return after run → "home-features"
  └ DailyChallengeManager.StartChallenge — primeira vez → "first-daily"
```

### Hints catalog inicial (sugestão)

| ID | Trigger | Texto |
|---|---|---|
| `first-switch` | Switch tile entra no field of view | "🔀 Decida cedo — aperta ← → ANTES de chegar no switch" |
| `first-lethal` | Warning marker de lethal spawna | "⚠️ Vermelho mata. Desvia ou usa Shield" |
| `first-barrier` | Warning de barrier | "🟡 Amarelo bloqueia. Shield absorve um hit" |
| `first-powerup-pickup` | Qualquer power-up pego | "✨ Power-ups dão habilidades temporárias" |
| `first-death` | Game over inicial | "💀 Restart pra tentar quebrar seu record" |
| `home-features` | OnEnable da Home pós-1ª run | "Tente o JOGAR DESAFIO pra ranking global" |

### Esforço estimado

- HintManager + HintPopupUI prefab + UI setup: **~1 sessão**.
- Adicionar 6-8 trigger calls nos sistemas existentes (~10 linhas no total): **~30min**.
- DebugPanel: reset all hints + lista per-hint: **~15min**.
- **Total: ~1 sessão (~2-3h).**

### Migration path

Adicionar contextual hints **não conflita** com a Fatia 10 atual.

Opções de coexistência:
- **A — Manter ambos:** overlay genérico na 1ª run (Fatia 10) introduz controles básicos, hints contextuais aparecem depois pra conceitos específicos. Mais info, possível ruído.
- **B — Substituir overlay por hint:** apaga TutorialOverlay, contextual hints fazem TUDO. Mais limpo, mas player precisa pegar conceito de controle "puramente jogando" (warmup gentle ajuda).

Recomendação: **A** — overlay genérico no início ainda vale pra dar segurança ao iniciante, contextual complementa.

### Pegadinhas a antecipar

1. **Triggers idempotentes:** cada hint deve ser disparado **uma vez por user** mesmo se o trigger condition repetir. Padrão: `if (!alreadyTriggered) { TryShow(); alreadyTriggered = true; }`.
2. **Hints simultâneos:** player vê switch + lethal + power-up no mesmo frame. Queue + 1 por vez, ou prioridade (lethal > switch > power-up).
3. **Timing:** mostrar hint enquanto player está fugindo de obstáculo é cruel. Considerar `Time.timeScale = 0` ao mostrar (igual Fatia 10), com cuidado pra não interromper momentum.
4. **Reset path:** "Reset all hints" no DebugPanel pra QA.

---

## Audio system + Settings panel

**Premissa:** jogo atual é **mudo**. Sem BGM, sem SFX. Game feel sofre.

### Componentes mínimos
- `AudioManager` singleton com AudioSources separados pra BGM + SFX.
- Pool de AudioClips: switch, coin pickup, hit, game over, power-up grant, UI click.
- Assets — Freesound.org / OpenGameArt tem CC0/CC-BY material decente.
- `SettingsPanelController` — sliders Music/SFX, toggles on/off, persistido em PlayerPrefs `RailMVP.Settings.*`.

### Esforço
- AudioManager + integração nos eventos existentes (PDM.OnCoinsChanged, GameManager.OnGameOver, etc.): **~1 sessão.**
- Procurar/importar assets + balancear volumes: **~1 sessão** (curadoria).
- Settings panel UI + persistence: **~30-45min.**
- **Total: ~2-3 sessões.**

### Por que adiar
- Não bloqueia ship pra F&F (jogo funciona mudo).
- Curadoria de assets requer "ear" — mais difícil que código.
- Pode acabar virando tempo de design game-feel em vez de coding.

---

## Pre-launch (Ship pra Play Store)

Resumo do que falta pra primeira build pública. Detalhes em
`Docs/Pre_Launch_Checklist.md`.

- Keystore Android (criar + backup duplicado).
- Privacy Policy URL hospedado (GitHub Pages OK).
- App icon (1024x1024 + adaptive Android).
- Splash screen (Unity Player Settings).
- Settings de produção: `verboseLogs = false`, `Test Mode = false` no AdsManager.
- Google Play Console: conta, App entry, store listing assets (screenshots, description, screenshots).
- Internal Testing track: primeiro upload, convidar 5-10 testers via email.

### Esforço
- ~3-4 sessões totais. **Tem partes burocráticas** (Play Console review, primeiro upload sempre dá problemas inesperados).

---

## Quick wins / polish

### Pull now runtime UI refresh
PDM.ApplyRemoteState dispara `OnCoinsChanged` + `OnEquippedCharChanged` +
`OnPlayerNameChanged`, mas **não dispara um sinal genérico "tudo mudou —
re-renderiza"**. HomeScreenController.RefreshPlayerData lê PDM tudo de uma
vez, mas só roda no OnEnable e quando coins muda — best distance, name (já),
total_runs não atualizam live se pull rolar.

**Fix:** adicionar `OnFullStateChanged` event no PDM disparado por
ApplyRemoteState + RecordResult update. HomeScreenController subscribe →
chama `Refresh()` completo. ~10 linhas. **~10min.**

### Leaderboard "Você ainda não jogou hoje" UI improvement
Texto atual fica meio neutro. Botão **"JOGAR DESAFIO"** dentro do panel
quando user não jogou → 1-clique pra começar. ~20 linhas. **~15min.**

### Tutorial "Skip" button
Pra returning users que reinstalaram, dismiss imediato com 1 tap em vez
de ler. ~5 linhas extras no TutorialOverlayController. **~5min.**

### Performance audit
Profiler check em runs longas (10+ min) — leak de tiles spawnados? GC
spikes? Particle systems acumulam? Provavelmente OK mas vale 1 sessão
de medição.

---

## Backend / Server features (pós-MVP)

### Anti-cheat server-side
Edge Function que valida (input replay vs seed) antes de aceitar submit.
Spec §11 não exige pra MVP — defer até cheating virar problema real.

### Cross-device via OAuth linking
Anon user atual é device-locked. Linkar a Google/Apple Sign-in permite
mesmo player_id em devices diferentes (continuar daily challenge, mesmo
leaderboard entry). Esforço: médio. Requires Supabase OAuth provider config
+ UI de "linkar conta" no profile.

### Realtime leaderboard updates
Supabase tem Realtime subscriptions. Leaderboard panel auto-atualiza quando
outro player submete melhor. Overkill pra MVP, mas legal pra ranking
competitivo ativo.

### Daily Challenge histórico
Tabela `daily_history` ou view pra mostrar leaderboards de daily anteriores
("ver desafio de ontem"). Foundation pra "completionist" achievements.

---

## Conteúdo / Game design

- Mais power-ups (Time Slow, Auto-Switcher, Magnet AOE).
- Mais obstáculos (Moving Switch, Disappearing Tile).
- Boss runs / eventos especiais (1 dia/semana, daily com mecânica única).
- Achievement system separado de Missions (long-term goals).
- Visual themes (alternate art styles desbloqueáveis).
- Battle Pass S1/S2 (§11.6 — futuro distante).

---

## Distribuição / Marketing

- Landing page (GitHub Pages, Notion, ou Carrd) com app preview.
- App preview video (Unity Recorder → 30s clipe).
- Press kit (screenshots, logos, descriptions em PT/EN).
- Submeter pra agregadores indie (itch.io, Game Jolt).
- Social presence (Twitter/X game dev community).

---

## Monetização

### IAP (§11.4 da spec — deferred)
- Remove Ads: ~R$5, marca flag em PDM, AdsManager para de mostrar.
- Coin Pack: R$5/15/30 pra X/Y/Z coins. Unity IAP plugin oficial é
  o caminho — bem documentado.
- Spec diz "só faz sentido depois de jogadores reais". Manter.

### Real Unity Ads (revisitar)
Project Unity Ads novo não tem inventory ainda (Fatia 5 → ver
`Docs/Ads_BackendIssue.md`). Mock Mode ligado por enquanto. Tentar
desligar mock + rebuild a cada ~2 semanas pra ver se backend
preencheu. Ou migrar pra LevelPlay (mediation com AdMob → inventory
imediato).
