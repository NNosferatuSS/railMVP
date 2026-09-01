#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.UI;
using RailSwitchMVP.UI.Theme;

namespace RailSwitchMVP.EditorTools.UI
{
    public static class BuildSplash
    {
        const string OutPath = "Assets/UI/Prefabs/Splash/Canvas_Splash.prefab";

        [MenuItem("Tools/RailSwitch/Build UI/7. Build Splash", priority = 70)]
        public static void Build()
        {
            var t = UITheme.Current;
            if (t == null) { Debug.LogError("Rode 1. Import Sprites primeiro."); return; }
            EnsureFolder("Assets/UI/Prefabs/Splash");

            var canvasGO = new GameObject("Canvas_Splash",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SplashController));
            var c = canvasGO.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = canvasGO.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = t.referenceResolution;
            s.matchWidthOrHeight = t.matchWidthOrHeight;
            var root = canvasGO.transform;

            var bg = UIBuilder.Icon("Background", root, t.bg_meta_grid, Color.white);
            UIBuilder.Stretch((RectTransform)bg.transform);

            // Logo
            var logo = UIBuilder.Text("Logo", root, "RAIL SWITCH", t.chakraBold, 42f, Color.white,
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.05f);
            UIBuilder.TopCenter((RectTransform)logo.transform, 246, 300, 50);
            var tagline = UIBuilder.Text("Tagline", root, "READ TWO SWITCHES AHEAD",
                t.barlowSemiBold, 11f, new Color(t.accent.r, t.accent.g, t.accent.b, 0.8f),
                TextAlignmentOptions.Center, uppercase: true, tracking: 0.40f);
            UIBuilder.TopCenter((RectTransform)tagline.transform, 300, 300, 14);

            // Progress bar
            var track = UIBuilder.Image9("BarTrack", root, t.bar_track, Color.white);
            UIBuilder.StretchHBottom((RectTransform)track.transform, 40, 40, 132, 6);
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(track.transform, false);
            UIBuilder.Stretch((RectTransform)fillGO.transform);
            var fill = fillGO.GetComponent<Image>();
            fill.sprite = t.bar_fill_cyan; fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left; fill.fillAmount = 0f;

            var loading = UIBuilder.Text("Loading", root, "LOADING TRACKS", t.barlowSemiBold, 11f,
                new Color(1, 1, 1, 0.5f), TextAlignmentOptions.Left, uppercase: true, tracking: 0.18f);
            UIBuilder.StretchHBottom((RectTransform)loading.transform, 40, 40, 142, 14);
            var pct = UIBuilder.Text("Pct", root, "0%", t.chakraBold, 12f, Color.white,
                TextAlignmentOptions.Right);
            UIBuilder.StretchHBottom((RectTransform)pct.transform, 40, 40, 142, 14);

            // Tip
            var tip = UIBuilder.Image9("TipCard", root, t.row_sm, Color.white);
            UIBuilder.StretchHBottom((RectTransform)tip.transform, 24, 24, 56, 50);
            var tipLabel = UIBuilder.Text("Label", tip.transform, "TIP", t.barlowBold, 11f, t.coins,
                TextAlignmentOptions.Left, uppercase: true, tracking: 0.18f);
            var tRt = (RectTransform)tipLabel.transform;
            tRt.anchorMin = new Vector2(0, 0.5f); tRt.anchorMax = new Vector2(0, 0.5f);
            tRt.pivot = new Vector2(0, 0.5f); tRt.anchoredPosition = new Vector2(14, 0); tRt.sizeDelta = new Vector2(28, 14);
            var tipText = UIBuilder.Text("Text", tip.transform, "READ TWO SWITCHES AHEAD.",
                t.barlowMedium, 13f, new Color(1, 1, 1, 0.8f), TextAlignmentOptions.Left);
            var txRt = (RectTransform)tipText.transform;
            txRt.anchorMin = new Vector2(0, 0); txRt.anchorMax = new Vector2(1, 1);
            txRt.offsetMin = new Vector2(50, 0); txRt.offsetMax = new Vector2(-14, 0);

            // Wire
            var ctrl = canvasGO.GetComponent<SplashController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("progressFill").objectReferenceValue = fill;
            so.FindProperty("progressText").objectReferenceValue = pct.GetComponent<TextMeshProUGUI>();
            so.FindProperty("tipText").objectReferenceValue = tipText.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(canvasGO, OutPath);
            Object.DestroyImmediate(canvasGO);
            AssetDatabase.SaveAssets();
            UIBuildContext.Info("Rail Switch UI", "Splash criado em " + OutPath);
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
