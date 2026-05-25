# Fatia 6 — Daily Challenge: Setup no Unity Editor

> Códigos prontos: `DailyChallengeManager` novo, `RailManager` agora chama
> `Random.InitState(seed)` no Start, `HomeScreenController` tem card de
> Daily, `GameOverController` registra resultado + prefixa reason com
> "DAILY CHALLENGE", `DebugPanelController` tem seção Daily.
> Falta setup de cena (1 GameObject + 1 card UI + wiring).

**Tempo estimado:** 10-15 min.

**Decisão de design:** sem leaderboard online ainda (Fatia 7 traz Supabase,
Fatia 8 traz leaderboard). Aqui é só "best do dia local + best-ever local".

---

## Bloco 1 — `_DailyChallengeManager` na HomeScene

1. Abrir `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → área vazia → **Create Empty** → renomear `_DailyChallengeManager`.
3. Inspector → **Add Component** → digitar `DailyChallengeManager` → selecionar
   (deve aparecer como `RailSwitchMVP.Meta.DailyChallengeManager`).
4. Os campos no Inspector são todos read-only (preenchidos via `Load()`):
   - `todayDate`, `todayBestM`, `bestEverM`, `bestEverDate` — deixar como estão.
5. **Não desativar.** O singleton precisa rodar `Awake()` pra que `Instance`
   seja válido quando o HomeScreenController consultar.

**Como saber se deu certo:**
- Hierarchy mostra `_DailyChallengeManager` (sibling de `_PlayerDataManager`,
  `_AdsManager`, etc).
- Após Play, Inspector mostra `todayBestM=0` (ou valor anterior se já jogou daily).
- Console: `[Daily] Loaded — todayDate='' todayBest=0m bestEver=0m ()` no boot.

---

## Bloco 2 — Card UI "Daily Challenge" na HomeScene

Layout sugerido: card próximo ao botão JOGAR principal, distinguível
visualmente (cor diferente, ex: roxo/azul) pra deixar claro que é modo
separado.

### 2.1 Container do card

1. Hierarchy → expandir `_HUD_Canvas` (ou Canvas principal da Home).
2. Identificar onde está o `Baú Grátis` button (ou outra row de botões de
   ação). Criar irmão dele.
3. Right-click no parent → **UI → Panel** → renomear `DailyChallengePanel`.
4. Rect Transform: dimensionar pra caber 1 botão + 1 texto. Sugestão:
   - Width `600`, Height `120`.
   - Posição: abaixo do chest button, com spacing.
5. Image: cor de fundo distintiva (ex: `#3A2E6B` — roxo escuro). Alpha `220`.

### 2.2 Botão "JOGAR DESAFIO"

1. Filho do `DailyChallengePanel` → **UI → Button - TextMeshPro** →
   renomear `DailyChallengeButton`.
2. Rect Transform: anchor left-center. Pos X `30`, Pos Y `0`. Width `260`, Height `90`.
3. Image: cor `#7B4FE5` (roxo claro) ou similar.
4. Expandir o button no Hierarchy → selecionar o `Text (TMP)` filho.
5. Text: `JOGAR DESAFIO`. Font Size: `36`. Bold. Color: branco.

### 2.3 Texto de stats

1. Filho do `DailyChallengePanel` → **UI → Text - TextMeshPro** →
   renomear `DailyChallengeStatsText`.
2. Rect Transform: anchor right-center. Pos X `-30`, Pos Y `0`. Width `280`, Height `90`.
3. Text: `Hoje: —   Best: —` (placeholder; código sobrescreve em runtime).
   Font Size: `26`. Alignment Right. Color: branco/cinza claro.

**Como saber se deu certo:**
- DailyChallengePanel visível no Scene View com botão + texto.
- Botão e texto estão posicionados sem sobreposição.

---

## Bloco 3 — Wire refs no `HomeScreenController`

1. Hierarchy → selecionar o GameObject que tem o `HomeScreenController`
   (geralmente o Canvas raiz ou um filho dedicado).
2. Inspector → achar seção **Daily Challenge (Fatia 6)**.
3. Arrastar:
   - **Daily Challenge Button** ← `DailyChallengeButton` do Hierarchy.
   - **Daily Challenge Text** ← `DailyChallengeStatsText` do Hierarchy.

**Como saber se deu certo:**
- Os 2 slots no Inspector estão preenchidos (não-None).
- Ao dar Play, o texto do `DailyChallengeStatsText` muda automaticamente
  pra `Hoje: —   Best: —` (ou com números se já jogou daily antes).

---

## Bloco 4 — Salvar e testar no Editor

1. **Ctrl+S** pra salvar HomeScene.
2. **Play.**
3. Olhar a Home:
   - Card "Daily Challenge" visível com botão JOGAR DESAFIO.
   - Texto "Hoje: —   Best: —".
