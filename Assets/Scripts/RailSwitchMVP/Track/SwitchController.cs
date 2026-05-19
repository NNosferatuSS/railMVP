using UnityEngine;

namespace RailSwitchMVP.Track
{
    public enum SwitchState
    {
        Left = -1,
        Middle = 0,
        Right = 1
    }

    /// <summary>
    /// Conector de 3 posições no fim de cada tile. Define qual lane da
    /// próxima linha o player vai tentar atingir ao sair do tile.
    /// </summary>
    public class SwitchController : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private SwitchState state = SwitchState.Middle;

        [Header("References")]
        [Tooltip("Tile dono deste switch — atribuído pelo TrackTile no Awake.")]
        public TrackTile OwnerTile;

        [Tooltip("Visual da seta. Rotaciona em Y conforme State.")]
        public Transform ArrowVisual;

        public SwitchState State => state;

        /// <summary>Lane absoluta para a qual o switch aponta na próxima linha.</summary>
        public int TargetLane => (OwnerTile != null ? OwnerTile.Lane : 0) + (int)state;

        void Start()
        {
            UpdateArrowRotation();
        }

        public void Nudge(int dir)
        {
            int next = Mathf.Clamp((int)state + dir, -1, 1);
            if (next == (int)state) return;
            state = (SwitchState)next;
            UpdateArrowRotation();
        }

        public void SetState(SwitchState s)
        {
            state = s;
            UpdateArrowRotation();
        }

        void UpdateArrowRotation()
        {
            if (ArrowVisual == null) return;
            float angle = (int)state * 45f;
            ArrowVisual.localEulerAngles = new Vector3(0f, angle, 0f);
        }
    }
}
