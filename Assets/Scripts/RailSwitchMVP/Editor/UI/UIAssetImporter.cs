#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RailSwitchMVP.UI.Theme;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Copia os 77 sprites do handoff (Docs/Game UI Mockups Request (1)/design_handoff_rail_switch_ui/assets)
    /// para Assets/UI/Sprites/ e aplica TextureImporter settings + spriteBorder conforme assets/README.md.
    ///
    /// Depois popula o UITheme.asset com refs aos sprites.
    ///
    /// Menu: Tools -> RailSwitch -> Build UI -> 1. Import Sprites.
    /// </summary>
    public static class UIAssetImporter
    {
        const string SourceRoot = "Docs/Game UI Mockups Request (1)/design_handoff_rail_switch_ui/assets";
        const string TargetRoot = "Assets/UI/Sprites";

        // Borders em px @3× conforme assets/README.md
        // Formato: (left, bottom, right, top)
        static readonly Dictionary<string, Vector4> BorderByName = new Dictionary<string, Vector4>
        {
            // 72 all sides
            {"panel_lg", V(72,72,72,72)},
            {"modal",    V(72,72,72,72)},
            // 48 all sides
            {"panel_md",             V(48,48,48,48)},
            {"panel_accent_cyan",    V(48,48,48,48)},
            {"panel_accent_gold",    V(48,48,48,48)},
            {"panel_accent_green",   V(48,48,48,48)},
            {"panel_accent_violet",  V(48,48,48,48)},
            // 36 all sides
            {"row_sm",               V(36,36,36,36)},
            {"banner_hazard_orange", V(36,36,36,36)},
            {"banner_hazard_pink",   V(36,36,36,36)},
            {"banner_hazard_violet", V(36,36,36,36)},
            // pill_dark 57 L/R apenas
            {"pill_dark",            V(57,0,57,0)},
            // Botões grandes 54
            {"btn_primary",          V(54,54,54,54)},
            {"btn_primary_pressed",  V(54,54,54,54)},
            {"btn_premium",          V(54,54,54,54)},
            {"btn_premium_pressed",  V(54,54,54,54)},
            {"btn_purchase",         V(54,54,54,54)},
            {"btn_purchase_pressed", V(54,54,54,54)},
            {"btn_cyan",             V(54,54,54,54)},
            {"btn_danger",           V(54,54,54,54)},
            {"btn_ghost",            V(54,54,54,54)},
            {"btn_disabled",         V(54,54,54,54)},
            // Botões pequenos 36
            {"btn_sm_gold",          V(36,36,36,36)},
            {"btn_sm_green",         V(36,36,36,36)},
            // Controls
            {"tabs_track",           V(42,42,42,42)},
            {"tabs_thumb",           V(33,33,33,33)},
            {"toggle_on",            V(45,0,45,0)},
            {"toggle_off",           V(45,0,45,0)},
            {"slider_track",         V(9,0,9,0)},
            {"slider_fill",          V(9,0,9,0)},
            {"bar_track",            V(12,0,12,0)},
            {"bar_fill_cyan",        V(12,0,12,0)},
            {"bar_fill_gold",        V(12,0,12,0)},
            {"bar_fill_violet",      V(12,0,12,0)},
            // Economy
            {"chip_neutral", V(45,0,45,0)},
            {"chip_gold",    V(45,0,45,0)},
            {"chip_cyan",    V(45,0,45,0)},
            {"chip_violet",  V(45,0,45,0)},
            {"badge_gold",   V(18,18,18,18)},
            {"badge_green",  V(18,18,18,18)},
            // Overlays
            {"toast_green", V(57,0,57,0)},
            {"toast_red",   V(57,0,57,0)},
            {"tooltip",     V(30,30,30,30)},
        };

        static Vector4 V(float l, float b, float r, float t) { return new Vector4(l, b, r, t); }

        // Sprites que NÃO devem ser fatiados (spriteBorder = 0)
        static readonly HashSet<string> NoSliceCategories = new HashSet<string>
        {
            "frames", "icons", "backgrounds",
        };
        static readonly HashSet<string> NoSliceNames = new HashSet<string>
        {
            "icon_coin", "icon_gem", "icon_token",
            "puck_ring_full", "puck_ring_62", "puck_tile_blue", "puck_tile_gold", "puck_tile_teal",
            "puck_empty",
            "btn_icon_back", "btn_icon_chevron", "btn_icon_close",
            "btn_icon_menu", "btn_icon_pause", "btn_icon_plus",
            "fab_violet", "slider_knob",
            "dot_green", "dot_red", "scrim",
        };

        [MenuItem("Tools/RailSwitch/Build UI/1. Import Sprites", priority = 10)]
        public static void ImportAll()
        {
            var sourceAbs = Path.Combine(Directory.GetCurrentDirectory(), SourceRoot);
            if (!Directory.Exists(sourceAbs))
            {
                Debug.LogError("[RailSwitchUI] Não achei os PNGs em:\n" + sourceAbs);
                UIBuildContext.Info("Rail Switch UI", "Não achei os PNGs em:\n" + sourceAbs);
                return;
            }

            EnsureFolder(TargetRoot);
            var copied = new List<string>();

            foreach (var subdir in Directory.GetDirectories(sourceAbs))
            {
                var cat = Path.GetFileName(subdir);
                var targetCat = TargetRoot + "/" + cat;
                EnsureFolder(targetCat);

                foreach (var src in Directory.GetFiles(subdir, "*.png"))
                {
                    var name = Path.GetFileName(src);
                    var dst = Path.Combine(sourceAbs.Contains("Assets")
                        ? Application.dataPath.Replace("Assets", "") : Directory.GetCurrentDirectory(),
                        targetCat, name);
                    dst = Path.Combine(targetCat, name);
                    var dstAbs = Path.Combine(Directory.GetCurrentDirectory(), dst);
                    File.Copy(src, dstAbs, true);
                    copied.Add(dst.Replace('\\', '/'));
                }
            }

            AssetDatabase.Refresh();

            int configured = 0;
            foreach (var assetPath in copied)
            {
                if (ConfigureImporter(assetPath)) configured++;
            }
            AssetDatabase.SaveAssets();

            // Popula o UITheme
            var theme = EnsureUITheme();
            PopulateThemeSprites(theme);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            Debug.Log($"[RailSwitchUI] Copiou {copied.Count} sprites, configurou {configured}. Theme populado.");
            UIBuildContext.Info("Rail Switch UI",
                $"Sprites importados: {copied.Count}\nConfigurados 9-slice: {configured}\nUITheme populado.");
        }

        static bool ConfigureImporter(string assetPath)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) return false;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            imp.filterMode = FilterMode.Bilinear;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;

            // Mesh Type = Full Rect (obrigatório para 9-slice)
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            imp.SetTextureSettings(settings);

            // Border
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var cat = Path.GetFileName(Path.GetDirectoryName(assetPath));

            bool noSlice = NoSliceCategories.Contains(cat) || NoSliceNames.Contains(name);
            Vector4 border = Vector4.zero;
            if (!noSlice && BorderByName.TryGetValue(name, out var b))
                border = b;
            imp.spriteBorder = border;

            imp.SaveAndReimport();
            return true;
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

        internal static UITheme EnsureUITheme()
        {
            EnsureFolder("Assets/UI");
            EnsureFolder("Assets/UI/ScriptableObjects");
            const string path = "Assets/UI/ScriptableObjects/UITheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            return theme;
        }

        static Sprite LoadSprite(string name)
        {
            // Procura em qualquer subpasta de Assets/UI/Sprites
            var guids = AssetDatabase.FindAssets("t:Sprite " + name);
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.StartsWith(TargetRoot)) continue;
                if (Path.GetFileNameWithoutExtension(p) == name)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            return null;
        }

        static void PopulateThemeSprites(UITheme t)
        {
            // panels
            t.panel_lg = LoadSprite("panel_lg");
            t.panel_md = LoadSprite("panel_md");
            t.modal = LoadSprite("modal");
            t.panel_accent_cyan   = LoadSprite("panel_accent_cyan");
            t.panel_accent_gold   = LoadSprite("panel_accent_gold");
            t.panel_accent_green  = LoadSprite("panel_accent_green");
            t.panel_accent_violet = LoadSprite("panel_accent_violet");
            t.pill_dark = LoadSprite("pill_dark");
            t.row_sm    = LoadSprite("row_sm");
            t.banner_hazard_orange = LoadSprite("banner_hazard_orange");
            t.banner_hazard_pink   = LoadSprite("banner_hazard_pink");
            t.banner_hazard_violet = LoadSprite("banner_hazard_violet");

            // buttons
            t.btn_primary          = LoadSprite("btn_primary");
            t.btn_primary_pressed  = LoadSprite("btn_primary_pressed");
            t.btn_premium          = LoadSprite("btn_premium");
            t.btn_premium_pressed  = LoadSprite("btn_premium_pressed");
            t.btn_purchase         = LoadSprite("btn_purchase");
            t.btn_purchase_pressed = LoadSprite("btn_purchase_pressed");
            t.btn_cyan             = LoadSprite("btn_cyan");
            t.btn_ghost            = LoadSprite("btn_ghost");
            t.btn_danger           = LoadSprite("btn_danger");
            t.btn_disabled         = LoadSprite("btn_disabled");
            t.btn_sm_gold          = LoadSprite("btn_sm_gold");
            t.btn_sm_green         = LoadSprite("btn_sm_green");

            // controls
            t.bar_track       = LoadSprite("bar_track");
            t.bar_fill_cyan   = LoadSprite("bar_fill_cyan");
            t.bar_fill_gold   = LoadSprite("bar_fill_gold");
            t.bar_fill_violet = LoadSprite("bar_fill_violet");
            t.btn_icon_back    = LoadSprite("btn_icon_back");
            t.btn_icon_chevron = LoadSprite("btn_icon_chevron");
            t.btn_icon_close   = LoadSprite("btn_icon_close");
            t.btn_icon_menu    = LoadSprite("btn_icon_menu");
            t.btn_icon_pause   = LoadSprite("btn_icon_pause");
            t.btn_icon_plus    = LoadSprite("btn_icon_plus");
            t.fab_violet    = LoadSprite("fab_violet");
            t.slider_track  = LoadSprite("slider_track");
            t.slider_fill   = LoadSprite("slider_fill");
            t.slider_knob   = LoadSprite("slider_knob");
            t.tabs_track    = LoadSprite("tabs_track");
            t.tabs_thumb    = LoadSprite("tabs_thumb");
            t.toggle_off    = LoadSprite("toggle_off");
            t.toggle_on     = LoadSprite("toggle_on");

            // economy
            t.chip_neutral = LoadSprite("chip_neutral");
            t.chip_gold    = LoadSprite("chip_gold");
            t.chip_cyan    = LoadSprite("chip_cyan");
            t.chip_violet  = LoadSprite("chip_violet");
            t.icon_coin  = LoadSprite("icon_coin");
            t.icon_gem   = LoadSprite("icon_gem");
            t.icon_token = LoadSprite("icon_token");
            t.puck_ring_full = LoadSprite("puck_ring_full");
            t.puck_ring_62   = LoadSprite("puck_ring_62");
            t.puck_empty     = LoadSprite("puck_empty");
            t.puck_tile_blue = LoadSprite("puck_tile_blue");
            t.puck_tile_gold = LoadSprite("puck_tile_gold");
            t.puck_tile_teal = LoadSprite("puck_tile_teal");
            t.badge_gold  = LoadSprite("badge_gold");
            t.badge_green = LoadSprite("badge_green");

            // frames
            t.frame_common    = LoadSprite("frame_common");
            t.frame_rare      = LoadSprite("frame_rare");
            t.frame_epic      = LoadSprite("frame_epic");
            t.frame_legendary = LoadSprite("frame_legendary");
            t.frame_locked    = LoadSprite("frame_locked");

            // overlays
            t.scrim       = LoadSprite("scrim");
            t.tooltip     = LoadSprite("tooltip");
            t.toast_green = LoadSprite("toast_green");
            t.toast_red   = LoadSprite("toast_red");
            t.dot_green   = LoadSprite("dot_green");
            t.dot_red     = LoadSprite("dot_red");

            // icons
            t.icon_shield        = LoadSprite("icon_shield");
            t.icon_magnet        = LoadSprite("icon_magnet");
            t.icon_ghost         = LoadSprite("icon_ghost");
            t.icon_lane_preview  = LoadSprite("icon_lane_preview");
            t.icon_coin_radar    = LoadSprite("icon_coin_radar");
            t.icon_coin_multiplier = LoadSprite("icon_coin_multiplier");
            t.icon_time_freeze   = LoadSprite("icon_time_freeze");
            t.icon_teleport      = LoadSprite("icon_teleport");
            t.icon_auto_critical = LoadSprite("icon_auto_critical");
            t.icon_difficulty_reset = LoadSprite("icon_difficulty_reset");
            t.icon_hazard_barrier  = LoadSprite("icon_hazard_barrier");
            t.icon_hazard_speedup  = LoadSprite("icon_hazard_speedup");
            t.icon_hazard_laneswap = LoadSprite("icon_hazard_laneswap");
            t.icon_hazard_vortex   = LoadSprite("icon_hazard_vortex");
            t.icon_warning_tri = LoadSprite("icon_warning_tri");
            t.icon_chest       = LoadSprite("icon_chest");
            t.nav_home  = LoadSprite("nav_home");
            t.nav_store = LoadSprite("nav_store");
            t.nav_skins = LoadSprite("nav_skins");
            t.nav_ranks = LoadSprite("nav_ranks");
            t.nav_more  = LoadSprite("nav_more");

            // backgrounds
            t.bg_meta_grid = LoadSprite("bg_meta_grid");
            t.bg_meta_city = LoadSprite("bg_meta_city");
            t.bg_game_rails = LoadSprite("bg_game_rails");
        }
    }
}
#endif
