using UnityEngine;
using RailSwitchMVP.Collectibles;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    public enum PowerUpType
    {
        Shield,
        SlowDown,
        Magnet,
        DifficultyReset,
        // PostMVP2.2:
        DoubleCoins,
        Ghost,
        LanePreview,
        CoinRadar,
        // PostMVP2.3:
        Teleport,
        // PostMVP2.4 — Idea 3:
        AutoCriticalFollow,
        // PostMVP2.5 — debuffs (obstáculos não-letais):
        SpeedUpDebuff,
        LaneSwapDebuff,
    }

    /// <summary>
    /// Singleton que gerencia power-ups ATIVOS (estado), separado dos pickups
    /// (GameObjects no mundo). Quando um pickup é coletado, ele chama
    /// PowerUpManager.Grant... que atualiza o estado e dispara eventos.
    ///
    /// Duração é em TILES (decrementada via OnTileEntered do PlayerRailRider),
    /// não em segundos — escala consistente em todos os tiers.
    ///
    /// Stack:
    /// - Shield: cargas adicionais (cada hit consome 1).
    /// - SlowDown: estende duração (não multiplica o efeito).
    /// - Magnet: estende duração.
    /// - DifficultyReset: instantâneo, sem state.
    /// </summary>
    public class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [Header("Tunables")]
        [Tooltip("Multiplicador de velocidade quando SlowDown está ativo (0.7 = -30%).")]
        [Range(0.3f, 1f)]
        [SerializeField] private float slowDownSpeedMultiplier = 0.7f;

        [Tooltip("Raio do magnet em unidades mundiais. ~laneSpacing+1 cobre lanes adjacentes.")]
        [SerializeField] private float magnetRadius = 4f;

        [Tooltip("Duração default em tiles do SlowDown pickup (pode ser override por pickup).")]
        [SerializeField] private int slowDownDefaultTiles = 8;

        [Tooltip("Duração default em tiles do Magnet pickup.")]
        [SerializeField] private int magnetDefaultTiles = 6;

        [Header("Passive power-ups defaults (PostMVP2.2)")]
        [SerializeField] private int doubleCoinsDefaultTiles = 10;
        [SerializeField] private int ghostDefaultTiles = 5;
        [SerializeField] private int lanePreviewDefaultTiles = 12;
        [SerializeField] private int coinRadarDefaultTiles = 12;

        [Header("Active-action passive (PostMVP2.3)")]
        [Tooltip("Tiles que o player pode usar Shift+←/→ pra teleportar. " +
            "Não conta cada teleport — só transições de tile. Stack estende.")]
        [SerializeField] private int teleportDefaultTiles = 8;

        [Header("Auto-pilot passive (Idea 3)")]
        [Tooltip("Tiles em que o jogo segue o critical path sozinho. " +
            "Manual input ainda funciona mas é sobrescrito por tile.")]
        [SerializeField] private int autoCriticalFollowDefaultTiles = 5;

        [Header("Debuffs (PostMVP2.5)")]
        [Tooltip("Tiles padrão do SpeedUp debuff. Stack ADICIONA duração.")]
        [SerializeField] private int speedUpDebuffDefaultTiles = 6;

        [Tooltip("Multiplier de speed quando SpeedUp debuff ativo. 1.5 = +50%.")]
        [Range(1f, 3f)]
        [SerializeField] private float speedUpMultiplier = 1.5f;

        [Tooltip("Tiles padrão do LaneSwap debuff (inverte ←/→ no input). " +
            "Stack RESETA duração (volta pro default).")]
        [SerializeField] private int laneSwapDebuffDefaultTiles = 2;

        [Header("Runtime state (read-only)")]
        [SerializeField] private int shieldCharges;
        [SerializeField] private int slowDownTilesRemaining;
        [SerializeField] private int magnetTilesRemaining;
        [SerializeField] private int doubleCoinsTilesRemaining;
        [SerializeField] private int ghostTilesRemaining;
        [SerializeField] private int lanePreviewTilesRemaining;
        [SerializeField] private int coinRadarTilesRemaining;
        [SerializeField] private int teleportTilesRemaining;
        [SerializeField] private int autoCriticalFollowTilesRemaining;
        [SerializeField] private int speedUpDebuffTilesRemaining;
        [SerializeField] private int laneSwapDebuffTilesRemaining;

        public int ShieldCharges => shieldCharges;
        public int SlowDownTilesRemaining => slowDownTilesRemaining;
        public int MagnetTilesRemaining => magnetTilesRemaining;
        public int DoubleCoinsTilesRemaining => doubleCoinsTilesRemaining;
        public int GhostTilesRemaining => ghostTilesRemaining;
        public int LanePreviewTilesRemaining => lanePreviewTilesRemaining;
        public int CoinRadarTilesRemaining => coinRadarTilesRemaining;
        public int TeleportTilesRemaining => teleportTilesRemaining;
        public int AutoCriticalFollowTilesRemaining => autoCriticalFollowTilesRemaining;
        public int SpeedUpDebuffTilesRemaining => speedUpDebuffTilesRemaining;
        public int LaneSwapDebuffTilesRemaining => laneSwapDebuffTilesRemaining;

        public bool HasShield => shieldCharges > 0;
        public bool HasSlowDown => slowDownTilesRemaining > 0;
        public bool HasMagnet => magnetTilesRemaining > 0;
        public bool HasDoubleCoins => doubleCoinsTilesRemaining > 0;
        public bool IsGhost => ghostTilesRemaining > 0;
        public bool HasLanePreview => lanePreviewTilesRemaining > 0;
        public bool HasCoinRadar => coinRadarTilesRemaining > 0;
        public bool HasTeleport => teleportTilesRemaining > 0;
        public bool HasAutoCriticalFollow => autoCriticalFollowTilesRemaining > 0;
        public bool HasSpeedUpDebuff => speedUpDebuffTilesRemaining > 0;
        public bool HasLaneSwapDebuff => laneSwapDebuffTilesRemaining > 0;
        public float SpeedUpMultiplier => HasSpeedUpDebuff ? speedUpMultiplier : 1f;

        public float SpeedMultiplier => HasSlowDown ? slowDownSpeedMultiplier : 1f;
        public int CoinMultiplier => HasDoubleCoins ? 2 : 1;
        public int SlowDownDefaultTiles => slowDownDefaultTiles;
        public int MagnetDefaultTiles => magnetDefaultTiles;
        public int DoubleCoinsDefaultTiles => doubleCoinsDefaultTiles;
        public int GhostDefaultTiles => ghostDefaultTiles;
        public int LanePreviewDefaultTiles => lanePreviewDefaultTiles;
        public int CoinRadarDefaultTiles => coinRadarDefaultTiles;
        public int TeleportDefaultTiles => teleportDefaultTiles;
        public int AutoCriticalFollowDefaultTiles => autoCriticalFollowDefaultTiles;
        public int SpeedUpDebuffDefaultTiles => speedUpDebuffDefaultTiles;
        public int LaneSwapDebuffDefaultTiles => laneSwapDebuffDefaultTiles;

        public event System.Action<PowerUpType> OnPowerUpActivated;
        public event System.Action<PowerUpType> OnPowerUpExpired;
        public event System.Action<PowerUpType, int> OnPowerUpTick;

        private PlayerRailRider _player;
        private static readonly Collider[] _magnetScanBuffer = new Collider[32];

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            _player = FindFirstObjectByType<PlayerRailRider>();
            if (_player != null)
                _player.OnTileEntered += HandleTileEntered;
        }

        void OnDestroy()
        {
            if (_player != null)
                _player.OnTileEntered -= HandleTileEntered;
            if (Instance == this) Instance = null;
        }

        // ===== Grants =====

        public void GrantShield()
        {
            shieldCharges++;
            Debug.Log($"[PowerUpManager] +Shield (charges={shieldCharges})");
            OnPowerUpActivated?.Invoke(PowerUpType.Shield);
            OnPowerUpTick?.Invoke(PowerUpType.Shield, shieldCharges);
        }

        public void GrantSlowDown(int tiles)
        {
            slowDownTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +SlowDown (tiles={slowDownTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.SlowDown);
            OnPowerUpTick?.Invoke(PowerUpType.SlowDown, slowDownTilesRemaining);
        }

        public void GrantMagnet(int tiles)
        {
            magnetTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +Magnet (tiles={magnetTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.Magnet);
            OnPowerUpTick?.Invoke(PowerUpType.Magnet, magnetTilesRemaining);
        }

        public void GrantDifficultyReset()
        {
            Debug.Log("[PowerUpManager] DifficultyReset!");
            OnPowerUpActivated?.Invoke(PowerUpType.DifficultyReset);
            if (DifficultyManager.Instance != null)
                DifficultyManager.Instance.ResetDifficulty();
        }

        // PostMVP2.2 — passive power-ups
        public void GrantDoubleCoins(int tiles)
        {
            doubleCoinsTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +DoubleCoins (tiles={doubleCoinsTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.DoubleCoins);
            OnPowerUpTick?.Invoke(PowerUpType.DoubleCoins, doubleCoinsTilesRemaining);
        }

        public void GrantGhost(int tiles)
        {
            ghostTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +Ghost (tiles={ghostTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.Ghost);
            OnPowerUpTick?.Invoke(PowerUpType.Ghost, ghostTilesRemaining);
        }

        public void GrantLanePreview(int tiles)
        {
            lanePreviewTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +LanePreview (tiles={lanePreviewTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.LanePreview);
            OnPowerUpTick?.Invoke(PowerUpType.LanePreview, lanePreviewTilesRemaining);
        }

        public void GrantCoinRadar(int tiles)
        {
            coinRadarTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +CoinRadar (tiles={coinRadarTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.CoinRadar);
            OnPowerUpTick?.Invoke(PowerUpType.CoinRadar, coinRadarTilesRemaining);
        }

        public void GrantTeleport(int tiles)
        {
            teleportTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +Teleport (tiles={teleportTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.Teleport);
            OnPowerUpTick?.Invoke(PowerUpType.Teleport, teleportTilesRemaining);
        }

        public void GrantAutoCriticalFollow(int tiles)
        {
            autoCriticalFollowTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +AutoCriticalFollow (tiles={autoCriticalFollowTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.AutoCriticalFollow);
            OnPowerUpTick?.Invoke(PowerUpType.AutoCriticalFollow, autoCriticalFollowTilesRemaining);
        }

        /// <summary>SpeedUp debuff: stack ADICIONA duração.</summary>
        public void GrantSpeedUpDebuff(int tiles)
        {
            speedUpDebuffTilesRemaining += tiles;
            Debug.Log($"[PowerUpManager] +SpeedUpDebuff (tiles={speedUpDebuffTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.SpeedUpDebuff);
            OnPowerUpTick?.Invoke(PowerUpType.SpeedUpDebuff, speedUpDebuffTilesRemaining);
        }

        /// <summary>LaneSwap debuff: stack RESETA duração (volta pro N passado).</summary>
        public void GrantLaneSwapDebuff(int tiles)
        {
            laneSwapDebuffTilesRemaining = tiles;
            Debug.Log($"[PowerUpManager] +LaneSwapDebuff (tiles={laneSwapDebuffTilesRemaining})");
            OnPowerUpActivated?.Invoke(PowerUpType.LaneSwapDebuff);
            OnPowerUpTick?.Invoke(PowerUpType.LaneSwapDebuff, laneSwapDebuffTilesRemaining);
        }

        /// <summary>
        /// Consome 1 shield. Retorna true se havia shield (ataque absorvido).
        /// </summary>
        public bool ConsumeShield()
        {
            if (shieldCharges <= 0) return false;
            shieldCharges--;
            Debug.Log($"[PowerUpManager] Shield consumed (remaining={shieldCharges})");
            if (shieldCharges == 0)
                OnPowerUpExpired?.Invoke(PowerUpType.Shield);
            else
                OnPowerUpTick?.Invoke(PowerUpType.Shield, shieldCharges);
            return true;
        }

        // ===== Tile transition (decrementa durações) =====

        void HandleTileEntered(TrackTile newTile)
        {
            Tick(ref slowDownTilesRemaining, PowerUpType.SlowDown);
            Tick(ref magnetTilesRemaining, PowerUpType.Magnet);
            Tick(ref doubleCoinsTilesRemaining, PowerUpType.DoubleCoins);
            Tick(ref ghostTilesRemaining, PowerUpType.Ghost);
            Tick(ref lanePreviewTilesRemaining, PowerUpType.LanePreview);
            Tick(ref coinRadarTilesRemaining, PowerUpType.CoinRadar);
            Tick(ref teleportTilesRemaining, PowerUpType.Teleport);
            Tick(ref autoCriticalFollowTilesRemaining, PowerUpType.AutoCriticalFollow);
            Tick(ref speedUpDebuffTilesRemaining, PowerUpType.SpeedUpDebuff);
            Tick(ref laneSwapDebuffTilesRemaining, PowerUpType.LaneSwapDebuff);
        }

        void Tick(ref int counter, PowerUpType type)
        {
            if (counter <= 0) return;
            counter--;
            if (counter == 0)
                OnPowerUpExpired?.Invoke(type);
            else
                OnPowerUpTick?.Invoke(type, counter);
        }

        // ===== Magnet — auto-coleta de coins próximas todo frame ativo =====

        void Update()
        {
            if (!HasMagnet || _player == null) return;

            Vector3 playerPos = _player.transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(playerPos, magnetRadius, _magnetScanBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                var coin = _magnetScanBuffer[i].GetComponent<CollectibleCoin>();
                if (coin != null) coin.Collect();
            }
        }

        /// <summary>
        /// Zera todo o state. Chamado em Restart (não usado atualmente — cena
        /// reload já reseta o singleton — mas útil pra debug).
        /// </summary>
        public void ResetAll()
        {
            shieldCharges = 0;
            slowDownTilesRemaining = 0;
            magnetTilesRemaining = 0;
            doubleCoinsTilesRemaining = 0;
            ghostTilesRemaining = 0;
            lanePreviewTilesRemaining = 0;
            coinRadarTilesRemaining = 0;
            teleportTilesRemaining = 0;
            autoCriticalFollowTilesRemaining = 0;
            speedUpDebuffTilesRemaining = 0;
            laneSwapDebuffTilesRemaining = 0;
        }
    }
}
