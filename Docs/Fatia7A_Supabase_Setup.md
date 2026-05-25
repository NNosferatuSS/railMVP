# Fatia 7A — Supabase setup + Anonymous Auth

> **Premissa deste doc:** primeira vez configurando Supabase. Cada passo
> descreve onde clicar/colar, o que esperar ver, e como saber se deu certo.
>
> **Códigos prontos:** `SupabaseClient.cs` (wrapper REST + auth coroutines),
> `AuthManager.cs` (singleton anon sign-in + refresh + persistência),
> `DebugPanelController` com seção Auth.

**Tempo estimado:** 20-30 min (a maior parte é setup de dashboard;
adicionar componentes no Unity leva 5min).

---

## Mapa do que vamos fazer

1. **Criar conta Supabase** (free tier).
2. **Criar Project** (`railmvp` ou similar).
3. **Rodar SQL** pra criar a tabela `players` + trigger de `updated_at`.
4. **Configurar RLS** (Row Level Security) — cada user só lê/escreve a própria row.
5. **Habilitar Anonymous Sign-ins** em Authentication → Providers.
6. **Copiar URL + anon key** do Project Settings.
7. **Adicionar `_AuthManager`** na HomeScene, colar URL/anon key no Inspector.
8. **Testar em Editor + build Android**.

Faça **na ordem**. Cada passo é pré-requisito do próximo.

---

## Passo 1 — Criar conta Supabase

1. Abrir `https://supabase.com` no browser.
2. Canto superior direito: **Start your project** (ou **Sign in** se já tem conta).
3. Sign in com **GitHub** ou email. Free tier sem cartão.
4. Após login, vai pro `supabase.com/dashboard` com lista de organizations
   (deve ter uma com o nome da sua conta).

**Como saber se deu certo:** dashboard mostra "Welcome to Supabase" com botão "New project".

---

## Passo 2 — Criar Project

1. Botão **New project**.
2. Preencher:
   - **Organization**: a sua.
   - **Project name**: `railmvp` (lowercase, sem espaços).
   - **Database password**: gera uma forte e **salva num gerenciador de senhas**.
     Não vai ser usada pelo app (app usa anon key), mas precisa pra acessar o
     banco direto se um dia precisar.
   - **Region**: **South America (São Paulo)** ou similar pra menor latência do device BR.
   - **Pricing plan**: Free.
3. **Create new project**. Aguardar ~2min pro provisioning.

**Como saber se deu certo:** dashboard do project abre. Sidebar mostra
Table Editor, SQL Editor, Authentication, Storage, Edge Functions, etc.

---

## Passo 3 — Criar tabela `players` via SQL Editor

1. Sidebar → **SQL Editor**.
2. **New query** (botão verde no canto superior direito).
3. Cole o SQL abaixo **inteiro** no editor:

```sql
-- Tabela players: mirror local-friendly do PlayerDataManager + DailyChallengeManager.
create table if not exists public.players (
  id uuid primary key references auth.users(id) on delete cascade,

  -- Core PDM
  coins int not null default 0,
  best_distance int not null default 0,
  best_coins int not null default 0,
  best_tier int not null default 0,
  best_time real not null default 0,
  total_runs int not null default 0,
  equipped_char int not null default 0,
  owned_chars text not null default '0',
  player_name text not null default 'Player',

  -- Daily Challenge
  daily_today_date text,
  daily_today_best_m int not null default 0,
  daily_best_ever_m int not null default 0,
  daily_best_ever_date text,

  -- Sync metadata
  updated_at timestamptz not null default now()
);

-- Trigger pra manter updated_at sempre atualizado em update.
create or replace function public.touch_updated_at()
returns trigger language plpgsql as $$
begin
  new.updated_at = now();
  return new;
end; $$;

drop trigger if exists touch_players_updated_at on public.players;
create trigger touch_players_updated_at
  before update on public.players
  for each row execute function public.touch_updated_at();
```

4. **Run** (botão verde, ou Ctrl+Enter).
5. Output esperado: **Success. No rows returned**.

**Como saber se deu certo:**
- Sidebar → **Table Editor** → schema `public` → vê `players` na lista.
- Click em `players` → tabela vazia mas com todas as colunas listadas.

