using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Economy;
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
        [Tooltip("Account level do jogador (Camada 1). Ex: 'Lv. 7'. Opcional — auto-skip se vazio.")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("Saldo de gems premium. Ex: '💎 12'. Opcional — auto-skip se vazio.")]
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private TMP_Text bestDistanceText;
        [SerializeField] private Button playButton;

        [Header("Missions — 3 daily + 3 weekly entries (preencha o array)")]
        [SerializeField] private MissionEntryUI[] dailyEntries = new MissionEntryUI[MissionTracker.DailySlots];
        [SerializeField] private MissionEntryUI[] weeklyEntries = new MissionEntryUI[MissionTracker.WeeklySlots];

        [Header("Daily Login popup")]
        [SerializeField] private GameObject loginPopupPanel;
        [SerializeField] private TMP_Text loginDayText;
        [SerializeField] private TMP_Text loginRewardText;
        [Tooltip("Texto opcional que aparece só quando o dia atual tem bônus de gem. " +
            "Ex: '+2 gems'. Esconde automaticamente nos dias sem gem. Pode ser estilizado " +
            "com ícone e cor diferente de loginRewardText.")]
        [SerializeField] private TMP_Text loginGemBonusText;
        [SerializeField] private Button loginClaimButton;
        [SerializeField] private Button loginCloseButton; // opcional — pode fechar sem reclamar

        [Header("Daily Login — streak panel")]
        [Tooltip("Botão que abre o painel de streak (calendário dos 7 dias). Opcional.")]
        [SerializeField] private Button loginStreakButton;
        [SerializeField] private LoginStreakPanelController loginStreakPanel;

        [Header("Daily Ad Chest")]
        [SerializeField] private Button chestButton;
        [SerializeField] private TMP_Text chestButtonText;

        [Header("Daily Challenge (Fatia 6)")]
        [SerializeField] private Button dailyChallengeButton;
        [Tooltip("Texto que mostra Today's Best + Best Ever. Substituído em runtime.")]
        [SerializeField] private TMP_Text dailyChallengeText;

        [Header("Shop")]
        [SerializeField] private Button shopButton;
        [SerializeField] private ShopController shopController;

        [Header("Leaderboard (Fatia 8)")]
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private LeaderboardPanelController leaderboardController;

        [Header("Profile (Fatia 9)")]
        [SerializeField] private Button profileButton;
        [SerializeField] private ProfilePanelController profileController;

        void OnEnable()
        {
            Refresh();

            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.OnCoinsChanged += HandleCoinsChanged;
                pdm.OnPlayerNameChanged += HandlePlayerNameChanged;
                pdm.OnFullStateChanged += HandleFullStateChanged;
            }

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged += RefreshMissions;

            var dl = DailyLoginManager.Instance;
            if (dl != null)
            {
                dl.OnLoginClaimed += HandleLoginClaimed;
                dl.OnChestClaimed += HandleChestClaimed;
            }

            var currency = CurrencyManager.Instance;
            if (currency != null) currency.OnCurrencyChanged += HandleCurrencyChanged;

            var ads = AdsManager.Instance;
            if (ads != null) ads.OnRewardedReadyChanged += HandleAdsReadyChanged;

            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.OnDailyResultRecorded += HandleDailyResultRecorded;

            if (playButton != null) playButton.onClick.AddListener(LoadGame);
            if (loginStreakButton != null) loginStreakButton.onClick.AddListener(OpenLoginStreak);
            if (loginClaimButton != null) loginClaimButton.onClick.AddListener(ClaimLogin);
            if (loginCloseButton != null) loginCloseButton.onClick.AddListener(CloseLoginPopup);
            if (chestButton != null) chestButton.onClick.AddListener(ClaimChest);
            if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
            if (dailyChallengeButton != null) dailyChallengeButton.onClick.AddListener(StartDailyChallenge);
            if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OpenLeaderboard);
            if (profileButton != null) profileButton.onClick.AddListener(OpenProfile);
        }

        void OnDisable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null)
            {
                pdm.OnCoinsChanged -= HandleCoinsChanged;
                pdm.OnPlayerNameChanged -= HandlePlayerNameChanged;
                pdm.OnFullStateChanged -= HandleFullStateChanged;
            }

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged -= RefreshMissions;

            var dl = DailyLoginManager.Instance;
            if (dl != null)
            {
                dl.OnLoginClaimed -= HandleLoginClaimed;
                dl.OnChestClaimed -= HandleChestClaimed;
            }

            var currency = CurrencyManager.Instance;
            if (currency != null) currency.OnCurrencyChanged -= HandleCurrencyChanged;

            var ads = AdsManager.Instance;
            if (ads != null) ads.OnRewardedReadyChanged -= HandleAdsReadyChanged;

            var daily = DailyChallengeManager.Instance;
            if (daily != null) daily.OnDailyResultRecorded -= HandleDailyResultRecorded;

            if (playButton != null) playButton.onClick.RemoveListener(LoadGame);
            if (loginStreakButton != null) loginStreakButton.onClick.RemoveListener(OpenLoginStreak);
            if (loginClaimButton != null) loginClaimButton.onClick.RemoveListener(ClaimLogin);
            if (loginCloseButton != null) loginCloseButton.onClick.RemoveListener(CloseLoginPopup);
            if (chestButton != null) chestButton.onClick.RemoveListener(ClaimChest);
            if (shopButton != null) shopButton.onClick.RemoveListener(OpenShop);
            if (dailyChallengeButton != null) dailyChallengeButton.onClick.RemoveListener(StartDailyChallenge);
            if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OpenLeaderboard);
            if (profileButton != null) profileButton.onClick.RemoveListener(OpenProfile);
        }

        void HandleCoinsChanged(int newTotal)
        {
            if (coinsText != null) coinsText.text = $"Coins: {newTotal}";
        }

        void HandleCurrencyChanged(CurrencyType type, int newTotal)
        {
            if (type == CurrencyType.Gems && gemsText != null)
                gemsText.text = $"Gems: {newTotal}";
        }

        void HandlePlayerNameChanged(string newName)
        {
            if (playerNameText != null) playerNameText.text = newName;
        }

        // Pull do server (Fatia 7B) atualiza vários campos de uma vez — coins/name
        // têm eventos próprios, mas best/runs não. Refresh() completo cobre todos.
        void HandleFullStateChanged() => Refresh();

        void HandleLoginClaimed() { CloseLoginPopup(); RefreshChestButton(); }
        void HandleChestClaimed() { RefreshChestButton(); }
        void HandleAdsReadyChanged(bool _) { RefreshChestButton(); }
        void HandleDailyResultRecorded() { RefreshDailyChallengeCard(); }

        void Refresh()
        {
            RefreshPlayerData();
            RefreshMissions();
            RefreshLoginPopup();
            RefreshChestButton();
            RefreshDailyChallengeCard();
        }

        void RefreshPlayerData()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null)
            {
                if (playerNameText != null) playerNameText.text = "Player";
                if (levelText != null) levelText.text = "Lv. 1";
                if (coinsText != null) coinsText.text = "Coins: 0";
                if (gemsText != null) gemsText.text = "Gems: 0";
                if (bestDistanceText != null) bestDistanceText.text = "Best: 0 m";
                Debug.LogWarning("[Home] PlayerDataManager.Instance null — adicione _PlayerDataManager na HomeScene.");
                return;
            }
            if (playerNameText != null) playerNameText.text = pdm.PlayerName;
            if (levelText != null) levelText.text = $"Lv. {pdm.AccountLevel}";
            if (coinsText != null) coinsText.text = $"Coins: {pdm.Coins}";
            if (gemsText != null)
            {
                int gems = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetBalance(CurrencyType.Gems) : 0;
                gemsText.text = $"Gems: {gems}";
            }
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

            // Bônus de gem: mostra só quando o dia atual tem gem, esconde nos outros.
            if (loginGemBonusText != null)
            {
                int gemBonus = DailyLoginManager.LoginGemBonuses[dl.NextDay - 1];
                if (gemBonus > 0)
                {
                    loginGemBonusText.text = $"+{gemBonus} gem{(gemBonus > 1 ? "s" : "")}";
                    loginGemBonusText.gameObject.SetActive(true);
                }
                else
                {
                    loginGemBonusText.gameObject.SetActive(false);
                }
            }
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

            // Fatia 5: se AdsManager existe mas ad não pronto (offline / load
            // falhou), esconde o botão completamente (spec §5.2 — sem fallback
            // grátis em produção). Sem AdsManager na cena = modo stub
            // (sempre claimable), pra Editor / dev rápido.
            var ads = AdsManager.Instance;
            bool adsAvailable = ads == null || ads.IsRewardedReady;

            bool show = available && adsAvailable;
            chestButton.gameObject.SetActive(show || !available);
            chestButton.interactable = show;
            if (chestButtonText != null)
            {
                if (!available) chestButtonText.text = "Baú já reclamado hoje";
                else if (!adsAvailable) chestButtonText.text = "Carregando...";
                else chestButtonText.text = $"Baú Grátis +{DailyLoginManager.ChestReward}";
            }
        }

        public void OpenLoginStreak()
        {
            if (loginStreakPanel != null) loginStreakPanel.Open();
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

        void RefreshDailyChallengeCard()
        {
            if (dailyChallengeText == null) return;
            var daily = DailyChallengeManager.Instance;
            if (daily == null)
            {
                dailyChallengeText.text = "Daily Challenge — (manager ausente)";
                if (dailyChallengeButton != null) dailyChallengeButton.interactable = false;
                return;
            }
            string today = daily.HasPlayedToday() ? $"{daily.TodayBestM} m" : "—";
            string ever = daily.BestEverM > 0 ? $"{daily.BestEverM} m" : "—";
            dailyChallengeText.text = $"Hoje: {today}   Best: {ever}";
            if (dailyChallengeButton != null) dailyChallengeButton.interactable = true;
        }

        public void StartDailyChallenge()
        {
            var daily = DailyChallengeManager.Instance;
            if (daily == null)
            {
                Debug.LogWarning("[Home] DailyChallengeManager.Instance null — adicione _DailyChallengeManager na HomeScene.");
                return;
            }
            daily.StartChallenge();
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void ClaimChest()
        {
            var dl = DailyLoginManager.Instance;
            if (dl == null || !dl.IsChestAvailable()) return;

            var ads = AdsManager.Instance;
            if (ads == null)
            {
                // Modo stub (Editor sem AdsManager na cena).
                dl.ClaimChest();
                return;
            }

            // Spec §5.2 — só credita NO callback de sucesso do ad. Botão já
            // foi escondido se !IsRewardedReady, mas double-check via TryShow.
            bool shown = ads.TryShowRewarded(
                onSuccess: () => dl.ClaimChest(),
                onFailed: () => Debug.Log("[Home] Chest ad failed/skipped — sem reward.")
            );
            if (!shown) RefreshChestButton();
        }

        public void OpenShop()
        {
            if (shopController != null) shopController.Open();
            else Debug.LogWarning("[Home] shopController não atribuído.");
        }

        public void OpenLeaderboard()
        {
            if (leaderboardController != null) leaderboardController.Open();
            else Debug.LogWarning("[Home] leaderboardController não atribuído.");
        }

        public void OpenProfile()
        {
            if (profileController != null) profileController.Open();
            else Debug.LogWarning("[Home] profileController não atribuído.");
        }

        public void LoadGame()
        {
            SceneManager.LoadScene(SceneNames.Game);
        }
    }
}
