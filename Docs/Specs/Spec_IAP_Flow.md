# Spec — IAP Flow (Remove Ads + Coin Pack)

> **Status:** spec; not yet implemented.
> **Quando:** após OAuth Google e Realtime Leaderboard.
> **Pré-requisitos:** nenhum técnico estrito. Beneficia-se de keystore Android pronto (Pre-launch) pra testes reais no Play Console.

---

## Problema

Spec §11.4 prevê IAP mas defere pra "após jogadores reais". Você quer
**validar o flow técnico agora** mesmo sem audiência real, pra não chegar
no momento de lançar com surpresas.

Razão tática: Unity IAP tem **Fake Store** mode no Editor que permite
testar todo o flow (open Purchase dialog, confirm, deliver, persist receipt)
SEM precisar de Play Console nem dinheiro real. Validamos arquitetura.
Depois é só switch o "Fake" → "Production" e ligar Play Console.

## Produtos planejados

### Remove Ads (Non-Consumable, ~R$ 4,99)
- Compra única, perpétua, restorable em reinstalação.
- Efeito: `PDM.RemoveAdsPurchased = true`. AdsManager + chest no Home + GameOver checam essa flag e simplesmente não mostram ads / não pedem ad pra reward (entrega grátis).
- Restore: User reinstala → faz Sign-In Google → flag restaurada via Supabase OU Unity IAP restore native.

### Coin Packs (Consumable)
- **Small:** R$ 4,99 = 500 coins
- **Medium:** R$ 14,99 = 1800 coins (20% bonus)
- **Large:** R$ 29,99 = 4000 coins (33% bonus)
- Consumable = comprável múltiplas vezes.
- Entrega: `PDM.AddCoins(amount); PDM.Save();` → sync via Fatia 7B.

## Arquitetura cliente

```
IAPManager (singleton DontDestroyOnLoad)
  ├ Unity.Purchasing.IStoreListener (callbacks)
  ├ StandardPurchasingModule.Instance() — Fake em Editor, Google Play em build
  ├ Products catalog (hardcoded match com Play Console IDs)
  ├ Initialize() — chama UnityPurchasing.Initialize on Start
  ├ BuyProduct(string productId)
  ├ RestorePurchases() — only Apple, mas Android tem auto-restore via Sign-In
  └ events OnPurchaseSuccess, OnPurchaseFailed

PlayerDataManager (modificado)
  ├ bool removeAdsPurchased (novo field)
  ├ PlayerPrefs key RailMVP.IAP.RemoveAds
  ├ public bool HasRemoveAds => removeAdsPurchased
  ├ SetRemoveAdsPurchased() — chamado pelo IAPManager após compra confirmada
  └ sync via 7B pra novos device pickarem

AdsManager (modificado)
  └ TryShowRewarded checks PDM.HasRemoveAds: se true, chama onSuccess
    direto sem mostrar ad (entrega reward grátis)

ShopPanelController (novo OU extensão do existente)
  └ Aba "Loja" → 4 cards (Remove Ads + 3 Coin Packs)
  └ OnClick → IAPManager.BuyProduct(id)
```

## Setup Unity IAP package

1. **Package Manager → Unity Registry → "In-App Purchasing"** → Install.
2. Window → General → Services → enable Unity Gaming Services pro project (free).
3. Project Settings → Services → In-App Purchasing → enable.
4. **Create IAP Catalog:** Window → Unity IAP → IAP Catalog → cria entries:
   - `remove_ads_oneshot` — Non-Consumable
   - `coin_pack_small` — Consumable
   - `coin_pack_medium` — Consumable
   - `coin_pack_large` — Consumable

## Fake Store mode (Editor sem Play Console)

Unity Editor padrão usa "Fake Store" — simula todas as compras. Dialog
aparece, user clica "Buy", sucesso instantâneo.

Configuração:
```cs
var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
// Em Editor: AppStore = AppStore.NotSpecified → Fake Store ativa.
// Em build: AppStore = AppStore.GooglePlay → Play Billing real.
```

Sem nenhum setup extra, o IAPManager funciona no Editor com Fake Store.

## Setup Google Play Console (real testing)

Opcional pra MVP — só necessário quando for shipar OU quando quiser testar
no device em vez de Editor.

1. **Play Console** ($25 one-time pra criar developer account).
2. Criar app entry (com.nosfera3d.railmvp).
3. **Setup → In-app products** → cria os 4 produtos com IDs idênticos ao
   catalog Unity.
4. **Internal Testing track** → upload signed APK → adicionar email do
   tester (você).
5. No device: instalar via link de Internal Testing. Login com test account.
6. Comprar via app — Google Play mostra "test card" dialog em vez de cobrar
   real.

Esse setup é parte do Pre-launch maior — pode esperar.

## Receipt validation

Unity IAP tem validation built-in:
```cs
var validator = new CrossPlatformValidator(GooglePlayTangle.Data(), null, Application.identifier);
var result = validator.Validate(purchaseEvent.purchasedProduct.receipt);
```

`GooglePlayTangle` gerado via Window → Unity IAP → IAP Receipt Validation
Obfuscator (precisa ter Google Play license key — pegar no Play Console
após criar o app).

**Pra MVP:** começar SEM validation (aceitar receipt cego em Editor +
build). Adicionar quando shipar pra produção real.

## Anti-fraud avançado

Cliente local validation pode ser bypassed por user determinado. Production
move pra **server-side validation:**

1. Edge Function no Supabase recebe `{ user_id, product_id, receipt }`.
2. Função chama Google Play Developer API pra validar receipt.
3. Se válido, marca `purchases` table no Supabase. PDM sync recebe a flag.

Defer pra futuro. MVP usa client validation OR no validation.

## Flow de uma compra (passo a passo)

