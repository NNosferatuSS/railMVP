using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;
using RailSwitchMVP.UI.Widgets;

namespace RailSwitchMVP.UI
{
    /// <summary>
    /// HUD visual novo (spec handoff opção 2a). Substitui o HUDController legado
    /// quando presente na cena. Mesma fonte de dados (GameTimer, PlayerRailRider,
    /// CoinManager, DifficultyManager, PowerUpManager) — só a apresentação é
    /// diferente: hero de distância + chip de moedas + tier pill + pucks visuais
    /// com anel radial + hazard banner + FAB de item ativo + zonas de input
    /// invisíveis + switch indicator.
    ///
    /// O wizard Tools -> RailSwitch -> Build UI -> 4. Build HUD constrói a
    /// hierarquia e liga as refs.
    /// </summary>
    public class HUDViewController : MonoBehaviour
    {
        [Header("Top-left stack")]
        [SerializeField] private TMP_Text distanceHero;   // "0" Chakra 34 white
        [SerializeField] private TMP_Text distanceSuffix; // "M" Barlow 15 @60%
        [SerializeField] private Image coinIcon;
        [SerializeField] private TMP_Text coinsValue;     // "0" Chakra 19 gold
        [SerializeField] private TMP_Text bestBadge;      // "BEST 5 263" Barlow 11 @45%

        [Header("Top-right")]
        [SerializeField] private TMP_Text tierPillText;   // número em Chakra 15 white
        [SerializeField] private Button pauseButton;

        [Header("Hazard banner")]
        [SerializeField] private HazardBannerUI hazardBanner;

        [Header("Power-up pucks")]
        [Tooltip("Três pucks pré-instanciados (spec: até 3 visíveis simultâneos).")]
        [SerializeField] private PuckUI[] puckSlots = new PuckUI[3];

        [Header("Icons por PowerUpType (setar via inspector, spec sprites)")]
        [SerializeField] private PuckIconMapping[] puckIconMap;

        [Header("Switch indicator")]
        [Tooltip("Container vazio. As barras são geradas em runtime — a quantidade " +
                 "de trilhos varia por tier (TrackTile.MaxLanesAtSpawn), então 3 " +
                 "barras fixas mentiriam a partir do momento que o tier abre mais.")]
        [SerializeField] private RectTransform switchBars;
        [SerializeField] private TMP_Text switchLabel;
        [SerializeField] private Color switchActiveColor = new Color(0.13f, 0.89f, 1f, 1f);
        [SerializeField] private Color switchIdleColor = new Color(1f, 1f, 1f, 0.22f);

        [Tooltip("Largura FIXA de cada barra. O grupo cresce pra direita conforme " +
                 "o tier abre mais trilhos — a barra em si nunca muda de tamanho.")]
        [SerializeField] private float barWidth = 12f;
        [SerializeField] private float barGap = 4f;
        [SerializeField] private float barHeightIdle = 18f;
        [SerializeField] private float barHeightActive = 30f;

        [Header("Visibilidade")]
        [Tooltip("Fade pra 0 no Game Over — o overlay tem scrim translúcido e o " +
                 "HUD aparecia por cima dele.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Refs (auto-resolved)")]
        [SerializeField] private GameTimer timer;
        [SerializeField] private PlayerRailRider player;
        [SerializeField] private DifficultyManager difficulty;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private PowerUpManager powerUpManager;

        // Estado interno
        readonly Dictionary<PowerUpType, int> _powerUpToSlot = new Dictionary<PowerUpType, int>();
        readonly bool[] _slotBusy = new bool[3];
        readonly Dictionary<PowerUpType, float> _initialDuration = new Dictionary<PowerUpType, float>();
        bool _distanceBaselineSet;
        float _distanceBaseline;

        readonly List<Image> _switchBarPool = new List<Image>();
        int _switchLaneCount = -1;
        int _switchLane = -1;
        PowerUpType? _activeHazard;

        Coroutine _coinPunch, _tierPunch;
        // Start() semeia os valores iniciais chamando os handlers — isso não é
        // "o valor mudou", então a primeira passada não anima.
        bool _coinsSeeded, _tierSeeded;

        void Start()
        {
            if (timer == null) timer = GameTimer.Instance;
            if (difficulty == null) difficulty = DifficultyManager.Instance;
            if (coinManager == null) coinManager = CoinManager.Instance;
            if (powerUpManager == null) powerUpManager = PowerUpManager.Instance;
            if (player == null) player = FindFirstObjectByType<PlayerRailRider>();

            if (coinManager != null)
            {
                coinManager.OnCoinsChanged += HandleCoinsChanged;
                HandleCoinsChanged(coinManager.Total);
            }
            if (difficulty != null)
            {
                difficulty.OnTierChanged += HandleTierChanged;
                HandleTierChanged(difficulty.CurrentTier);
            }
            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpActivated += HandlePowerUpActivated;
                powerUpManager.OnPowerUpTick += HandlePowerUpTick;
                powerUpManager.OnPowerUpExpired += HandlePowerUpExpired;
            }

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver += HandleGameOver;
                GameManager.Instance.OnRevived += HandleRevived;
            }

