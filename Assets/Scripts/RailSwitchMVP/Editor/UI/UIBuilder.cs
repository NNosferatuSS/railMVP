#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.UI.Theme;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Helpers pra construir GameObjects UGUI de forma declarativa.
    /// Todo wizard usa esses métodos em vez de mexer em RectTransform na mão.
    ///
    /// Convenção de anchors:
    ///  - Anchor(1,1)  = topo-direito, pivot 1,1
    ///  - Anchor(0,1)  = topo-esquerdo, pivot 0,1
    ///  - Anchor(0.5,1)= topo-centro, pivot 0.5,1
    ///  - Anchor(0.5,0.5) = centro (default do UGUI)
    ///  - Stretch      = anchor min 0,0 max 1,1 (full)
    /// </summary>
    public static class UIBuilder
    {
        public static UITheme Theme => UITheme.Current;

        // ---------- root helpers ----------

        public static GameObject NewGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Rect(GameObject go) => (RectTransform)go.transform;

        /// <summary>Ancora full-stretch com padding zero.</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform TopRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform TopCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform BottomLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform BottomRight(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = new Vector2(-x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform BottomCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Stretch horizontal (left/right), altura fixa a partir do topo.</summary>
        public static RectTransform StretchHTop(RectTransform rt, float left, float right, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -y - h);
            rt.offsetMax = new Vector2(-right, -y);
            return rt;
        }

        /// <summary>Stretch horizontal (left/right), altura fixa a partir da base.</summary>
        public static RectTransform StretchHBottom(RectTransform rt, float left, float right, float y, float h)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(-right, y + h);
            return rt;
        }

        // ---------- images ----------

        /// <summary>
        /// Multiplicador de 9-slice vindo do tema (default 3 — sprites @3×).
        /// Ver UITheme.sliceMultiplier pra o porquê.
        /// </summary>
        public static float SliceMultiplier
            => Theme != null && Theme.sliceMultiplier > 0f ? Theme.sliceMultiplier : 3f;

        /// <summary>
        /// Ponto ÚNICO onde uma Image vira 9-sliced. Sempre use isto em vez de
        /// setar `img.type = Image.Type.Sliced` na mão — senão o
        /// pixelsPerUnitMultiplier fica em 1 e o widget vira cápsula.
        /// Sprite sem border cai pra Simple automaticamente.
        /// </summary>
        public static Image Slice(Image img, Sprite sprite)
        {
            img.sprite = sprite;
            bool hasBorder = sprite != null && sprite.border != Vector4.zero;
            img.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
            if (hasBorder) img.pixelsPerUnitMultiplier = SliceMultiplier;
            return img;
        }

        public static Image AddImage(GameObject go, Sprite sprite, Color? color = null,
            Image.Type type = Image.Type.Simple)
        {
            var img = go.AddComponent<Image>();
            img.color = color ?? Color.white;
            if (type == Image.Type.Sliced) Slice(img, sprite);
            else { img.sprite = sprite; img.type = type; }
            img.raycastTarget = false;
            return img;
        }

        public static GameObject Image9(string name, Transform parent, Sprite sprite, Color? color = null)
        {
            var go = NewGO(name, parent);
            AddImage(go, sprite, color, Image.Type.Sliced);
            return go;
        }

        public static GameObject SolidColor(string name, Transform parent, Color color)
        {
            var go = NewGO(name, parent);
            AddImage(go, null, color);
            return go;
        }

        public static GameObject Icon(string name, Transform parent, Sprite sprite, Color? color = null)
        {
            var go = NewGO(name, parent);
            AddImage(go, sprite, color);
            return go;
        }

        // ---------- text ----------

        public static TextMeshProUGUI AddText(GameObject go, string text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
            bool uppercase = false, float tracking = 0)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            // Piso de legibilidade mobile — ver UITheme.minFontSize.
            float floor = Theme != null && Theme.minFontSize > 0f ? Theme.minFontSize : 11f;
            t.fontSize = Mathf.Max(size, floor);
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            if (uppercase) t.fontStyle |= FontStyles.UpperCase;
            // TMP characterSpacing é % do font size (100 = 1em inteiro entre letras).
            // Os tokens do handoff vêm em em (0.14em), então em → % é *100.
            // (Era *1000 antes: 0.14em virava 140 = letras espalhadas pela tela.)
            if (tracking != 0) t.characterSpacing = tracking * 100f;
            return t;
        }

        public static GameObject Text(string name, Transform parent, string text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
            bool uppercase = false, float tracking = 0)
        {
            var go = NewGO(name, parent);
            // Default = stretch no pai. Sem isso o RectTransform nasce 100x100 no
            // centro do pai, e vários labels do mesmo card se empilham no meio.
            // Quem quiser posição específica sobrescreve o rect depois.
            Stretch(Rect(go));
            AddText(go, text, font, size, color, align, uppercase, tracking);
            return go;
        }

        // ---------- button ----------

        public static GameObject Button(string name, Transform parent, Sprite normal, Sprite pressed,
            string label, TMP_FontAsset labelFont, float labelSize, Color labelColor,
            out Button btn, out TextMeshProUGUI labelText, float tracking = 100f)
        {
            var go = Image9(name, parent, normal, Color.white);
            btn = go.AddComponent<Button>();
            var img = go.GetComponent<Image>();
            img.raycastTarget = true;
            if (pressed != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var s = btn.spriteState;
                s.pressedSprite = pressed;
                btn.spriteState = s;
                btn.targetGraphic = img;
            }

            var lbl = NewGO("Label", go.transform);
            Stretch(Rect(lbl));
            labelText = AddText(lbl, label, labelFont, labelSize, labelColor,
                TextAlignmentOptions.Center, uppercase: true, tracking: tracking / 1000f);
            return go;
        }

        // ---------- utils ----------

        /// <summary>Cria/garante Canvas + CanvasScaler + GraphicRaycaster.</summary>
        public static Canvas EnsureCanvas(string name, int sortOrder, Transform sceneParent = null)
        {
            var found = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in found) if (c.name == name) return c;

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (sceneParent != null) go.transform.SetParent(sceneParent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var theme = Theme;
            scaler.referenceResolution = theme != null ? theme.referenceResolution : new Vector2(390, 844);
            scaler.matchWidthOrHeight = theme != null ? theme.matchWidthOrHeight : 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            return canvas;
        }

        /// <summary>Cria um filled Image (progress bar, ring de puck).</summary>
        public static Image AddFilledImage(GameObject go, Sprite sprite, Color color,
            Image.FillMethod method, int origin, bool clockwise = true)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = origin;
            img.fillClockwise = clockwise;
            img.fillAmount = 1f;
            img.raycastTarget = false;
            return img;
        }

        // ---------- layout ----------

        public static HorizontalLayoutGroup AddHLayout(GameObject go, float spacing = 8f,
            TextAnchor align = TextAnchor.MiddleCenter, RectOffset padding = null)
        {
            var l = go.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing;
            l.childAlignment = align;
            l.padding = padding ?? new RectOffset(0, 0, 0, 0);
            l.childControlWidth = false;
            l.childControlHeight = false;
            l.childForceExpandWidth = false;
            l.childForceExpandHeight = false;
            return l;
        }

        public static VerticalLayoutGroup AddVLayout(GameObject go, float spacing = 8f,
            TextAnchor align = TextAnchor.UpperLeft, RectOffset padding = null)
        {
            var l = go.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing;
            l.childAlignment = align;
            l.padding = padding ?? new RectOffset(0, 0, 0, 0);
            l.childControlWidth = false;
            l.childControlHeight = false;
            l.childForceExpandWidth = false;
            l.childForceExpandHeight = false;
            return l;
        }

        public static GridLayoutGroup AddGrid(GameObject go, Vector2 cell, Vector2 spacing,
            int constraintCount, TextAnchor align = TextAnchor.UpperLeft)
        {
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = constraintCount;
            g.childAlignment = align;
            return g;
        }

        // ---------- fluxo vertical determinístico ----------

        /// <summary>
        /// Cursor de empilhamento vertical. Cada Push() ancora o filho em
        /// StretchHTop no Y corrente e avança o cursor pela altura + gap.
        /// Substitui offsets Y hardcoded (que driftam quando qualquer altura muda).
        /// </summary>
        public class Col
        {
            readonly Transform _parent;
            readonly float _padL, _padR, _gap;
            float _y;

            public Col(Transform parent, float top = 0f, float padL = 16f, float padR = 16f, float gap = 14f)
            {
                _parent = parent; _y = top; _padL = padL; _padR = padR; _gap = gap;
            }

            /// <summary>Y atual do cursor (topo do próximo item).</summary>
            public float Y => _y;

            /// <summary>Largura útil dado o reference width do canvas.</summary>
            public float Width(float canvasWidth = 390f) => canvasWidth - _padL - _padR;

            /// <summary>Ancora o GO na posição corrente com a altura dada e avança o cursor.</summary>
            public RectTransform Push(GameObject go, float height, float gapAfter = -1f)
            {
                var rt = (RectTransform)go.transform;
                StretchHTop(rt, _padL, _padR, _y, height);
                _y += height + (gapAfter >= 0 ? gapAfter : _gap);
                return rt;
            }

            /// <summary>Push sem margem lateral (edge-to-edge).</summary>
            public RectTransform PushFull(GameObject go, float height, float gapAfter = -1f)
            {
                var rt = (RectTransform)go.transform;
                StretchHTop(rt, 0, 0, _y, height);
                _y += height + (gapAfter >= 0 ? gapAfter : _gap);
                return rt;
            }

            /// <summary>Avança o cursor sem colocar nada.</summary>
            public void Space(float h) => _y += h;
        }

        /// <summary>Texto ancorado horizontalmente com topo/altura explícitos. Nunca fica sem rect.</summary>
        public static TextMeshProUGUI TxtTop(Transform parent, string name, string text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions align, float top, float h,
            float padL = 14f, float padR = 14f, bool uppercase = false, float tracking = 0f)
        {
            var go = Text(name, parent, text, font, size, color, align, uppercase, tracking);
            StretchHTop((RectTransform)go.transform, padL, padR, top, h);
            return go.GetComponent<TextMeshProUGUI>();
        }

        /// <summary>Texto ancorado a partir da base do pai.</summary>
        public static TextMeshProUGUI TxtBottom(Transform parent, string name, string text, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions align, float bottom, float h,
            float padL = 14f, float padR = 14f, bool uppercase = false, float tracking = 0f)
        {
            var go = Text(name, parent, text, font, size, color, align, uppercase, tracking);
            StretchHBottom((RectTransform)go.transform, padL, padR, bottom, h);
            return go.GetComponent<TextMeshProUGUI>();
        }

        // ---------- panel (fill + stroke via 2 imagens) ----------

        public static GameObject Panel(string name, Transform parent, Sprite panelSprite = null)
        {
            var go = NewGO(name, parent);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            if (panelSprite != null)
            {
                Slice(img, panelSprite);
            }
            else
            {
                img.color = new Color(1, 1, 1, 0.05f);
            }
            return go;
        }
    }
}
#endif
