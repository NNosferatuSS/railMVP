using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Profile / Edit Name panel (Fatia 9). Modal sobreposto na HomeScene.
    /// Permite editar PDM.PlayerName. Validation 3-12 chars (after trim) via
    /// PDM.SetPlayerName. Em sucesso, dispara sync automático via Fatia 7B.
    ///
    /// Padrão do LeaderboardPanelController: Awake NÃO chama SetActive(false)
    /// no panel root (componente vive nele). Setup doc instrui usuário a
    /// desativar no Inspector.
    /// </summary>
    public class ProfilePanelController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private Button closeButton;

        [Header("Edit field")]
        [Tooltip("Input pra digitar o novo nome. Length controlado via PDM.SetPlayerName.")]
        [SerializeField] private TMP_InputField nameInputField;

        [Tooltip("Botão de confirmar.")]
        [SerializeField] private Button saveButton;

        [Tooltip("Texto vermelho que aparece em erro de validação. Inativo em estado normal.")]
        [SerializeField] private TMP_Text errorText;

        [Header("Strings (PT-BR defaults)")]
        [SerializeField] private string errorTooShort = "Nome muito curto (mínimo {min}).";
        [SerializeField] private string errorTooLong = "Nome muito longo (máximo {max}).";
        [SerializeField] private string errorEmpty = "Digite um nome.";

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
            // NÃO chamar profilePanel.SetActive(false) aqui — bug-trap quando o
            // componente vive no próprio panel root (Awake só dispara na 1ª
            // ativação, desativando logo após). Inspector já tem panel inativo.
            if (errorText != null) errorText.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (saveButton != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        public void Open()
        {
            if (profilePanel == null) return;
            profilePanel.SetActive(true);

            // Popula input com nome atual.
            var pdm = PlayerDataManager.Instance;
            if (nameInputField != null && pdm != null)
                nameInputField.text = pdm.PlayerName ?? "";

            HideError();
        }

        public void Close()
        {
            if (profilePanel != null) profilePanel.SetActive(false);
            HideError();
        }

        void OnSaveClicked()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null)
            {
                ShowError("Sistema indisponível.");
                return;
            }
            string raw = nameInputField != null ? nameInputField.text : "";
            string trimmed = (raw ?? "").Trim();

            // Validação client-side pra dar feedback amigável. PDM faz revalidação.
            if (trimmed.Length == 0)
            {
                ShowError(errorEmpty);
                return;
            }
            if (trimmed.Length < PlayerDataManager.PlayerNameMinLength)
            {
                ShowError(errorTooShort.Replace("{min}", PlayerDataManager.PlayerNameMinLength.ToString()));
                return;
            }
            if (trimmed.Length > PlayerDataManager.PlayerNameMaxLength)
            {
                ShowError(errorTooLong.Replace("{max}", PlayerDataManager.PlayerNameMaxLength.ToString()));
                return;
            }

            if (pdm.SetPlayerName(trimmed))
            {
                Close();
            }
            else
            {
                ShowError("Nome inválido.");
            }
        }

        void ShowError(string msg)
        {
            if (errorText == null) return;
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }

        void HideError()
        {
            if (errorText != null) errorText.gameObject.SetActive(false);
        }
    }
}
