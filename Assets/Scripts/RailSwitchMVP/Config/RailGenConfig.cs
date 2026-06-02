using System.Text;
using UnityEngine;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Estratégia de escolha do slot ocupado por hazard/power-up quando o tile
    /// recebe um. Coins ocupam os slots restantes (após reservas).
    /// </summary>
    public enum SlotPlacement
    {
        /// <summary>Sempre tenta o slot central. Se reservado, pega o livre mais próximo do centro.</summary>
        CenterSlot,
        /// <summary>Amostra aleatoriamente entre os slots livres.</summary>
        RandomFree,
    }

    /// <summary>
    /// Estratégia de distribuição das N coins ao longo dos slots do tile.
    /// Aplica-se apenas a coins (hazard/powerup usam SlotPlacement).
    /// </summary>
    public enum CoinPlacement
    {
        /// <summary>Posições determinísticas espalhadas uniformemente no grid (ex: 3 coins → slots 0, 2, 4). Coin é skipada se o slot-alvo está reservado.</summary>
        UniformGrid,
        /// <summary>Sorteia N slots livres aleatoriamente, sem repetição. Sempre respeita reservas.</summary>
        RandomFree,
    }

    /// <summary>
    /// Parâmetros globais do jogo que NÃO variam com dificuldade.
    /// Parâmetros que escalam com progressão vivem em DifficultyConfig (tiers).
    /// </summary>
    [CreateAssetMenu(fileName = "RailGenConfig", menuName = "RailSwitchMVP/Rail Gen Config")]
    public class RailGenConfig : ScriptableObject, IValidatedConfig
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

        [Header("Tile Slots")]
        [Tooltip("Quantos slots de spawn cada tile tem ao longo do comprimento (Z). " +
            "Coins, hazards e power-ups ocupam slots discretos — slot 0 fica perto do " +
            "StartPoint, slot N-1 perto do EndPoint. Garante que nada se sobrepõe.")]
        [Range(1, 15)]
        public int coinSlotsPerTile = 5;

        [Tooltip("Fração do comprimento mantida livre em cada extremidade do tile. " +
            "0.1 = primeiro slot fica a 10% do start, último a 90%. " +
            "Aplica-se a coins, hazards e power-ups (todos compartilham o mesmo grid de slots).")]
        [Range(0f, 0.45f)]
        public float coinSlotPadding = 0.1f;

        [Tooltip("Onde o hazard é colocado quando o tile recebe um. " +
            "CenterSlot = visual previsível (atual). RandomFree = mais variedade.")]
        public SlotPlacement hazardSlotStrategy = SlotPlacement.CenterSlot;

        [Tooltip("Onde o power-up é colocado quando o tile recebe um.")]
        public SlotPlacement powerUpSlotStrategy = SlotPlacement.CenterSlot;

        [Tooltip("Como as N coins são distribuídas nos slots do tile. " +
            "UniformGrid = posições fixas (0,2,4 pra 3 coins) — previsível mas repetitivo. " +
            "RandomFree = N slots livres sorteados a cada tile — variação visual.")]
        public CoinPlacement coinSlotStrategy = CoinPlacement.RandomFree;

        [Header("Power-up spawn gating")]
        [Tooltip("Gap GLOBAL em rows entre power-ups: após QUALQUER power-up spawnar, " +
            "as próximas N rows não recebem nenhum power-up (evita power-up em rows " +
            "seguidas). 0 = sem gap. Vale por cima do cooldown por tipo (PowerUpPool).")]
        [Min(0)]
        public int powerUpMinRowGap = 3;

        [Header("Hazard spawn gating")]
        [Tooltip("Gap GLOBAL em rows entre hazards: após QUALQUER hazard spawnar, " +
            "as próximas N rows não recebem nenhum hazard. 0 = sem gap (comportamento " +
            "atual). CUIDADO: valores altos deixam o jogo mais fácil. Vale por cima do " +
            "cooldown por tipo (HazardPool).")]
        [Min(0)]
        public int hazardMinRowGap = 0;

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

        [Tooltip("Quão abaixo do centro da tela o player aparece (pra ver mais pista à frente). " +
            "0 = player no centro; maior = mais embaixo. É COMPENSADO pelo zoom automaticamente " +
            "(o offset escala com a distância), então o player fica TRAVADO no mesmo ponto da " +
            "tela em qualquer zoom/tier. ~0.3-0.5 é um bom começo.")]
        [Range(0f, 1.5f)]
        public float cameraLookAhead = 0.35f;

        [Tooltip("Campo de visão da câmera em graus (perspective). 60 = default Unity. " +
            "Valores baixos (~40) = lente tele, perspectiva achatada, sensação de zoom. " +
            "Valores altos (~80+) = grande angular, mais coisa na tela, distorção nas bordas.")]
        [Range(20f, 100f)]
        public float cameraFieldOfView = 60f;

        [Tooltip("Velocidade de transição do zoom adaptativo")]
        public float cameraZoomSpeed = 8f;

        [Tooltip("Multiplier global de zoom — escala o cameraZoom per-tier (distância ao foco). " +
            "1 = sem alteração. 0.7 = ~30% mais perto. 1.5 = ~50% mais longe. " +
            "Aplica em cima do cameraZoom de cada tier do DifficultyConfig.")]
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

        [Tooltip("Quanto a câmera se aproxima no death cam (subtrai do cameraZoom, efeito zoom-in).")]
        public float deathCamZoomDelta = 1.5f;

        [Tooltip("Quanto o tilt da câmera aumenta no death cam.")]
        [Range(-30f, 30f)]
        public float deathCamTiltDelta = 5f;

        [Header("Camera — Shield impact slow-mo")]
        [Tooltip("timeScale no pico do slow-mo quando o Shield absorve uma barreira. Menor = mais lento/dramático.")]
        [Range(0.05f, 1f)]
        public float shieldImpactSlowMo = 0.35f;

        [Tooltip("Duração total (segundos REAIS) do slow-mo de impacto do Shield: segura um instante e faz lerp de volta a 1.")]
        [Range(0.05f, 2f)]
        public float shieldImpactDuration = 0.45f;

        [Header("Shield impact — player slowdown (decay)")]
        [Tooltip("Quando o Shield absorve uma barreira, a VELOCIDADE DO PLAYER cai pra esta " +
            "fração no impacto e recupera com decay (simula a batida/perda de momentum). " +
            "0.3 = cai pra 30% e volta. NÃO mexe no Time.timeScale (mundo segue normal).")]
        [Range(0.05f, 1f)]
        public float shieldImpactSpeedFactor = 0.3f;

        [Tooltip("Segundos (reais) pra velocidade recuperar do impacto de volta ao normal.")]
        [Range(0.1f, 3f)]
        public float shieldImpactRecoverSeconds = 1f;

        [Header("Debug")]
        [Tooltip("Desenhar Gizmos do critical path no editor")]
        public bool debugDrawCriticalPath = true;

        [Tooltip("Cor dos tiles que fazem parte do critical path (debug)")]
        public Color criticalPathGizmoColor = new Color(0f, 1f, 0.4f, 0.8f);

        [Tooltip("Cor dos tiles decoy (debug)")]
        public Color decoyGizmoColor = new Color(1f, 0.4f, 0.2f, 0.5f);

        public string GetValidationWarnings()
        {
            var sb = new StringBuilder();

            if (laneSpacing <= 0f) sb.AppendLine("• laneSpacing deve ser > 0.");
            if (trackLength <= 0f) sb.AppendLine("• trackLength deve ser > 0.");
            if (rowGap < 0f) sb.AppendLine("• rowGap não pode ser negativo.");
            if (globalMaxLanes < 1) sb.AppendLine("• globalMaxLanes deve ser ≥ 1.");
            if (globalMaxLanes % 2 == 0)
                sb.AppendLine($"• globalMaxLanes = {globalMaxLanes} é par. Convenção do projeto usa ímpar (3,5,7,9...) pra ter lane central.");

            if (coinSlotsPerTile < 1) sb.AppendLine("• coinSlotsPerTile deve ser ≥ 1.");
            if (coinSlotPadding < 0f || coinSlotPadding >= 0.5f)
                sb.AppendLine($"• coinSlotPadding = {coinSlotPadding:0.##} fora do range válido [0, 0.5).");

            if (rowsAhead < 1) sb.AppendLine("• rowsAhead deve ser ≥ 1.");
            if (rowsBehind < 0) sb.AppendLine("• rowsBehind não pode ser negativo.");

            if (warmupSpeedMultiplier > 1f)
                sb.AppendLine($"• warmupSpeedMultiplier = {warmupSpeedMultiplier:0.##} > 1 — warmup ficaria MAIS rápido que tier 0.");

            if (cameraZoomGlobalMultiplier <= 0f) sb.AppendLine("• cameraZoomGlobalMultiplier deve ser > 0.");
            if (cameraFieldOfView < 20f || cameraFieldOfView > 100f)
                sb.AppendLine($"• cameraFieldOfView = {cameraFieldOfView} fora do range usual [20, 100].");

            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }
    }
}
