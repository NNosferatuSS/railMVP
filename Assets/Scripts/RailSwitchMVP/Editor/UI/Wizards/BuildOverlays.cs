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
    /// Cria 3 prefabs Canvas em Assets/UI/Prefabs/Overlays/:
    ///  - Canvas_GameOver (usa GameOverController legado)
    ///  - Canvas_Pause (usa PauseController novo)
    ///  - Canvas_Warmup (usa WarmupCountdownUI novo)
    /// Menu: Tools -> RailSwitch -> Build UI -> 5. Build Overlays.
    /// </summary>
    public static class BuildOverlays
    {
        const string OutRoot = "Assets/UI/Prefabs/Overlays";

        [MenuItem("Tools/RailSwitch/Build UI/5. Build Overlays", priority = 50)]
        public static void BuildAll()
        {
            var t = UITheme.Current;
            if (t == null) { Debug.LogError("Rode 1. Import Sprites primeiro."); return; }
            EnsureFolder(OutRoot);

            SafeRun("GameOver", () => BuildGameOver(t));
            SafeRun("Pause",    () => BuildPause(t));
            SafeRun("Warmup",   () => BuildWarmup(t));

            AssetDatabase.SaveAssets();
            UIBuildContext.Info("Rail Switch UI", "Overlays finalizado — cheque Console pra falhas.");
        }

        static void SafeRun(string label, System.Action a)
        {
            try { a(); Debug.Log("[Overlays][OK] " + label); }
            catch (System.Exception ex) { Debug.LogError("[Overlays][FAIL] " + label + "\n" + ex); }
        }

        static Canvas MakeCanvas(string name, int sortOrder, UITheme t)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = t.referenceResolution;
            s.matchWidthOrHeight = t.matchWidthOrHeight;
            return c;
        }

        static GameObject Scrim(Transform parent, UITheme t)
        {
            var g = UIBuilder.SolidColor("Scrim", parent, t.scrimColor);
            UIBuilder.Stretch((RectTransform)g.transform);
            g.GetComponent<Image>().raycastTarget = true;
            return g;
        }

        // ---------- GameOver ----------
        static void BuildGameOver(UITheme t)
        {
            var canvas = MakeCanvas("Canvas_GameOver", 10, t);
            var root = canvas.transform;

            // TUDO fica dentro de Content. GameOverController.Start faz panel.SetActive(false),
            // então apontamos `panel` field pro Content — hide/show único p/ scrim + card + botões.
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root, false);
            UIBuilder.Stretch((RectTransform)content.transform);

            var scrim = Scrim(content.transform, t);
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(content.transform, false);
            UIBuilder.Stretch((RectTransform)panel.transform);
            // Pop sutil ao aparecer (OnEnable dispara quando o Content é ativado).
            panel.AddComponent<UIPopOnEnable>();

            // Header
            var deadEnd = UIBuilder.Text("DeadEndLabel", panel.transform, "DEAD END", t.barlowBold, 12f,
                t.danger, TextAlignmentOptions.Center, uppercase: true, tracking: 0.40f);
            UIBuilder.TopCenter((RectTransform)deadEnd.transform, 90, 260, 16);
            var title = UIBuilder.Text("Title", panel.transform, "RUN OVER", t.chakraBold, 34f,
                Color.white, TextAlignmentOptions.Center, uppercase: true, tracking: 0.08f);
            UIBuilder.TopCenter((RectTransform)title.transform, 110, 320, 40);

            // Painel de resultado (panel_lg)
            var result = UIBuilder.Image9("ResultPanel", panel.transform, t.panel_lg, Color.white);
            UIBuilder.StretchHTop((RectTransform)result.transform, 24, 24, 184, 300);

            // Distance hero + suffix
            var distHero = UIBuilder.Text("Distance", result.transform, "0", t.chakraBold, 58f, t.accent,
                TextAlignmentOptions.Center);
            var dhRt = (RectTransform)distHero.transform;
            dhRt.anchorMin = new Vector2(0, 1); dhRt.anchorMax = new Vector2(1, 1);
            dhRt.pivot = new Vector2(0.5f, 1);
            dhRt.anchoredPosition = new Vector2(0, -18);
            dhRt.sizeDelta = new Vector2(0, 72);

            var distSuffix = UIBuilder.Text("Suffix", result.transform, "M", t.barlowSemiBold, 20f,
                new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center);
            UIBuilder.TopCenter((RectTransform)distSuffix.transform, 88, 40, 24);

            var bestLine = UIBuilder.Text("BestLine", result.transform, "BEST 0 M · TIER 1 REACHED", t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, uppercase: true, tracking: 0.18f);
            UIBuilder.TopCenter((RectTransform)bestLine.transform, 118, 320, 14);

            // Divisor
            var divisor = UIBuilder.SolidColor("Divisor", result.transform, new Color(1, 1, 1, 0.12f));
            var divRt = (RectTransform)divisor.transform;
            divRt.anchorMin = new Vector2(0, 1); divRt.anchorMax = new Vector2(1, 1);
            divRt.pivot = new Vector2(0.5f, 1); divRt.anchoredPosition = new Vector2(0, -142);
            divRt.sizeDelta = new Vector2(-40, 1); divRt.offsetMin = new Vector2(20, divRt.offsetMin.y);
            divRt.offsetMax = new Vector2(-20, divRt.offsetMax.y);

            // 3 cards
            var cards = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cards.transform.SetParent(result.transform, false);
            var crt = (RectTransform)cards.transform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1); crt.anchoredPosition = new Vector2(0, -154);
            crt.sizeDelta = new Vector2(-40, 78); crt.offsetMin = new Vector2(20, -232); crt.offsetMax = new Vector2(-20, -154);
            var hl = cards.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true;

            var coinsCard = ResultCard(cards.transform, "COINS", "0", t.coins, t);
            var xpCard = ResultCard(cards.transform, "BATTLE PASS XP", "+0 XP", t.gems, t);
            var missionCard = ResultCard(cards.transform, "MISSIONS", "0/3", t.safe, t);

            // Continue por gem
            var contPanel = UIBuilder.Image9("ContinuePanel", panel.transform, t.panel_md,
                new Color(t.gems.r, t.gems.g, t.gems.b, 0.18f));
            UIBuilder.StretchHBottom((RectTransform)contPanel.transform, 24, 24, 150, 62);
            var contGem = UIBuilder.Icon("Gem", contPanel.transform, t.icon_gem, Color.white);
            var cgRt = (RectTransform)contGem.transform;
            cgRt.anchorMin = new Vector2(0, 0.5f); cgRt.anchorMax = new Vector2(0, 0.5f);
            cgRt.pivot = new Vector2(0, 0.5f); cgRt.anchoredPosition = new Vector2(14, 0); cgRt.sizeDelta = new Vector2(26, 26);

            var contTitle = UIBuilder.Text("Title", contPanel.transform, "CONTINUE RUN", t.chakraBold, 14f, Color.white,
                TextAlignmentOptions.Left, uppercase: true);
            var ctRt = (RectTransform)contTitle.transform;
            ctRt.anchorMin = new Vector2(0, 0.5f); ctRt.anchorMax = new Vector2(0, 0.5f);
            ctRt.pivot = new Vector2(0, 0.5f); ctRt.anchoredPosition = new Vector2(48, 8); ctRt.sizeDelta = new Vector2(180, 18);
            var contSub = UIBuilder.Text("Sub", contPanel.transform, "Respawn on the safe path", t.barlowMedium, 11f,
                new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left);
            var csRt = (RectTransform)contSub.transform;
            csRt.anchorMin = new Vector2(0, 0.5f); csRt.anchorMax = new Vector2(0, 0.5f);
            csRt.pivot = new Vector2(0, 0.5f); csRt.anchoredPosition = new Vector2(48, -8); csRt.sizeDelta = new Vector2(180, 14);
            var contCost = UIBuilder.Text("Cost", contPanel.transform, "2", t.chakraBold, 18f, Color.white,
                TextAlignmentOptions.Right);
            var ccRt = (RectTransform)contCost.transform;
            ccRt.anchorMin = new Vector2(1, 0.5f); ccRt.anchorMax = new Vector2(1, 0.5f);
            ccRt.pivot = new Vector2(1, 0.5f); ccRt.anchoredPosition = new Vector2(-14, 0); ccRt.sizeDelta = new Vector2(50, 20);

            // UIBuilder.Image9 desliga raycastTarget — sem religar, o Button abaixo
            // nunca receberia clique.
            var contImg = contPanel.GetComponent<Image>();
            contImg.raycastTarget = true;
            var contBtn = contPanel.AddComponent<Button>();
            contBtn.targetGraphic = contImg;

            // RUN AGAIN
            var runAgain = MakePrimaryButton(panel.transform, "RUN AGAIN", t, out var runAgainBtn);
            UIBuilder.StretchHBottom((RectTransform)runAgain.transform, 24, 24, 76, 62);

            // Rodapé HOME · LEADERBOARD · SHARE
            var footer = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            footer.transform.SetParent(panel.transform, false);
            UIBuilder.StretchHBottom((RectTransform)footer.transform, 24, 24, 26, 22);
            var fhl = footer.GetComponent<HorizontalLayoutGroup>();
            fhl.spacing = 0; fhl.childAlignment = TextAnchor.MiddleCenter;
            fhl.childForceExpandWidth = true; fhl.childControlWidth = true; fhl.childControlHeight = true;
            Button homeBtn = null;
            foreach (var lbl in new[] { "HOME", "LEADERBOARD", "SHARE" })
            {
                // Container (Image + Button) + filho TMP — mesmo motivo do TabsUI.
                var s = new GameObject(lbl, typeof(RectTransform), typeof(Image), typeof(Button));
                s.transform.SetParent(footer.transform, false);
                s.GetComponent<Image>().color = new Color(0, 0, 0, 0);
                var txt = UIBuilder.Text("Label", s.transform, lbl, t.barlowSemiBold, 11f,
                    new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, uppercase: true, tracking: 0.18f);
                UIBuilder.Stretch((RectTransform)txt.transform);
                if (lbl == "HOME") homeBtn = s.GetComponent<Button>();
            }

            // GameOverController legado. `panel` field controla o hide/show do overlay
            // inteiro (Start faz SetActive(false), DeathSequence faz SetActive(true)).
            // Apontamos pro Content wrapper, não pro Panel interno — assim o Scrim
            // também some ao iniciar a cena.
            var ctrl = canvas.gameObject.AddComponent<GameOverController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("panel").objectReferenceValue = content;
            so.FindProperty("reasonText").objectReferenceValue = deadEnd.GetComponent<TextMeshProUGUI>();
            so.FindProperty("restartButton").objectReferenceValue = runAgainBtn;
            so.FindProperty("homeButton").objectReferenceValue = homeBtn;
            // distanceText / coinsText / bestTierText / xpGainedText ficam NULL de
            // propósito: o controller escreve frases inteiras neles ("Distance: 0 m
            // (Best: 8537)"), o que estoura o hero de 58pt e os cards de 18pt.
            // Quem formata o layout novo é o GameOverView abaixo, lendo os valores crus.
            so.ApplyModifiedProperties();

            var view = canvas.gameObject.AddComponent<GameOverView>();
            var vso = new SerializedObject(view);
            vso.FindProperty("controller").objectReferenceValue    = ctrl;
            vso.FindProperty("distanceValue").objectReferenceValue = distHero.GetComponent<TextMeshProUGUI>();
            vso.FindProperty("bestLine").objectReferenceValue      = bestLine.GetComponent<TextMeshProUGUI>();
            vso.FindProperty("coinsValue").objectReferenceValue    = coinsCard.value;
            vso.FindProperty("xpValue").objectReferenceValue       = xpCard.value;
            vso.FindProperty("missionsValue").objectReferenceValue = missionCard.value;
            vso.FindProperty("continueRow").objectReferenceValue      = contPanel;
            vso.FindProperty("continueButton").objectReferenceValue   = contBtn;
            vso.FindProperty("continueCost").objectReferenceValue     = contCost.GetComponent<TextMeshProUGUI>();
            vso.FindProperty("continueSubtitle").objectReferenceValue = contSub.GetComponent<TextMeshProUGUI>();
            vso.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, OutRoot + "/Canvas_GameOver.prefab");
            Object.DestroyImmediate(canvas.gameObject);
        }

        struct CardRefs { public TextMeshProUGUI value; public TextMeshProUGUI label; }
        static CardRefs ResultCard(Transform parent, string label, string value, Color accent, UITheme t)
        {
            var card = new GameObject("Card_" + label, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var img = card.GetComponent<Image>();
            img.color = new Color(accent.r, accent.g, accent.b, 0.10f);
            img.raycastTarget = false;

            var v = UIBuilder.Text("Value", card.transform, value, t.chakraBold, 18f, Color.white,
                TextAlignmentOptions.Center);
            UIBuilder.TopCenter((RectTransform)v.transform, 14, 100, 26);
            var l = UIBuilder.Text("Label", card.transform, label, t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
            UIBuilder.BottomCenter((RectTransform)l.transform, 10, 100, 14);
            return new CardRefs {
                value = v.GetComponent<TextMeshProUGUI>(),
                label = l.GetComponent<TextMeshProUGUI>()
            };
        }

        static GameObject MakePrimaryButton(Transform parent, string label, UITheme t, out Button btn)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), t.btn_primary);
            btn = go.GetComponent<Button>();
            var s = btn.spriteState; s.pressedSprite = t.btn_primary_pressed; btn.spriteState = s;
            btn.transition = Selectable.Transition.SpriteSwap; btn.targetGraphic = img;
            var lbl = UIBuilder.Text("Label", go.transform, label, t.chakraBold, 24f,
                UITheme.Hex("#05210D"), TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            return go;
        }

        // ---------- Pause ----------
        static void BuildPause(UITheme t)
        {
            var canvas = MakeCanvas("Canvas_Pause", 12, t);
            var canvasRoot = canvas.transform;

            // Wrapper que a gente aponta como `panel` do PauseController — Start
            // do controller faz SetActive(false), então nasce tudo escondido
            // (scrim + card + botões inferiores).
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(canvasRoot, false);
            UIBuilder.Stretch((RectTransform)content.transform);
            var root = content.transform;

            Scrim(root, t);
            var panel = UIBuilder.Image9("Panel", root, t.panel_lg, Color.white);
            UIBuilder.StretchHTop((RectTransform)panel.transform, 24, 24, 150, 500);
            panel.AddComponent<UIPopOnEnable>();

            var pauseLabel = UIBuilder.Text("Label", panel.transform, "PAUSED", t.barlowBold, 11f,
                new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center, uppercase: true, tracking: 0.40f);
            UIBuilder.TopCenter((RectTransform)pauseLabel.transform, 24, 200, 14);
            var distanceText = UIBuilder.Text("Distance", panel.transform, "0", t.chakraBold, 40f,
                Color.white, TextAlignmentOptions.Center);
            UIBuilder.TopCenter((RectTransform)distanceText.transform, 44, 300, 46);

            var statsRow = new GameObject("StatsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            statsRow.transform.SetParent(panel.transform, false);
            UIBuilder.StretchHTop((RectTransform)statsRow.transform, 20, 20, 96, 24);
            var srh = statsRow.GetComponent<HorizontalLayoutGroup>();
            srh.spacing = 12; srh.childAlignment = TextAnchor.MiddleCenter;
            var coinsText = UIBuilder.Text("Coins", statsRow.transform, "0", t.chakraBold, 15f, t.coins,
                TextAlignmentOptions.Left);
            var tierText = UIBuilder.Text("Tier", statsRow.transform, "TIER 1", t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left, uppercase: true, tracking: 0.18f);

            // Sliders
            var musicRow = SettingRow(panel.transform, "MUSIC", t, out var musicSlider);
            UIBuilder.StretchHTop((RectTransform)musicRow.transform, 20, 20, 152, 32);
            var sfxRow = SettingRow(panel.transform, "SFX", t, out var sfxSlider);
            UIBuilder.StretchHTop((RectTransform)sfxRow.transform, 20, 20, 190, 32);
            var hapticsRow = ToggleRow(panel.transform, "HAPTICS", t, out var hapticsToggle);
            UIBuilder.StretchHTop((RectTransform)hapticsRow.transform, 20, 20, 228, 32);

            var info = UIBuilder.Text("Info", panel.transform, "Active power-ups keep their timers while paused.",
                t.barlowMedium, 11f, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Center);
            UIBuilder.StretchHTop((RectTransform)info.transform, 20, 20, 280, 30);

            // Resume + Restart + Quit
            var resume = MakePrimaryButton(root, "RESUME", t, out var resumeBtn);
            UIBuilder.StretchHBottom((RectTransform)resume.transform, 24, 24, 150, 62);
            var restart = MakeGhostButton(root, "RESTART", t, out var restartBtn);
            var quit = MakeDangerButton(root, "QUIT", t, out var quitBtn);
            var restartRt = (RectTransform)restart.transform;
            restartRt.anchorMin = new Vector2(0, 0); restartRt.anchorMax = new Vector2(0.5f, 0);
            restartRt.pivot = new Vector2(0, 0); restartRt.offsetMin = new Vector2(24, 76);
            restartRt.offsetMax = new Vector2(-6, 128);
            var quitRt = (RectTransform)quit.transform;
            quitRt.anchorMin = new Vector2(0.5f, 0); quitRt.anchorMax = new Vector2(1, 0);
            quitRt.pivot = new Vector2(1, 0); quitRt.offsetMin = new Vector2(6, 76);
            quitRt.offsetMax = new Vector2(-24, 128);

            var warn = UIBuilder.Text("Warn", root, "Quitting loses this run — coins are kept.", t.barlowMedium, 11f,
                new Color(1, 1, 1, 0.4f), TextAlignmentOptions.Center);
            UIBuilder.StretchHBottom((RectTransform)warn.transform, 24, 24, 44, 14);

            var ctrl = canvas.gameObject.AddComponent<PauseController>();
            var so = new SerializedObject(ctrl);
            // Content wrapper (não o Panel visual) — hide/show único do overlay inteiro.
            so.FindProperty("panel").objectReferenceValue = content;
            so.FindProperty("distanceText").objectReferenceValue = distanceText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("coinsText").objectReferenceValue = coinsText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("tierText").objectReferenceValue = tierText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("hapticsToggle").objectReferenceValue = hapticsToggle;
            so.FindProperty("resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("restartButton").objectReferenceValue = restartBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, OutRoot + "/Canvas_Pause.prefab");
            Object.DestroyImmediate(canvas.gameObject);
        }

        static GameObject SettingRow(Transform parent, string label, UITheme t, out Slider slider)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var lbl = UIBuilder.Text("Label", row.transform, label, t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left, uppercase: true, tracking: 0.14f);
            var lRt = (RectTransform)lbl.transform;
            lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(0, 1);
            lRt.pivot = new Vector2(0, 0.5f); lRt.anchoredPosition = new Vector2(0, 0); lRt.sizeDelta = new Vector2(52, 0);

            var sldGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sldGO.transform.SetParent(row.transform, false);
            var sRt = (RectTransform)sldGO.transform;
            sRt.anchorMin = new Vector2(0, 0); sRt.anchorMax = new Vector2(1, 1);
            sRt.offsetMin = new Vector2(64, 4); sRt.offsetMax = new Vector2(0, -4);
            slider = sldGO.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0.7f;

            // Hierarquia padrão do Slider do Unity. O Slider dirige SÓ anchorMax.x
            // do fill e anchoredPosition.x do handle — todo o resto (offsets,
            // sizeDelta) tem que estar correto AQUI. A versão anterior criava o
            // Fill com UIBuilder.Image9 (RectTransform default 100x100), e esses
            // 100px de altura viravam um blob gigante cobrindo meia tela.
            var track = UIBuilder.Image9("Background", sldGO.transform, t.slider_track, Color.white);
            var tRt = (RectTransform)track.transform;
            tRt.anchorMin = new Vector2(0, 0.5f); tRt.anchorMax = new Vector2(1, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = Vector2.zero;
            tRt.sizeDelta = new Vector2(0, 8);
            slider.targetGraphic = track.GetComponent<Image>();

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sldGO.transform, false);
            var faRt = (RectTransform)fillArea.transform;
            faRt.anchorMin = new Vector2(0, 0.5f); faRt.anchorMax = new Vector2(1, 0.5f);
            faRt.pivot = new Vector2(0.5f, 0.5f);
            faRt.offsetMin = new Vector2(11f, -4f); faRt.offsetMax = new Vector2(-11f, 4f);

            var fill = UIBuilder.Image9("Fill", fillArea.transform, t.slider_fill, Color.white);
            var fRt = (RectTransform)fill.transform;
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
            fRt.pivot = new Vector2(0.5f, 0.5f);
            fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
            fRt.sizeDelta = Vector2.zero;              // <- crítico
            slider.fillRect = fRt;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sldGO.transform, false);
            var haRt = (RectTransform)handleArea.transform;
            haRt.anchorMin = new Vector2(0, 0); haRt.anchorMax = new Vector2(1, 1);
            haRt.pivot = new Vector2(0.5f, 0.5f);
            haRt.offsetMin = new Vector2(11f, 0f); haRt.offsetMax = new Vector2(-11f, 0f);

            var handle = UIBuilder.Icon("Handle", handleArea.transform, t.slider_knob, Color.white);
            var hRt = (RectTransform)handle.transform;
            hRt.anchorMin = new Vector2(0, 0); hRt.anchorMax = new Vector2(0, 1);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(22f, 0f);      // altura vem da âncora
            slider.handleRect = hRt;
            handle.GetComponent<Image>().raycastTarget = true;
            return row;
        }

        static GameObject ToggleRow(Transform parent, string label, UITheme t, out Toggle toggle)
        {
            var row = new GameObject("Row_" + label, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var lbl = UIBuilder.Text("Label", row.transform, label, t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Left, uppercase: true, tracking: 0.14f);
            var lRt = (RectTransform)lbl.transform;
            lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(0, 1);
            lRt.pivot = new Vector2(0, 0.5f); lRt.anchoredPosition = new Vector2(0, 0); lRt.sizeDelta = new Vector2(80, 0);

            var togGO = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            togGO.transform.SetParent(row.transform, false);
            var tImg = UIBuilder.Slice(togGO.GetComponent<Image>(), t.toggle_off);
            toggle = togGO.GetComponent<Toggle>();
            toggle.targetGraphic = tImg;
            toggle.transition = Selectable.Transition.SpriteSwap;
            var s = toggle.spriteState; s.selectedSprite = t.toggle_on; toggle.spriteState = s;
            var tgRt = (RectTransform)togGO.transform;
            tgRt.anchorMin = new Vector2(1, 0.5f); tgRt.anchorMax = new Vector2(1, 0.5f);
            tgRt.pivot = new Vector2(1, 0.5f); tgRt.anchoredPosition = Vector2.zero; tgRt.sizeDelta = new Vector2(56, 28);
            return row;
        }

        static GameObject MakeGhostButton(Transform parent, string label, UITheme t, out Button btn)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), t.btn_ghost);
            btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var lbl = UIBuilder.Text("Label", go.transform, label, t.chakraBold, 20f, Color.white,
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            return go;
        }

        static GameObject MakeDangerButton(Transform parent, string label, UITheme t, out Button btn)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), t.btn_danger);
            btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var lbl = UIBuilder.Text("Label", go.transform, label, t.chakraBold, 20f, Color.white,
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            return go;
        }

        // ---------- Warmup ----------
        static void BuildWarmup(UITheme t)
        {
            var canvas = MakeCanvas("Canvas_Warmup", 8, t);
            var canvasGO = canvas.gameObject;
            canvasGO.AddComponent<CanvasGroup>();
            var ctrl = canvasGO.AddComponent<WarmupCountdownUI>();

            var container = new GameObject("Content", typeof(RectTransform));
            container.transform.SetParent(canvas.transform, false);
            var crt = (RectTransform)container.transform;
            UIBuilder.Center(crt, 200, 200);

            var big = UIBuilder.Text("Number", container.transform, "3", t.chakraBold, 92f,
                t.accent, TextAlignmentOptions.Center);
            UIBuilder.Stretch((RectTransform)big.transform);

            var so = new SerializedObject(ctrl);
            so.FindProperty("group").objectReferenceValue = canvasGO.GetComponent<CanvasGroup>();
            so.FindProperty("bigNumber").objectReferenceValue = big.GetComponent<TextMeshProUGUI>();
            so.FindProperty("bigRt").objectReferenceValue = container.transform;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvasGO, OutRoot + "/Canvas_Warmup.prefab");
            Object.DestroyImmediate(canvasGO);
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
