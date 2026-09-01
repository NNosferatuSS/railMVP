using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI.Widgets
{
    /// <summary>
    /// Chip de moeda (coins/gems/tokens). Ícone + valor + opcional botão "+".
    /// SetValue formata com separador de milhar.
    /// </summary>
    public class ChipUI : MonoBehaviour
    {
        [SerializeField] private Image iconImg;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Button plusButton; // opcional
        [SerializeField] private Button rootButton; // opcional (chip inteiro clicável)

        public Button PlusButton => plusButton;
        public Button RootButton => rootButton;

        public void SetValue(long v)
        {
            if (valueText != null) valueText.text = v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public void SetIcon(Sprite s) { if (iconImg != null) iconImg.sprite = s; }
    }
}
