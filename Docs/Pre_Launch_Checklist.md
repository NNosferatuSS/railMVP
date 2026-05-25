# Pre-Launch Checklist — Google Play / App Store

> Checklist do que precisa estar pronto **antes do primeiro upload em produção**.
> Nada aqui é pra fazer agora — é um lembrete pra você não ser surpreendido
> quando chegar a hora. Cada item linka pro lugar onde a decisão foi tomada.

Cada seção tem: **Por que**, **Onde fazer**, **Critério de pronto**.

---

## 1. Configuração de Ads pra produção

Atual: AdsManager está com `Test Mode = true` pra dev. Em produção, ads
reais precisam ser servidos pra gerar revenue.

- [ ] **`_AdsManager` na cena → Test Mode = false** (desmarcar checkbox).
- [ ] Confirmar Game IDs no Inspector batem com os do dashboard
  (`dashboard.unity3d.com` → Monetization → Game IDs).
- [ ] Verificar `verboseLogs = false` em release (logs detalhados poluem
  Logcat em prod).

**Critério de pronto:** Build de produção mostra ad REAL (não overlay roxo
placeholder) quando você dispara chest ou GameOver 2x.

Ver `Docs/AdsFatia5_Setup.md` pra contexto da Fatia 5.

---

## 2. Keystore Android (assinatura da APK)

Toda APK publicada no Play Store precisa ser assinada com uma chave digital.
Sem keystore, Play Console rejeita o upload no primeiro pixel.

**A chave é PRA SEMPRE.** Uma vez publicado, todas as updates futuras
precisam ser assinadas pela MESMA keystore. **Perder o keystore = perder
o app**. Backup obrigatório.

- [ ] Criar keystore no Editor: **File → Build Settings → Player Settings
  → Publishing Settings → Keystore Manager → Keystore... → Create New →
  In Custom Location**.
  - Salvar em local FORA do projeto (ex: `D:\Keystores\railMVP.keystore`).
  - **Senha do keystore** + **senha da chave**: escolher forte, anotar em
    password manager. Sem essas, não consegue assinar updates.
- [ ] Configurar Player Settings → Publishing Settings:
  - Custom Keystore = true.
  - Apontar pro arquivo criado.
  - Selecionar Alias.
- [ ] **Backup do keystore em 2 lugares** (cloud + drive externo).
- [ ] **Backup das senhas** em password manager.

**Critério de pronto:** Build → Android → APK assinada (não aparece
warning de "unsigned"). Você consegue rodar o build no device E o Play
Console aceita upload de teste interno.

---

## 3. Privacy Policy URL

Unity Ads **exige** um link público de Privacy Policy. Google Play exige
também (formulário do Play Console). Sem isso, o app não passa review.

- [ ] Escrever (ou gerar via template) um Privacy Policy cobrindo:
  - Que tipo de dado é coletado (Unity Ads coleta IDFA/AAID, analytics).
  - Pra que é usado (servir ads, melhorar o jogo).
  - Direitos do user (delete data, opt-out).
  - Email de contato pra requests.
- [ ] Hospedar em uma URL pública (GitHub Pages, Netlify free, etc).
- [ ] Colar a URL:
  - No dashboard.unity3d.com → Project Settings → Privacy Policy URL.
  - No Play Console → App content → Privacy policy.

**Geradores gratuitos úteis:**
- termsfeed.com
- privacypolicytemplate.net
- iubenda.com (free tier)

**Critério de pronto:** URL pública abre em qualquer navegador e mostra
o documento (não 404).

---

## 4. GDPR / consent dialog (se publicar na UE)

Se o app for distribuído em qualquer país da UE (Play Store default = mundo
todo, inclui UE), você precisa pedir consent pro user antes de personalizar
ads. Lei é GDPR + ePrivacy.

Unity Ads tem o **Consent SDK** built-in que oferece dialog padronizado.

- [ ] Adicionar consent dialog no primeiro launch.
  - Opção A: integrar com `MetaData` API do Unity Ads (consent flag).
  - Opção B: usar Google's UMP (User Messaging Platform) — funciona pra
    AdMob + outras nets.
  - Opção C: rolar próprio dialog UI + setar consent na inicialização do AdsManager.
- [ ] Se não quiser lidar agora: na Play Console restringir distribuição
  pra fora da UE (Países: deselecionar EU). Permite shipar sem consent
  e adicionar depois.

