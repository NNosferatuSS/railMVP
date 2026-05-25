# Setup — Fatia 5: Rewarded Ads (Unity Ads)

> **Premissa deste doc:** primeira vez configurando Unity Cloud, Unity Ads e
> linkando um projeto Unity a um dashboard. Cada passo descreve onde clicar,
> o que você deve ver, e como saber se deu certo antes de avançar.

Substitui o stub do Daily Ad Chest e adiciona botão "Watch Ad +N coins" no
GameOver. Tudo via Unity Ads SDK 4.x (`com.unity.ads`).

---

## Mapa do que vamos fazer

1. **Conta Unity + Organization** — você provavelmente já tem (mesma conta do Hub).
2. **Criar/linkar um Unity Cloud Project** — dá um ID único pro jogo nos serviços da Unity.
3. **Linkar o Editor ao Project** — Edit → Project Settings → Services.
4. **Habilitar Monetization no dashboard** — pega os Game IDs.
5. **Resolver o package `com.unity.ads` no Editor** — automático ao abrir Unity.
6. **Criar `_AdsManager` na cena HomeScene** — preencher Game IDs no Inspector.
7. **Adicionar botão "Watch Ad +N coins" no `_GameOver` panel** — wire no `GameOverController`.
8. **Testar em Play Mode no Editor** — Test Mode = true mostra placeholder ads.

Cada um dos 8 passos abaixo tem subitens. Faça **na ordem**. Se algum passo falhar, **pare e debug** — pular adiante torna o erro mais difícil de localizar.

---

## Passo 1 — Conta Unity + Organization (provável que já exista)

Você já tem isso se logou no Unity Hub alguma vez. Pra confirmar:

1. Abrir o Unity Hub (ícone na taskbar / menu start).
2. Canto superior direito: deve ter seu avatar/email logado. Se não, clicar
   em **Sign in** e logar.
3. Tomar nota do email — é o mesmo da Unity Organization. Cada conta vem com
   uma "Personal Organization" automática (geralmente nome `Personal` ou seu nome).

**Como saber se deu certo:** Unity Hub mostra seu nome/email no canto.

---

## Passo 2 — Criar/linkar um Unity Cloud Project

