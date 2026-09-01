using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Barra de tabs (tabs_track + tabs_thumb).
    /// Chame SetTabs(strings) e assine OnTabSelected.
    ///
    /// Layout por ÂNCORAS FRACIONÁRIAS: a tab i ocupa [i/n, (i+1)/n] do track.
    /// Isso não depende de track.rect.width estar resolvido, então funciona
    /// igual em build-time (prefab), em runtime e em qualquer resolução.
    /// (A versão anterior media rect.width no momento do build — quando ainda
    /// era 0 — e o thumb saía com largura lixo, vazando pra fora da tela.)
    /// </summary>
    public class TabsUI : MonoBehaviour
    {
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform thumb;
        [SerializeField] private RectTransform labelsContainer;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private float labelSize = 12f;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.55f);

        [Tooltip("Tabs iniciais. Se preenchido, são construídas no Start — " +
                 "assim o prefab já nasce com as tabs certas sem chamada externa.")]
        [SerializeField] private string[] initialTabs;

        public event Action<int> OnTabSelected;

        readonly List<TMP_Text> _labels = new List<TMP_Text>();
        int _count;
        int _current;

        public int Current => _current;

        void Start()
        {
            if (_labels.Count == 0 && initialTabs != null && initialTabs.Length > 0)
                SetTabs(initialTabs);
            else
                ApplyThumb();
        }

        public void SetTabs(string[] tabs)
        {
            if (tabs == null || tabs.Length == 0) return;
            if (labelsContainer == null) return;

            // Limpa (DestroyImmediate em editor/build, Destroy em runtime)
            for (int i = labelsContainer.childCount - 1; i >= 0; i--)
            {
                var child = labelsContainer.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            _labels.Clear();
            _count = tabs.Length;

            for (int i = 0; i < tabs.Length; i++)
            {
                // Container (Image p/ raycast + Button). O TMP vai num FILHO —
                // Image e TextMeshProUGUI no mesmo GO se cancelam (ambos MaskableGraphic).
                var go = new GameObject("Tab_" + tabs[i], typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(labelsContainer, false);

                // Âncora fracionária: fatia i de n. Independe de largura resolvida.
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2((float)i / tabs.Length, 0f);
                rt.anchorMax = new Vector2((float)(i + 1) / tabs.Length, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                img.raycastTarget = true;

                var lblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGO.transform.SetParent(go.transform, false);
                var lblRt = (RectTransform)lblGO.transform;
                lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;

                var t = lblGO.GetComponent<TextMeshProUGUI>();
                t.font = labelFont;
                t.text = tabs[i];
                t.fontSize = labelSize;
                t.color = i == 0 ? activeColor : inactiveColor;
                t.alignment = TextAlignmentOptions.Center;
                t.fontStyle |= FontStyles.UpperCase;
                t.characterSpacing = 8f;   // ~0.08em. (era 100 = 1em, letras espalhadas)
                t.raycastTarget = false;
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Ellipsis;
                _labels.Add(t);

                int captured = i;
                var btn = go.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Select(captured));
            }

            Select(0, notify: false);
        }

        public void Select(int index, bool notify = true)
        {
            if (_labels.Count == 0) return;
            _current = Mathf.Clamp(index, 0, _labels.Count - 1);

            for (int i = 0; i < _labels.Count; i++)
                _labels[i].color = i == _current ? activeColor : inactiveColor;

            ApplyThumb();
            if (notify) OnTabSelected?.Invoke(_current);
        }

        // Thumb também por âncora fracionária — nada de sizeDelta calculado.
        void ApplyThumb()
        {
            if (thumb == null) return;
            int n = Mathf.Max(1, _count > 0 ? _count : _labels.Count);
            thumb.anchorMin = new Vector2((float)_current / n, 0f);
            thumb.anchorMax = new Vector2((float)(_current + 1) / n, 1f);
            thumb.pivot = new Vector2(0.5f, 0.5f);
            thumb.offsetMin = new Vector2(2f, 2f);
            thumb.offsetMax = new Vector2(-2f, -2f);
        }
    }
}
