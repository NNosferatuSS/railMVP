using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tela Home (placeholder Fatia 1). Mostra nome, coins e best distance,
    /// botão JOGAR carrega a GameScene. Stubs de Loja/Leaderboard/Perfil
    /// ficam inertes até as próximas fatias.
    ///
    /// Lê do PlayerDataManager no OnEnable (não só Start) — spec §7.2 — pra
    /// refletir mudanças quando voltar do GameOver via "HOME".
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        public const string GameSceneName = "RailSwitchMVP";

        [Header("UI refs")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text bestDistanceText;
        [SerializeField] private Button playButton;

        [Header("Stubs (Fatias futuras — desabilitados por padrão)")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button profileButton;

        void OnEnable()
        {
            Refresh();
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged += HandleCoinsChanged;

            if (playButton != null) playButton.onClick.AddListener(LoadGame);

            // Stubs ficam desabilitados visualmente até a fatia que os ativa.
            if (shopButton != null) shopButton.interactable = false;
            if (leaderboardButton != null) leaderboardButton.interactable = false;
            if (profileButton != null) profileButton.interactable = false;
        }

        void OnDisable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged -= HandleCoinsChanged;

            if (playButton != null) playButton.onClick.RemoveListener(LoadGame);
        }

        void HandleCoinsChanged(int newTotal)
        {
            if (coinsText != null) coinsText.text = $"Coins: {newTotal}";
        }

        void Refresh()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null)
            {
                if (playerNameText != null) playerNameText.text = "Player";
                if (coinsText != null) coinsText.text = "Coins: 0";
                if (bestDistanceText != null) bestDistanceText.text = "Best: 0 m";
                Debug.LogWarning("[Home] PlayerDataManager.Instance null — adicione _PlayerDataManager na HomeScene.");
                return;
            }

            if (playerNameText != null) playerNameText.text = pdm.PlayerName;
            if (coinsText != null) coinsText.text = $"Coins: {pdm.Coins}";
            if (bestDistanceText != null) bestDistanceText.text = $"Best: {pdm.BestDistance} m";
        }

        public void LoadGame()
        {
            SceneManager.LoadScene(GameSceneName);
        }
    }
}
