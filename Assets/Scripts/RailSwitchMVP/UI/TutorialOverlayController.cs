using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tutorial overlay no início da Game scene (Fatia 10). Pausa o jogo
    /// (Time.timeScale=0) enquanto mostra hints de controle. Dismiss salva
    /// PlayerPrefs RailMVP.Tutorial.SeenV1=1 pra não repetir.
    ///
    /// Versionado (V1) pra que futuros tutoriais reset-em em conjunto com
    /// adições — só incrementar pra V2 quando o tutorial mudar materialmente.
    ///
    /// Texto adaptado por plataforma: mobile vê instruções de toque, desktop
    /// vê setas/teclas.
    /// </summary>
    public class TutorialOverlayController : MonoBehaviour
    {
        public const string PrefsKey = "RailMVP.Tutorial.SeenV1";

        [Header("UI refs")]
        [SerializeField] private GameObject overlayPanel;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button gotItButton;

        [Header("Strings (PT-BR)")]
        [TextArea(3, 6)]
        [SerializeField] private string mobileText =
            "BEM-VINDO!\n\nToque na ESQUERDA ou DIREITA da tela pra trocar de trilho.\n\nEvite obstáculos e colete moedas.";

        [TextArea(3, 6)]
        [SerializeField] private string desktopText =
            "BEM-VINDO!\n\nUse as setas ← → (ou A/D) pra trocar de trilho.\n\nEvite obstáculos e colete moedas.";

        bool _gameWasPaused;

        void Awake()
        {
            if (gotItButton != null) gotItButton.onClick.AddListener(Dismiss);
            // NÃO chamar overlayPanel.SetActive(false) aqui — componente vive no
            // próprio panel root, mesmo bug trap. Inspector já tem panel inativo
            // (ou ativo, dependendo do setup), e Start() decide visibilidade.
        }

        void OnDestroy()
        {
            if (gotItButton != null) gotItButton.onClick.RemoveListener(Dismiss);
        }

        void Start()
        {
            if (overlayPanel == null) return;

            bool seen = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
            if (seen)
            {
                overlayPanel.SetActive(false);
                return;
            }

            // Texto por plataforma. Application.isMobilePlatform é true em
            // Android/iOS builds, false em Editor/standalone (mesmo se estiver
            // simulando mobile via Touch Input). Pra testar mobile no Editor,
            // checar o tap zones via swipe simulado.
            if (bodyText != null)
                bodyText.text = Application.isMobilePlatform ? mobileText : desktopText;

            overlayPanel.SetActive(true);
            PauseGame();
        }

        public void Dismiss()
        {
            PlayerPrefs.SetInt(PrefsKey, 1);
            PlayerPrefs.Save();
            ResumeGame();
            if (overlayPanel != null) overlayPanel.SetActive(false);
            Debug.Log("[Tutorial] Dismissed. Prefs saved.");
        }

        void PauseGame()
        {
            _gameWasPaused = true;
            Time.timeScale = 0f;
        }

        void ResumeGame()
        {
            if (!_gameWasPaused) return;
            _gameWasPaused = false;
            Time.timeScale = 1f;
        }

        // Garantia: se o overlay for destruído (ex: scene transition) com pause
        // ativo, restore timeScale pra não deixar o jogo congelado pra sempre.
        void OnDisable()
        {
            if (_gameWasPaused) Time.timeScale = 1f;
        }
    }
}
