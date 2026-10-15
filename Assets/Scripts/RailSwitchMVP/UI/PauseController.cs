using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using RailSwitchMVP.Core;
using RailSwitchMVP.Meta;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// Overlay de Pause (spec S5). Time.timeScale = 0 enquanto aberto.
    /// Sliders MUSIC/SFX e toggle HAPTICS persistem em PlayerPrefs.
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;   // botão no HUD (pause)
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [Header("Stats")]
        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text tierText;

        [Header("Audio settings")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle hapticsToggle;

        const string KMusic = "settings.music";
        const string KSfx   = "settings.sfx";
        const string KHap   = "settings.haptics";

        bool _wasPaused;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            if (musicSlider != null)
            {
                musicSlider.value = PlayerPrefs.GetFloat(KMusic, 0.7f);
                // Volume lido pelo AuroraAudio (música e SFX separados) — não mexe no AudioListener global.
                musicSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KMusic, v));
            }
            if (sfxSlider != null)
            {
                sfxSlider.value = PlayerPrefs.GetFloat(KSfx, 0.8f);
                sfxSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(KSfx, v));
            }
            if (hapticsToggle != null)
            {
                hapticsToggle.isOn = PlayerPrefs.GetInt(KHap, 1) == 1;
                hapticsToggle.onValueChanged.AddListener(v => PlayerPrefs.SetInt(KHap, v ? 1 : 0));
            }
        }

        public void Open()
        {
            if (panel == null) return;
            RefreshStats();
            _wasPaused = true;
            Time.timeScale = 0f;
            panel.SetActive(true);
        }

        public void Close()
        {
            if (panel == null) return;
            panel.SetActive(false);
            Time.timeScale = _wasPaused ? 1f : Time.timeScale;
            _wasPaused = false;
        }

        void RefreshStats()
        {
            var player = FindFirstObjectByType<PlayerRailRider>();
            if (distanceText != null && player != null)
                distanceText.text = Mathf.FloorToInt(player.DistanceTraveled).ToString("N0",
                    System.Globalization.CultureInfo.InvariantCulture);
            if (coinsText != null && CoinManager.Instance != null)
                coinsText.text = CoinManager.Instance.Total.ToString("N0",
                    System.Globalization.CultureInfo.InvariantCulture);
            if (tierText != null && DifficultyManager.Instance != null)
                tierText.text = DifficultyManager.Instance.CurrentTierIndex.ToString();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void Quit()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Home);
        }
    }
}
