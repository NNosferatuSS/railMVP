# Ads — Backend Issue (Mock Mode On)

> **Data deste registro:** 2026-05-25
> **Estado:** Mock Mode ligado no `_AdsManager` (HomeScene). Real ads pausados.
> **Setup original (passo-a-passo Unity Cloud / Dashboard):** ver `AdsFatia5_Setup.md`.

## TL;DR

Real ads do Unity Ads não estão sendo servidos no build Android, mesmo com:
- Game IDs corretos no Inspector
- Project linkado ao Unity Cloud (`cloudProjectId 1d96aef4-...`, org `nosfera-as-publisher`)
- Ad Units `Rewarded_Android` / `Rewarded_iOS` criados e Active no dashboard
- Test Device whitelisted (GAID `2c709e9f-7df6-431f-9503-c7dd6507e890`)
- `testMode=true` no SDK
- LGPD `consent=true` setado via `SetMetaData` no código (commit pós-fatia 5)
- `privacy.mode=mixed` setado via `SetMetaData`
- App data limpo (`adb shell pm clear ...`) — webapp fresh

O servidor retorna `INVALID_ARGUMENT — adMarkup is missing; objectId is missing` em todo `Load Rewarded_Android`. **Diagnóstico:** project Unity Ads novo sem inventory de test ads servível. Sem solução client-side.

**Decisão:** religar Mock Mode (toggle no `_AdsManager` Inspector). Mock simula um "ad" de 1.5s e dispara `onSuccess`, mantendo a UX da Fatia 5 funcional (Baú Grátis na Home, Watch Ad 2x coins no GameOver) sem depender do backend Unity.

## O erro

Log relevante do `adb logcat -s Unity:V` (representativo, várias tentativas):

```
[Ads] Initialize gameId=6122531 test=True
UnityAds: Initializing Unity Services 4.4.2 (4420) with game id 6122531 in test mode
UnityAds: Unity Services environment check OK
UnityAds: Setting new header bidding token
[Ads] Initialization complete.
[Ads] Load Rewarded_Android
UnityAds: Header bidding load invocation failed: adMarkup is missing; objectId is missing
[Ads] Failed to load Rewarded_Android: INVALID_ARGUMENT — adMarkup is missing; objectId is missing
```

JWT da response de configuration (decodificado) confirma que o backend recebeu corretamente:
- `azp: 1d96aef4-f88f-4bd0-95c3-844856a40247` ← bate com cloudProjectId
- `consent: true` ← consent foi propagado
- `country: BR`, `lglf: lgpd`, `legalTerritory: 0` ← LGPD aplicado
- `mixed: false` ← backend ignorou nosso `privacy.mode=mixed`
- `bundleId: com.nosfera3d.railmvp` ← package reconhecido

Ou seja: o servidor reconheceu o app, aceitou consent, mas o header bidding não tem com o que responder.

## O que tentamos (cronológico)

1. **Inicialmente Mock Mode estava ON** desde o commit `85644e5`. Ligamos OFF pra tentar real ads.
2. **Game IDs no Inspector** — verificado: `6122531` (Android), `6122530` (iOS). Bate com dashboard.
3. **Ad Units no dashboard** — `Rewarded_Android` e `Rewarded_iOS` ambos existem, Format=Rewarded, Status=Active.
4. **Test Device whitelist** — GAID adicionado em Monetization → Testing.
5. **Bundle ID** — confirmado que o dashboard infere automaticamente da request (vimos `bundleId=com.nosfera3d.railmvp` na URL de configuration). Não precisa setar manual.
6. **Hipótese consent/LGPD** — adicionamos `SetMetaData("gdpr", "consent", "true")` antes de `Advertisement.Initialize`. JWT confirmou `consent:true` aplicado. Não resolveu.
7. **Hipótese privacy mode** — adicionamos `SetMetaData("privacy", "mode", "mixed")`. URL passou `dpm=mixed`. Backend ignorou (`mixed:false` no JWT). Não resolveu.
8. **Hipótese cache stale** — `adb shell pm clear com.nosfera3d.railmvp` pra forçar webapp fresh. Logs confirmaram `loading webapp from https://...` ao invés de `from local cache`. Não resolveu.

Esgotamos as variáveis client-side. Setup está correto. Problema é demand inventory do backend Unity Ads pra este project específico.

## Estado atual do código

Em `Assets/Scripts/RailSwitchMVP/Meta/AdsManager.cs:Start()`, antes de `Advertisement.Initialize`:

```cs
// DEV ONLY — hardcoded consent + privacy mode destravam header bidding
// em LGPD/GDPR e maximizam inventory disponível (mixed audience flag).
// Produção: substituir pelo resultado real de um CMP/popup de consent.
var gdprMetaData = new MetaData("gdpr");
gdprMetaData.Set("consent", "true");
Advertisement.SetMetaData(gdprMetaData);

var privacyMetaData = new MetaData("privacy");
privacyMetaData.Set("mode", "mixed");
Advertisement.SetMetaData(privacyMetaData);
```

Esse código fica **dormente** quando `useMockAds=true` (caminho do early return em `Start()` que bypassa o SDK). Acorda automaticamente quando o toggle Mock for desligado pra revisitar real ads.

## Como revisitar

Quando? Sugestões em ordem:
- **Depois de 1-2 semanas** — projects Unity Ads novos costumam ganhar inventory de test ads conforme propagam. Não é garantido, mas é o caminho zero-effort.
- **Quando shipar uma build de friends-and-family** — usuários reais gerando tráfego "esquentam" o project no backend.
- **Quando bater na limitação do mock** — se algum comportamento de prod (rede caindo, ad skip, falha de carregamento) precisa ser testado de verdade.

Roteiro pra testar:
1. No Editor, abrir `Assets/Scenes/HomeScene.unity`.
2. Hierarchy → selecionar `_AdsManager`.
3. Inspector → seção "Mock Mode (DEV ONLY)" → **desmarcar** `Use Mock Ads`.
4. **Ctrl+S**, Build & Run.
5. Capturar logs:
   ```powershell
   $adb = "C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
   & $adb logcat -c
   & $adb logcat | Select-String "Ads|Home|GameOver"
   ```
6. Procurar a linha-chave **`[Ads] Loaded Rewarded_Android`** — se aparecer, backend propagou, real ads funcionam.
7. Se ainda `adMarkup is missing`, reverter pra mock e esperar mais.

## Migration paths (se backend nunca propagar)

Em ordem de esforço:

1. **LevelPlay** (Unity-recomendado long-term) — mediation framework que agrega Unity Ads + AdMob + outras. Inventory imediato porque AdMob/etc preenchem o gap. Migração: substituir `AdsManager.cs` (~150 linhas), API pública (`TryShowRewarded`, `IsRewardedReady`) fica idêntica, callers não mudam. Trade-off: setup mais elaborado (LevelPlay account, AdMob app, mediation groups).

2. **AdMob direto** — Google AdMob via plugin oficial. Inventory robusto, conta no Google Play Console facilita ship. Trade-off: SDK separado, requirements de Privacy Policy, integração mais pesada.

3. **Continuar com Mock indefinidamente** — opção pra MVP/demo que não precisa de revenue real. UX da fatia 5 funciona, just sem servir real ads.

## Refs

- Setup completo Unity Ads: `Docs/AdsFatia5_Setup.md`
- Pre-launch checklist (Test Mode off etc): `Docs/Pre_Launch_Checklist.md`
- Código: `Assets/Scripts/RailSwitchMVP/Meta/AdsManager.cs`
- Callers: `HomeScreenController.cs` (chest), `GameOverController.cs` (2x coins)
