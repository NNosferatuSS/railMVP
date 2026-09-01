using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Core;
using RailSwitchMVP.Economy;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Preenche o layout novo do Canvas_GameOver (spec S11) com números limpos e
    /// controla a linha "CONTINUE RUN".
    ///
    /// Por que existe: o GameOverController legado escreve FRASES nos seus
    /// TMP_Text serializados ("Distance: 0 m  (Best: 8537)"). Isso funciona no
    /// layout antigo, mas estoura um hero de 58pt e os cards de 18pt do design
    /// novo. Então os campos verbosos do controller ficam null e esta view lê
    /// RunMeters/RunCoins/RunXP crus via OnPanelShown.
    ///
    /// A oferta de continue vive AQUI (não num overlay antes do game over) —
    /// ver ReviveController.offerBeforeGameOver.
    /// </summary>
    public class GameOverView : MonoBehaviour
    {
        [Header("Hero")]
        [SerializeField] private TMP_Text distanceValue;   // "1 234" — só o número
        [SerializeField] private TMP_Text bestLine;        // "BEST 8 537 M · TIER 4"
        // O label "DEAD END" é escrito pelo próprio GameOverController (reasonText),
        // que já monta "DAILY CHALLENGE — Dead End" quando é run diária.

        [Header("Cards")]
        [SerializeField] private TMP_Text coinsValue;
        [SerializeField] private TMP_Text xpValue;
        [SerializeField] private TMP_Text missionsValue;

        [Header("Continue run (revive)")]
        [SerializeField] private GameObject continueRow;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueCost;
        [SerializeField] private TMP_Text continueSubtitle;

        [SerializeField] private GameOverController controller;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Awake()
        {
            if (controller == null) controller = GetComponent<GameOverController>();
            if (controller != null) controller.OnPanelShown += Populate;
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
        }

        void OnDestroy()
        {
            if (controller != null) controller.OnPanelShown -= Populate;
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        void Populate()
        {
            if (controller == null) return;

            if (distanceValue != null)
                distanceValue.text = controller.RunMeters.ToString("N0", Inv);

            if (bestLine != null)
            {
                var pdm = PlayerDataManager.Instance;
                string best = pdm != null ? pdm.BestDistance.ToString("N0", Inv) : "—";
                bestLine.text = $"BEST {best} M · TIER {controller.RunTier}";
            }

            if (coinsValue != null)
                coinsValue.text = controller.RunCoins.ToString("N0", Inv);

            if (xpValue != null)
                xpValue.text = "+" + controller.RunXP.ToString("N0", Inv);

            if (missionsValue != null)
            {
                int done = MissionTracker.Instance != null
                    ? MissionTracker.Instance.MissionsCompletedThisRun : 0;
                missionsValue.text = $"{done}/{MissionTracker.DailySlots}";
            }

            RefreshContinueRow();
        }

        void RefreshContinueRow()
        {
            var revive = ReviveController.Instance;

            // Sem ReviveController na cena, ou continues esgotados → esconde a linha.
            if (revive == null || !revive.CanContinue())
            {
                if (continueRow != null) continueRow.SetActive(false);
                return;
            }
            if (continueRow != null) continueRow.SetActive(true);

            int cost = revive.GetCurrentContinueGemCost();
            int gems = CurrencyManager.Instance != null
                ? CurrencyManager.Instance.GetBalance(CurrencyType.Gems) : 0;
            bool affordable = gems >= cost;

            if (continueCost != null) continueCost.text = cost.ToString();
            if (continueButton != null) continueButton.interactable = affordable;
            if (continueSubtitle != null)
                continueSubtitle.text = affordable
                    ? "Respawn no caminho seguro"
                    : $"Gems insuficientes ({gems}/{cost})";
        }

        void OnContinueClicked()
        {
            var revive = ReviveController.Instance;
            if (revive == null) return;
            // TryReviveFromGameOver cobra as gems, reposiciona o player, liga o
            // grace period e devolve o GameManager pra Playing.
            if (revive.TryReviveFromGameOver() && controller != null)
                controller.HideForRevive();
        }
    }
}
