using UnityEngine;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Teleport active item: ao usar, player pula INSTANTANEAMENTE pra a
    /// lane adjacente na mesma row.
    ///
    /// Input scheme (Option B): Shift + ← / → diretamente teleporta naquela
    /// direção. Não usa o switch state — direção vem do input. Switch normal
    /// continua funcionando (setas sem Shift).
    ///
    /// Lane destino sem tile → use falha (item não é consumido).
    /// Diferença vs switch normal: switch + gap é DELAYED. Teleport é
    /// INSTANTÂNEO — Z preserve, X muda. Panic button.
    /// </summary>
    public class TeleportController : MonoBehaviour
    {
        public static TeleportController Instance { get; private set; }

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
        }

        /// <summary>
        /// Tenta teleportar na direção indicada (-1 = left, +1 = right).
        /// Retorna false se a use falha (direção 0, lane destino sem tile,
        /// player em gap). Nesses casos, o item NÃO é consumido.
        /// </summary>
        public bool TryTrigger(int direction)
        {
            if (direction == 0) return false;
            var player = GetPlayer();
            if (player == null || player.CurrentTile == null) return false;

            int targetLane = player.CurrentTile.Lane + direction;
            var row = RailManager.Instance != null
                ? RailManager.Instance.GetRow(player.CurrentTile.Row)
                : null;

            if (row == null || !row.HasTile(targetLane))
            {
                Debug.Log($"[Teleport] Lane {targetLane} sem tile — use cancelada.");
                return false;
            }

            return player.TeleportToAdjacent(row.Tiles[targetLane]);
        }

        static PlayerRailRider _cachedPlayer;
        static PlayerRailRider GetPlayer()
        {
            if (_cachedPlayer == null)
                _cachedPlayer = Object.FindFirstObjectByType<PlayerRailRider>();
            return _cachedPlayer;
        }
    }
}
