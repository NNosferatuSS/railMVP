using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Net;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Painel de Leaderboard global (Fatia 8). HomeScreenController abre via
    /// Open(). Padrão do ShopController: panel SetActive toggle, fica inativo
    /// no Inspector por default.
    ///
    /// Quando aberto: dispara LeaderboardManager.FetchToday + FetchMyRank
    /// (usa cache se válido). Renderiza entries via Instantiate do prefab
    /// LeaderboardEntryUI dentro do container do Scroll View.
    /// </summary>
    public class LeaderboardPanelController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private Button closeButton;

        [Header("Header")]
        [Tooltip("Texto com a data do daily, ex: 'Daily Challenge — 25/05/2026'.")]
        [SerializeField] private TMP_Text headerText;

        [Tooltip("Banner com tua posição, ex: 'Você: #12 — 850m'. Esconde se ainda não jogou hoje.")]
        [SerializeField] private TMP_Text myRankBannerText;

        [Tooltip("Indicador de loading enquanto fetch está em flight.")]
        [SerializeField] private GameObject loadingIndicator;

        [Header("List")]
        [Tooltip("Content do ScrollView (Vertical Layout Group). Rows são instanciadas aqui.")]
        [SerializeField] private Transform entriesContainer;

        [Tooltip("Prefab da row (deve ter componente LeaderboardEntryUI).")]
        [SerializeField] private LeaderboardEntryUI entryPrefab;

        [Tooltip("Texto opcional mostrado quando a lista vem vazia.")]
        [SerializeField] private GameObject emptyStatePanel;

        readonly List<LeaderboardEntryUI> _spawnedEntries = new List<LeaderboardEntryUI>();

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            // Nota: NÃO chamar leaderboardPanel.SetActive(false) aqui — o painel
            // hospeda o próprio componente, então Awake só dispara na primeira
            // SetActive(true). Chamar SetActive(false) aqui desativava o painel
            // imediatamente após Open(), tornando o fetch invisível.
            // O panel deve estar setado como INATIVO no Inspector (setup doc).
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (leaderboardPanel == null) return;
            leaderboardPanel.SetActive(true);
            UpdateHeader();
            // Sempre força refresh ao abrir — submits em outros devices não invalidam
            // nosso cache local, então confiar no cache do panel mostraria stale data.
            // Cache do LeaderboardManager continua útil pra outros consumers (ex: HUD).
            Refresh(forceRefresh: true);
        }

        public void Close()
        {
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        }

        void UpdateHeader()
        {
            if (headerText == null) return;
            string today = System.DateTime.UtcNow.ToString("dd/MM/yyyy");
            headerText.text = $"Daily Challenge — {today}";
        }

        public void Refresh(bool forceRefresh)
        {
            var lb = LeaderboardManager.Instance;
            if (lb == null)
            {
                ShowError("(LeaderboardManager ausente)");
                return;
            }

            SetLoading(true);
            ClearEntries();
            if (myRankBannerText != null) myRankBannerText.text = "";

            int pending = 2; // 2 fetches: top + my rank
            void Done()
            {
                pending--;
                if (pending <= 0) SetLoading(false);
            }

            lb.FetchToday(entries =>
            {
                RenderEntries(entries);
                Done();
            }, forceRefresh);

            lb.FetchMyRank((rank, distance) =>
            {
                UpdateMyRankBanner(rank, distance);
                Done();
            }, forceRefresh);
        }

        void RenderEntries(LeaderboardEntry[] entries)
        {
            ClearEntries();
            if (entries == null || entries.Length == 0)
            {
                if (emptyStatePanel != null) emptyStatePanel.SetActive(true);
                return;
            }
            if (emptyStatePanel != null) emptyStatePanel.SetActive(false);
            if (entriesContainer == null || entryPrefab == null) return;

            string myId = AuthManager.Instance != null ? AuthManager.Instance.UserId : "";
            foreach (var entry in entries)
            {
                var row = Instantiate(entryPrefab, entriesContainer);
                bool isMyRow = !string.IsNullOrEmpty(myId) && entry.player_id == myId;
                row.Bind(entry, isMyRow);
                _spawnedEntries.Add(row);
            }
        }

        void UpdateMyRankBanner(int rank, int distance)
        {
            if (myRankBannerText == null) return;
            if (rank < 0)
            {
                myRankBannerText.text = "Você ainda não jogou hoje.";
            }
            else
            {
                myRankBannerText.text = $"Você: #{rank} — {distance} m";
            }
        }

        void ClearEntries()
        {
            foreach (var e in _spawnedEntries)
            {
                if (e != null) Destroy(e.gameObject);
            }
            _spawnedEntries.Clear();
        }

        void SetLoading(bool isLoading)
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(isLoading);
        }

        void ShowError(string msg)
        {
            if (myRankBannerText != null) myRankBannerText.text = msg;
            SetLoading(false);
        }
    }
}
