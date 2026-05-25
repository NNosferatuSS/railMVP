using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Component pra uma row do leaderboard (Fatia 8). Atribuir refs ao prefab
    /// no Editor; LeaderboardPanelController instancia múltiplas dessas dentro
    /// do Scroll View.
    /// </summary>
    public class LeaderboardEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text distanceText;

        [Tooltip("Image de fundo da row (opcional) — usada pra highlight da row do user.")]
        [SerializeField] private Image background;

        [Header("Highlight (row do user)")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.05f);
        [SerializeField] private Color myRowColor = new Color(0.95f, 0.78f, 0.25f, 0.25f); // dourado translucido

        public void Bind(LeaderboardEntry entry, bool isMyRow)
        {
            if (rankText != null) rankText.text = $"#{entry.rank}";
            if (nameText != null) nameText.text = entry.player_name ?? "Player";
            if (distanceText != null) distanceText.text = $"{entry.distance} m";
            if (background != null) background.color = isMyRow ? myRowColor : normalColor;
        }
    }
}
