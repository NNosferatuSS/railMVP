using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Painel de streak de login diário. Mostra os 7 dias do ciclo com suas
    /// recompensas, destaca o dia atual e permite reclamar sem fechar o painel.
    ///
    /// Setup: crie um painel com 7 filhos LoginDayEntryUI (um por dia) e
    /// atribua-os ao array dayEntries em ordem (índice 0 = Dia 1).
    /// </summary>
    public class LoginStreakPanelController : MonoBehaviour
    {
        [Header("Painel")]
        [SerializeField] private GameObject panel;
        [Tooltip("Opcional — título acima dos cards, ex: 'Login Diário'.")]
        [SerializeField] private TMP_Text titleText;

        [Header("7 cards de dia (índice 0 = Dia 1, índice 6 = Dia 7)")]
        [SerializeField] private LoginDayEntryUI[] dayEntries = new LoginDayEntryUI[DailyLoginManager.LoginCycleLength];

        [Header("Botões")]
        [Tooltip("Botão de Reclamar — só interagível quando há recompensa disponível hoje.")]
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimButtonText;
        [SerializeField] private Button closeButton;

        void Awake()
        {
            if (claimButton != null) claimButton.onClick.AddListener(Claim);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (claimButton != null) claimButton.onClick.RemoveListener(Claim);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);

            var dl = DailyLoginManager.Instance;
            if (dl != null) dl.OnLoginClaimed -= Refresh;
        }

        public void Open()
        {
            if (panel != null) panel.SetActive(true);
            Refresh();

            // Assina o evento pra atualizar os cards imediatamente após Claim.
            var dl = DailyLoginManager.Instance;
            if (dl != null)
            {
                dl.OnLoginClaimed -= Refresh; // evita duplicata se Open() chamado 2x
                dl.OnLoginClaimed += Refresh;
            }
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);

            var dl = DailyLoginManager.Instance;
            if (dl != null) dl.OnLoginClaimed -= Refresh;
        }

        public bool IsOpen => panel != null && panel.activeSelf;

        void Claim()
        {
            var dl = DailyLoginManager.Instance;
            if (dl != null) dl.ClaimLogin();
            // Refresh disparado pelo evento OnLoginClaimed.
        }

        void Refresh()
        {
            var dl = DailyLoginManager.Instance;
            if (dl == null) return;

            bool canClaim = dl.ShouldShowPopup();
            int nextDay   = dl.NextDay;           // 1-7
            int lastDay   = dl.LastClaimedDay;    // 0 = nunca; 1-7

            // Atualiza cada card.
            for (int i = 0; i < dayEntries.Length; i++)
            {
                if (dayEntries[i] == null) continue;

                int dayNumber  = i + 1;                                     // 1-7
                int coins      = DailyLoginManager.LoginRewards[i];
                int gems       = DailyLoginManager.LoginGemBonuses[i];
                var status     = ResolveStatus(dayNumber, nextDay, lastDay, canClaim);

                dayEntries[i].Bind(dayNumber, coins, gems, status);
            }

            // Botão de Claim.
            if (claimButton != null)
            {
                claimButton.interactable = canClaim;
                if (claimButtonText != null)
                    claimButtonText.text = canClaim
                        ? $"Reclamar Dia {nextDay}"
                        : "Já reclamado hoje";
            }
        }

        static LoginDayStatus ResolveStatus(int dayNumber, int nextDay, int lastDay, bool canClaim)
        {
            if (dayNumber < nextDay)
                return LoginDayStatus.Completed;      // dias anteriores do ciclo atual

            if (dayNumber == nextDay)
                return canClaim
                    ? LoginDayStatus.AvailableToday   // hoje, ainda não reclamado
                    : LoginDayStatus.ClaimedToday;    // hoje, já reclamado

            return LoginDayStatus.Upcoming;           // dias futuros
        }
    }
}
