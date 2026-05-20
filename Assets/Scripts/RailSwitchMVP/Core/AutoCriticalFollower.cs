using UnityEngine;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Sistema de auto-follow do critical path. Sobrescreve a switch state
    /// do tile atual cada vez que o player entra num tile novo, mirando a
    /// melhor lane na próxima row (preferindo critical path).
    ///
    /// Ativo quando UM dos dois é verdade:
    /// 1. DebugForceActive = true (toggle no DebugPanel).
    /// 2. PowerUpManager.HasAutoCriticalFollow = true (power-up coletado).
    ///
    /// Manual input (setas) ainda funciona, mas é sobrescrito na próxima
    /// transição de tile pelo auto-follow. Player pode "burlar" pra escolher
    /// decoy intencionalmente (pegar power-up etc.).
    /// </summary>
    public class AutoCriticalFollower : MonoBehaviour
    {
        public static AutoCriticalFollower Instance { get; private set; }

        [Tooltip("Toggle de debug. Pode ser ligado/desligado via DebugPanel ou Inspector.")]
        [SerializeField] private bool debugForceActive;

        public bool DebugForceActive
        {
            get => debugForceActive;
            set => debugForceActive = value;
        }

        public bool IsActive => debugForceActive
            || (PowerUpManager.Instance != null && PowerUpManager.Instance.HasAutoCriticalFollow);

        private PlayerRailRider _player;
        private bool _subscribed;
        private bool _initialApplied;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_player != null) _player.OnTileEntered -= HandleTileEntered;
        }

        void Update()
        {
            // Lazy subscribe ao player (caso este componente exista antes do player Start)
            if (!_subscribed)
            {
                _player = FindFirstObjectByType<PlayerRailRider>();
                if (_player != null)
                {
                    _player.OnTileEntered += HandleTileEntered;
                    _subscribed = true;
                }
            }

            // Aplica auto-follow no tile inicial (OnTileEntered não dispara pra ele).
            if (IsActive && !_initialApplied && _player != null && _player.CurrentTile != null)
            {
                AutoFollow(_player.CurrentTile);
                _initialApplied = true;
            }
            if (!IsActive) _initialApplied = false;
        }

        void HandleTileEntered(TrackTile newTile)
        {
            if (IsActive) AutoFollow(newTile);
        }

        /// <summary>
        /// Ajusta o switch do tile pra apontar à lane do critical path
        /// alcançável na próxima row. Fallback: qualquer tile populado.
        /// </summary>
        void AutoFollow(TrackTile currentTile)
        {
            if (currentTile == null || currentTile.Switch == null) return;
            var rm = RailManager.Instance;
            if (rm == null) return;
            var nextRow = rm.GetRow(currentTile.Row + 1);
            if (nextRow == null) return;

            int chosenOffset = FindBestOffset(nextRow, currentTile.Lane);
            currentTile.Switch.SetState((SwitchState)chosenOffset);
        }

        static int FindBestOffset(RowData nextRow, int currentLane)
        {
            // Preferência: Middle, depois Left, depois Right.
            int[] offsets = { 0, -1, 1 };

            // 1ª passada: critical path (sem hazard por design).
            foreach (var off in offsets)
            {
                int target = currentLane + off;
                if (nextRow.HasTile(target) && nextRow.IsCriticalLane(target))
                    return off;
            }

            // 2ª passada: qualquer tile populado.
            foreach (var off in offsets)
            {
                int target = currentLane + off;
                if (nextRow.HasTile(target)) return off;
            }

            return 0;
        }
    }
}
