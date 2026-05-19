using UnityEngine;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Parâmetros globais do jogo que NÃO variam com dificuldade.
    /// Parâmetros que escalam com progressão vivem em DifficultyConfig (tiers).
    /// </summary>
    [CreateAssetMenu(fileName = "RailGenConfig", menuName = "RailSwitchMVP/Rail Gen Config")]
    public class RailGenConfig : ScriptableObject
    {
        [Header("Track Geometry")]
        [Tooltip("Distância lateral (X) entre lanes adjacentes")]
        public float laneSpacing = 2.5f;

        [Tooltip("Comprimento (Z) de cada trilho")]
        public float trackLength = 10f;

        [Tooltip("Espaço entre o fim de uma linha e o início da próxima (onde fica o switch)")]
        public float rowGap = 2f;

        [Tooltip("Número GLOBAL de lanes. Define o range fixo de posições X no mundo. " +
            "Cada tier ATIVA um subset centrado deste range (Tier 0 com maxLanes=3 usa apenas " +
            "as 3 lanes centrais; Tier 5 com maxLanes=9 usa todas as 9). " +
            "Deve ser >= o maxLanes do tier mais alto.")]
        public int globalMaxLanes = 9;

        [Header("Streaming")]
        [Tooltip("Quantas linhas spawnar à frente do player")]
        public int rowsAhead = 12;

        [Tooltip("Quantas linhas manter atrás antes de despawn")]
        public int rowsBehind = 2;

        [Header("Camera")]
        [Tooltip("Inclinação da câmera em graus (0 = top-down puro, 35 = recomendado)")]
        [Range(0f, 60f)]
        public float cameraTilt = 35f;

        [Tooltip("Offset Z (quão atrás do player a câmera fica)")]
        public float cameraDistance = 6f;

        [Tooltip("Quanto a câmera olha para frente do player")]
        public float cameraLookAhead = 4f;

        [Tooltip("Velocidade de transição do zoom adaptativo")]
        public float cameraZoomSpeed = 8f;

        [Header("Debug")]
        [Tooltip("Desenhar Gizmos do critical path no editor")]
        public bool debugDrawCriticalPath = true;

        [Tooltip("Cor dos tiles que fazem parte do critical path (debug)")]
        public Color criticalPathGizmoColor = new Color(0f, 1f, 0.4f, 0.8f);

        [Tooltip("Cor dos tiles decoy (debug)")]
        public Color decoyGizmoColor = new Color(1f, 0.4f, 0.2f, 0.5f);
    }
}