---

## Passo 4 — RLS (Row Level Security) policies

Sem RLS, qualquer cliente com a anon key consegue ler/escrever qualquer
row. RLS faz cada user só acessar a própria row (autenticada via JWT).

1. **SQL Editor** → **New query**.
2. Cole:

```sql
-- Habilita RLS na tabela
alter table public.players enable row level security;

-- Política de SELECT — user só lê a própria row
create policy "Players can read own row" on public.players
  for select using (auth.uid() = id);

-- Política de INSERT — só pode inserir row com id=próprio uuid
create policy "Players can insert own row" on public.players
  for insert with check (auth.uid() = id);

-- Política de UPDATE — só pode atualizar a própria row
create policy "Players can update own row" on public.players
  for update using (auth.uid() = id) with check (auth.uid() = id);
```

3. **Run**. Output: **Success. No rows returned**.

**Como saber se deu certo:**
- Table Editor → `players` → ícone de cadeado no header (RLS habilitado).
- Sidebar → **Authentication** → **Policies** → vê 3 entries pra `players`.

---

## Passo 5 — Habilitar Anonymous Sign-Ins

Por padrão Supabase não permite anon sign-in (require email/OAuth).
Pra MVP queremos UUID anônimo sem fricção de cadastro.

1. Sidebar → **Authentication** → submenu **Providers** (ou **Settings**
   dependendo do layout).
2. Scroll até achar **Anonymous Sign-Ins** (geralmente no final, marcado
   "experimental" em alguns layouts).
3. Toggle ✅ **Enable Anonymous Sign-Ins**.
4. **Save** (pode estar no rodapé da página).

**Como saber se deu certo:**
- A página confirma "Anonymous sign-ins enabled" (ou o toggle fica verde).
- Em **Authentication → Users** vai aparecer os anon users após o primeiro signup do app.

---

## Passo 6 — Copiar URL + Anon Key

1. Sidebar → **Project Settings** (ícone de engrenagem) → **API**.
2. Você verá:
   - **Project URL**: `https://xxxxxxxx.supabase.co` — copia.
   - **Project API Keys**:
     - **anon public**: começa com `eyJhbGc...` — copia. **Esta é a chave pública, safe pra colocar no cliente.**
     - ⚠️ **NÃO USE** `service_role` no cliente — ela bypassa RLS e expõe tudo. Só pra backend admin.

3. Cole as duas em algum lugar temporário (notepad) — vão ser usadas no Passo 7.

**Como saber se deu certo:** você tem dois valores anotados:
- URL: `https://xxxxxxxx.supabase.co`
- Anon key: `eyJhbGciOi...` (string longa, ~200 chars)

---

## Passo 7 — Adicionar `_AuthManager` na HomeScene

1. Unity Editor → **File → Open Scene** → `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → área vazia → **Create Empty** → renomear `_AuthManager`.
3. Inspector → **Add Component** → digitar `AuthManager` → selecionar
   (`RailSwitchMVP.Meta.AuthManager`).
4. Preencher:
   - **Supabase Url** → cola a URL do Passo 6.
   - **Supabase Anon Key** → cola o anon key.
   - **Auto Sign In On Awake** → ✅ marcado.
   - **Verbose Logs** → ✅ marcado.
5. **Ctrl+S** pra salvar a cena.

**Como saber se deu certo:**
- Hierarchy mostra `_AuthManager` (irmão de `_PlayerDataManager`, `_AdsManager`, etc).
- Inspector com URL e Anon Key preenchidos.

---

## Passo 8 — Testar em Editor + Android build

### 8.1 — Editor

1. **Play** na HomeScene.
2. Olhar o Console. Sequência esperada (primeira execução, sem refresh token salvo):
   ```
   [Auth] No saved session, creating new anonymous user.
   [Auth] Signed up anon. UserId=12345678… expires_in=3600s
   ```
3. Voltar ao Supabase dashboard → **Authentication → Users**.
4. Refresh a página. Deve aparecer **1 user** com ID começando em `12345678…`,
   marcado `is_anonymous: true`.

### 8.2 — Persistência de sessão

5. Pare o Play e dê **Play** novamente.
6. Console deve mostrar:
   ```
   [Auth] Found saved session for user 12345678…, refreshing...
   [Auth] Refreshed session. UserId=12345678… expires_in=3600s
   ```
7. Sem novo user no dashboard (mesma sessão).

### 8.3 — DebugPanel

8. Durante Play, **F1** (ou botão DBG mobile).
9. Painel mostra seção **Auth (Fatia 7A)** com:
   - `auth'd=True | uid=12345678...`
   - Botões: **Log status**, **Re-auth**, **Sign out**.