**Critério de pronto:** Primeira vez que abre o app, user vê dialog
"Allow personalized ads?" com Yes/No. Escolha é salva e respeitada nos
ads servidos.

---

## 5. Aguardar fill rate estabilizar (24-48h pós-launch)

Unity Ads + qualquer outro ad network começam com **fill rate baixo** (% de
requests que retornam ad) em projetos novos. Pode levar 24-48h pra estabilizar
em 70-90%+.

- [ ] Antes do soft launch: testar 1 dia inteiro com fill rate baixo
  e confirmar que o jogo funciona (botões somem corretamente quando ad
  não tá pronto, sem crash).
- [ ] Monitorar dashboard.unity3d.com → Monetization → Revenue na 1ª semana.

**Critério de pronto:** Fill rate ≥ 80% sustentado por 24h. Revenue > 0.

---

## 6. Player Settings — outros tunings de release

- [ ] **Bundle Version Code**: increment pra cada upload no Play Console.
  Player Settings → Other → Bundle Version Code.
- [ ] **Version**: semver visível pro usuário (1.0.0, 1.0.1...).
- [ ] **Minimum API Level**: ≥ 26 (Android 8.0). Play Console exige um mínimo.
- [ ] **Target API Level**: a mais recente disponível no Editor (Play Console
  exige updates anuais).
- [ ] **Scripting Backend**: IL2CPP (obrigatório pra ARM64 e pra Google Play).
- [ ] **Target Architectures**: ARM64 ✅ (obrigatório). ARMv7 opcional.
- [ ] **Splash screen**: customizar ou desabilitar (Player Settings → Splash Image).
  Unity default tem logo da Unity — pode ser feio.
- [ ] **Icon**: arte do app icon em todas as resoluções (Adaptive Icon).
- [ ] **Development Build = false** em build de produção (vs dev). Sem isso,
  os debug panels (F1/F2) e MobileDebugButtons ficariam visíveis pro user.

---

## 7. Play Console — primeiro setup

A primeira vez no Play Console é cara também: $25 USD de taxa única.

- [ ] Conta de developer em `play.google.com/console/`.
- [ ] Pagar $25 USD (one-time).
- [ ] Verificação de identidade (D.I. + cartão).
- [ ] Criar app:
  - App name, default language, app/game, free/paid.
- [ ] Preencher **Store listing**:
  - Short description (80 chars).
  - Full description (4000 chars).
  - Screenshots (mín. 2, ideal 4-8, várias proporções).
  - Feature graphic (1024x500).
  - App icon (512x512).
  - Categoria, content rating questionnaire.
- [ ] **Internal testing track** primeiro (em vez de Production):
  - Pequeno círculo de testers (lista de emails).
  - Permite iterar sem afetar review do public release.

---

## 8. Soft launch antes de production

Recomendo NÃO ir direto pra "Production" no Play Console. Caminho seguro:

1. **Internal testing**: você + 2-3 amigos via lista de emails.
2. **Closed testing**: ~10-20 testers fora do círculo próximo.
3. **Open testing**: público pode entrar via link, mas marca como "beta".
4. **Production**: liberação geral.

Ad revenue e crash analytics aparecem em todos os tracks. Internal testing
não conta pra rankings, mas conta pra ad fill rate aprender o app.

---

## Cronograma sugerido (quando chegar a hora)

| Quando | O que |
|---|---|
| T-2 semanas | Itens 1-3 (Ads prod, keystore, Privacy Policy). |
| T-1 semana | Itens 4-6 (GDPR, Player Settings). |
| T-3 dias | Item 7 (Play Console + Internal testing). |
| T-0 | Internal testing live, monitorar 3-7 dias. |
| T+1 semana | Closed testing. |
| T+2 semanas | Open testing / Production. |

Não é regra — é só estrutura. Cada item é independente.

---

## Itens NÃO necessários no MVP (mencionados aqui pra você não confundir)

- ❌ ProGuard / R8 obfuscation — nice-to-have, não obrigatório.
- ❌ Cloud save / login social — fica pra spec §11 (Supabase).
- ❌ In-App Purchases — não tem no MVP.
- ❌ Analytics avançado (eventos custom) — Unity Analytics já dá baseline.
- ❌ Localização (idiomas) — pode shipar só em pt-br ou en inicialmente.
- ❌ Suporte a tablets / múltiplas resoluções — Canvas Screen Space Overlay
  cobre.
