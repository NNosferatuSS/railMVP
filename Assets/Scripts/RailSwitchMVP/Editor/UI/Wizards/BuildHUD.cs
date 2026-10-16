#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Core;
using RailSwitchMVP.UI;
using RailSwitchMVP.UI.Theme;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Constrói Assets/UI/Prefabs/HUD/Canvas_HUD.prefab (spec 2a).
    ///
    /// REGRAS DE RAYCAST — importante: TouchDirectionalInput.IsOverUI() usa
    /// EventSystem.RaycastAll e descarta o toque se QUALQUER coisa for atingida.
    /// Então nada no HUD pode ter raycastTarget ligado além do botão de pause.
    /// (A versão anterior tinha LeftZone/RightZone invisíveis cobrindo os 150px
    /// de baixo da tela inteira — engoliam todo swipe naquela faixa.)
    ///
    /// Menu: Tools -> RailSwitch -> Build UI -> 4. Build HUD.
    /// </summary>
    public static class BuildHUD
    {
        const string OutPath = "Assets/UI/Prefabs/HUD/Canvas_HUD.prefab";

        [MenuItem("Tools/RailSwitch/Build UI/4. Build HUD (in-game)", priority = 40)]
        public static void Build()
        {
            var t = UITheme.Current;
            if (t == null) { Debug.LogError("Rode 1. Import Sprites primeiro."); return; }
            EnsureFolder("Assets/UI/Prefabs/HUD");

            var canvasGO = new GameObject("Canvas_HUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(CanvasGroup), typeof(HUDViewController), typeof(GameSceneBinder));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = t.referenceResolution;
            scaler.matchWidthOrHeight = t.matchWidthOrHeight;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            var root = canvasGO.transform;

            // ---------- Gradiente do topo (4 faixas fake — não temos PNG de gradiente) ----------
            // Uma faixa sólida única deixava uma borda dura horizontal cortando a tela.
            var gradient = UIBuilder.NewGO("TopGradient", root);
            UIBuilder.StretchHTop((RectTransform)gradient.transform, 0, 0, 0, 200f);
            var alphas = new[] { 0.62f, 0.44f, 0.26f, 0.10f };
            for (int i = 0; i < alphas.Length; i++)
            {
                var band = UIBuilder.SolidColor("Band_" + i, gradient.transform,
                    new Color(t.bgDeep.r, t.bgDeep.g, t.bgDeep.b, alphas[i]));
                UIBuilder.StretchHTop((RectTransform)band.transform, 0, 0, i * 50f, 50f);
            }

            // ---------- Top-left: distância ----------
            // Row com HorizontalLayoutGroup pra que o "M" acompanhe o número.
            // (Antes o sufixo era hardcoded em x=78 e o número passava por cima
            // assim que a distância chegava a 4 dígitos.)
            var distRow = UIBuilder.NewGO("DistanceRow", root);
            UIBuilder.TopLeft((RectTransform)distRow.transform, 16f, 10f, 300f, 46f);
            var dLayout = distRow.AddComponent<HorizontalLayoutGroup>();
            dLayout.childAlignment = TextAnchor.LowerLeft;
            dLayout.spacing = 6f;
            dLayout.childControlWidth = true;   dLayout.childForceExpandWidth = false;
            dLayout.childControlHeight = true;  dLayout.childForceExpandHeight = false;

            var distanceHero = UIBuilder.Text("DistanceHero", distRow.transform, "0",
                t.chakraBold, 36f, Color.white, TextAlignmentOptions.BottomLeft);
            var distanceSuffix = UIBuilder.Text("DistanceSuffix", distRow.transform, "M",
                t.barlowSemiBold, 15f, t.textMid, TextAlignmentOptions.BottomLeft);

            // ---------- Top-left: moedas ----------
            var coinsRow = UIBuilder.NewGO("CoinsRow", root);
            UIBuilder.TopLeft((RectTransform)coinsRow.transform, 16f, 60f, 240f, 24f);
            var cLayout = coinsRow.AddComponent<HorizontalLayoutGroup>();
            cLayout.childAlignment = TextAnchor.MiddleLeft;
            cLayout.spacing = 6f;
            cLayout.childControlWidth = true;   cLayout.childForceExpandWidth = false;
            cLayout.childControlHeight = true;  cLayout.childForceExpandHeight = false;

            var coinIcon = UIBuilder.Icon("CoinIcon", coinsRow.transform, t.icon_coin, Color.white);
            var coinLE = coinIcon.AddComponent<LayoutElement>();
            coinLE.preferredWidth = 18f; coinLE.preferredHeight = 18f;
            var coinsValue = UIBuilder.Text("CoinsValue", coinsRow.transform, "0",
                t.chakraBold, 19f, t.coins, TextAlignmentOptions.MidlineLeft);

            // ---------- Top-left: best badge (linha própria) ----------
            var bestBadge = UIBuilder.Text("BestBadge", root, "BEST 0 M", t.barlowSemiBold, 11f,
                t.textLow, TextAlignmentOptions.Left, uppercase: true, tracking: 0.14f);
            UIBuilder.TopLeft((RectTransform)bestBadge.transform, 16f, 88f, 260f, 16f);

            // ---------- Top-right: tier pill ----------
            var pill = UIBuilder.Image9("TierPill", root, t.pill_dark,
                new Color(t.gems.r, t.gems.g, t.gems.b, 0.30f));
            UIBuilder.TopRight((RectTransform)pill.transform, 16f, 14f, 84f, 30f);

            var pillLabel = UIBuilder.Text("TierLabel", pill.transform, "TIER", t.barlowSemiBold, 11f,
                UITheme.Hex("#C9B3FF"), TextAlignmentOptions.MidlineLeft, uppercase: true, tracking: 0.08f);
            var pLbl = (RectTransform)pillLabel.transform;
            pLbl.anchorMin = new Vector2(0, 0); pLbl.anchorMax = new Vector2(0, 1);
            pLbl.pivot = new Vector2(0, 0.5f);
            pLbl.anchoredPosition = new Vector2(12f, 0f);
            pLbl.sizeDelta = new Vector2(34f, 0f);

            var pillNumber = UIBuilder.Text("Number", pill.transform, "1", t.chakraBold, 16f,
                Color.white, TextAlignmentOptions.MidlineRight);
            var pNum = (RectTransform)pillNumber.transform;
            pNum.anchorMin = new Vector2(1, 0); pNum.anchorMax = new Vector2(1, 1);
            pNum.pivot = new Vector2(1, 0.5f);
            pNum.anchoredPosition = new Vector2(-12f, 0f);
            pNum.sizeDelta = new Vector2(28f, 0f);

            // ---------- Top-right: pause (único raycastTarget do HUD) ----------
            var pauseBtn = new GameObject("PauseButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            pauseBtn.transform.SetParent(root, false);
            UIBuilder.TopRight((RectTransform)pauseBtn.transform, 16f, 52f, 38f, 38f);
            var pauseImg = pauseBtn.GetComponent<Image>();
            pauseImg.sprite = t.btn_icon_pause;
            pauseImg.raycastTarget = true;
            pauseBtn.GetComponent<Button>().targetGraphic = pauseImg;

            // ---------- Hazard banner (abaixo do pause, sem colidir) ----------
            HazardBannerUI hazardBanner = null;
            var bannerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/UI/Prefabs/Base/HazardBanner.prefab");
            if (bannerPrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(bannerPrefab, root);
                UIBuilder.StretchHTop((RectTransform)instance.transform, 14f, 14f, 104f, 56f);
                hazardBanner = instance.GetComponent<HazardBannerUI>();
                instance.SetActive(false);
            }

            // ---------- Pucks de power-up ----------
            var puckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/Prefabs/Base/Puck.prefab");
            var pucks = new PuckUI[3];
            for (int i = 0; i < 3; i++)
            {
                if (puckPrefab == null) break;
                var p = (GameObject)PrefabUtility.InstantiatePrefab(puckPrefab, root);
                p.name = "Puck_" + i;
                UIBuilder.TopLeft((RectTransform)p.transform, 14f, 176f + i * 62f, 54f, 54f);
                pucks[i] = p.GetComponent<PuckUI>();
                p.SetActive(false);
            }

            // ---------- Switch indicator (canto inferior esquerdo) ----------
            var switchGroup = UIBuilder.NewGO("SwitchIndicator", root);
            // 150 de largura cabe as 9 lanes do tier máximo (9*12 + 8*4 = 140).
            UIBuilder.BottomLeft((RectTransform)switchGroup.transform, 20f, 38f, 150f, 52f);

            // Container VAZIO — o HUDViewController gera N barras em runtime a
            // partir de TrackTile.MaxLanesAtSpawn (varia por tier). Barras fixas
            // aqui virariam mentira assim que o tier abrisse mais trilhos.
            var switchBars = UIBuilder.NewGO("SwitchBars", switchGroup.transform);
            UIBuilder.StretchHBottom((RectTransform)switchBars.transform, 0f, 0f, 17f, 32f);

            var switchLabel = UIBuilder.Text("SwitchLabel", switchGroup.transform, "LANE 1 / 3",
                t.barlowSemiBold, 11f, new Color(1, 1, 1, 0.55f),
                TextAlignmentOptions.BottomLeft, uppercase: true, tracking: 0.10f);
            UIBuilder.StretchHBottom((RectTransform)switchLabel.transform, 0f, 0f, 0f, 14f);

            // NOTA: sem LeftZone/RightZone e sem ActiveItemFAB.
            //  - As zonas bloqueavam o TouchDirectionalInput (ver header da classe).
            //  - O FAB de item ativo contradiz a diretriz do projeto: todo power-up
            //    é consumido na colisão, não existe inventário/slot.

            // ---------- Wiring ----------
            var hud = canvasGO.GetComponent<HUDViewController>();
            var so = new SerializedObject(hud);
            so.FindProperty("distanceHero").objectReferenceValue   = distanceHero.GetComponent<TextMeshProUGUI>();
            so.FindProperty("distanceSuffix").objectReferenceValue = distanceSuffix.GetComponent<TextMeshProUGUI>();
            so.FindProperty("coinIcon").objectReferenceValue       = coinIcon.GetComponent<Image>();
            so.FindProperty("coinsValue").objectReferenceValue     = coinsValue.GetComponent<TextMeshProUGUI>();
            so.FindProperty("bestBadge").objectReferenceValue      = bestBadge.GetComponent<TextMeshProUGUI>();
            so.FindProperty("tierPillText").objectReferenceValue   = pillNumber.GetComponent<TextMeshProUGUI>();
            so.FindProperty("pauseButton").objectReferenceValue    = pauseBtn.GetComponent<Button>();
            so.FindProperty("hazardBanner").objectReferenceValue   = hazardBanner;
            so.FindProperty("canvasGroup").objectReferenceValue    = canvasGO.GetComponent<CanvasGroup>();

            var slotsArr = so.FindProperty("puckSlots");
            slotsArr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                slotsArr.GetArrayElementAtIndex(i).objectReferenceValue = pucks[i];

            var iconMap = new (PowerUpType, Sprite)[]
            {
                (PowerUpType.Shield,             t.icon_shield),
                (PowerUpType.SlowDown,           t.icon_time_freeze),
                (PowerUpType.Magnet,             t.icon_magnet),
                (PowerUpType.DoubleCoins,        t.icon_coin_multiplier),
                (PowerUpType.Ghost,              t.icon_ghost),
                (PowerUpType.LanePreview,        t.icon_lane_preview),
                (PowerUpType.CoinRadar,          t.icon_coin_radar),
                (PowerUpType.Teleport,           t.icon_teleport),
                (PowerUpType.AutoCriticalFollow, t.icon_auto_critical),
                (PowerUpType.SpeedUpDebuff,      t.icon_hazard_speedup),
                (PowerUpType.LaneSwapDebuff,     t.icon_hazard_laneswap),
                (PowerUpType.Comet,              AssetDatabase.LoadAssetAtPath<Sprite>(CometSetup.BadgePath)),
            };
            var mapProp = so.FindProperty("puckIconMap");
            mapProp.arraySize = iconMap.Length;
            for (int i = 0; i < iconMap.Length; i++)
            {
                var elem = mapProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("type").intValue = (int)iconMap[i].Item1;
                elem.FindPropertyRelative("icon").objectReferenceValue = iconMap[i].Item2;
            }

            so.FindProperty("switchBars").objectReferenceValue        = (RectTransform)switchBars.transform;
            so.FindProperty("switchLabel").objectReferenceValue       = switchLabel.GetComponent<TextMeshProUGUI>();
            so.FindProperty("switchActiveColor").colorValue           = t.accent;
            so.FindProperty("switchIdleColor").colorValue             = new Color(1, 1, 1, 0.22f);
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvasGO, OutPath);
            Object.DestroyImmediate(canvasGO);
            AssetDatabase.SaveAssets();

            Debug.Log("[RailSwitchUI] HUD prefab: " + OutPath);
            UIBuildContext.Info("Rail Switch UI",
                "Canvas_HUD reconstruído. Use 'Apply Prefabs to Scenes' pra sincronizar a cena.");
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