            // Best distance
            if (bestBadge != null && Meta.PlayerDataManager.Instance != null)
                bestBadge.text = $"BEST {Meta.PlayerDataManager.Instance.BestDistance} M";

            // Reset pucks
            for (int i = 0; i < puckSlots.Length; i++)
            {
                if (puckSlots[i] != null) puckSlots[i].gameObject.SetActive(false);
                _slotBusy[i] = false;
            }
        }

        void OnDestroy()
        {
            if (coinManager != null) coinManager.OnCoinsChanged -= HandleCoinsChanged;
            if (difficulty != null) difficulty.OnTierChanged -= HandleTierChanged;
            if (powerUpManager != null)
            {
                powerUpManager.OnPowerUpActivated -= HandlePowerUpActivated;
                powerUpManager.OnPowerUpTick -= HandlePowerUpTick;
                powerUpManager.OnPowerUpExpired -= HandlePowerUpExpired;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameOver -= HandleGameOver;
                GameManager.Instance.OnRevived -= HandleRevived;
            }
        }

        // O overlay de Game Over tem scrim translúcido (alpha .72), então o HUD
        // aparecia por cima dele. Some junto com a morte.
        void HandleGameOver(GameOverReason _) => SetHudVisible(false);

        // Revive (CONTINUE RUN) — a run segue, HUD volta.
        void HandleRevived() => SetHudVisible(true);

        void SetHudVisible(bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        void LateUpdate()
        {
            // Distância
            if (player != null && distanceHero != null)
            {
                bool inWarmup = GameManager.Instance != null && GameManager.Instance.IsWarmup;
                if (inWarmup)
                {
                    distanceHero.text = "0";
                }
                else
                {
                    float raw = player.DistanceTraveled;
                    if (!_distanceBaselineSet) { _distanceBaseline = raw; _distanceBaselineSet = true; }
                    int meters = Mathf.Max(0, Mathf.FloorToInt(raw - _distanceBaseline));
                    distanceHero.text = meters.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            UpdateSwitchIndicator();
        }

        // ---------- switch indicator ----------

        void UpdateSwitchIndicator()
        {
            if (switchBars == null || player == null) return;
            var tile = player.CurrentTile;
            if (tile == null) return;

            // tile.MaxLanesAtSpawn é o GLOBAL max (9) — o espaço fixo de X no mundo,
            // não o que está ativo. Cada tier ativa um subset CENTRADO dele, com a
            // mesma conta do ProceduralRailGenerator.PlanRow:
            //     canonLower = (globalMax - tierMax) / 2
            // Ex: globalMax=9, tierMax=3 → lanes 3,4,5. Usar MaxLanesAtSpawn direto
            // mostrava "9" desde o tier 0.
            int globalMax = Mathf.Max(1, tile.MaxLanesAtSpawn);
            int tierMax = globalMax;   // DifficultyTier é struct — nunca null
            if (difficulty != null)
            {
                int m = difficulty.CurrentTier.maxLanes;
                if (m > 0) tierMax = Mathf.Min(m, globalMax);
            }

            // Durante a transição de reset de dificuldade o range ativo pode
            // expandir além do canônico. Se o player estiver fora da janela,
            // alarga a exibição pra incluí-lo em vez de clampar (mentiria).
            int lower = Mathf.Min((globalMax - tierMax) / 2, tile.Lane);
            int upper = Mathf.Max(lower + tierMax - 1, tile.Lane);

            int count = Mathf.Max(1, upper - lower + 1);
            int lane = Mathf.Clamp(tile.Lane - lower, 0, count - 1);
            if (count == _switchLaneCount && lane == _switchLane) return;

            if (count != _switchLaneCount) RebuildSwitchBars(count);
            _switchLane = lane;

            for (int i = 0; i < _switchBarPool.Count; i++)
            {
                bool active = i == lane;
                _switchBarPool[i].color = active ? switchActiveColor : switchIdleColor;
                var rt = (RectTransform)_switchBarPool[i].transform;
                rt.sizeDelta = new Vector2(barWidth, active ? barHeightActive : barHeightIdle);
            }

            if (switchLabel != null) switchLabel.text = $"LANE {lane + 1} / {count}";
        }

        void RebuildSwitchBars(int count)
        {
            for (int i = 0; i < _switchBarPool.Count; i++)
                if (_switchBarPool[i] != null) Destroy(_switchBarPool[i].gameObject);
            _switchBarPool.Clear();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Bar_" + i, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(switchBars, false);
                var rt = (RectTransform)go.transform;
                // Largura FIXA, ancorada na base-esquerda do container. Com âncora
                // fracionária (i/count) as barras encolhiam conforme o tier abria
                // mais trilhos; aqui o grupo é que cresce pra direita.
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(i * (barWidth + barGap), 0f);
                rt.sizeDelta = new Vector2(barWidth, barHeightIdle);

                var img = go.GetComponent<Image>();
                img.color = switchIdleColor;
                // CRÍTICO: TouchDirectionalInput.IsOverUI() descarta o toque se
                // qualquer raycast acertar. Nada do HUD pode ser raycastTarget.
                img.raycastTarget = false;
                _switchBarPool.Add(img);
            }
            _switchLaneCount = count;
            _switchLane = -1;
        }

        // ---------- hazard banner ----------

        static bool IsHazard(PowerUpType t)
            => t == PowerUpType.SpeedUpDebuff || t == PowerUpType.LaneSwapDebuff;

        void HandlePowerUpActivated(PowerUpType type)
        {
            if (!IsHazard(type) || hazardBanner == null) return;
            _activeHazard = type;
            if (type == PowerUpType.LaneSwapDebuff)
                hazardBanner.Show(HazardBannerUI.Kind.Pink, "LANE SWAP", "controles invertidos");
            else
                hazardBanner.Show(HazardBannerUI.Kind.Orange, "SPEED UP", "velocidade aumentada");
        }

        // ---------- handlers ----------

        void HandleCoinsChanged(int total)
        {
            if (coinsValue == null) return;
            coinsValue.text = total.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            // O primeiro valor vem do seed no Start — não é uma coleta, não anima.
            if (!_coinsSeeded) { _coinsSeeded = true; return; }
            Punch(ref _coinPunch, coinsValue.transform, 0.20f, 0.20f);
        }

        void HandleTierChanged(DifficultyTier _)
        {
            if (tierPillText == null || difficulty == null) return;
            tierPillText.text = difficulty.CurrentTierIndex.ToString();

            if (!_tierSeeded) { _tierSeeded = true; return; }
            // Pulsa a pill inteira (pai do número), não só o dígito.
            var pill = tierPillText.transform.parent != null
                ? tierPillText.transform.parent : tierPillText.transform;
            Punch(ref _tierPunch, pill, 0.14f, 0.30f);
        }

        /// <summary>Reinicia o punch com segurança — coleta rápida cancelaria o anterior no meio.</summary>
        void Punch(ref Coroutine handle, Transform target, float amount, float duration)
        {
            if (target == null || !isActiveAndEnabled) return;
            if (handle != null) StopCoroutine(handle);
            target.localScale = Vector3.one;
            handle = StartCoroutine(UITween.PunchScale(target, amount, duration));
        }

        void HandlePowerUpTick(PowerUpType type, int value)
        {
            // Contador do banner (tiles restantes do hazard ativo).
            if (hazardBanner != null && _activeHazard == type)
                hazardBanner.UpdateCounterTiles(value);

            // Aloca um slot pro tipo se não tem
            if (!_powerUpToSlot.TryGetValue(type, out int slot))
            {
                slot = -1;
                for (int i = 0; i < _slotBusy.Length; i++)
                {
                    if (!_slotBusy[i]) { slot = i; break; }
                }
                if (slot < 0) return; // sem slot livre
                _slotBusy[slot] = true;
                _powerUpToSlot[type] = slot;

                var puck = puckSlots[slot];
                if (puck != null)
                {
                    puck.gameObject.SetActive(true);
                    puck.SetIcon(GetIconFor(type));
                    _initialDuration[type] = Mathf.Max(1, value);
                    // Feedback de coleta: o puck entra com pop.
                    if (isActiveAndEnabled)
                        StartCoroutine(UITween.PopIn(puck.transform));
                }
            }

            var pukk = puckSlots[slot];
            if (pukk != null)
            {
                float dur = _initialDuration.TryGetValue(type, out var d) ? d : Mathf.Max(1, value);
                pukk.SetProgress(value, dur);
            }
        }

        void HandlePowerUpExpired(PowerUpType type)
        {
            if (_activeHazard == type)
            {
                _activeHazard = null;
                if (hazardBanner != null) hazardBanner.Hide();
            }

            if (!_powerUpToSlot.TryGetValue(type, out int slot)) return;
            if (puckSlots[slot] != null) puckSlots[slot].gameObject.SetActive(false);
            _slotBusy[slot] = false;
            _powerUpToSlot.Remove(type);
            _initialDuration.Remove(type);
        }

        Sprite GetIconFor(PowerUpType type)
        {
            if (puckIconMap == null) return null;
            for (int i = 0; i < puckIconMap.Length; i++)
                if (puckIconMap[i].type == type) return puckIconMap[i].icon;
            return null;
        }

        public void ResetDistanceBaseline() => _distanceBaselineSet = false;

        [System.Serializable]
        public struct PuckIconMapping
        {
            public PowerUpType type;
            public Sprite icon;
        }
    }
}