O "Project" no contexto do Unity Cloud é um registro central que conecta
seu jogo aos serviços Unity (Ads, Analytics, Cloud Build, etc). É **diferente**
do projeto Unity local (o folder `D:\Perforce\Pessoal\railMVP\`).

Você tem **duas opções**: criar pelo dashboard primeiro, ou deixar o Editor
criar automático no Passo 3. **Recomendo deixar o Editor criar** — menos
passos manuais.

→ **Pule pro Passo 3.** Quando você sign in + clica "Create" no painel
Services, o Editor mesmo cria o Project no Cloud com o nome do projeto local.

(Se preferir criar manual: `cloud.unity.com` → menu lateral "Projects" →
botão "Create project" → nome `railMVP`. Mas o Editor faz isso pra você.)

---

## Passo 3 — Linkar o Editor ao Unity Cloud Project

Aqui o Editor vai conversar com o Cloud pra associar este projeto local a
um Project ID que o dashboard reconhece.

1. **Abrir o projeto no Unity Editor** (Unity Hub → Projects → click railMVP).
2. Aguardar até a janela do Editor abrir totalmente (importa packages, etc).
   Se o Unity reclamar de algum erro de compile relacionado a `UnityEngine.Advertisements`,
   é esperado neste momento porque ainda não resolvemos o package — siga em frente.
3. Menu superior: **Edit → Project Settings...** (no Mac é `Unity → Settings`).
4. Na janela que abre, painel da esquerda: rolar até **Services**. Clicar.
5. Se aparecer botão **Sign in**, clicar e logar com a mesma conta do Hub.
   Pode pedir confirmação no browser — confirme.
6. Após sign in, vai aparecer um seletor de **Organization**. Selecione a sua
   (geralmente só tem uma).
7. Embaixo: dois botões grandes — **Use an existing Unity project ID** ou
   **Create project ID**. Como é a primeira vez:
   - Clicar em **Create project ID**.
   - O nome dele será baseado no folder do projeto local (`railMVP`).
   - Aguardar uns 2-5s. Aparece um Project ID (UUID tipo `a1b2c3d4-...`).

**Como saber se deu certo:**
- O painel Services mostra:
  - Project Name: `railMVP` (ou similar)
  - Organization: seu nome
  - **Project ID**: um UUID
- Aparece uma lista de serviços do lado (Cloud Build, Analytics, Cloud Diagnostics, etc).

**Configurar COPPA (obrigatório pra Unity Ads):**
- Ainda no painel Services, vai aparecer uma pergunta sobre crianças
  ("Is your project directed at children..."). Selecione conforme aplicável:
  - "No" se o jogo NÃO é primariamente pra crianças < 13 anos. Provavelmente o seu caso.
  - "Yes" só se for explicitamente um jogo infantil.
- Isso afeta a política de targeting de ads e é exigido pela Unity/COPPA/GDPR.

---

## Passo 4 — Habilitar Monetization no dashboard + pegar Game IDs

1. Abrir o browser em `dashboard.unity3d.com`.
2. Logar (mesma conta). Se for primeira vez, pode redirecionar pro Unity Cloud.
3. Sidebar / menu — procurar **Projects** e selecionar `railMVP`.
4. Dentro do project, sidebar — clicar em **Monetization** (pode estar dentro
   de **Solutions** ou **Grow**, o layout muda).
5. Se for a primeira vez, vai pedir pra **enable** a Monetization no project.
   Aceitar termos (você precisa concordar com Unity Ads Publisher Agreement).
6. Configuração inicial vai perguntar:
   - **Platforms supported**: marcar Android. iOS opcional (só Android funciona
     no seu fluxo agora).
   - **COPPA / Children**: confirmar a resposta dada no Passo 3.
   - **Test mode default**: deixar test mode habilitado por padrão.
7. Após enable, vai aparecer um dashboard com cards: **Game IDs**, **Ad Units**,
   **Analytics**, **Revenue** (vazio por enquanto).

**Pegar os Game IDs:**

8. Clicar em **Game IDs** (ou Settings → Game IDs).
9. Você vai ver dois IDs gerados automaticamente:
   - **Android Game ID**: número de 7 dígitos (ex: `1234567`).
   - **iOS Game ID**: outro número de 7 dígitos.
10. Anote os dois (copy/paste num notepad). Vamos precisar no Passo 6.

**Confirmar Placement IDs (Ad Units):**

11. Sidebar → **Ad Units** (ou **Placements**).
12. Você deve ver entradas pre-criadas:
    - `Rewarded_Android` (Android platform, Rewarded type)
    - `Rewarded_iOS` (iOS platform, Rewarded type)
    - Possivelmente também `Interstitial_Android`, `Banner_Android` — ignore, não usamos.
13. Se NÃO existirem `Rewarded_Android` e `Rewarded_iOS`, criar manualmente:
    - Botão **Add Ad Unit**.
    - Name: `Rewarded_Android`, Platform: Android, Ad format: Rewarded.
    - Repetir pra iOS.

**Como saber se deu certo:**
- Você tem 2 Game IDs anotados.
- A página de Ad Units mostra `Rewarded_Android` e `Rewarded_iOS` na lista.

---

## Passo 5 — Resolver o package `com.unity.ads` no Editor

O package já foi adicionado pelo Claude no `Packages/manifest.json`. Quando
você abriu o Editor (Passo 3), Unity já tentou resolver. Vamos confirmar.

1. No Editor: **Window → Package Manager**.
2. Topo da janela: dropdown filter, selecionar **Packages: In Project**.
3. Procurar **Advertisement** ou **Unity Ads** na lista.
4. Deve aparecer **Advertisement** versão `4.4.2` ou similar.
5. Se aparecer ⚠️ ou erro vermelho:
   - Clicar nela → ver mensagem.
   - Possíveis erros:
     - "Cannot find package" → muito raro; tenta clicar **Refresh** no canto inferior esquerdo.
     - Versão não disponível → no canto superior direito do detalhe, dropdown
       de versões, escolher a mais recente disponível (4.x.x).
6. Após resolução, no Console (Window → General → Console) — não deve ter
   erros de compile sobre `UnityEngine.Advertisements`. Pode ter warnings
   sobre obsoletos — ignorar.

**Como saber se deu certo:** Package Manager mostra "Advertisement" instalado,
Console sem errors vermelhos relativos a Advertisement.

---

## Passo 6 — Criar `_AdsManager` GameObject na HomeScene

Agora a parte na cena. O `AdsManager` é singleton DontDestroyOnLoad — você
só precisa adicionar em UMA cena (a que abre primeiro). HomeScene é a entrada.

1. No Editor: **File → Open Scene** → escolher `Assets/Scenes/HomeScene.unity`.
2. Aguardar a cena abrir.
3. Painel **Hierarchy** (esquerda) — geralmente vê o `_PlayerDataManager`,
   `_MissionTracker`, `_DailyLogin`, `_HUD_Canvas`, etc, na cena.
4. **Botão direito em área vazia do Hierarchy → Create Empty**.
5. Renomear o novo GameObject: clicar 1 vez, F2 (ou clicar de novo) — nome: `_AdsManager`.
6. Com `_AdsManager` selecionado, painel **Inspector** (direita):
   - Botão **Add Component** na parte de baixo.
   - Digitar `AdsManager` na busca.
   - Selecionar `Ads Manager` (deve aparecer como `RailSwitchMVP.Meta`).
7. O componente aparece com vários campos. Preencher:

   **Unity Ads Game IDs (do dashboard):**
   - **Android Game ID** → cole o número Android que anotou no Passo 4.
   - **iOS Game ID** → cole o iOS. Se Android-only, pode deixar como está.

   **Placement IDs:**
   - **Rewarded Android Id** → deixar `Rewarded_Android` (default já correto).
   - **Rewarded Ios Id** → deixar `Rewarded_iOS`.

   **Behavior:**
   - **Test Mode** → ✅ marcado (CHECKBOX MARCADO durante dev).
   - **Verbose Logs** → ✅ marcado (vê cada callback no Console).

8. **Importante — salvar a cena**: Ctrl+S (ou File → Save).

**Como saber se deu certo:**
- Hierarchy mostra `_AdsManager` na lista.
- Inspector com o AdsManager preenchido, Game IDs visíveis.
- Cena salva (asterisco do nome da scene some no topo).

---

## Passo 7 — Adicionar botão "Watch Ad +N coins" no `_GameOver` panel

Esta é a parte mais granular. Vamos criar um Button novo no painel de Game Over
e plugar nos slots do `GameOverController`.

1. **File → Open Scene** → `Assets/Scenes/RailSwitchMVP.unity`.
2. No Hierarchy, expandir os filhos do `_HUD_Canvas` (ou onde quer que o
   GameOver panel viva). Procurar por algo tipo `_GameOver` ou
   `GameOverPanel`. Vai estar desativado por default (cinza no Hierarchy).

   Se você não achar, busque na barra de busca do Hierarchy: "GameOver".
   Clicar pra revelar.

3. Pra editar a UI de game over, você precisa que o painel esteja **visível**
   no Editor temporariamente:
   - Selecionar `_GameOver` (ou o panel filho dele que tem os botões).
   - No Inspector, no topo, marcar a checkbox ao lado do nome → ativa o painel.
   - Agora você vê o painel no Scene View.
   - **Lembrar de desmarcar depois!** Senão o painel aparece de cara no Play.

4. Achar o **Restart Button** existente dentro do painel (procurar `RestartButton`
   na hierarquia ou olhar no painel visualmente).
5. Right-click no parent do RestartButton (geralmente é um Layout Group ou
   o panel vertical de botões) → **UI → Button - TextMeshPro**.
   - Se Unity perguntar pra importar TMP Essentials, **importar**.
6. Renomear o botão novo: `WatchAdButton`.
7. Com `WatchAdButton` selecionado, **Inspector**:
   - **Rect Transform**: ajustar a posição se precisar. Se está num Vertical
     Layout Group, vai posicionar automaticamente.
   - Procurar o filho `Text (TMP)` dentro do botão (expandir o WatchAdButton
     no Hierarchy).
   - Selecionar o `Text (TMP)` filho → Inspector → campo **Text** → deixar
     qualquer placeholder, tipo "Watch Ad". O código sobrescreve em runtime.

8. **Plugar no GameOverController:**
   - Selecionar o `_GameOver` GameObject (o que tem o componente `GameOverController`).
   - No Inspector, achar a seção **Rewarded Ad — 2x coins (Fatia 5)**.
   - **Double Coins Button**: arrastar `WatchAdButton` (do Hierarchy) pra esse slot.
   - **Double Coins Button Text**: arrastar o `Text (TMP)` filho do WatchAdButton
     pra esse slot.

9. **Desmarcar o panel** (revert a ativação temporária do passo 3):
   - Selecionar o `_GameOver` (ou panel) → Inspector → desmarcar a checkbox
     no topo. O painel volta a ficar desativado.
   - Verificar visualmente no Scene View — o painel não deve estar visível.

10. **Salvar**: Ctrl+S.

**Como saber se deu certo:**
- Hierarchy mostra `WatchAdButton` como filho do panel de Game Over.
- Inspector do `_GameOver` mostra os 2 slots da seção "Rewarded Ad" preenchidos.
- Painel está desativado (não aparece quando você dá Play).

---

## Passo 8 — Testar em Play Mode no Editor

Test Mode = true significa que o SDK mostra um **placeholder ad** (overlay
roxo da Unity com um botão de close/skip + um timer). Não precisa de internet
real nem espera 24h de propagação.

### 8.1 — Verificar inicialização

1. Voltar pra HomeScene (File → Open Scene → HomeScene).
2. Clicar **Play** (botão de triângulo no topo).
3. **Abrir Console** (Window → General → Console).
4. Filtros do Console: garantir que **Log** (não só Error) está habilitado
   (ícones na barra do Console).
5. Logs esperados, em ordem:
   - `[Ads] Initialize gameId=1234567 test=True`
   - `[Ads] Initialization complete.`
   - `[Ads] Load Rewarded_Android`
   - `[Ads] Loaded Rewarded_Android`

Se você ver `Initialization failed` → Game ID errado. Re-confere com o dashboard.

### 8.2 — Testar Chest Ad

6. Na Home (com o jogo rodando em Play), olhar o botão **Baú Grátis**:
   - Se você nunca reclamou hoje + AdsManager pronto → aparece "Baú Grátis +150".
   - Se já reclamou → "Baú já reclamado hoje". Pra resetar:
     - Sair do Play.
     - Selecionar `_DailyLogin` na Hierarchy.
     - Botão direito no componente DailyLoginManager → não tem context menu;
       use o DebugPanel: durante Play, **F1** (em build mobile clica o botão DBG).
     - Achar seção Daily Login → botão "Force Chest Available".
   - Voltar pro Play, botão volta a aparecer.
7. Clicar o **Baú Grátis +150**.
8. Aparece overlay roxo da Unity (test ad placeholder) com um botão tipo
   "Close" no canto. Pode esperar o timer ou clicar em "Close" se aparecer
   um botão pra concluir.
9. Após o ad fechar:
   - Se **completou** → log `[Ads] Show complete Rewarded_Android: COMPLETED`
     + `[DailyChest] Claimed → +150 coins`. Coins na Home sobem.
   - Se **skipou** → log `[Ads] Show complete: SKIPPED` + `[Home] Chest ad failed/skipped`.
     Coins NÃO sobem.

### 8.3 — Testar GameOver "Watch Ad"

10. Na Home, clicar **JOGAR**.
11. Jogar uma run, coletar **algumas moedas** (>0), morrer.
12. Após a death sequence, painel de GameOver aparece. Olhar pelo botão
    **"Watch Ad +N coins"** onde N = quantas coins você fez no run.
13. Se você fez 0 coins, o botão não aparece (intencional — não há nada pra dobrar).
14. Clicar **Watch Ad +N**.
15. Mesmo overlay roxo de test ad. Aguardar conclusão.
16. Se completou:
    - Log: `[GameOver] 2x coins granted: +N extra.`
    - O texto do `Coins:` no painel muda pra `Coins: 2N (2x via ad)`.
    - Botão Watch Ad some.
17. Clicar **Restart** ou **Home**. Voltando pra Home, o display de Coins
    da PDM deve ter aumentado em `2N` (run base + ad bonus).

### Critérios de validação (Editor)

- [ ] Console mostra `[Ads] Initialization complete` + `[Ads] Loaded Rewarded_Android` nos primeiros segundos.
- [ ] Chest aparece quando disponível + ad ready; clicar dispara placeholder ad.
- [ ] Completar chest ad → +150 coins. Skipar → sem reward.
- [ ] GameOver com runCoins > 0 → "Watch Ad +N" aparece.
- [ ] Completar ad GameOver → coins dobrados, botão some.
- [ ] Tentar clicar Watch Ad de novo no mesmo run → não aparece.
- [ ] Restart/Home transfere coins corretamente pra PDM.

---

## Passo 9 (opcional, depois) — Build Android e testar no device

1. **File → Build Settings** (ou Build Profiles em Unity 6).
2. Platform = Android (já configurado). Marcar **Development Build** ✅.
3. Conectar device USB (com USB Debugging ativado).
4. **Build And Run**.
5. No device, repetir os critérios de validação acima.
6. Com Test Mode = true, ainda vê test ads no device.

**Logs do device:** PowerShell no PC com device conectado:
```powershell
adb logcat -s Unity
```
Procura linhas `[Ads]` e `[Home]` / `[GameOver]`.

---

## Antes de shipar pra Google Play

Os passos pra preparar produção (Test Mode off, keystore, Privacy Policy,
GDPR consent, Play Console setup, soft launch, etc) foram movidos pro
checklist dedicado em **`Docs/Pre_Launch_Checklist.md`**.

Não faz parte da Fatia 5 — vai ser consultado quando chegar a hora do
primeiro upload em produção.

---

## Troubleshooting comum

### "Initialization failed: INVALID_ARGUMENT — invalid gameId"
Game ID errado no Inspector. Reconfere no dashboard → Game IDs e cola de novo.
Tem que ser só números, sem espaços / aspas.

### "Failed to load: NO_FILL"
Normal em test mode às vezes. SDK tenta reload sozinho — espera 30-60s.
Se persistir, mude pra outro device de teste ou aceite que reload eventual rola.

### Botão Chest não aparece nunca
- Confere log se `[Ads] Loaded Rewarded_Android` rolou.
- Se não rolou: AdsManager não inicializou (verifica Game ID).
- Confere se já reclamou hoje (`[DailyChest] Already claimed today`).
- Reseta via F1 → "Force Chest Available".

### Botão Watch Ad nunca aparece no GameOver
- Confirma que você tem `_runCoins > 0` na run (precisa coletar ao menos
  uma moeda).
- Confirma que o slot `Double Coins Button` está preenchido no Inspector
  do `_GameOver`.
- Confirma que ad está pronto (`IsRewardedReady` true).

### Compile error: "type or namespace UnityEngine.Advertisements not found"
Package não resolveu. Passo 5 — Package Manager, verificar `Advertisement`
instalado. Force resolve: Edit → Project Settings → Package Manager → "Reset"
ou deletar `Library/` e abrir o projeto de novo (Unity re-importa tudo,
~5min).

### Coins não persistem após Restart
Não é problema do Ads — é problema da PDM. Confere se `_PlayerDataManager`
está na HomeScene (no Hierarchy). Sem ele, AddCoins é no-op silencioso.

---

## API exposta pelo AdsManager (pra referência futura)

```cs
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; }

    public bool IsInitialized { get; }
    public bool IsRewardedReady { get; }
    public event Action<bool> OnRewardedReadyChanged;

    public bool TryShowRewarded(Action onSuccess, Action onFailed = null);

    // Debug
    public void DebugLogState();
}
```

Padrão de uso por um caller (ex: futuro novo botão de "skip cooldown"):
```cs
var ads = AdsManager.Instance;
if (ads == null || !ads.IsRewardedReady) { HideMyButton(); return; }

ads.TryShowRewarded(
    onSuccess: () => GrantTheReward(),
    onFailed:  () => Debug.Log("Ad foi skipado")
);
```

Subscrever no evento pra ocultar/mostrar o botão quando o load termina:
```cs
ads.OnRewardedReadyChanged += ready => myButton.gameObject.SetActive(ready);
```

---

## Decisão técnica: classic Unity Ads SDK, não LevelPlay

LevelPlay (mediação com AdMob/etc) é o caminho recomendado long-term pela
Unity. Adicionei dependência só do `com.unity.ads` (clássico) porque:
- Setup simpler (uma SDK só).
- API estável e bem documentada.
- Pra MVP com Unity como única source de ad, suficiente.
- Migração futura pra LevelPlay é localizada — só `AdsManager.cs` precisa
  ser reescrito; a API pública (`TryShowRewarded`, `IsRewardedReady`) fica
  idêntica e callers não mudam.
