using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Feedback do toque no trilho (overlay de tela, sem shader próprio):
    ///  • moldura no trilho TOCADO (dourada = conectou; vermelha + tremida = não alcança);
    ///  • marcador ciano no CONECTOR que trocou (fim do trilho anterior);
    ///  • linha de ligação conector → trilho tocado.
    /// Escuta PlayerRailRider.TapResolved. Criado em runtime pelo SmartTouchInput.
    /// </summary>
    public class TapFeedbackUI : MonoBehaviour
    {
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private float trackHalfWidth = 0.8f;   // metade da largura do trilho (mundo)
        [SerializeField] private Color okColor = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField] private Color linkColor = new Color(0.45f, 1f, 0.9f, 1f);
        [SerializeField] private Color failColor = new Color(1f, 0.3f, 0.35f, 1f);

        class Fx
        {
            public RectTransform root, frame, marker, line;
            public Image[] frameEdges, markerEdges;
            public Image lineImg;
            public TrackTile tapped, changed;
            public bool ok;
            public float t = -1f;
        }

        const int PoolSize = 4;
        readonly List<Fx> _pool = new List<Fx>();
        RectTransform _canvas;
        Camera _cam;

        void Awake()
        {
            var go = new GameObject("TapFeedback_Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 6;   // acima do HUD (5), abaixo do tutorial (7)
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080, 2340);
            sc.matchWidthOrHeight = 0.5f;
            _canvas = (RectTransform)go.transform;
            for (int i = 0; i < PoolSize; i++) _pool.Add(Build(i));
        }

        void OnEnable() => PlayerRailRider.TapResolved += OnTap;
        void OnDisable() => PlayerRailRider.TapResolved -= OnTap;

        void OnTap(TrackTile tapped, TrackTile changed, bool ok)
        {
            if (tapped == null) return;
            // Reusa o mais antigo.
            Fx fx = null;
            float oldest = -1f;
            foreach (var f in _pool)
            {
                if (f.t < 0f) { fx = f; break; }
                if (f.t > oldest) { oldest = f.t; fx = f; }
            }
            fx.tapped = tapped;
            fx.changed = changed;
            fx.ok = ok;
            fx.t = 0f;
            fx.root.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // engasgo de frame não engole o feedback
            foreach (var fx in _pool)
            {
                if (fx.t < 0f) continue;
                fx.t += dt;
                float k = fx.t / Mathf.Max(0.05f, duration);
                if (k >= 1f || _cam == null || fx.tapped == null || !fx.tapped.isActiveAndEnabled)
                {
                    fx.t = -1f;
                    fx.root.gameObject.SetActive(false);
                    continue;
                }
                float alpha = k < 0.45f ? 1f : 1f - (k - 0.45f) / 0.55f;
                float pop = 1f + 0.35f * (1f - Smooth(k * 2.2f));
                float shake = fx.ok ? 0f : Mathf.Sin(fx.t * 70f) * 14f * (1f - k);

                // Moldura no trilho tocado
                Color fc = fx.ok ? okColor : failColor;
                if (Project(fx.tapped.StartPoint.position, out var a) && Project(fx.tapped.EndPoint.position, out var b))
                {
                    float w = 2f * PixelHalfWidth(fx.tapped.StartPoint.position, a) + 36f;
                    float h = Mathf.Abs(b.y - a.y) + 36f;
                    fx.frame.gameObject.SetActive(true);
                    fx.frame.anchoredPosition = (a + b) * 0.5f + new Vector2(shake, 0f);
                    fx.frame.localScale = Vector3.one * pop;
                    LayoutFrame(fx.frameEdges, w, h, 9f, fc, alpha);
                }
                else fx.frame.gameObject.SetActive(false);

                // Conector que trocou + linha de ligação
                bool showLink = fx.changed != null && fx.changed.EndPoint != null;
                if (showLink && Project(fx.changed.EndPoint.position, out var c) && Project(fx.tapped.StartPoint.position, out var d))
                {
                    Color lc = fx.ok ? linkColor : failColor;
                    fx.marker.gameObject.SetActive(true);
                    fx.marker.anchoredPosition = c;
                    fx.marker.localScale = Vector3.one * pop;
                    fx.marker.localRotation = Quaternion.Euler(0, 0, 45f);
                    LayoutFrame(fx.markerEdges, 46f, 46f, 7f, lc, alpha);

                    fx.line.gameObject.SetActive(fx.ok);
                    if (fx.ok)
                    {
                        var dir = d - c;
                        float len = dir.magnitude * Mathf.Clamp01(k * 3f);   // a linha "corre" até o trilho
                        fx.line.anchoredPosition = c;
                        fx.line.sizeDelta = new Vector2(len, 10f);
                        fx.line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        var col = linkColor; col.a = alpha * 0.9f;
                        fx.lineImg.color = col;
                    }
                }
                else
                {
                    fx.marker.gameObject.SetActive(false);
                    fx.line.gameObject.SetActive(false);
                }
            }
        }

        // ------------------------------------------------------------ helpers

        bool Project(Vector3 world, out Vector2 local)
        {
            local = default;
            var s = _cam.WorldToScreenPoint(world);
            if (s.z < 0f) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, s, null, out local);
        }

        float PixelHalfWidth(Vector3 world, Vector2 center)
        {
            if (!Project(world + Vector3.right * trackHalfWidth, out var e)) return 50f;
            return Mathf.Clamp(Mathf.Abs(e.x - center.x), 24f, 140f);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static void LayoutFrame(Image[] edges, float w, float h, float th, Color c, float alpha)
        {
            c.a *= Mathf.Clamp01(alpha);
            Set(edges[0], new Vector2(0, h / 2), new Vector2(w + th, th), c);
            Set(edges[1], new Vector2(0, -h / 2), new Vector2(w + th, th), c);
            Set(edges[2], new Vector2(-w / 2, 0), new Vector2(th, h), c);
            Set(edges[3], new Vector2(w / 2, 0), new Vector2(th, h), c);
        }

        static void Set(Image i, Vector2 pos, Vector2 size, Color c)
        {
            var rt = i.rectTransform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            i.color = c;
        }

        Fx Build(int i)
        {
            var fx = new Fx();
            fx.root = Node($"Tap{i}", _canvas);
            fx.line = Node("Link", fx.root);
            fx.line.pivot = new Vector2(0f, 0.5f);
            fx.lineImg = fx.line.gameObject.AddComponent<Image>();
            fx.lineImg.raycastTarget = false;
            fx.frame = Node("Frame", fx.root);
            fx.frameEdges = Edges(fx.frame);
            fx.marker = Node("Connector", fx.root);
            fx.markerEdges = Edges(fx.marker);
            fx.root.gameObject.SetActive(false);
            return fx;
        }

        static Image[] Edges(RectTransform parent)
        {
            var e = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var rt = Node($"E{i}", parent);
                e[i] = rt.gameObject.AddComponent<Image>();
                e[i].raycastTarget = false;
            }
            return e;
        }

        static RectTransform Node(string n, Transform parent)
        {
            var go = new GameObject(n, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return rt;
        }
    }
}