10. **Re-auth** → dashboard ganha NOVO user (anon antigo continua lá, novo é criado).
11. **Sign out** → uid fica vazio. Próximo Play vai criar outro novo anon.

### 8.4 — Build Android

12. **Build & Run** pra device.
13. Capturar logs:
    ```powershell
    $adb = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    & $adb logcat -c
    & $adb logcat | Select-String "Auth"
    ```
14. Procurar:
    ```
    [Auth] Signed up anon. UserId=... expires_in=3600s
    ```
15. Confirmar no dashboard → Authentication → Users que um user novo apareceu
    (diferente do Editor).

### Critérios de validação

- [ ] Tabela `players` existe no Table Editor.
- [ ] 3 RLS policies em `Authentication → Policies` pra `players`.
- [ ] Anonymous Sign-Ins habilitado.
- [ ] `_AuthManager` na HomeScene com URL + anon key.
- [ ] Editor: Play → console mostra "Signed up anon" + user aparece no dashboard.
- [ ] Editor: stop + Play de novo → "Refreshed session" (mesmo uid).
- [ ] DebugPanel mostra status correto + botões funcionam.
- [ ] Android build: user novo aparece no dashboard.

---

## Troubleshooting

### "Anon signup failed: http 422: ... Signups not allowed for this instance"
Anonymous Sign-Ins não está habilitado. Volta no Passo 5.

### "Anon signup failed: http 401: ..."
Anon key errado (ou copiou o service_role por engano). Volta no Passo 6,
copia o **anon public** (não service_role).

### "Anon signup failed: Cannot connect to destination host"
URL errado, ou falta internet, ou Android sem permission de rede.
Conferir URL no Inspector (deve começar com `https://`).

### Editor funciona mas Android não
Possíveis:
- AndroidManifest sem `android.permission.INTERNET` (default tem, mas verifica em Player Settings → Other Settings → Internet Access = "Require" se em dúvida).
- Cert pinning issue (raro com Supabase domain padrão).
- Olhar `adb logcat` pra erro específico.

### "Refresh failed: ..." em todo boot
Refresh token foi revogado server-side. Esperado se você deletou o user
no dashboard. O AuthManager faz fallback automático pra signup novo —
no log esperado: `[Auth] Refresh failed (...), falling back to new anonymous signup.`

### User_id muda toda vez que reabro o app
PlayerPrefs não está persistindo. Conferir:
- No Editor (Windows), keys vão pra `HKCU\Software\Nosfera3D\RailMVP`.
- No Android, vão pra `/data/data/com.nosfera3d.railmvp/shared_prefs/`.
- `Sign out` debug button apaga essas keys propositalmente — não confundir com bug.

---

## Próximas fatias

- **Fatia 7B** — PDM sync. Implementa Pull no init + Push no Save com
  conflict resolution via `updated_at`. A tabela `players` deste passo já está
  pronta pra receber.
- **Fatia 8** — Leaderboard online. Cria tabela `daily_results`, queries
  pra top global, UI na Home.

---

## Notas de segurança

- **Anon key é pública** — pode commitar no Inspector (já está em prefab/scene serializado). NÃO é segredo.
- **Service role key é segredo** — nunca colocar no cliente. Só backend admin.
- **RLS é a única defesa** entre clientes — se desabilitar RLS, qualquer device com a anon key consegue ler/escrever qualquer row. Por isso o Passo 4 é obrigatório.
- Em produção, pra LGPD/GDPR: anon users não têm PII (só uuid + game data). Se um dia coletar email/nome real, precisará de consent + privacy policy.
