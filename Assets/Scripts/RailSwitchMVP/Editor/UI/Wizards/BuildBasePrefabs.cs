#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.UI.Theme;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Cria os prefabs base reutilizáveis em Assets/UI/Prefabs/Base/:
    ///  - Button primary/premium/ghost/danger/purchase
    ///  - Chip coins/gems/tokens
    ///  - Puck (power-up)
    ///  - HazardBanner
    ///  - ProgressBar
    ///  - Tabs
    ///  - Toast
    ///  - BottomNav
    ///
    /// Menu: Tools -> RailSwitch -> Build UI -> 3. Build Base Prefabs.
    /// </summary>
    public static class BuildBasePrefabs
    {
        const string OutRoot = "Assets/UI/Prefabs/Base";

        [MenuItem("Tools/RailSwitch/Build UI/3. Build Base Prefabs", priority = 30)]
        public static void BuildAll()
        {
            var t = UITheme.Current;
            if (t == null) { Debug.LogError("[RailSwitchUI] UITheme não encontrado. Rode 1. Import Sprites primeiro."); return; }
            EnsureFolder(OutRoot);

            SavePrefab(BuildButton("Btn_Primary", t.btn_primary, t.btn_primary_pressed, "PLAY", UITheme.Hex("#05210D"), t.chakraBold, t.sizeCtaLg), "Btn_Primary");
            SavePrefab(BuildButton("Btn_Premium", t.btn_premium, t.btn_premium_pressed, "GO PREMIUM", Color.white, t.chakraBold, t.sizeCtaMd), "Btn_Premium");
            SavePrefab(BuildButton("Btn_Purchase", t.btn_purchase, t.btn_purchase_pressed, "BUY", UITheme.Hex("#2A1600"), t.chakraBold, t.sizeCtaMd), "Btn_Purchase");
            SavePrefab(BuildButton("Btn_Cyan",  t.btn_cyan,   null, "OK",     Color.white, t.chakraBold, t.sizeCtaMd), "Btn_Cyan");
            SavePrefab(BuildButton("Btn_Ghost", t.btn_ghost,  null, "CANCEL", Color.white, t.chakraBold, t.sizeCtaMd), "Btn_Ghost");
            SavePrefab(BuildButton("Btn_Danger", t.btn_danger,null, "QUIT",   Color.white, t.chakraBold, t.sizeCtaMd), "Btn_Danger");

            SavePrefab(BuildChip("Chip_Coins",  t.chip_gold,    t.icon_coin,  t.coins,  t), "Chip_Coins");
            SavePrefab(BuildChip("Chip_Gems",   t.chip_violet,  t.icon_gem,   Color.white, t), "Chip_Gems");
            SavePrefab(BuildChip("Chip_Tokens", t.chip_cyan,    t.icon_token, Color.white, t), "Chip_Tokens");

            SavePrefab(BuildPuck("Puck",  t), "Puck");
            SavePrefab(BuildProgressBar("ProgressBar_Cyan",   t.bar_track, t.bar_fill_cyan,   t), "ProgressBar_Cyan");
            SavePrefab(BuildProgressBar("ProgressBar_Gold",   t.bar_track, t.bar_fill_gold,   t), "ProgressBar_Gold");
            SavePrefab(BuildProgressBar("ProgressBar_Violet", t.bar_track, t.bar_fill_violet, t), "ProgressBar_Violet");

            SavePrefab(BuildHazardBanner(t), "HazardBanner");
            SavePrefab(BuildTabs(t), "Tabs");
            SavePrefab(BuildToast(t), "Toast");
            SavePrefab(BuildBottomNav(t), "BottomNav");

            AssetDatabase.SaveAssets();
            UIBuildContext.Info("Rail Switch UI", "Base prefabs criados em " + OutRoot);
        }

        // ---------- helpers ----------

        static GameObject BuildButton(string name, Sprite normal, Sprite pressed, string label, Color labelColor,
            TMP_FontAsset font, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(342, 62);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), normal);
            var btn = go.GetComponent<Button>();
            if (pressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var s = btn.spriteState;
                s.pressedSprite = pressed;
                btn.spriteState = s;
                btn.targetGraphic = img;
            }

            var lbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)lbl.transform);
            var t = lbl.GetComponent<TextMeshProUGUI>();
            t.text = label;
            t.font = font;
            t.fontSize = size;
            t.color = labelColor;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle |= FontStyles.UpperCase;
            t.characterSpacing = 8f;   // ~0.08em (era 100 = 1em inteiro entre letras)
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return go;
        }

        static GameObject BuildChip(string name, Sprite bgSprite, Sprite iconSprite, Color valueColor, UITheme t)
        {
            // Largura AUTOMÁTICA. Com 96px fixos, "9 638" já estourava o chip e o
            // texto vazava por cima do ícone. HorizontalLayoutGroup + ContentSizeFitter
            // fazem o fundo crescer junto com o número.
            var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(ChipUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(96, 28);   // altura vale; largura é recalculada
            UIBuilder.Slice(go.GetComponent<Image>(), bgSprite);

            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(8, 12, 0, 0);
            h.spacing = 6;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;   h.childForceExpandWidth = false;
            h.childControlHeight = false; h.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var icon = UIBuilder.Icon("Icon", go.transform, iconSprite, Color.white);
            var iconRt = (RectTransform)icon.transform;
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(18, 18);
            var iconLE = icon.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 18; iconLE.preferredHeight = 18;

            // Só o número — o ícone já diz qual é a moeda. Quem escreve é o
            // ChipUI.SetValue (com separador de milhar).
            var val = UIBuilder.Text("Value", go.transform, "0", t.chakraBold, 15f, valueColor,
                TextAlignmentOptions.MidlineLeft);
            var vRt = (RectTransform)val.transform;
            vRt.anchorMin = vRt.anchorMax = new Vector2(0.5f, 0.5f);
            vRt.pivot = new Vector2(0.5f, 0.5f);
            vRt.sizeDelta = new Vector2(0, 20);

            var chip = go.GetComponent<ChipUI>();
            var so = new SerializedObject(chip);
            so.FindProperty("iconImg").objectReferenceValue = icon.GetComponent<Image>();
            so.FindProperty("valueText").objectReferenceValue = val.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildPuck(string name, UITheme t)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(PuckUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(52, 52);

            var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)ring.transform);
            var ringImg = ring.GetComponent<Image>();
            ringImg.sprite = t.puck_ring_full;
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = true;
            ringImg.fillAmount = 1f;
            ringImg.raycastTarget = false;

            var tile = UIBuilder.Icon("Tile", go.transform, t.puck_tile_blue, Color.white);
            var tRt = (RectTransform)tile.transform;
            UIBuilder.Stretch(tRt, 6, 6, 6, 6);

            var icon = UIBuilder.Icon("Icon", go.transform, t.icon_shield, Color.white);
            var iRt = (RectTransform)icon.transform;
            UIBuilder.Stretch(iRt, 10, 10, 10, 10);

            var badge = UIBuilder.Image9("Badge", go.transform, t.badge_gold, Color.white);
            var bRt = (RectTransform)badge.transform;
            bRt.anchorMin = new Vector2(1, 0); bRt.anchorMax = new Vector2(1, 0);
            bRt.pivot = new Vector2(1, 0);
            bRt.anchoredPosition = new Vector2(2, -2);
            bRt.sizeDelta = new Vector2(24, 18);   // cabe 11pt (piso de legibilidade)
            var badgeText = UIBuilder.Text("Text", badge.transform, "0", t.chakraBold, 11f,
                UITheme.Hex("#2A1600"), TextAlignmentOptions.Center);
            UIBuilder.Stretch((RectTransform)badgeText.transform);

            var puck = go.GetComponent<PuckUI>();
            var so = new SerializedObject(puck);
            so.FindProperty("ring").objectReferenceValue = ringImg;
            so.FindProperty("tile").objectReferenceValue = tile.GetComponent<Image>();
            so.FindProperty("iconImg").objectReferenceValue = icon.GetComponent<Image>();
            so.FindProperty("badgeText").objectReferenceValue = badgeText.GetComponent<TextMeshProUGUI>();
            so.FindProperty("badgeGO").objectReferenceValue = badge;
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildProgressBar(string name, Sprite track, Sprite fill, UITheme t)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ProgressBarUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(260, 8);
            UIBuilder.Slice(go.GetComponent<Image>(), track);

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)fillGO.transform);
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.sprite = fill;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0.5f;
            fillImg.raycastTarget = false;

            var bar = go.GetComponent<ProgressBarUI>();
            var so = new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue = fillImg;
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildHazardBanner(UITheme t)
        {
            var go = new GameObject("HazardBanner", typeof(RectTransform), typeof(Image), typeof(HazardBannerUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(362, 56);
            var img = UIBuilder.Slice(go.GetComponent<Image>(), t.banner_hazard_pink);
            img.raycastTarget = false;

            var tri = UIBuilder.Icon("Triangle", go.transform, t.icon_warning_tri, Color.white);
            var trRt = (RectTransform)tri.transform;
            trRt.anchorMin = new Vector2(0, 0.5f); trRt.anchorMax = new Vector2(0, 0.5f);
            trRt.pivot = new Vector2(0, 0.5f);
            trRt.anchoredPosition = new Vector2(14, 0);
            trRt.sizeDelta = new Vector2(22, 22);

            var title = UIBuilder.Text("Title", go.transform, "LANE SWAP", t.chakraBold, 13f, Color.white,
                TextAlignmentOptions.Left, uppercase: true, tracking: 0.06f);
            var tRt = (RectTransform)title.transform;
            tRt.anchorMin = new Vector2(0, 0.5f); tRt.anchorMax = new Vector2(0, 0.5f);
            tRt.pivot = new Vector2(0, 0.5f);
            tRt.anchoredPosition = new Vector2(44, 10);
            tRt.sizeDelta = new Vector2(220, 18);

            var subtitle = UIBuilder.Text("Subtitle", go.transform, "inputs invertidos por 4s", t.barlowMedium, 11f,
                new Color(1, 1, 1, 0.66f), TextAlignmentOptions.Left);
            var sRt = (RectTransform)subtitle.transform;
            sRt.anchorMin = new Vector2(0, 0.5f); sRt.anchorMax = new Vector2(0, 0.5f);
            sRt.pivot = new Vector2(0, 0.5f);
            sRt.anchoredPosition = new Vector2(44, -8);
            sRt.sizeDelta = new Vector2(220, 14);

            var counter = UIBuilder.Text("Counter", go.transform, "3s", t.chakraBold, 15f, t.hazard,
                TextAlignmentOptions.Right);
            var cRt = (RectTransform)counter.transform;
            cRt.anchorMin = new Vector2(1, 0.5f); cRt.anchorMax = new Vector2(1, 0.5f);
            cRt.pivot = new Vector2(1, 0.5f);
            cRt.anchoredPosition = new Vector2(-14, 0);
            cRt.sizeDelta = new Vector2(40, 18);

            var banner = go.GetComponent<HazardBannerUI>();
            var so = new SerializedObject(banner);
            so.FindProperty("bg").objectReferenceValue = img;
            so.FindProperty("warningTriangle").objectReferenceValue = tri.GetComponent<Image>();
            so.FindProperty("titleText").objectReferenceValue = title.GetComponent<TextMeshProUGUI>();
            so.FindProperty("subtitleText").objectReferenceValue = subtitle.GetComponent<TextMeshProUGUI>();
            so.FindProperty("counterText").objectReferenceValue = counter.GetComponent<TextMeshProUGUI>();
            so.FindProperty("bgOrange").objectReferenceValue = t.banner_hazard_orange;
            so.FindProperty("bgPink").objectReferenceValue = t.banner_hazard_pink;
            so.FindProperty("bgViolet").objectReferenceValue = t.banner_hazard_violet;
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildTabs(UITheme t)
        {
            var go = new GameObject("Tabs", typeof(RectTransform), typeof(TabsUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(358, 42);

            var track = UIBuilder.Image9("Track", go.transform, t.tabs_track, Color.white);
            UIBuilder.Stretch((RectTransform)track.transform);

            // Thumb por âncora fracionária (TabsUI.ApplyThumb reescreve conforme
            // a tab ativa). Default = primeira de duas, só pra não nascer torto.
            var thumb = UIBuilder.Image9("Thumb", go.transform, t.tabs_thumb, Color.white);
            var thumbRt = (RectTransform)thumb.transform;
            thumbRt.anchorMin = new Vector2(0f, 0f); thumbRt.anchorMax = new Vector2(0.5f, 1f);
            thumbRt.pivot = new Vector2(0.5f, 0.5f);
            thumbRt.offsetMin = new Vector2(2, 2);
            thumbRt.offsetMax = new Vector2(-2, -2);

            var labels = new GameObject("Labels", typeof(RectTransform));
            labels.transform.SetParent(go.transform, false);
            UIBuilder.Stretch((RectTransform)labels.transform);

            var tabs = go.GetComponent<TabsUI>();
            var so = new SerializedObject(tabs);
            so.FindProperty("track").objectReferenceValue = track.transform;
            so.FindProperty("thumb").objectReferenceValue = thumb.transform;
            so.FindProperty("labelsContainer").objectReferenceValue = labels.transform;
            so.FindProperty("labelFont").objectReferenceValue = t.barlowSemiBold;
            so.FindProperty("labelSize").floatValue = 12f;
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildToast(UITheme t)
        {
            var go = new GameObject("Toast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(ToastUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(320, 44);
            var bg = UIBuilder.Slice(go.GetComponent<Image>(), t.toast_red);
            bg.raycastTarget = false;

            var dot = UIBuilder.Icon("Dot", go.transform, t.dot_red, Color.white);
            var dRt = (RectTransform)dot.transform;
            dRt.anchorMin = new Vector2(0, 0.5f); dRt.anchorMax = new Vector2(0, 0.5f);
            dRt.pivot = new Vector2(0, 0.5f);
            dRt.anchoredPosition = new Vector2(14, 0);
            dRt.sizeDelta = new Vector2(10, 10);

            var txt = UIBuilder.Text("Text", go.transform, "TOAST", t.barlowBold, 12f, Color.white,
                TextAlignmentOptions.Left, uppercase: true, tracking: 0.18f);
            var txtRt = (RectTransform)txt.transform;
            txtRt.anchorMin = new Vector2(0, 0); txtRt.anchorMax = new Vector2(1, 1);
            txtRt.offsetMin = new Vector2(34, 0);
            txtRt.offsetMax = new Vector2(-14, 0);

            var toast = go.GetComponent<ToastUI>();
            var so = new SerializedObject(toast);
            so.FindProperty("bg").objectReferenceValue = bg;
            so.FindProperty("dot").objectReferenceValue = dot.GetComponent<Image>();
            so.FindProperty("text").objectReferenceValue = txt.GetComponent<TextMeshProUGUI>();
            so.FindProperty("toastGreen").objectReferenceValue = t.toast_green;
            so.FindProperty("toastRed").objectReferenceValue = t.toast_red;
            so.FindProperty("dotGreen").objectReferenceValue = t.dot_green;
            so.FindProperty("dotRed").objectReferenceValue = t.dot_red;
            so.FindProperty("group").objectReferenceValue = go.GetComponent<CanvasGroup>();
            so.ApplyModifiedProperties();
            return go;
        }

        static GameObject BuildBottomNav(UITheme t)
        {
            var go = new GameObject("BottomNav", typeof(RectTransform), typeof(Image), typeof(BottomNavUI));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(390, 68);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.05f, 0.86f);
            bg.raycastTarget = false;

            var slots = new[] { "Home", "Store", "Skins", "Ranks", "More" };
            var icons = new[] { t.nav_home, t.nav_store, t.nav_skins, t.nav_ranks, t.nav_more };
            var buttons = new Button[5];
            var slotIcons = new Image[5];
            var slotLabels = new TextMeshProUGUI[5];

            for (int i = 0; i < 5; i++)
            {
                var slot = new GameObject("Slot_" + slots[i], typeof(RectTransform), typeof(Image), typeof(Button));
                slot.transform.SetParent(go.transform, false);
                var srt = (RectTransform)slot.transform;
                srt.anchorMin = new Vector2(i / 5f, 0);
                srt.anchorMax = new Vector2((i + 1) / 5f, 1);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var simg = slot.GetComponent<Image>();
                simg.color = new Color(0, 0, 0, 0);
                simg.raycastTarget = true;
                var btn = slot.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = simg;
                buttons[i] = btn;

                var icon = UIBuilder.Icon("Icon", slot.transform, icons[i], new Color(1, 1, 1, 0.55f));
                var iRt = (RectTransform)icon.transform;
                UIBuilder.TopCenter(iRt, 12, 26, 26);
                slotIcons[i] = icon.GetComponent<Image>();

                var lbl = UIBuilder.Text("Label", slot.transform, slots[i].ToUpper(), t.barlowSemiBold, 11f,
                    new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Center, uppercase: true, tracking: 0.10f);
                var lRt = (RectTransform)lbl.transform;
                UIBuilder.BottomCenter(lRt, 8, 60, 12);
                slotLabels[i] = lbl.GetComponent<TextMeshProUGUI>();
            }

            var nav = go.GetComponent<BottomNavUI>();
            var so = new SerializedObject(nav);
            var names = new[] { "homeBtn", "storeBtn", "skinsBtn", "ranksBtn", "moreBtn" };
            var iconNames = new[] { "homeIcon", "storeIcon", "skinsIcon", "ranksIcon", "moreIcon" };
            var labelNames = new[] { "homeLabel", "storeLabel", "skinsLabel", "ranksLabel", "moreLabel" };
            for (int i = 0; i < 5; i++)
            {
                so.FindProperty(names[i]).objectReferenceValue = buttons[i];
                so.FindProperty(iconNames[i]).objectReferenceValue = slotIcons[i];
                so.FindProperty(labelNames[i]).objectReferenceValue = slotLabels[i];
            }
            so.ApplyModifiedProperties();
            return go;
        }

        // ---------- I/O ----------

        static void SavePrefab(GameObject sceneObj, string name)
        {
            var path = OutRoot + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(sceneObj, path);
            Object.DestroyImmediate(sceneObj);
            Debug.Log($"[RailSwitchUI] Prefab: {path}");
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
