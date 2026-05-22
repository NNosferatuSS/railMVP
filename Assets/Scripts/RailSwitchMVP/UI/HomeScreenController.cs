using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Tela Home. Mostra nome, coins, best distance, 3 missões diárias + 3
    /// semanais (com botão Reclamar), botão JOGAR. Stubs de Loja/Leaderboard/
    /// Perfil ficam inertes até as próximas fatias.
    ///
    /// Lê do PlayerDataManager + MissionTracker no OnEnable (não só Start) —
    /// spec §7.2 — pra refletir mudanças ao voltar do GameOver via "HOME".
    /// Também subscribe nos eventos pra atualizar live durante a Home.
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

        [Header("Stubs (Fatias futuras — desabilitados por padrão)")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button profileButton;

        void OnEnable()
        {
            Refresh();

            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged += HandleCoinsChanged;

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged += RefreshMissions;

            if (playButton != null) playButton.onClick.AddListener(LoadGame);

            if (shopButton != null) shopButton.interactable = false;
            if (leaderboardButton != null) leaderboardButton.interactable = false;
            if (profileButton != null) profileButton.interactable = false;
        }

        void OnDisable()
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) pdm.OnCoinsChanged -= HandleCoinsChanged;

            var mt = MissionTracker.Instance;
            if (mt != null) mt.OnMissionsChanged -= RefreshMissions;

            if (playButton != null) playButton.onClick.RemoveListener(LoadGame);
        }

        void HandleCoinsChanged(int newTotal)
        {
            if (coinsText != null) coinsText.text = $"Coins: {newTotal}";
        }

        void Refresh()
        {
            RefreshPlayerData();
            RefreshMissions();
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

        public void LoadGame()
        {
            SceneManager.LoadScene(SceneNames.Game);
        }
    }
}
