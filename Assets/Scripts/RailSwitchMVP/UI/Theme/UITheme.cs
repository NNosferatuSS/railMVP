using UnityEngine;
using TMPro;

namespace RailSwitchMVP.UI.Theme
{
    /// <summary>
    /// Design tokens do handoff (Rail Switch UI Kit). Cores, tipografia,
    /// spacing/radius, refs de sprites e fontes. Todos os wizards de UI
    /// e todos os prefabs consomem este SO — trocar aqui reflete em toda a UI.
    ///
    /// Criado automaticamente pelo menu Tools -> RailSwitch -> Build UI -> 0. Build Design System.
    /// Salvo em Assets/UI/ScriptableObjects/UITheme.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "RailSwitch/UI/UITheme", order = 0)]
    public class UITheme : ScriptableObject
    {
        // ---- Design canvas reference ----
        public Vector2 referenceResolution = new Vector2(390f, 844f);
        public float matchWidthOrHeight = 0.5f;

        // ---- Cores (hex do handoff) ----
        [Header("Palette")]
        public Color accent      = Hex("#22E3FF");
        public Color safe        = Hex("#7CFF4F");
        public Color danger      = Hex("#FF4D5E");
        public Color coins       = Hex("#FFC23D");
        public Color gems        = Hex("#7C4DFF");
        public Color gemsBorder  = Hex("#A078FF");
        public Color hazard      = Hex("#FF3EA5");
        public Color hazardSpeed = Hex("#FFB050");
        public Color bgDeep      = Hex("#04060C");
        public Color bgMid       = Hex("#0A1024");
        public Color bgLift      = Hex("#1B2A52");
        public Color bgCity      = Hex("#0D0A1C");
        public Color railTile    = Hex("#4C505C");

        [Header("Panel")]
        public Color panelFill   = new Color(1f, 1f, 1f, 0.05f);
        public Color panelStroke = new Color(1f, 1f, 1f, 0.12f);

        [Header("Text hierarchy")]
        public Color textHi  = Color.white;
        public Color textMid = new Color(1f, 1f, 1f, 0.60f);
        public Color textLow = new Color(1f, 1f, 1f, 0.40f);

        [Header("Overlay scrim")]
        public Color scrimColor = new Color(0.016f, 0.024f, 0.047f, 0.72f);

        // ---- Text sizes (pt @390x844) ----
        [Header("Font sizes (pt @390x844)")]
        public float sizeHero          = 58f;   // Game Over hero
        public float sizeScreenTitle   = 34f;   // Título de tela
        public float sizeCtaLg         = 28f;
        public float sizeCtaMd         = 22f;
        public float sizeScreenHeader  = 17f;
        public float sizeStatValue     = 22f;
        public float sizeStatSmall     = 15f;
        public float sizeSectionLabel  = 13f;   // Barlow SB, MAIÚSC, tracking .14em
        public float sizeBody          = 14f;
        public float sizeCaption       = 11f;   // Nunca abaixo disso em tela
        public float sizeMicro         = 10f;   // LEGADO — não usar em tela, ver minFontSize

        [Tooltip("Piso de tamanho de fonte, aplicado por UIBuilder.AddText em TODO texto.\n\n" +
            "Abaixo de 11pt num alvo mobile fica ilegível, ainda mais com tracking alto " +
            "em maiúsculas (que é o estilo dos labels do handoff). Rede de segurança: " +
            "qualquer call site que peça menos é elevado pra cá.")]
        public float minFontSize       = 11f;

        // ---- Spacing / radius ----
        [Header("Spacing / radius")]
        public float sp4 = 4f, sp8 = 8f, sp12 = 12f, sp16 = 16f, sp24 = 24f;
        public float rad4 = 4f, rad8 = 8f, rad12 = 12f, rad14 = 14f, rad16 = 16f, rad18 = 18f, rad20 = 20f, rad22 = 22f, radPill = 999f;
        [Tooltip("pixelsPerUnitMultiplier aplicado em TODA Image 9-sliced.\n\n" +
            "Os sprites do handoff são @3× e os borders estão em px @3× (btn = 54px). " +
            "Sem isso, um botão de 62px de altura recebe 54+54=108px de borda, o Unity " +
            "comprime as curvas pra caber e o botão vira uma CÁPSULA.\n\n" +
            "Com 3, cada border cai pro raio de design real: btn 54→18 (rad18), " +
            "panel_md 48→16 (rad16), row_sm 36→12 (rad12), tabs_track 42→14 (rad14).")]
        public float sliceMultiplier = 3f;

