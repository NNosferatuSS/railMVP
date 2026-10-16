#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.UI;
using RailSwitchMVP.UI.Theme;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Constrói Assets/UI/Prefabs/Meta/Canvas_Meta.prefab com todas as telas de
    /// meta como paineis empilhados (S2 Home, S3 Store, S4 Collection, S6 BattlePass,
    /// S7 Missions, S8 Leaderboard, S9 Profile, S10 Settings). ScreenNavigator
    /// alterna via BottomNav; MetaCanvasBinder liga os controllers.
    ///
    /// LAYOUT: todo posicionamento passa por UIBuilder.Col (cursor vertical) ou por
    /// StretchHTop/StretchHBottom explícitos. Nenhum elemento fica sem RectTransform
    /// definido, e nenhum offset Y é hardcoded em cadeia — mudar a altura de um bloco
    /// reposiciona os de baixo sozinho.
    ///
    /// Menu: Tools -> RailSwitch -> Build UI -> 6. Build Meta Canvas.
    /// </summary>
    public static class BuildMetaCanvas
    {
        const string OutPath = "Assets/UI/Prefabs/Meta/Canvas_Meta.prefab";
        const string BasePrefabs = "Assets/UI/Prefabs/Base/";

        // Geometria do design (390x844, iPhone 14 Pro Max scaled)
        const float PadX       = 16f;   // margem lateral padrão
        const float ContentW   = 358f;  // 390 - 2*PadX
        const float TopBarY    = 44f;   // safe area
        const float TopBarH    = 32f;
        const float HostTop    = 84f;   // topo do ScreenHost
        const float NavH       = 68f;
        const float HeaderH    = 36f;
        const float Gap        = 14f;

        [MenuItem("Tools/RailSwitch/Build UI/6. Build Meta Canvas", priority = 60)]
        public static void Build()
        {
            var t = UITheme.Current;
            if (t == null) { Debug.LogError("Rode 1. Import Sprites primeiro."); return; }
            EnsureFolder("Assets/UI/Prefabs/Meta");

            var canvasGO = new GameObject("Canvas_Meta",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(ScreenNavigator), typeof(MetaCanvasBinder));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = t.referenceResolution;
            scaler.matchWidthOrHeight = t.matchWidthOrHeight;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            var root = canvasGO.transform;

            // ---------- Background ----------
            var bg = UIBuilder.Icon("Background", root, t.bg_meta_grid, Color.white);
            UIBuilder.Stretch((RectTransform)bg.transform);
            var bgImg = bg.GetComponent<Image>();
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;

            // ---------- TopBar (chips à direita) ----------
            var topBar = UIBuilder.NewGO("TopBar", root);
            UIBuilder.StretchHTop((RectTransform)topBar.transform, PadX, PadX, TopBarY, TopBarH);

            // Linha de chips alinhada à direita. Os chips têm largura automática
            // (ContentSizeFitter), então posicioná-los com offsets fixos fazia um
            // sobrepor o outro assim que o saldo crescia. O layout group resolve.
            var chipRow = UIBuilder.NewGO("Chips", topBar.transform);
            var crRt = (RectTransform)chipRow.transform;
            crRt.anchorMin = new Vector2(1, 0.5f);
            crRt.anchorMax = new Vector2(1, 0.5f);
            crRt.pivot = new Vector2(1, 0.5f);
            crRt.anchoredPosition = Vector2.zero;
            crRt.sizeDelta = new Vector2(0, 28);

            var chipLayout = chipRow.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 8;
            chipLayout.childAlignment = TextAnchor.MiddleRight;
            // childControlWidth FALSE de propósito: cada chip já tem o próprio
            // ContentSizeFitter dimensionando sua largura. Se o pai também
            // controlasse, os dois brigariam pelo mesmo valor.
            chipLayout.childControlWidth = false;  chipLayout.childForceExpandWidth = false;
            chipLayout.childControlHeight = false; chipLayout.childForceExpandHeight = false;

            var chipFitter = chipRow.AddComponent<ContentSizeFitter>();
            chipFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            chipFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            InstantiatePrefab(BasePrefabs + "Chip_Gems.prefab", chipRow.transform);
            InstantiatePrefab(BasePrefabs + "Chip_Coins.prefab", chipRow.transform);

            // ---------- ScreenHost ----------
            var host = new GameObject("ScreenHost", typeof(RectTransform), typeof(RectMask2D));
            host.transform.SetParent(root, false);
            var hRt = (RectTransform)host.transform;
            hRt.anchorMin = new Vector2(0, 0);
            hRt.anchorMax = new Vector2(1, 1);
            hRt.offsetMin = new Vector2(0, NavH);
            hRt.offsetMax = new Vector2(0, -HostTop);

            // ---------- BottomNav ----------
            var bottomNav = InstantiatePrefab(BasePrefabs + "BottomNav.prefab", root);
            BottomNavUI bn = null;
            if (bottomNav != null)
            {
                var bnRt = (RectTransform)bottomNav.transform;
                bnRt.anchorMin = new Vector2(0, 0);
                bnRt.anchorMax = new Vector2(1, 0);
                bnRt.pivot = new Vector2(0.5f, 0);
                bnRt.offsetMin = new Vector2(0, 0);
                bnRt.offsetMax = new Vector2(0, NavH);
                bn = bottomNav.GetComponent<BottomNavUI>();
            }

            // ---------- Screens ----------
            var home        = SafeBuild("Home",        () => BuildHomeScreen(host.transform, t));
            var store       = SafeBuild("Store",       () => BuildStoreScreen(host.transform, t));
            var collection  = SafeBuild("Collection",  () => BuildCollectionScreen(host.transform, t));
            var missions    = SafeBuild("Missions",    () => BuildMissionsScreen(host.transform, t));
            var leaderboard = SafeBuild("Leaderboard", () => BuildLeaderboardScreen(host.transform, t));
            var profile     = SafeBuild("Profile",     () => BuildProfileScreen(host.transform, t));
            var battlepass  = SafeBuild("BattlePass",  () => BuildBattlePassScreen(host.transform, t));
            var settings    = SafeBuild("Settings",    () => BuildSettingsScreen(host.transform, t));

            // ---------- ScreenNavigator ----------
            var nav = canvasGO.GetComponent<ScreenNavigator>();
            var so = new SerializedObject(nav);
            so.FindProperty("bottomNav").objectReferenceValue = bn;
            var arr = so.FindProperty("screens");
            var slots = new (ScreenNavigator.Screen, GameObject)[]
            {
                (ScreenNavigator.Screen.Home,       home),
                (ScreenNavigator.Screen.Store,      store),
                (ScreenNavigator.Screen.Skins,      collection),
                (ScreenNavigator.Screen.Ranks,      leaderboard),
                (ScreenNavigator.Screen.More,       missions),
                (ScreenNavigator.Screen.Settings,   settings),
                (ScreenNavigator.Screen.Missions,   missions),
                (ScreenNavigator.Screen.BattlePass, battlepass),
                (ScreenNavigator.Screen.Profile,    profile),
            };
            arr.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").intValue = (int)slots[i].Item1;
                e.FindPropertyRelative("panel").objectReferenceValue = slots[i].Item2;
            }
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvasGO, OutPath);
            Object.DestroyImmediate(canvasGO);
            AssetDatabase.SaveAssets();

            Debug.Log("[RailSwitchUI] Meta Canvas: " + OutPath);
            UIBuildContext.Info("Rail Switch UI",
                "Canvas_Meta reconstruído. Use 'Apply Prefabs to Scenes' pra sincronizar a cena.");
        }

        static GameObject SafeBuild(string label, System.Func<GameObject> factory)
        {
            try { var g = factory(); Debug.Log("[MetaCanvas][OK] " + label); return g; }
            catch (System.Exception ex)
            {
                Debug.LogError("[MetaCanvas][FAIL] " + label + "\n" + ex);
                return null;
            }
        }

        // ==================== S2 · HOME ====================
        static GameObject BuildHomeScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Home");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "CHOOSE TRACK", t, back: false), HeaderH);

            // --- Card Classic ---
            var classic = UIBuilder.Image9("ClassicCard", s.transform, t.panel_lg, Color.white);
            col.Push(classic, 236f);
            UIBuilder.TxtTop(classic.transform, "Kicker", "CLASSIC · ALWAYS OPEN", t.barlowSemiBold, 11f,
                t.textMid, TextAlignmentOptions.Center, 16f, 14f, uppercase: true, tracking: 0.14f);
            UIBuilder.TxtTop(classic.transform, "Title", "ENDLESS TRACK", t.chakraBold, 24f,
                Color.white, TextAlignmentOptions.Center, 34f, 32f);
            UIBuilder.TxtTop(classic.transform, "Desc", "Survive. Switch rails. Bank coins.", t.barlowMedium, 12f,
                t.textMid, TextAlignmentOptions.Center, 70f, 18f);
            UIBuilder.TxtTop(classic.transform, "BestLine", "BEST 0 M", t.barlowSemiBold, 11f,
                t.coins, TextAlignmentOptions.Center, 94f, 16f, uppercase: true, tracking: 0.14f);
            var startBtn = PrimaryButton(classic.transform, "Btn_StartRun", "START RUN", t, 22f);
            UIBuilder.StretchHBottom((RectTransform)startBtn.transform, 16f, 16f, 18f, 58f);

            // --- Card Daily ---
            var daily = UIBuilder.Image9("DailyCard", s.transform, t.panel_accent_violet, Color.white);
            col.Push(daily, 190f);
            UIBuilder.TxtTop(daily.transform, "Kicker", "DAILY CHALLENGE", t.barlowSemiBold, 11f,
                UITheme.Hex("#C9B3FF"), TextAlignmentOptions.Center, 16f, 14f, uppercase: true, tracking: 0.14f);
            UIBuilder.TxtTop(daily.transform, "Attempts", "3 ATTEMPTS LEFT", t.chakraBold, 19f,
                Color.white, TextAlignmentOptions.Center, 36f, 26f);
            UIBuilder.TxtTop(daily.transform, "Counter", "RESETS IN 6h 12m", t.barlowSemiBold, 11f,
                t.coins, TextAlignmentOptions.Center, 66f, 16f, uppercase: true, tracking: 0.10f);
            var playDaily = PremiumButton(daily.transform, "Btn_PlayDaily", "PLAY DAILY", t, 20f);
            UIBuilder.StretchHBottom((RectTransform)playDaily.transform, 16f, 16f, 16f, 54f);

            return s;
        }

        // ==================== S3 · STORE ====================
        static GameObject BuildStoreScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Store");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "STORE", t), HeaderH);
            col.Push(Tabs(s.transform, new[] { "DEALS", "COINS", "GEMS", "ITEMS" }), 42f);

            // --- Daily deal ---
            var deal = UIBuilder.Image9("DailyDeal", s.transform, t.panel_accent_gold, Color.white);
            col.Push(deal, 128f);
            var dark = UITheme.Hex("#2A1600");
            UIBuilder.TxtTop(deal.transform, "Kicker", "DAILY DEAL · RESETS 6h 12m", t.barlowSemiBold, 11f,
                UITheme.WithA(dark, 0.75f), TextAlignmentOptions.Left, 14f, 16f, uppercase: true, tracking: 0.08f);
            UIBuilder.TxtTop(deal.transform, "Title", "STARTER BUNDLE", t.chakraBold, 19f,
                dark, TextAlignmentOptions.Left, 32f, 26f);
            UIBuilder.TxtTop(deal.transform, "Contents", "500 coins  ·  20 gems  ·  1 skin", t.barlowMedium, 12f,
                UITheme.WithA(dark, 0.8f), TextAlignmentOptions.Left, 60f, 18f);
            var buy = PurchaseButton(deal.transform, "Btn_BuyDeal", "R$ 9,90", t, 16f);
            var buyRt = (RectTransform)buy.transform;
            buyRt.anchorMin = new Vector2(1, 0); buyRt.anchorMax = new Vector2(1, 0);
            buyRt.pivot = new Vector2(1, 0);
            buyRt.anchoredPosition = new Vector2(-14f, 14f);
            buyRt.sizeDelta = new Vector2(120f, 40f);

            // --- Packs grid ---
            var grid = Grid(s.transform, "Packs", new Vector2(173f, 96f), new Vector2(12f, 12f), 2);
            col.Push(grid, 204f);
            var packs = new (string value, string label, bool gold)[]
            {
                ("500",  "COINS",  true),
                ("1 200","COINS",  true),
                ("20",   "GEMS",   false),
                ("60",   "GEMS",   false),
            };
            foreach (var p in packs)
            {
                var cell = UIBuilder.Image9("Pack_" + p.label + "_" + p.value, grid.transform,
                    p.gold ? t.panel_md : t.panel_accent_violet, Color.white);
                UIBuilder.TxtTop(cell.transform, "Value", p.value, t.chakraBold, 22f,
                    p.gold ? t.coins : Color.white, TextAlignmentOptions.Center, 18f, 28f, 10f, 10f);
                UIBuilder.TxtTop(cell.transform, "Label", p.label, t.barlowSemiBold, 11f,
                    t.textLow, TextAlignmentOptions.Center, 46f, 16f, 8f, 8f, uppercase: true, tracking: 0.10f);
                UIBuilder.TxtBottom(cell.transform, "Price", "R$ 4,90", t.barlowBold, 12f,
                    Color.white, TextAlignmentOptions.Center, 12f, 18f, 10f, 10f);
            }

            return s;
        }

        // ==================== S4 · COLLECTION ====================
        static GameObject BuildCollectionScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Collection");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "COLLECTION", t), HeaderH);

            // --- Chips de categoria (largura igual, sem layout group) ---
            var chipsRow = UIBuilder.NewGO("Categories", s.transform);
            col.Push(chipsRow, 32f);
            var cats = new[] { "CHARACTER", "TRAIL", "TILE", "FX" };
            float chipGap = 8f;
            float chipW = (ContentW - chipGap * (cats.Length - 1)) / cats.Length;
            for (int i = 0; i < cats.Length; i++)
            {
                var chip = UIBuilder.Image9("Chip_" + cats[i], chipsRow.transform, t.pill_dark,
                    i == 0 ? UITheme.WithA(t.accent, 0.22f) : Color.white);
                var cRt = (RectTransform)chip.transform;
                cRt.anchorMin = new Vector2(0, 0); cRt.anchorMax = new Vector2(0, 1);
                cRt.pivot = new Vector2(0, 0.5f);
                cRt.anchoredPosition = new Vector2(i * (chipW + chipGap), 0f);
                cRt.sizeDelta = new Vector2(chipW, 0f);
                chip.GetComponent<Image>().raycastTarget = true;
                chip.AddComponent<Button>();
                var lbl = UIBuilder.Text("Label", chip.transform, cats[i], t.barlowSemiBold, 11f,
                    i == 0 ? t.accent : t.textMid, TextAlignmentOptions.Center, uppercase: true, tracking: 0.04f);
                UIBuilder.Stretch((RectTransform)lbl.transform);
            }

            // --- Grid de itens ---
            var grid = Grid(s.transform, "Grid", new Vector2(110f, 116f), new Vector2(14f, 14f), 3);
            col.Push(grid, 376f);
            for (int i = 0; i < 9; i++)
            {
                var cell = UIBuilder.Image9("Cell_" + i, grid.transform,
                    i < 3 ? t.frame_rare : t.frame_locked, Color.white);
                UIBuilder.TxtBottom(cell.transform, "Name", i < 3 ? "OWNED" : "LOCKED",
                    t.barlowSemiBold, 11f, i < 3 ? t.safe : t.textLow,
                    TextAlignmentOptions.Center, 8f, 14f, 4f, 4f, uppercase: true, tracking: 0.06f);
            }

            return s;
        }

        // ==================== S6 · BATTLE PASS ====================
        static GameObject BuildBattlePassScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "BattlePass");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "SEASON 1", t), HeaderH);

            // --- Painel de XP ---
            var xpPanel = UIBuilder.Image9("XpPanel", s.transform, t.panel_md, Color.white);
            col.Push(xpPanel, 82f);
            UIBuilder.TxtTop(xpPanel.transform, "Label", "LEVEL 14", t.barlowSemiBold, 11f,
                t.textMid, TextAlignmentOptions.Left, 14f, 16f, uppercase: true, tracking: 0.14f);
            UIBuilder.TxtTop(xpPanel.transform, "Value", "2 340 / 3 000 XP", t.chakraBold, 14f,
                Color.white, TextAlignmentOptions.Right, 14f, 16f);
            var bar = InstantiatePrefab(BasePrefabs + "ProgressBar_Violet.prefab", xpPanel.transform);
            if (bar != null) UIBuilder.StretchHBottom((RectTransform)bar.transform, 14f, 14f, 18f, 10f);

            // --- Trilha de níveis ---
            const float RowH = 64f, RowGap = 10f;
            var levels = UIBuilder.NewGO("Levels", s.transform);
            col.Push(levels, RowH * 5 + RowGap * 4);
            for (int i = 0; i < 5; i++)
            {
                bool claimed = i == 0;
                var row = UIBuilder.Image9("Lvl_" + (14 + i), levels.transform, t.panel_md, Color.white);
                UIBuilder.StretchHTop((RectTransform)row.transform, 0f, 0f, i * (RowH + RowGap), RowH);

                UIBuilder.TxtTop(row.transform, "Level", (14 + i).ToString(), t.chakraBold, 18f,
                    claimed ? t.gems : Color.white, TextAlignmentOptions.Left, 12f, 24f, 16f, 16f);
                UIBuilder.TxtTop(row.transform, "Reward", i % 2 == 0 ? "+250 coins" : "+5 gems",
                    t.barlowMedium, 12f, t.textMid, TextAlignmentOptions.Left, 36f, 16f, 16f, 16f);

                var pill = UIBuilder.Image9("Status", row.transform, t.pill_dark,
                    claimed ? UITheme.WithA(t.safe, 0.20f) : UITheme.WithA(Color.white, 0.08f));
                var pRt = (RectTransform)pill.transform;
                pRt.anchorMin = new Vector2(1, 0.5f); pRt.anchorMax = new Vector2(1, 0.5f);
                pRt.pivot = new Vector2(1, 0.5f);
                pRt.anchoredPosition = new Vector2(-14f, 0f);
                pRt.sizeDelta = new Vector2(88f, 32f);
                var pl = UIBuilder.Text("Label", pill.transform, claimed ? "CLAIMED" : "LOCKED",
                    t.barlowSemiBold, 11f, claimed ? t.safe : t.textLow,
                    TextAlignmentOptions.Center, uppercase: true, tracking: 0.06f);
                UIBuilder.Stretch((RectTransform)pl.transform);
            }

            return s;
        }

        // ==================== S7 · MISSIONS ====================
        static GameObject BuildMissionsScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Missions");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "MISSIONS", t), HeaderH);
            col.Push(Tabs(s.transform, new[] { "DAILY", "WEEKLY" }), 42f);

            // --- Lista de missões (3 slots, nomes estáveis pro binder) ---
            const float RowH = 80f, RowGap = 12f;
            var list = UIBuilder.NewGO("MissionList", s.transform);
            col.Push(list, RowH * 3 + RowGap * 2);
            for (int i = 0; i < 3; i++)
            {
                var row = UIBuilder.Image9("Mission_" + i, list.transform, t.panel_md, Color.white);
                UIBuilder.StretchHTop((RectTransform)row.transform, 0f, 0f, i * (RowH + RowGap), RowH);
                row.AddComponent<MissionEntryUI>();

                var desc = UIBuilder.TxtTop(row.transform, "Description", "Collect 100 coins",
                    t.barlowBold, 13f, Color.white, TextAlignmentOptions.Left, 14f, 18f, 16f, 108f);
                var prog = UIBuilder.TxtTop(row.transform, "Progress", "0 / 100",
                    t.barlowMedium, 11f, t.textMid, TextAlignmentOptions.Left, 34f, 16f, 16f, 108f);
                var reward = UIBuilder.TxtTop(row.transform, "Reward", "+50 coins",
                    t.barlowSemiBold, 11f, t.coins, TextAlignmentOptions.Left, 54f, 16f, 16f, 108f);

                var claim = SmallButton(row.transform, "ClaimButton", "CLAIM", t, t.btn_sm_green, t.safe);
                var clRt = (RectTransform)claim.transform;
                clRt.anchorMin = new Vector2(1, 0.5f); clRt.anchorMax = new Vector2(1, 0.5f);
                clRt.pivot = new Vector2(1, 0.5f);
                clRt.anchoredPosition = new Vector2(-14f, 0f);
                clRt.sizeDelta = new Vector2(84f, 36f);

                // Wire do MissionEntryUI (o binder só precisa dar Bind depois)
                var entry = row.GetComponent<MissionEntryUI>();
                var eso = new SerializedObject(entry);
                eso.FindProperty("descriptionText").objectReferenceValue = desc;
                eso.FindProperty("progressText").objectReferenceValue = prog;
                eso.FindProperty("rewardText").objectReferenceValue = reward;
                eso.FindProperty("claimButton").objectReferenceValue = claim.GetComponent<Button>();
                eso.FindProperty("claimButtonText").objectReferenceValue =
                    claim.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                eso.ApplyModifiedProperties();
            }

            // --- Baú de conclusão ---
            var chest = UIBuilder.Image9("Chest", s.transform, t.panel_accent_gold, Color.white);
            col.Push(chest, 88f);
            var chestDark = UITheme.Hex("#2A1600");
            var chestIcon = UIBuilder.Icon("Icon", chest.transform, t.icon_chest, Color.white);
            var ciRt = (RectTransform)chestIcon.transform;
            ciRt.anchorMin = new Vector2(0, 0.5f); ciRt.anchorMax = new Vector2(0, 0.5f);
            ciRt.pivot = new Vector2(0, 0.5f);
            ciRt.anchoredPosition = new Vector2(16f, 0f);
            ciRt.sizeDelta = new Vector2(40f, 40f);
            UIBuilder.TxtTop(chest.transform, "Title", "ALL 3 DAILIES", t.chakraBold, 15f,
                chestDark, TextAlignmentOptions.Left, 20f, 20f, 68f, 110f, uppercase: true);
            UIBuilder.TxtTop(chest.transform, "Sub", "Complete all to unlock", t.barlowMedium, 11f,
                UITheme.WithA(chestDark, 0.75f), TextAlignmentOptions.Left, 44f, 16f, 68f, 110f);
            var chestBtn = SmallButton(chest.transform, "ChestButton", "0/3", t, t.btn_sm_gold, chestDark);
            var cbRt = (RectTransform)chestBtn.transform;
            cbRt.anchorMin = new Vector2(1, 0.5f); cbRt.anchorMax = new Vector2(1, 0.5f);
            cbRt.pivot = new Vector2(1, 0.5f);
            cbRt.anchoredPosition = new Vector2(-14f, 0f);
            cbRt.sizeDelta = new Vector2(84f, 38f);

            return s;
        }

        // ==================== S8 · LEADERBOARD ====================
        static GameObject BuildLeaderboardScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Leaderboard");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "LEADERBOARD", t), HeaderH);
            col.Push(Tabs(s.transform, new[] { "DAILY", "GLOBAL" }), 42f);

            // --- Pódio (3 colunas posicionadas à mão, ancoradas na base) ---
            const float PodiumH = 176f;
            var podium = UIBuilder.NewGO("Podium", s.transform);
            col.Push(podium, PodiumH);
            var colW = 108f;
            var colGap = (ContentW - colW * 3) / 2f;
            var ranks = new (int rank, float h, Color c)[]
            {
                (2, 118f, UITheme.WithA(Color.white, 0.16f)),
                (1, 152f, UITheme.WithA(t.coins, 0.28f)),
                (3, 104f, UITheme.WithA(Color.white, 0.10f)),
            };
            for (int i = 0; i < 3; i++)
            {
                var (rank, h, c) = ranks[i];
                var podCol = UIBuilder.Image9("Pos_" + rank, podium.transform, t.panel_md, c);
                var pRt = (RectTransform)podCol.transform;
                pRt.anchorMin = new Vector2(0, 0); pRt.anchorMax = new Vector2(0, 0);
                pRt.pivot = new Vector2(0, 0);
                pRt.anchoredPosition = new Vector2(i * (colW + colGap), 0f);
                pRt.sizeDelta = new Vector2(colW, h);

                UIBuilder.TxtTop(podCol.transform, "Rank", "#" + rank, t.chakraBold, 20f,
                    rank == 1 ? t.coins : Color.white, TextAlignmentOptions.Center, 10f, 26f, 6f, 6f);
                UIBuilder.TxtTop(podCol.transform, "Name", "—", t.barlowSemiBold, 11f,
                    t.textMid, TextAlignmentOptions.Center, 38f, 16f, 6f, 6f);
                UIBuilder.TxtBottom(podCol.transform, "Score", "0 m", t.chakraBold, 14f,
                    Color.white, TextAlignmentOptions.Center, 10f, 20f, 6f, 6f);
            }

            // --- Lista (container que o LeaderboardPanelController popula) ---
            const float RowH = 54f, RowGap = 8f;
            var list = UIBuilder.NewGO("EntriesContainer", s.transform);
            col.Push(list, RowH * 4 + RowGap * 3);
            var vl = list.AddComponent<VerticalLayoutGroup>();
            vl.spacing = RowGap;
            vl.childControlWidth = true;  vl.childForceExpandWidth = true;
            vl.childControlHeight = false; vl.childForceExpandHeight = false;
            vl.childAlignment = TextAnchor.UpperCenter;

            // --- Linha "você" ---
            var you = UIBuilder.Image9("MyRankBanner", s.transform, t.panel_md, UITheme.WithA(t.accent, 0.14f));
            col.Push(you, 56f);
            UIBuilder.TxtTop(you.transform, "Label", "YOU", t.barlowBold, 11f,
                t.accent, TextAlignmentOptions.Left, 10f, 14f, 16f, 16f, uppercase: true, tracking: 0.18f);
            UIBuilder.TxtTop(you.transform, "Value", "No record yet", t.barlowMedium, 12f,
                Color.white, TextAlignmentOptions.Left, 28f, 18f, 16f, 16f);

            return s;
        }

        // ==================== S9 · PROFILE ====================
        static GameObject BuildProfileScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Profile");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "PROFILE", t), HeaderH);

            // --- Identidade ---
            var id = UIBuilder.Image9("Identity", s.transform, t.panel_lg, Color.white);
            col.Push(id, 126f);
            var avatar = UIBuilder.Image9("Avatar", id.transform, t.frame_epic, Color.white);
            var aRt = (RectTransform)avatar.transform;
            aRt.anchorMin = new Vector2(0, 0.5f); aRt.anchorMax = new Vector2(0, 0.5f);
            aRt.pivot = new Vector2(0, 0.5f);
            aRt.anchoredPosition = new Vector2(16f, 0f);
            aRt.sizeDelta = new Vector2(76f, 76f);
            UIBuilder.TxtTop(id.transform, "Name", "Player", t.chakraBold, 22f,
                Color.white, TextAlignmentOptions.Left, 28f, 30f, 104f, 16f);
            UIBuilder.TxtTop(id.transform, "Level", "Lv. 1", t.barlowSemiBold, 12f,
                t.accent, TextAlignmentOptions.Left, 60f, 18f, 104f, 16f, uppercase: true, tracking: 0.14f);

            // --- Grid de stats ---
            var grid = Grid(s.transform, "Stats", new Vector2(173f, 96f), new Vector2(12f, 12f), 2);
            col.Push(grid, 204f);
            var stats = new (string label, Color c)[]
            {
                ("BEST DISTANCE", t.accent),
                ("TOTAL RUNS",    Color.white),
                ("COINS EARNED",  t.coins),
                ("BEST TIER",     t.gems),
            };
            foreach (var (label, c) in stats)
            {
                var card = UIBuilder.Image9("Stat_" + label.Replace(" ", ""), grid.transform,
                    t.panel_md, UITheme.WithA(c, 0.12f));
                UIBuilder.TxtTop(card.transform, "Value", "0", t.chakraBold, 24f,
                    Color.white, TextAlignmentOptions.Center, 22f, 32f, 10f, 10f);
                UIBuilder.TxtBottom(card.transform, "Label", label, t.barlowSemiBold, 11f,
                    t.textLow, TextAlignmentOptions.Center, 14f, 16f, 6f, 6f, uppercase: true, tracking: 0.08f);
            }

            // --- Editar nome ---
            var edit = GhostButton(s.transform, "Btn_EditName", "EDIT NAME", t, 16f);
            col.Push(edit, 54f);

            return s;
        }

        // ==================== S10 · SETTINGS ====================
        static GameObject BuildSettingsScreen(Transform parent, UITheme t)
        {
            var s = Screen(parent, "Settings");
            var col = new UIBuilder.Col(s.transform, 0f, PadX, PadX, Gap);

            col.Push(Header(s.transform, "SETTINGS", t), HeaderH);

            col.Push(SettingsSection(s.transform, "AUDIO",
                new[] { "Music", "Effects" }, t), 132f);
            col.Push(SettingsSection(s.transform, "GAMEPLAY",
                new[] { "Vibration", "Left-handed mode" }, t), 132f);
            col.Push(SettingsSection(s.transform, "ACCOUNT",
                new[] { "Sincronizar dados" }, t), 96f);

            return s;
        }

        static GameObject SettingsSection(Transform parent, string title, string[] rows, UITheme t)
        {
            var sec = UIBuilder.Image9("Section_" + title, parent, t.panel_md, Color.white);
            UIBuilder.TxtTop(sec.transform, "SectionLabel", title, t.barlowSemiBold, 11f,
                t.accent, TextAlignmentOptions.Left, 12f, 16f, 16f, 16f, uppercase: true, tracking: 0.18f);

            const float RowH = 40f;
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 34f + i * RowH;
                UIBuilder.TxtTop(sec.transform, "Row_" + rows[i], rows[i], t.barlowMedium, 13f,
                    Color.white, TextAlignmentOptions.Left, y + 10f, 20f, 16f, 90f);

                var ctrl = UIBuilder.Image9("Ctrl_" + rows[i], sec.transform, t.pill_dark,
                    UITheme.WithA(Color.white, 0.10f));
                var cRt = (RectTransform)ctrl.transform;
                cRt.anchorMin = new Vector2(1, 1); cRt.anchorMax = new Vector2(1, 1);
                cRt.pivot = new Vector2(1, 1);
                cRt.anchoredPosition = new Vector2(-16f, -(y + 4f));
                cRt.sizeDelta = new Vector2(64f, 30f);
                ctrl.GetComponent<Image>().raycastTarget = true;
                ctrl.AddComponent<Button>();
                var lbl = UIBuilder.Text("Label", ctrl.transform, "ON", t.barlowSemiBold, 11f,
                    t.safe, TextAlignmentOptions.Center, uppercase: true, tracking: 0.08f);
                UIBuilder.Stretch((RectTransform)lbl.transform);
            }
            return sec;
        }

        // ==================== HELPERS ====================

        static GameObject Screen(Transform parent, string name)
        {
            var s = UIBuilder.NewGO("Screen_" + name, parent);
            UIBuilder.Stretch((RectTransform)s.transform);
            // Pop bem sutil na troca de aba (ScreenNavigator faz SetActive).
            // Valores baixos de propósito — navegação tem que continuar rápida.
            var pop = s.AddComponent<UIPopOnEnable>();
            var pso = new SerializedObject(pop);
            pso.FindProperty("from").floatValue = 0.97f;
            pso.FindProperty("duration").floatValue = 0.15f;
            pso.ApplyModifiedProperties();
            return s;
        }

        /// <summary>Header de tela: back (opcional) à esquerda + título centralizado.</summary>
        static GameObject Header(Transform parent, string title, UITheme t, bool back = true)
        {
            var h = UIBuilder.NewGO("Header", parent);

            if (back)
            {
                var b = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
                b.transform.SetParent(h.transform, false);
                var bRt = (RectTransform)b.transform;
                bRt.anchorMin = new Vector2(0, 0.5f); bRt.anchorMax = new Vector2(0, 0.5f);
                bRt.pivot = new Vector2(0, 0.5f);
                bRt.anchoredPosition = Vector2.zero;
                bRt.sizeDelta = new Vector2(32f, 32f);
                var bImg = b.GetComponent<Image>();
                bImg.sprite = t.btn_icon_back;
                bImg.type = Image.Type.Simple;
            }

            // Padding lateral 44 dos dois lados pra centralizar sem colidir com o back.
            UIBuilder.TxtTop(h.transform, "Title", title, t.barlowBold, 17f,
                Color.white, TextAlignmentOptions.Center, 8f, 22f, 44f, 44f,
                uppercase: true, tracking: 0.10f);
            return h;
        }

        static GameObject Tabs(Transform parent, string[] labels)
        {
            var go = InstantiatePrefab(BasePrefabs + "Tabs.prefab", parent);
            if (go == null) return UIBuilder.NewGO("Tabs_missing", parent);
            var ui = go.GetComponent<TabsUI>();
            if (ui != null)
            {
                // Declarativo: TabsUI.Start() constrói as tabs em runtime, com as
                // âncoras já resolvidas. Construir aqui usaria rect.width = 0.
                var so = new SerializedObject(ui);
                var arr = so.FindProperty("initialTabs");
                arr.arraySize = labels.Length;
                for (int i = 0; i < labels.Length; i++)
                    arr.GetArrayElementAtIndex(i).stringValue = labels[i];
                so.ApplyModifiedProperties();
            }
            return go;
        }

        static GameObject Grid(Transform parent, string name, Vector2 cell, Vector2 spacing, int cols)
        {
            var go = UIBuilder.NewGO(name, parent);
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = cols;
            g.childAlignment = TextAnchor.UpperCenter;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            return go;
        }

        static GameObject PrimaryButton(Transform parent, string name, string label, UITheme t, float size)
            => StyledButton(parent, name, label, t, t.btn_primary, t.btn_primary_pressed, UITheme.Hex("#05210D"), size);

        static GameObject PremiumButton(Transform parent, string name, string label, UITheme t, float size)
            => StyledButton(parent, name, label, t, t.btn_premium, t.btn_premium_pressed, Color.white, size);

        static GameObject PurchaseButton(Transform parent, string name, string label, UITheme t, float size)
            => StyledButton(parent, name, label, t, t.btn_purchase, t.btn_purchase_pressed, UITheme.Hex("#2A1600"), size);

        static GameObject GhostButton(Transform parent, string name, string label, UITheme t, float size)
            => StyledButton(parent, name, label, t, t.btn_ghost, null, Color.white, size);

        static GameObject StyledButton(Transform parent, string name, string label, UITheme t,
            Sprite normal, Sprite pressed, Color labelColor, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), normal);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (pressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var st = btn.spriteState; st.pressedSprite = pressed; btn.spriteState = st;
            }
            var lbl = UIBuilder.Text("Label", go.transform, label, t.chakraBold, size, labelColor,
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.08f);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            return go;
        }

        static GameObject SmallButton(Transform parent, string name, string label, UITheme t,
            Sprite sprite, Color labelColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), sprite);
            img.raycastTarget = true;
            go.GetComponent<Button>().targetGraphic = img;
            var lbl = UIBuilder.Text("Label", go.transform, label, t.barlowBold, 12f, labelColor,
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            return go;
        }

        static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[MetaCanvas] Prefab não achado: " + path); return null; }
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string acc = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = acc + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(acc, parts[i]);
                acc = next;
            }
        }
    }
}
#endif
