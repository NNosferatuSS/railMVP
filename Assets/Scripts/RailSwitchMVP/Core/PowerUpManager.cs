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

        [Header("Runtime state (read-only)")]
        [SerializeField] private int shieldCharges;
        [SerializeField] private int slowDownTilesRemaining;
        [SerializeField] private int magnetTilesRemaining;

        public int ShieldCharges => shieldCharges;
        public int SlowDownTilesRemaining => slowDownTilesRemaining;
        public int MagnetTilesRemaining => magnetTilesRemaining;

        public bool HasShield => shieldCharges > 0;
        public bool HasSlowDown => slowDownTilesRemaining > 0;
        public bool HasMagnet => magnetTilesRemaining > 0;

        public float SpeedMultiplier => HasSlowDown ? slowDownSpeedMultiplier : 1f;
        public int SlowDownDefaultTiles => slowDownDefaultTiles;
        public int MagnetDefaultTiles => magnetDefaultTiles;

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
            if (slowDownTilesRemaining > 0)
            {
                slowDownTilesRemaining--;
                if (slowDownTilesRemaining == 0)
                    OnPowerUpExpired?.Invoke(PowerUpType.SlowDown);
                else
                    OnPowerUpTick?.Invoke(PowerUpType.SlowDown, slowDownTilesRemaining);
            }

            if (magnetTilesRemaining > 0)
            {
                magnetTilesRemaining--;
                if (magnetTilesRemaining == 0)
                    OnPowerUpExpired?.Invoke(PowerUpType.Magnet);
                else
                    OnPowerUpTick?.Invoke(PowerUpType.Magnet, magnetTilesRemaining);
            }
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
        }
    }
}