        public float sideMargin = 16f;
        public float modalMargin = 24f;
        public float safeAreaTop = 44f;
        public float safeAreaBottom = 34f;
        public float touchTargetMin = 44f;

        // ---- Fonts ----
        [Header("Fonts (TMP atlas)")]
        [Tooltip("Chakra Petch Bold — números, títulos, CTA.")]
        public TMP_FontAsset chakraBold;
        [Tooltip("Barlow Semi Condensed Bold — section labels, captions.")]
        public TMP_FontAsset barlowBold;
        [Tooltip("Barlow Semi Condensed SemiBold — captions e chips.")]
        public TMP_FontAsset barlowSemiBold;
        [Tooltip("Barlow Semi Condensed Medium — body.")]
        public TMP_FontAsset barlowMedium;

        // ---- Sprite refs (auto-populado pelo UIAssetImporter) ----
        [Header("Sprites — panels")]
        public Sprite panel_lg;
        public Sprite panel_md;
        public Sprite modal;
        public Sprite panel_accent_cyan;
        public Sprite panel_accent_gold;
        public Sprite panel_accent_green;
        public Sprite panel_accent_violet;
        public Sprite pill_dark;
        public Sprite row_sm;
        public Sprite banner_hazard_orange;
        public Sprite banner_hazard_pink;
        public Sprite banner_hazard_violet;

        [Header("Sprites — buttons")]
        public Sprite btn_primary;
        public Sprite btn_primary_pressed;
        public Sprite btn_premium;
        public Sprite btn_premium_pressed;
        public Sprite btn_purchase;
        public Sprite btn_purchase_pressed;
        public Sprite btn_cyan;
        public Sprite btn_ghost;
        public Sprite btn_danger;
        public Sprite btn_disabled;
        public Sprite btn_sm_gold;
        public Sprite btn_sm_green;

        [Header("Sprites — controls")]
        public Sprite bar_track;
        public Sprite bar_fill_cyan;
        public Sprite bar_fill_gold;
        public Sprite bar_fill_violet;
        public Sprite btn_icon_back;
        public Sprite btn_icon_chevron;
        public Sprite btn_icon_close;
        public Sprite btn_icon_menu;
        public Sprite btn_icon_pause;
        public Sprite btn_icon_plus;
        public Sprite fab_violet;
        public Sprite slider_track;
        public Sprite slider_fill;
        public Sprite slider_knob;
        public Sprite tabs_track;
        public Sprite tabs_thumb;
        public Sprite toggle_off;
        public Sprite toggle_on;

        [Header("Sprites — economy")]
        public Sprite chip_neutral, chip_gold, chip_cyan, chip_violet;
        public Sprite icon_coin, icon_gem, icon_token;
        public Sprite puck_ring_full, puck_ring_62, puck_empty;
        public Sprite puck_tile_blue, puck_tile_gold, puck_tile_teal;
        public Sprite badge_gold, badge_green;

        [Header("Sprites — frames")]
        public Sprite frame_common, frame_rare, frame_epic, frame_legendary, frame_locked;

        [Header("Sprites — overlays")]
        public Sprite scrim, tooltip, toast_green, toast_red, dot_green, dot_red;

        [Header("Sprites — icons (power-ups + nav)")]
        public Sprite icon_shield, icon_magnet, icon_ghost, icon_lane_preview, icon_coin_radar;
        public Sprite icon_coin_multiplier, icon_time_freeze, icon_teleport, icon_auto_critical;
        public Sprite icon_difficulty_reset;
        public Sprite icon_hazard_barrier, icon_hazard_speedup, icon_hazard_laneswap, icon_hazard_vortex;
        public Sprite icon_warning_tri, icon_chest;
        public Sprite nav_home, nav_store, nav_skins, nav_ranks, nav_more;

        [Header("Sprites — backgrounds")]
        public Sprite bg_meta_grid, bg_meta_city, bg_game_rails;

        // ---- Helpers ----
        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta;
        }

        /// <summary>Retorna cor com alpha custom (0..1).</summary>
        public static Color WithA(Color c, float a) { c.a = a; return c; }

        /// <summary>Localiza o asset padrão (Assets/UI/ScriptableObjects/UITheme.asset) via AssetDatabase.
        /// Runtime só: usa o campo estático cachado (setado pelo bootstrap).</summary>
        static UITheme _cached;
        public static UITheme Current
        {
            get
            {
                if (_cached != null) return _cached;
#if UNITY_EDITOR
                var guids = UnityEditor.AssetDatabase.FindAssets("t:UITheme");
                if (guids != null && guids.Length > 0)
                    _cached = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
                return _cached;
            }
            set { _cached = value; }
        }
    }
}
