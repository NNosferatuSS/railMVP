using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tela Home. Mostra nome, coins, best distance, 3 missões diárias + 3
    /// semanais (com botão Reclamar), botão JOGAR, popup de Login diário
    /// e botão de Daily Ad Chest. Stubs de Loja/Leaderboard/Perfil ficam
    /// inertes até as próximas fatias.
    ///
    /// Lê do PlayerDataManager + MissionTracker + DailyLoginManager no
    /// OnEnable (não só Start) — spec §7.2 — pra refletir mudanças ao
    /// voltar do GameOver via "HOME". Também subscribe nos eventos pra
    /// atualizar live.
    /// </summary>
    public class HomeScreenController : MonoBehaviour
    {
        [Header("Top — Player info")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text bestDistanceText;
        [SerializeField] private Button playButton;

        [Header("Missions — 3 daily + 3 weekly entries (preencha o array)")]
        [SerializeField] private MissionEntryUI[] dailyEntries = new MissionEntryUI[MissionTracker.DailySlots];
        [SerializeField] private MissionEntryUI[] weeklyEntries = new MissionEntryUI[MissionTracker.WeeklySlots];

        [Header("Daily Login popup")]
        [SerializeField] private GameObject loginPopupPanel;
        [SerializeField] private TMP_Text loginDayText;
        [SerializeField] private TMP_Text loginRewardText;
        [SerializeField] private Button loginClaimButton;
        [SerializeField] private Button loginCloseButton; // opcional — pode fechar sem reclamar

        [Header("Daily Ad Chest")]
        [SerializeField] private Button chestButton;
        [SerializeField] private TMP_Text chestButtonText;

        [Header("Shop")]
        [SerializeField] private Button shopButton;
        [SerializeField] private ShopController shopController;

        [Header("Stubs (Fatias futuras — desabilitados por padrão)")]
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button profileButton;

        void OnEnable()
        {
            Refresh();

            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged += HandleCoinsChanged;

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged += RefreshMissions;

            var dl = DailyLoginManager.Instance;
            if (dl != null)
            {
                dl.OnLoginClaimed += HandleLoginClaimed;
                dl.OnChestClaimed += HandleChestClaimed;
            }

            if (playButton != null) playButton.onClick.AddListener(LoadGame);
            if (loginClaimButton != null) loginClaimButton.onClick.AddListener(ClaimLogin);
            if (loginCloseButton != null) loginCloseButton.onClick.AddListener(CloseLoginPopup);
            if (chestButton != null) chestButton.onClick.AddListener(ClaimChest);
            if (shopButton != null) shopButton.onClick.AddListener(OpenShop);

            if (leaderboardButton != null) leaderboardButton.interactable = false;
            if (profileButton != null) profileButton.interactable = false;
        }

        void OnDisable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged -= HandleCoinsChanged;

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged -= RefreshMissions;

            var dl = DailyLoginManager.Instance;
            if (dl != null)
            {
                dl.OnLoginClaimed -= HandleLoginClaimed;
                dl.OnChestClaimed -= HandleChestClaimed;
            }

            if (playButton != null) playButton.onClick.RemoveListener(LoadGame);
            if (loginClaimButton != null) loginClaimButton.onClick.RemoveListener(ClaimLogin);
            if (loginCloseButton != null) loginCloseButton.onClick.RemoveListener(CloseLoginPopup);
            if (chestButton != null) chestButton.onClick.RemoveListener(ClaimChest);
            if (shopButton != null) shopButton.onClick.RemoveListener(OpenShop);
        }

        void HandleCoinsChanged(int newTotal)
        {
            if (coinsText != null) coinsText.text = $"Coins: {newTotal}";
        }

        void HandleLoginClaimed() { CloseLoginPopup(); RefreshChestButton(); }
        void HandleChestClaimed() { RefreshChestButton(); }

        void Refresh()
        {
            RefreshPlayerData();
            RefreshMissions();
            RefreshLoginPopup();
            RefreshChestButton();
        }

        void RefreshPlayerData()
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

        void RefreshMissions()
        {
            var mt = MissionTracker.Instance;
            if (mt == null)
            {
                Debug.LogWarning("[Home] MissionTracker.Instance null — adicione _MissionTracker na HomeScene.");
                return;
            }

            for (int i = 0; i < dailyEntries.Length && i < MissionTracker.DailySlots; i++)
            {
                if (dailyEntries[i] == null) continue;
                int slot = i;
                dailyEntries[i].Bind(mt.GetDailyMission(slot), () => mt.ClaimDaily(slot));
            }
            for (int i = 0; i < weeklyEntries.Length && i < MissionTracker.WeeklySlots; i++)
            {
                if (weeklyEntries[i] == null) continue;
                int slot = i;
                weeklyEntries[i].Bind(mt.GetWeeklyMission(slot), () => mt.ClaimWeekly(slot));
            }
        }

        void RefreshLoginPopup()
        {
            var dl = DailyLoginManager.Instance;
            if (dl == null)
            {
                if (loginPopupPanel != null) loginPopupPanel.SetActive(false);
                return;
            }
            bool show = dl.ShouldShowPopup();
            if (loginPopupPanel != null) loginPopupPanel.SetActive(show);
            if (!show) return;

            if (loginDayText != null)
                loginDayText.text = $"Dia {dl.NextDay} de {DailyLoginManager.LoginCycleLength}";
            if (loginRewardText != null)
                loginRewardText.text = $"+{dl.NextDayReward} coins";
        }

        void RefreshChestButton()
        {
            var dl = DailyLoginManager.Instance;
            if (chestButton == null) return;
            if (dl == null)
            {
                chestButton.interactable = false;
                if (chestButtonText != null) chestButtonText.text = "Baú Grátis";
                return;
            }
            bool available = dl.IsChestAvailable();
            chestButton.interactable = available;
            if (chestButtonText != null)
            {
                chestButtonText.text = available
                    ? $"Baú Grátis +{DailyLoginManager.ChestReward}"
                    : "Baú já reclamado hoje";
            }
        }

        public void ClaimLogin()
        {
            var dl = DailyLoginManager.Instance;
            if (dl != null) dl.ClaimLogin();
        }

        public void CloseLoginPopup()
        {
            if (loginPopupPanel != null) loginPopupPanel.SetActive(false);
        }

        public void ClaimChest()
        {
            var dl = DailyLoginManager.Instance;
            if (dl != null) dl.ClaimChest();
        }

        public void OpenShop()
        {
            if (shopController != null) shopController.Open();
            else Debug.LogWarning("[Home] shopController não atribuído.");
        }

        public void LoadGame()
        {
            SceneManager.LoadScene(SceneNames.Game);
        }
    }
}
