using UnityEngine;

namespace RailSwitchMVP.Visual
{
    /// <summary>
    /// Assenta o visual na superfície da pista (Y mundo fixo), independente da
    /// altura de spawn do root (obstacleHeight). Só move este transform visual —
    /// collider e lógica continuam no root.
    /// </summary>
    public class AuroraGroundSnap : MonoBehaviour
    {
        [Tooltip("Y mundo do topo da pista (trilho Aurora: 0.1).")]
        public float surfaceY = 0.1f;

        [Tooltip("ON: reaplica todo frame (ex.: visual do player, cujo root muda de altura). " +
            "OFF: só no Start (hazards, que não se movem).")]
        public bool continuous = false;

        void Start() => Snap();

        void LateUpdate()
        {
            if (continuous) Snap();
        }

        public void Snap()
        {
            var p = transform.position;
            transform.position = new Vector3(p.x, surfaceY, p.z);
        }
    }
}
