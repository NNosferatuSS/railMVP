using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Componente que representa visualmente uma única missão na Home.
    /// Agrupa os 4 refs (description, progress, reward, claim button) num
    /// só GameObject pra reduzir refs serializados no HomeScreenController.
    ///
    /// Use `Bind(entry, onClaim)` pra preencher dados e wirar o botão.
    /// </summary>
    public class MissionEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimButtonText;

        Action _onClaim;

        void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(HandleClick);
        }

        void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(HandleClick);
        }

        void HandleClick()
        {
            _onClaim?.Invoke();
        }

        public void Bind(MissionEntry entry, Action onClaim)
        {
            _onClaim = onClaim;

            if (descriptionText != null) descriptionText.text = entry.Description;

            float displayProgress = Mathf.Min(entry.Progress, entry.Target);
            if (progressText != null)
                progressText.text = $"{FormatNum(displayProgress)} / {FormatNum(entry.Target)}";
            if (rewardText != null)
                rewardText.text = $"+{entry.Reward}";

            if (claimButtonText != null)
            {
                if (entry.IsClaimed) claimButtonText.text = "Reclamado";
                else if (entry.IsComplete) claimButtonText.text = "Reclamar";
                else claimButtonText.text = "—";
            }

            if (claimButton != null)
                claimButton.interactable = entry.IsComplete && !entry.IsClaimed;
        }

        static string FormatNum(float value)
        {
            return value >= 100 ? Mathf.RoundToInt(value).ToString() : value.ToString("0.#");
        }
    }
}
