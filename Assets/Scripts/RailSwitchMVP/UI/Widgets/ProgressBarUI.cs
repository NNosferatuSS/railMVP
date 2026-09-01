using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>Barra track + fill (Image Filled Horizontal). Opcional TMP a direita.</summary>
    public class ProgressBarUI : MonoBehaviour
    {
        [SerializeField] private Image fill;      // fillMethod=Horizontal, origin=Left
        [SerializeField] private TMP_Text valueText;

        public void SetProgress(float t, string label = null)
        {
            if (fill != null) fill.fillAmount = Mathf.Clamp01(t);
            if (valueText != null && label != null) valueText.text = label;
        }
    }
}
