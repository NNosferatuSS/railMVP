using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RailSwitchMVP.UI
{
    public enum LoginDayStatus
    {
        Completed,      // já reclamado em dia anterior do ciclo
        ClaimedToday,   // reclamado hoje
        AvailableToday, // disponível pra reclamar agora
        Upcoming,       // dias futuros do ciclo
    }

    /// <summary>
    /// Card visual de um único dia no painel de streak de login.
    /// Bind() preenche todos os campos e aplica o estilo correto pro status.
    /// </summary>
    public class LoginDayEntryUI : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text dayLabel;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text gemBonusText;

        [Header("Status visuals")]
        [Tooltip("Ícone/overlay que aparece em dias concluídos (checkmark, etc).")]
        [SerializeField] private GameObject completedOverlay;
        [Tooltip("Borda ou highlight que aparece no dia de hoje.")]
        [SerializeField] private GameObject todayHighlight;

        [Header("Background tint (opcional)")]
        [SerializeField] private Image background;
        [SerializeField] private Color colorCompleted  = new Color(0.35f, 0.35f, 0.35f);
        [SerializeField] private Color colorToday      = new Color(0.20f, 0.70f, 0.30f);
        [SerializeField] private Color colorAvailable  = new Color(1.00f, 0.85f, 0.10f);
        [SerializeField] private Color colorUpcoming   = new Color(0.15f, 0.15f, 0.15f);

        public void Bind(int dayNumber, int coinsReward, int gemBonus, LoginDayStatus status)
        {
            if (dayLabel != null)
                dayLabel.text = $"Dia {dayNumber}";

            if (coinsText != null)
                coinsText.text = $"+{coinsReward}";

            if (gemBonusText != null)
            {
                if (gemBonus > 0)
                {
                    gemBonusText.text = $"+{gemBonus} gem{(gemBonus > 1 ? "s" : "")}";
                    gemBonusText.gameObject.SetActive(true);
                }
                else
                {
                    gemBonusText.gameObject.SetActive(false);
                }
            }

            bool done = status == LoginDayStatus.Completed || status == LoginDayStatus.ClaimedToday;
            bool isToday = status == LoginDayStatus.ClaimedToday || status == LoginDayStatus.AvailableToday;

            if (completedOverlay != null) completedOverlay.SetActive(done);
            if (todayHighlight != null)   todayHighlight.SetActive(isToday);

            if (background != null)
            {
                background.color = status switch
                {
                    LoginDayStatus.Completed     => colorCompleted,
                    LoginDayStatus.ClaimedToday  => colorToday,
                    LoginDayStatus.AvailableToday => colorAvailable,
                    _                            => colorUpcoming,
                };
            }
        }
    }
}
