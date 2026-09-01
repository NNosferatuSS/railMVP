using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Puck de power-up 52×52. Ring radial (fillAmount = tempoRestante/duração),
    /// tile de cor no centro, ícone por cima, badge com segundos. Pisca vermelho nos
    /// últimos 3s (durationRemaining ≤ 3f).
    /// </summary>
    public class PuckUI : MonoBehaviour
    {
        [SerializeField] private Image ring;         // Image tipo Filled/Radial360
        [SerializeField] private Image tile;
        [SerializeField] private Image iconImg;
        [SerializeField] private TMP_Text badgeText; // segundos restantes
        [SerializeField] private GameObject badgeGO; // container do badge (esconde se sem badge)

        [Header("Warning")]
        [SerializeField] private float warningThreshold = 3f;
        [SerializeField] private Color warningColor = new Color(1f, 0.30f, 0.36f, 1f);

        Color _ringNormalColor;
        Color _tileNormalColor;

        void Awake()
        {
            if (ring != null) _ringNormalColor = ring.color;
            if (tile != null) _tileNormalColor = tile.color;
        }

        public void SetIcon(Sprite s) { if (iconImg != null) iconImg.sprite = s; }

        public void SetProgress(float remaining, float duration)
        {
            if (ring == null) return;
            float t = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            ring.fillAmount = t;
            bool warn = remaining <= warningThreshold;
            ring.color = warn ? warningColor : _ringNormalColor;
            if (badgeText != null)
                badgeText.text = Mathf.CeilToInt(remaining).ToString();
        }

        public void SetBadge(string text)
        {
            if (badgeText != null) badgeText.text = text;
            if (badgeGO != null) badgeGO.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetVisible(bool v) { gameObject.SetActive(v); }
    }
}
