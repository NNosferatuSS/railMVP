# Rail Switch — UI Build Guide

Guia rápido pra rodar a toolchain que constrói toda a UI 2D no projeto Unity.
Fonte de verdade do design: `Docs/Game UI Mockups Request (1)/design_handoff_rail_switch_ui/README.md`.

## Como rodar

Abra o projeto no Unity Editor e execute o menu:

**Tools → RailSwitch → Build UI → BUILD ALL**

O menu executa em ordem:

1. **Import Sprites** — copia 77 PNGs de `Docs/…/design_handoff_rail_switch_ui/assets/` para
   `Assets/UI/Sprites/`. Aplica TextureImporter: PPU 100, Bilinear, Alpha Is Transparency,
   Full Rect, e o `spriteBorder` de cada sprite conforme `assets/README.md` do handoff.
   Popula `Assets/UI/ScriptableObjects/UITheme.asset` com refs a todos os sprites.
2. **Import Fonts** — baixa `ChakraPetch-Bold.ttf`, `BarlowSemiCondensed-Bold.ttf`,
   `BarlowSemiCondensed-SemiBold.ttf` e `BarlowSemiCondensed-Medium.ttf` do repositório
   oficial Google Fonts (OFL) para `Assets/UI/Fonts/`. Gera `TMP_FontAsset` em modo
   **Dynamic (SDF)** — TMP renderiza glyphs on-demand e o Editor mostra as fontes prontas.
   Popula os campos de fonte no `UITheme`.
3. **Build Base Prefabs** — cria os widgets reutilizáveis em `Assets/UI/Prefabs/Base/`:
   Btn_Primary/Premium/Purchase/Cyan/Ghost/Danger · Chip_Coins/Gems/Tokens · Puck ·
   ProgressBar_Cyan/Gold/Violet · HazardBanner · Tabs · Toast · BottomNav.
4. **Build HUD (in-game)** — cria `Assets/UI/Prefabs/HUD/Canvas_HUD.prefab`.
5. **Build Overlays** — cria `Canvas_GameOver`, `Canvas_Pause`, `Canvas_Warmup` em
   `Assets/UI/Prefabs/Overlays/`.
6. **Build Meta Canvas** — cria `Assets/UI/Prefabs/Meta/Canvas_Meta.prefab` com todas as
   telas de meta empilhadas (Home S2, Store S3, Collection S4, BattlePass S6, Missions S7,
   Leaderboard S8, Profile S9, Settings S10). `ScreenNavigator` alterna via `BottomNav`.
7. **Build Splash** — cria `Assets/UI/Prefabs/Splash/Canvas_Splash.prefab`.

Você pode rodar cada passo separadamente pelo submenu `Tools → RailSwitch → Build UI → N. …`
se quiser iterar num só.

## Depois do build

1. **RailSwitchMVP.unity** (in-game):
   - Arraste `Canvas_HUD.prefab` pra hierarquia da cena. Ele carrega o `HUDViewController`
     novo — o `HUDController` legado pode ficar como está (paralelo pra debug) ou ser removido.
   - Arraste `Canvas_GameOver.prefab`, `Canvas_Pause.prefab`, `Canvas_Warmup.prefab`.
2. **HomeScene.unity** (meta):
   - Arraste `Canvas_Meta.prefab`. Se quiser aposentar o Canvas antigo, delete-o (os controllers
     legados — `HomeScreenController`, `ShopController`, `ProfilePanelController`, `LeaderboardPanelController`,
     `MissionEntryUI` etc — continuam funcionando; basta religar os TMPs deles nos gameobjects
     dentro dos paineis correspondentes do `Canvas_Meta` — os nomes são estáveis).
3. **Splash** (opcional): crie uma cena `SplashScene.unity`, arraste `Canvas_Splash.prefab`
   e mova-a pra posição 0 no Build Settings.
4. Rode em Play. Faltando algum campo? Todos os controllers têm auto-resolve por singleton
   e checagem `!= null` — nada estoura NRE, mas alguns TMPs podem aparecer vazios se você
   ainda não religou (esperado).

## Estrutura gerada

```
Assets/UI/
├── Sprites/          (77 PNGs, com 9-slice configurado)
├── Fonts/            (4 TTFs + 4 TMP_FontAsset SDF)
├── ScriptableObjects/
│   └── UITheme.asset (design tokens + refs)
└── Prefabs/
    ├── Base/         (widgets reutilizáveis)
    ├── HUD/          (Canvas_HUD)
    ├── Overlays/     (GameOver / Pause / Warmup)
    ├── Meta/         (Canvas_Meta com todas as telas)
    └── Splash/       (Canvas_Splash)

Assets/Scripts/RailSwitchMVP/
├── UI/
│   ├── Theme/UITheme.cs           (SO tokens)
│   ├── Widgets/                    (PuckUI, ChipUI, TabsUI, BottomNavUI, ProgressBarUI, HazardBannerUI, ToastUI)
│   ├── HUDViewController.cs       (HUD novo)
│   ├── PauseController.cs         (Pause novo)
│   ├── WarmupCountdownUI.cs       (Warmup novo)
│   ├── SplashController.cs
│   └── ScreenNavigator.cs
├── Meta/
│   └── BattlePassManager.cs       (novo)
└── Editor/UI/
    ├── UIAssetImporter.cs         (menu 1)
    ├── UIFontImporter.cs          (menu 2)
    ├── UIBuilder.cs               (helpers)
    └── Wizards/
        ├── BuildBasePrefabs.cs    (menu 3)
        ├── BuildHUD.cs            (menu 4)
        ├── BuildOverlays.cs       (menu 5)
        ├── BuildMetaCanvas.cs     (menu 6)
        ├── BuildSplash.cs         (menu 7)
        └── BuildAll.cs            (menu 0)
```

## Notas de escopo

Foco do primeiro passo: **HomeScene + gameplay in-game** (HUD + GameOver + Pause + Warmup).
Restante das telas está montado em layout aproximado do handoff — refinamento fino é iterado
via Inspector depois. As decisões abertas do handoff foram confirmadas com o dono do produto:

- Daily Challenge: 3 tentativas por dia.
- Revive: 2 gems, dobrando por uso (2 → 4 → 8) na mesma run.
- Weekly missions: 3 por semana, mesma mecânica das diárias.
- Collection: 8 categorias mostradas; MVP libera character funcional, outras ficam locked.
- Battle Pass: `BattlePassManager` novo com free/premium tracks e XP linear (500·level).

## Iterando o layout

Todo layout é declarativo em código (`UIBuilder` + `UITheme`). Pra ajustar:

- **Cor/tamanho/token global** → edite `UITheme.asset` no Inspector.
- **Layout de uma tela** → edite o `Build<Tela>Screen` correspondente e rode só aquele menu.
- **Refinamento fino (drag no Editor)** → mova o RectTransform no Editor mesmo; se preservar
  as refs serializadas, funciona.
- **Perdeu uma ref?** Rode de novo o wizard da tela — sobrescreve o prefab.

## Fonts em modo Dynamic vs Static

O importer gera atlas **Dynamic**, ideal pra desenvolvimento (glyphs on-demand, sem gastar
tempo abrindo Font Asset Creator). Antes de shippar em prod, se quiser atlas estático (mais
otimizado em memória), abra `Window → TextMeshPro → Font Asset Creator` e regere estáticos
das 4 fontes. O `UITheme` pode receber os novos assets sem quebrar refs de prefab.
