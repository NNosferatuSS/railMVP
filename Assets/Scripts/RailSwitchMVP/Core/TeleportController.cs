using UnityEngine;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Core
{
    /// <summary>
    /// Teleport active item: ao usar, player pula INSTANTANEAMENTE pra a
    /// lane adjacente na mesma row, direção pelo switch state atual.
    ///
    /// Switch Left → teleporta pra lane -1 da current.
    /// Switch Right → teleporta pra lane +1.
    /// Switch Middle → use falha (sem direção definida). Item não é consumido.
    /// Lane destino sem tile → use falha (sem destino válido).
    ///
    /// Diferença vs switch normal: switch + atravessar gap é DELAYED.
    /// Teleport é INSTANTÂNEO — Z preserve, X muda. Panic button.
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
        /// Tenta teleportar. Retorna false se a use falha (sem direção válida
        /// ou lane destino sem tile). Nesses casos, o item NÃO é consumido.
        /// </summary>
        public bool TryTrigger()
        {
            var player = GetPlayer();
            if (player == null || player.CurrentTile == null) return false;

            var sw = player.CurrentTile.Switch;
            if (sw == null) return false;

            int dir = (int)sw.State; // -1, 0, +1
            if (dir == 0)
            {
                Debug.Log("[Teleport] Switch em Middle — use cancelada (sem direção).");
                return false;
            }

            int targetLane = player.CurrentTile.Lane + dir;
            var row = RailManager.Instance != null
                ? RailManager.Instance.GetRow(player.CurrentTile.Row)
                : null;

            if (row == null || !row.HasTile(targetLane))
            {
                Debug.Log($"[Teleport] Lane {targetLane} sem tile — use cancelada.");
                return false;
            }

            var targetTile = row.Tiles[targetLane];
            return player.TeleportToAdjacent(targetTile);
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
