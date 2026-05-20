using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Core;
using RailSwitchMVP.Player;
using RailSwitchMVP.Track;

namespace RailSwitchMVP.Obstacles
{
    /// <summary>
    /// Vortex: ao tocar, força o switch do tile atual pra uma direção
    /// DIFERENTE da escolhida pelo player. Não mata — apenas rouba a escolha.
    ///
    /// Modes:
    /// - OppositeOfSwitch (default): tenta apontar pro oposto. Se inviável
    ///   (lane sem tile), fallback pra random valid.
    /// - PureRandom: escolhe random entre opções válidas != current.
    ///
    /// Sempre push pra lane com TILE válido na próxima row — nunca mata.
    /// Se nenhuma alternativa válida, vortex é no-op (player segue como tava).
    /// </summary>
    public class VortexObstacle : ObstacleBase
    {
        public enum PushMode
        {
            OppositeOfSwitch,
            PureRandom,
        }

        [Tooltip("Como o Vortex escolhe a direção pra empurrar o switch.")]
        public PushMode pushMode = PushMode.OppositeOfSwitch;

        protected override void OnPlayerHit(Collider playerCollider)
        {
            var player = playerCollider.GetComponent<PlayerRailRider>()
                      ?? playerCollider.GetComponentInParent<PlayerRailRider>();
            if (player == null || player.CurrentTile == null) return;
            var sw = player.CurrentTile.Switch;
            if (sw == null) return;

            int? newOffset = ComputePushOffset(player.CurrentTile, sw.State);
            if (newOffset.HasValue)
            {
                sw.SetState((SwitchState)newOffset.Value);
                Debug.Log($"[Vortex] Player switch redirected to offset {newOffset.Value}");
            }

            Destroy(gameObject);
        }

        int? ComputePushOffset(TrackTile currentTile, SwitchState current)
        {
            var rm = RailManager.Instance;
            if (rm == null) return null;
            var nextRow = rm.GetRow(currentTile.Row + 1);
            if (nextRow == null) return null;

            int original = (int)current;
            var validOffsets = new List<int>();
            for (int off = -1; off <= 1; off++)
            {
                if (off == original) continue;
                int candLane = currentTile.Lane + off;
                if (candLane < 0 || candLane >= nextRow.MaxLanesAtSpawn) continue;
                if (!nextRow.HasTile(candLane)) continue;
                validOffsets.Add(off);
            }
            if (validOffsets.Count == 0) return null;

            if (pushMode == PushMode.OppositeOfSwitch)
            {
                int opposite = -original;
                if (validOffsets.Contains(opposite)) return opposite;
            }
            return validOffsets[Random.Range(0, validOffsets.Count)];
        }
    }
}