4. Console esperado no boot:
   ```
   [Daily] Loaded — todayDate='' todayBest=0m bestEver=0m ()
   ```

### 4.1 Iniciar Daily Challenge

5. Clicar **JOGAR DESAFIO** na Home.
6. Cena `RailSwitchMVP` carrega. Console:
   ```
   [Daily] StartChallenge — seed=20260525   (← seed do dia, yyyyMMdd)
   [RailManager] Run seed=20260525 daily=True
   ```
7. Joga uma run. Anota distância final (ex: 230 m).
8. Morre → painel de GameOver aparece.
9. Reason text mostra: **"DAILY CHALLENGE — Dead End"** (ou OutOfBounds, etc).
10. Se foi a 1ª daily do dia → `★ NEW RECORD! ... DailyToday DailyEver`.
11. Console:
    ```
    [Daily] EndChallenge — meters=230 today=230 ever=230 brokeToday=True brokeEver=True
    ```

### 4.2 Voltar pra Home e ver stats

12. Clica **Home** no GameOver.
13. Console: `[Daily] ConsumeChallengeFlag — back to normal mode.`
14. Home agora mostra: `Hoje: 230 m   Best: 230 m`.

### 4.3 Retry com mesmo seed (Restart)

15. Clica **JOGAR DESAFIO** de novo.
16. Console: `[Daily] StartChallenge — seed=20260525` (mesma de antes!).
17. **O level deve ser IDÊNTICO** ao da primeira tentativa — switches,
    obstáculos, power-ups, tudo no mesmo lugar. (Deterministic seed.)
18. Joga melhor — ex: chega a 350 m. Morre.
19. Console: `[Daily] EndChallenge — meters=350 today=350 ever=350 brokeToday=True brokeEver=True`.
20. Home: `Hoje: 350 m   Best: 350 m`.

### 4.4 Validar que normal vs daily não conflitam

21. Clicar **JOGAR** (botão normal, NÃO o daily).
22. Console: `[RailManager] Run seed=<tickCount> daily=False`.
23. **O level deve ser DIFERENTE** do daily (seed random).
24. Morre, volta pra Home.
25. Card Daily continua mostrando `Hoje: 350 m   Best: 350 m`
    (normal run não afeta daily best).

### 4.5 DebugPanel — Daily section

26. Em qualquer cena, **F1** (ou botão DBG em mobile).
27. Painel mostra seção **Daily Challenge (Fatia 6)** com:
    - Seed do dia + modo ativo
    - Today best + Ever best + data
    - Botões: `Toggle Daily`, `Reset Today`, `Wipe All Daily`.
28. Click **Reset Today** → `todayBestM` zera (bestEver fica).
29. Click **Wipe All Daily** → tudo zera.

---

## Critérios de validação (Editor)

- [ ] `_DailyChallengeManager` na HomeScene, console mostra `[Daily] Loaded`.
- [ ] Card "Daily Challenge" visível na Home com botão + texto.
- [ ] Click no botão → cena Game carrega, console mostra `seed=YYYYMMDD daily=True`.
- [ ] GameOver mostra prefix "DAILY CHALLENGE — ..." na reason.
- [ ] EndChallenge atualiza Today e BestEver.
- [ ] Restart de daily → MESMO seed → MESMO level.
- [ ] Run normal não afeta daily best.
- [ ] GoHome de daily run consome flag (próxima entrada normal usa seed random).
- [ ] DebugPanel seção Daily mostra info correta + botões funcionam.

## Critérios de validação (build Android)

Mesmos da Editor. Build & Run, capturar log:
```powershell
$adb = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $adb logcat -c
& $adb logcat | Select-String "Daily|RailManager"
```

Procurar `[Daily] StartChallenge — seed=20260525` ao clicar JOGAR DESAFIO,
e `[RailManager] Run seed=20260525 daily=True` em seguida.

---

## Próximas fatias

- **Fatia 7** — Supabase: conta + project + tabelas, auth anônima, PDM
  sync. Daily Challenge ganha primary key (uuid do player + date) pra
  preparar pra leaderboard online.
- **Fatia 8** — Leaderboard online: POST daily result, GET top global +
  tua posição. Substitui o stub `leaderboardButton` na Home.

---

## Limitação conhecida do RNG

`UnityEngine.Random` é estático global. Outros sistemas (particles,
animator transitions com random, etc) podem consumir valores entre o
`Random.InitState` e a geração das primeiras rows, causando drift
mínimo no level entre tentativas. Pra MVP é aceitável — o "feel" do
level é determinístico mesmo com flutuações marginais.

Se virar problema (jogadores reclamando que daily não bate entre
dispositivos), migrar `ProceduralRailGenerator` pra usar
`System.Random` instance privada. ~8 callsites pra refatorar.
