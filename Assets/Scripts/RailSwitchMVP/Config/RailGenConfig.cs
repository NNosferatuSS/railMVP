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

        [Header("Warmup (Idea 1)")]
        [Tooltip("Quantas rows iniciais são warmup (single lane, sem hazards/coins/power-ups). " +
            "Player atravessa elas em modo \"calmaria\" antes do jogo real começar.")]
        [Range(0, 20)]
        public int warmupRowCount = 5;

        [Tooltip("Multiplicador de velocidade durante o warmup. 0.5 = metade do tier 0 — calmo.")]
        [Range(0.1f, 1f)]
        public float warmupSpeedMultiplier = 0.5f;

        [Header("Streaming")]
        [Tooltip("Quantas linhas spawnar à frente do player")]
        public int rowsAhead = 12;

        [Tooltip("Quantas linhas manter atrás antes de despawn")]
        public int rowsBehind = 2;

        [Header("Camera")]
        [Tooltip("Inclinação da câmera em graus (0 = top-down puro, 35 = recomendado, 90 = perfil lateral)")]
        [Range(0f, 90f)]
        public float cameraTilt = 35f;

        [Tooltip("Offset Z (quão atrás do player a câmera fica)")]
        public float cameraDistance = 6f;

        [Tooltip("Quanto a câmera olha para frente do player, EM UNIDADES no speed mínimo. " +
            "Escala proporcionalmente com a playerSpeed do tier ativo " +
            "(ratio = tier.playerSpeed / speedAtMinZoom do PlayerCameraRig), " +
            "mantendo o horizonte temporal constante (~X segundos à frente) em todos os tiers.")]
        public float cameraLookAhead = 4f;

        [Tooltip("Campo de visão da câmera em graus (perspective). 60 = default Unity. " +
            "Valores baixos (~40) = lente tele, perspectiva achatada, sensação de zoom. " +
            "Valores altos (~80+) = grande angular, mais coisa na tela, distorção nas bordas.")]
        [Range(20f, 100f)]
        public float cameraFieldOfView = 60f;

        [Tooltip("Velocidade de transição do zoom adaptativo")]
        public float cameraZoomSpeed = 8f;

        [Tooltip("Multiplier global de zoom — escala cameraDistance (Z) e altura Y juntos, " +
            "preservando o ângulo de visão. 1 = sem alteração. 0.7 = ~30% mais perto. " +
            "1.5 = ~50% mais longe. Aplica em cima dos valores per-tier do DifficultyConfig.")]
        [Range(0.3f, 3f)]
        public float cameraZoomGlobalMultiplier = 1f;

        [Header("Camera — Smoothing")]
        [Tooltip("Suavização da posição da câmera (Lerp factor). " +
            "Alto = quase teleporta (responsivo), baixo = mais suave (cinematic). " +
            "0 = teleporta sem smoothing.")]
        [Range(0f, 30f)]
        public float cameraPositionSmoothing = 12f;

        [Header("Camera — Shake presets")]
        [Tooltip("Intensidade do shake leve (tier change, debuffs leves).")]
        [Range(0f, 1f)] public float shakeLightIntensity = 0.15f;
        [Tooltip("Duração do shake leve em segundos.")]
        public float shakeLightDuration = 0.15f;

        [Range(0f, 1f)] public float shakeMediumIntensity = 0.3f;
        public float shakeMediumDuration = 0.25f;

        [Range(0f, 2f)] public float shakeHeavyIntensity = 0.6f;
        public float shakeHeavyDuration = 0.5f;

        [Header("Camera — Death sequence (Game Over)")]
        [Tooltip("Duração da sequência de morte (slow-mo + zoom) antes do painel aparecer. Unscaled.")]
        public float deathCamDuration = 1.0f;

        [Tooltip("Time.timeScale durante a sequência. 0.3 = câmera lenta dramática.")]
        [Range(0.05f, 1f)] public float deathCamSlowMo = 0.3f;

        [Tooltip("Quanto a câmera se aproxima no death cam (subtrai do cameraDistance). " +
            "Usado apenas em Perspective — ortho usa deathCamOrthoSizeDelta.")]
        public float deathCamZoomDelta = 1.5f;

        [Tooltip("Quanto o orthographicSize se reduz no death cam (efeito zoom-in em ortho). " +
            "Usado apenas em Orthographic.")]
        public float deathCamOrthoSizeDelta = 1.5f;

        [Tooltip("Quanto o tilt da câmera aumenta no death cam.")]
        [Range(-30f, 30f)]
        public float deathCamTiltDelta = 5f;

        [Header("Debug")]
        [Tooltip("Desenhar Gizmos do critical path no editor")]
        public bool debugDrawCriticalPath = true;

        [Tooltip("Cor dos tiles que fazem parte do critical path (debug)")]
        public Color criticalPathGizmoColor = new Color(0f, 1f, 0.4f, 0.8f);

        [Tooltip("Cor dos tiles decoy (debug)")]
        public Color decoyGizmoColor = new Color(1f, 0.4f, 0.2f, 0.5f);
    }
}
