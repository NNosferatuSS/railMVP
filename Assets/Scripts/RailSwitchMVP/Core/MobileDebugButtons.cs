using UnityEngine;
using UnityEngine.UI;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// 2 botões on-screen pra toggle F1 (DebugPanel) e F2 (SpawnOverride)
    /// em Android, onde não há teclas F1/F2. Em desktop, atalho keyboard
    /// continua funcionando — esses botões são alternativos.
    ///
    /// Auto-hide quando restrictToDebugBuilds = true E não é Editor/Debug build.
    /// Pra usar em release: desmarcar restrictToDebugBuilds.
    /// </summary>
    public class MobileDebugButtons : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button debugPanelToggleButton;
        [SerializeField] private Button spawnOverrideToggleButton;

        [Header("Targets (auto-resolved no Awake se vazios)")]
        [SerializeField] private DebugPanelController debugPanel;
        [SerializeField] private SpawnOverrideController spawnOverride;

        [Tooltip("Quando true, esconde os botões em release builds. " +
            "Editor e Development Builds (Build Settings → Development Build) sempre veem.")]
        public bool restrictToDebugBuilds = true;

        bool ShouldShow => !restrictToDebugBuilds || Application.isEditor || Debug.isDebugBuild;

        void Awake()
        {
            if (debugPanel == null) debugPanel = FindFirstObjectByType<DebugPanelController>();
            if (spawnOverride == null) spawnOverride = FindFirstObjectByType<SpawnOverrideController>();
        }

        void Start()
        {
            if (!ShouldShow)
            {
                if (debugPanelToggleButton != null) debugPanelToggleButton.gameObject.SetActive(false);
                if (spawnOverrideToggleButton != null) spawnOverrideToggleButton.gameObject.SetActive(false);
                return;
            }

            if (debugPanelToggleButton != null && debugPanel != null)
                debugPanelToggleButton.onClick.AddListener(debugPanel.Toggle);

            if (spawnOverrideToggleButton != null && spawnOverride != null)
                spawnOverrideToggleButton.onClick.AddListener(spawnOverride.Toggle);
        }
    }
}