```
1. User abre Shop, clica "Remove Ads R$4,99"
2. ShopPanel chama IAPManager.BuyProduct("remove_ads_oneshot")
3. IAPManager calls UnityPurchasing.controller.InitiatePurchase(...)
4. Google Play (or Fake Store) opens purchase dialog
5. User confirms
6. Unity IAP fires ProcessPurchase callback no IAPManager
7. IAPManager:
   - Validate receipt (skip in MVP)
   - Match productId → delivery action:
     - remove_ads_oneshot → PDM.SetRemoveAdsPurchased()
     - coin_pack_small → PDM.AddCoins(500)
     - etc
   - PDM.Save() (sync push happens via Fatia 7B)
   - return PurchaseProcessingResult.Complete (finishes the transaction)
8. UI feedback: toast "Compra confirmada! +500 coins" ou "Ads removidos!"
```

## Edge cases

### 1. User compra Remove Ads, depois reinstala
- Re-Sign-In via Google → user_id antigo → PDM pull do Supabase → `remove_ads_purchased = true` restaurado.
- Alternativa: Unity IAP's RestorePurchases() pode também recuperar do Play (Android auto, iOS manual). Mas confiar no Supabase é mais robusto se OAuth está pronto.

### 2. Compra interrompida (network drop, app crash)
- Google Play guarda transação pendente. Próximo Init do IAP, ProcessPurchase é re-chamado com a transaction pendente. IAPManager entrega normalmente.
- **Importante:** IAPManager precisa ser idempotente — se PDM já tem o coin pack delivered (via receipt_id que ele rastreia), não duplicar.

### 3. Refund
- Google Play notifica via Developer Notifications (precisa Pub/Sub setup). Pra MVP, ignorar — refunds raros.

### 4. User estraga sistema relógio pra abusar coin packs
- Coin packs não dependem de timer, então N/A.

### 5. Fake Store em Editor sempre sucede
- Pode ser problema pra testar caminhos de falha. Workaround: hack temporário no IAPManager pra simular fail (toggle em DebugPanel).

## UI — Shop com nova aba

Aproveitar o ShopPanel existente (Fatia 4). Adicionar abas:

```
┌─────────────────────────────────┐
│ LOJA                            │
├─[ Personagens ]─[ IAP ]─────────│
│                                  │
│  ╔══════════════════════════╗   │
│  ║ Remove Ads               ║   │
│  ║ Sem mais anúncios pra    ║   │
│  ║ sempre.                  ║   │
│  ║          R$ 4,99 [Buy]   ║   │
│  ╚══════════════════════════╝   │
│                                  │
│  ╔══════════════════════════╗   │
│  ║ 500 Coins                ║   │
│  ║           R$ 4,99 [Buy]  ║   │
│  ╚══════════════════════════╝   │
│                                  │
│  ... (medium, large) ...         │
└─────────────────────────────────┘
```

Tab system: Toggle component + Animator/simple GameObject.SetActive.

## Esforço estimado

| Etapa | Tempo |
|---|---|
| Unity IAP package install + Catalog config | 30min |
| IAPManager singleton (Initialize, BuyProduct, ProcessPurchase) | 2-3h |
| PDM extension (HasRemoveAds field + key + getter) | 30min |
| AdsManager check HasRemoveAds before TryShowRewarded | 30min |
| ShopPanel UI extension (cards, tabs) | 1-2h |
| Editor Fake Store testing (all 4 products) | 1h |
| Idempotency + double-purchase prevention | 1h |
| Setup doc + debug panel hooks | 30min |
| **Total Fake Store validation** | **~6-8h (~1 sessão)** |

| Real Play Console deploy | Tempo |
|---|---|
| Play Console developer account ($25 + verification) | 1-2 dias wait |
| App entry + products config | 1h |
| Signed APK + Internal Testing upload | 1h |
| Tester device setup + buy flow | 30min |
| **Total real testing** | **+ ~3h + 1-2 days wait** |

## Out of scope

- **Server-side receipt validation** (Edge Function). Pra produção real.
- **Refund handling via Pub/Sub.** Future.
- **Subscriptions** (Battle Pass premium track no futuro — Spec próprio).
- **Cross-platform Coin Pack sync** (compra no Android, vê coins no iOS): depende de OAuth + PDM sync. Já temos foundation.

## Open questions

1. **Preços:** R$ 4,99 / 14,99 / 29,99 são tier comuns BR mas Play Console pricing tier impõe valores específicos. Validar na hora de criar produtos. App Store iOS tier semelhante.
2. **Remove Ads deveria também afetar Daily Chest?** Atualmente chest pede rewarded ad pra entregar +150 coins. Se Remove Ads = on, entregar chest grátis? **Sim, recomendação:** consistência. User pagou pra não ver ads — não faz sentido bloquear rewards.
3. **Bonus % nos coin packs:** atual 0% / 20% / 33% incentiva tier maior. Validar com analytics depois.

## Validação MVP (Editor Fake Store)

Critério de "spec implementado":
- [ ] Abrir Shop em Editor mostra os 4 cards IAP.
- [ ] Click "Buy 500 coins" → Fake Store dialog → confirma → coins atualizadas em PDM.
- [ ] Click "Remove Ads" → confirma → PDM.HasRemoveAds = true → Daily Chest entrega grátis sem ad.
- [ ] Restart Editor → PDM.HasRemoveAds persistido → ainda grátis.
- [ ] Wipe All player data → HasRemoveAds reseta — esperado.
- [ ] Comprar coin pack 2x → recebe 2x — idempotency NÃO desejada pra consumable.
- [ ] Comprar Remove Ads 2x → no-op na 2ª (botão deve disable após primeira) — idempotency desejada pra non-consumable.
