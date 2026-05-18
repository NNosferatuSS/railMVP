using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.Track
{
    /// <summary>
    /// Representa um trilho individual em uma posição (Row, Lane).
    /// Na Iteração 1 contém apenas geometria e gizmos de debug.
    /// Em iterações futuras receberá SwitchController, CoinSpawner, etc.
    /// </summary>
    public class TrackTile : MonoBehaviour
    {
        [Header("Grid Position")]
        public int Row;
        public int Lane;

        [Tooltip("maxLanes ativo no momento que este tile foi spawnado")]
        public int MaxLanesAtSpawn;

        [Header("Anchor Points")]
        [Tooltip("Ponto onde o player entra no tile")]
        public Transform StartPoint;

        [Tooltip("Ponto onde o player sai do tile (e onde o switch fica)")]
        public Transform EndPoint;

        [Header("Procedural State")]
        [Tooltip("Este tile faz parte do critical path?")]
        public bool IsOnCriticalPath;

        [Header("Debug")]
        [SerializeField] private RailGenConfig debugConfig;

        // Cached references — populadas em iterações futuras
        // public SwitchController Switch;
        // public CoinSpawner Coins;

        void OnDrawGizmos()
        {
            if (debugConfig == null || !debugConfig.debugDrawCriticalPath) return;

            // Cor baseada em critical path vs decoy
            Gizmos.color = IsOnCriticalPath
                ? debugConfig.criticalPathGizmoColor
                : debugConfig.decoyGizmoColor;

            // Desenha um cubo wireframe acima do tile pra sinalização visível
            if (StartPoint != null && EndPoint != null)
            {
                Vector3 center = (StartPoint.position + EndPoint.position) * 0.5f;
                center.y += 1.5f; // eleva pra ficar visível em câmera inclinada
                Vector3 size = new Vector3(
                    0.8f,
                    0.2f,
                    Vector3.Distance(StartPoint.position, EndPoint.position) * 0.9f
                );
                Gizmos.DrawWireCube(center, size);

                // Marcador no StartPoint (entrada) — esfera
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(StartPoint.position + Vector3.up * 0.5f, 0.2f);

                // Marcador no EndPoint (saída/switch) — esfera
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(EndPoint.position + Vector3.up * 0.5f, 0.3f);
            }
        }

        /// <summary>
        /// Helper: calcula a posição mundial do center do tile dado os parâmetros do grid.
        /// Usado pelo gerador procedural.
        /// </summary>
        public static Vector3 ComputeWorldPosition(int row, int lane, int maxLanesAtSpawn, RailGenConfig config)
        {
            float x = (lane - (maxLanesAtSpawn - 1) / 2f) * config.laneSpacing;
            float z = row * (config.trackLength + config.rowGap) + config.trackLength * 0.5f;
            return new Vector3(x, 0f, z);
        }
    }
}
