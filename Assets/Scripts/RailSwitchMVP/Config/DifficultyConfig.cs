using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Entrada do hazardPool — define que tipos podem aparecer e com que
    /// peso relativo (0 = excluído, equivalente a omitir). Vive dentro de
    /// um HazardPool ScriptableObject, não diretamente no tier.
    /// </summary>
    [System.Serializable]
    public struct HazardWeight
    {
        public HazardKind kind;
        [Range(0f, 10f)] public float weight;

        [Tooltip("Cooldown EM ROWS deste hazard: após spawnar, ele não volta a spawnar " +
            "por este nº de rows (outros tipos podem). 0 = sem cooldown próprio. " +
            "O gap global (RailGenConfig.hazardMinRowGap) vale por cima disto.")]
        [Min(0)] public int cooldownRows;
    }

    /// <summary>
    /// Entrada do powerUpPool — quais power-ups podem dropar e com que peso
    /// relativo. Type sem prefab registrado no generator é skipado em runtime.
    /// </summary>
    [System.Serializable]
    public struct PowerUpWeight
    {
        public PowerUpType type;
        [Range(0f, 10f)] public float weight;

        [Tooltip("Cooldown EM ROWS deste tipo: após spawnar, ele não volta a spawnar " +
            "por este nº de rows (outros tipos podem). 0 = sem cooldown próprio. " +
            "O gap global (RailGenConfig.powerUpMinRowGap) vale por cima disto.")]
        [Min(0)] public int cooldownRows;
    }

    /// <summary>
    /// Snapshot completo de configuração para um tier de dificuldade.
    /// Cada tier é ativado quando o player atinge triggerAtDistance.
    /// Pools de hazard e power-up agora são SOs separados (HazardPool / PowerUpPool),
    /// permitindo reuso entre tiers e edição isolada.
    /// </summary>
    [System.Serializable]
    public struct DifficultyTier
    {
        [Header("Trigger")]
        [Tooltip("Distância acumulada (Z) para ativar este tier")]
        public float triggerAtDistance;

        [Header("Generation")]
        [Tooltip("Número máximo de lanes paralelas (3, 5, 7, 9...)")]
        public int maxLanes;

        [Tooltip("Mínimo de tiles por linha (critical + decoys)")]
        public int minLanesPerRow;

        [Tooltip("Máximo de tiles por linha")]
        public int maxLanesPerRow;

        [Tooltip("Quantos critical paths existem em paralelo nesta dificuldade")]
        public int criticalPathsPerRow;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de uma lane decoy ser populada")]
        public float lanePopulationChance;

        [Header("Streaming")]
        [Tooltip("Quantas rows spawnar à frente do player neste tier. " +
            "Tiers rápidos precisam de mais rows pra evitar pop-in visível. " +
            "Sobrescreve config.rowsAhead. SpawnOverrideController ainda tem prioridade máxima.")]
        [Min(1)]
        public int rowsAhead;

        [Header("Player")]
        [Tooltip("Velocidade forward do player neste tier")]
        public float playerSpeed;

        [Header("Camera")]
        [Tooltip("Distância da câmera ao foco (player) ao longo do eixo de visão neste tier. " +
            "Maior = mais longe (zoom out), menor = mais perto (zoom in). Valor único por tier; " +
            "a câmera sempre olha pro foco, então mudar o zoom NÃO desloca o player na tela.")]
        public float cameraZoom;

        [Tooltip("Se true, este tier SOBRESCREVE o tilt e o FOV globais do RailGenConfig " +
            "(pra tunar o ângulo/lente da câmera por dificuldade). False = usa os globais.")]
        public bool overrideCameraAngle;

        [ShowIf(nameof(overrideCameraAngle))]
        [Range(0f, 90f)]
        [Tooltip("Inclinação da câmera em graus deste tier (0 = top-down, 90 = lateral). " +
            "Só aplicado se overrideCameraAngle = true.")]
        public float cameraTilt;

        [ShowIf(nameof(overrideCameraAngle))]
        [Range(20f, 100f)]
        [Tooltip("Campo de visão / FOV deste tier (60 = default). Menor = lente tele/zoom; " +
            "maior = grande angular. Só aplicado se overrideCameraAngle = true.")]
        public float cameraFieldOfView;

        [Header("Coins")]
        [Tooltip("Mínimo de moedas em cada tile do critical path. Sample uniforme em [min, max+1).")]
        [FormerlySerializedAs("coinsPerCriticalTile")]
        public int criticalCoinsMin;

        [Tooltip("Máximo de moedas em cada tile do critical path (inclusivo).")]
        public int criticalCoinsMax;

        [Tooltip("Mínimo de moedas em cada tile decoy (0 = pode ficar sem moedas).")]
        [FormerlySerializedAs("coinsPerDecoyTile")]
        public int decoyCoinsMin;

        [Tooltip("Máximo de moedas em cada tile decoy (inclusivo).")]
        public int decoyCoinsMax;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber moedas (controle granular por tier). " +
            "1 = todo decoy recebe (comportamento antigo); 0 = decoys nunca recebem. Critical path " +
            "sempre recebe. ATENÇÃO: vem 0 por default ao adicionar o campo — setar nos tiers!")]
        public float decoyCoinChance;

        [Header("Hazards")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber QUALQUER hazard. " +
            "Critical path NUNCA recebe hazard (design rule). Pool null/vazio = " +
            "nenhum hazard, mesmo com chance > 0.")]
        public float hazardChanceOnDecoy;

        [Tooltip("HazardPool SO com tipos elegíveis e pesos. Pode ser compartilhado " +
            "entre tiers. Null = sem hazards neste tier.")]
        public HazardPool hazardPool;

        [Header("Power-ups")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile do CRITICAL PATH receber um power-up.")]
        public float powerUpChanceOnCritical;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber um power-up. " +
            "Tile que já recebeu hazard NUNCA recebe power-up.")]
        public float powerUpChanceOnDecoy;

        [Tooltip("PowerUpPool SO com tipos elegíveis e pesos. Pode ser compartilhado " +
            "entre tiers. Null = sem power-ups neste tier.")]
        public PowerUpPool powerUpPool;

        [Tooltip("Pool DEDICADO da Mystery Box (caixa surpresa): quais power-ups podem sair " +
            "dela e com que peso. Pode ser um SO compartilhado entre tiers (controle global) " +
            "ou um por tier (varia). Null = a Mystery Box cai no powerUpPool normal do tier.")]
        public PowerUpPool mysteryBoxPool;
    }

    /// <summary>
    /// Regra de starting tier adaptativo (Camada 1). A partir de minAccountLevel,
    /// o run começa no startingTierIndex. Lista avaliada em ordem crescente.
    /// </summary>
    [System.Serializable]
    public struct StartingTierRule
    {
        [Tooltip("Account level mínimo pra esta regra se aplicar")]
        public int minAccountLevel;

        [Tooltip("Índice do tier em que o run começa (0-based)")]
        public int startingTierIndex;
    }

    /// <summary>
    /// Curva de dificuldade do jogo. Lista ordenada de tiers.
    /// O DifficultyManager avança entre tiers conforme a distância percorrida.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "RailSwitchMVP/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject, IValidatedConfig
    {
        [Tooltip("Lista de tiers em ordem crescente de triggerAtDistance. O tier 0 sempre tem trigger = 0.")]
        public List<DifficultyTier> tiers = new List<DifficultyTier>();

        [Header("Adaptive Start (Camada 1)")]
        [Tooltip("Regras de starting tier por account level, em ordem crescente de minAccountLevel.")]
        public List<StartingTierRule> startingTierRules = new List<StartingTierRule>();

        [Tooltip("Teto absoluto de starting tier. Nunca começar acima disto, garantindo que " +
            "sempre haja tiers acima pra acelerar durante o run.")]
        public int maxStartingTierIndex = 4;

        [Header("Endless (depois do último tier)")]
        [Tooltip("Tiers gerados por código depois do último configurado — o jogo nunca para de " +
            "evoluir. Velocidade tende a um teto (sempre jogável); intervalo entre tiers cresce em %.")]
        public EndlessScaling endless = new EndlessScaling();

        /// <summary>Distância (lógica) em que o tier infinito <paramref name="step"/> (1, 2, 3…) começa.</summary>
        public float EndlessTrigger(int step)
        {
            if (tiers == null || tiers.Count == 0 || step <= 0) return float.PositiveInfinity;
            var last = tiers[tiers.Count - 1];
            float first = endless.firstIntervalMeters > 0f
                ? endless.firstIntervalMeters
                : (tiers.Count >= 2 ? Mathf.Max(100f, last.triggerAtDistance - tiers[tiers.Count - 2].triggerAtDistance) : 1000f);
            float d = last.triggerAtDistance, interval = first;
            for (int k = 1; k <= step; k++)
            {
                d += interval;
                interval *= 1f + Mathf.Max(0f, endless.intervalGrowth);
            }
            return d;
        }

        /// <summary>
        /// Tier infinito <paramref name="step"/>: cópia do último tier configurado com
        /// velocidade aproximando endless.speedCap, zoom acompanhando a velocidade (mantém
        /// tempo de leitura) e hazards crescendo até endless.hazardCap.
        /// </summary>
        public DifficultyTier BuildEndlessTier(int step)
        {
            var last = tiers[tiers.Count - 1];
            var t = last;
            float baseSpeed = Mathf.Max(0.01f, last.playerSpeed);
            float cap = Mathf.Max(baseSpeed, endless.speedCap);
            float k = Mathf.Pow(1f - Mathf.Clamp01(endless.speedApproach), step);
            t.playerSpeed = cap - (cap - baseSpeed) * k;
            t.cameraZoom = Mathf.Min(Mathf.Max(last.cameraZoom, endless.zoomCap),
                last.cameraZoom * Mathf.Pow(t.playerSpeed / baseSpeed, endless.zoomPerSpeedExponent));
            t.hazardChanceOnDecoy = Mathf.Min(Mathf.Max(last.hazardChanceOnDecoy, endless.hazardCap),
                last.hazardChanceOnDecoy + step * endless.hazardPerStep);
            t.triggerAtDistance = EndlessTrigger(step);
            return t;
        }

        void OnValidate()
        {
            if (tiers == null || tiers.Count == 0) return;

            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                bool changed = false;

                // Tier 0 sempre começa em distância 0.
                if (i == 0 && t.triggerAtDistance != 0f)
                {
                    t.triggerAtDistance = 0f;
                    changed = true;
                }

                // Auto-copia min→max na primeira abertura pós-migração de Slice 2
                // (FormerlySerializedAs preenche min com o valor antigo; max fica 0).
                if (t.criticalCoinsMax < t.criticalCoinsMin)
                {
                    t.criticalCoinsMax = t.criticalCoinsMin;
                    changed = true;
                }
                if (t.decoyCoinsMax < t.decoyCoinsMin)
                {
                    t.decoyCoinsMax = t.decoyCoinsMin;
                    changed = true;
                }

                if (changed) tiers[i] = t;
            }
        }

        public string GetValidationWarnings()
        {
            if (tiers == null || tiers.Count == 0)
                return "• Nenhum tier definido — DifficultyManager vai falhar.";

            var sb = new StringBuilder();

            if (endless != null && endless.enabled)
            {
                var lastT = tiers[tiers.Count - 1];
                if (endless.speedCap < lastT.playerSpeed)
                    sb.AppendLine($"• Endless: speedCap ({endless.speedCap}) < velocidade do último tier ({lastT.playerSpeed}) — os tiers infinitos não aceleram.");
                if (endless.speedApproach <= 0f)
                    sb.AppendLine("• Endless: speedApproach = 0 — a velocidade nunca sobe depois do último tier.");
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                var t = tiers[i];
                string prefix = $"• Tier {i} (dist {t.triggerAtDistance:0.#}): ";

                // Ordem monotônica
                if (i > 0 && t.triggerAtDistance <= tiers[i - 1].triggerAtDistance)
                    sb.AppendLine($"{prefix}triggerAtDistance deve ser > tier anterior ({tiers[i - 1].triggerAtDistance:0.#}).");

                // Coerências básicas
                if (t.maxLanes < 1)
                    sb.AppendLine($"{prefix}maxLanes deve ser ≥ 1.");
                if (t.minLanesPerRow > t.maxLanesPerRow)
                    sb.AppendLine($"{prefix}minLanesPerRow ({t.minLanesPerRow}) > maxLanesPerRow ({t.maxLanesPerRow}).");
                if (t.maxLanesPerRow > t.maxLanes)
                    sb.AppendLine($"{prefix}maxLanesPerRow ({t.maxLanesPerRow}) > maxLanes ({t.maxLanes}).");
                if (t.criticalPathsPerRow < 1)
                    sb.AppendLine($"{prefix}criticalPathsPerRow deve ser ≥ 1.");
                if (t.criticalPathsPerRow > t.minLanesPerRow)
                    sb.AppendLine($"{prefix}criticalPathsPerRow ({t.criticalPathsPerRow}) > minLanesPerRow ({t.minLanesPerRow}) — impossível garantir lanes críticas em linhas mínimas.");
                if (t.rowsAhead < 1)
                    sb.AppendLine($"{prefix}rowsAhead deve ser ≥ 1.");
                if (t.playerSpeed <= 0f)
                    sb.AppendLine($"{prefix}playerSpeed deve ser > 0.");

                // Camera
                if (t.cameraZoom <= 0f)
                    sb.AppendLine($"{prefix}cameraZoom ({t.cameraZoom}) deve ser > 0.");

                // Pool x chance — chance > 0 sem pool é desperdício
                if (t.hazardChanceOnDecoy > 0f && (t.hazardPool == null || t.hazardPool.Count == 0))
                    sb.AppendLine($"{prefix}hazardChanceOnDecoy = {t.hazardChanceOnDecoy:0.##} mas hazardPool null/vazio — chance ignorada.");
                if ((t.powerUpChanceOnCritical > 0f || t.powerUpChanceOnDecoy > 0f) && (t.powerUpPool == null || t.powerUpPool.Count == 0))
                    sb.AppendLine($"{prefix}powerUpChance > 0 mas powerUpPool null/vazio — chance ignorada.");

                // Coin ranges
                if (t.criticalCoinsMin < 0)
                    sb.AppendLine($"{prefix}criticalCoinsMin ({t.criticalCoinsMin}) < 0.");
                if (t.criticalCoinsMax < t.criticalCoinsMin)
                    sb.AppendLine($"{prefix}criticalCoinsMax ({t.criticalCoinsMax}) < criticalCoinsMin ({t.criticalCoinsMin}).");
                if (t.decoyCoinsMin < 0)
                    sb.AppendLine($"{prefix}decoyCoinsMin ({t.decoyCoinsMin}) < 0.");
                if (t.decoyCoinsMax < t.decoyCoinsMin)
                    sb.AppendLine($"{prefix}decoyCoinsMax ({t.decoyCoinsMax}) < decoyCoinsMin ({t.decoyCoinsMin}).");
            }

            // Adaptive start (Camada 1)
            if (startingTierRules != null && startingTierRules.Count > 0)
            {
                for (int i = 0; i < startingTierRules.Count; i++)
                {
                    var r = startingTierRules[i];
                    string rp = $"• StartingTierRule {i} (minLevel {r.minAccountLevel}): ";
                    if (i > 0 && r.minAccountLevel <= startingTierRules[i - 1].minAccountLevel)
                        sb.AppendLine($"{rp}minAccountLevel deve ser > regra anterior ({startingTierRules[i - 1].minAccountLevel}) — lista é avaliada em ordem crescente.");
                    if (r.startingTierIndex < 0 || r.startingTierIndex >= tiers.Count)
                        sb.AppendLine($"{rp}startingTierIndex ({r.startingTierIndex}) fora do range de tiers [0,{tiers.Count - 1}].");
                }
                if (tiers.Count >= 2 && maxStartingTierIndex > tiers.Count - 2)
                    sb.AppendLine($"• maxStartingTierIndex ({maxStartingTierIndex}) > penúltimo tier ({tiers.Count - 2}) — será limitado em runtime pra deixar tiers acima pra acelerar.");
            }

            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Retorna o tier ativo para uma dada distância percorrida.
        /// </summary>
        public DifficultyTier GetTierForDistance(float distance)
        {
            if (tiers == null || tiers.Count == 0)
            {
                Debug.LogError("[DifficultyConfig] No tiers defined!");
                return default;
            }

            DifficultyTier result = tiers[0];
            for (int i = 1; i < tiers.Count; i++)
            {
                if (distance >= tiers[i].triggerAtDistance)
                    result = tiers[i];
                else
                    break;
            }
            return result;
        }

        /// <summary>
        /// Retorna o índice do tier ativo para a distância dada.
        /// </summary>
        public int GetTierIndexForDistance(float distance)
        {
            if (tiers == null || tiers.Count == 0) return 0;

            int idx = 0;
            for (int i = 1; i < tiers.Count; i++)
            {
                if (distance >= tiers[i].triggerAtDistance)
                    idx = i;
                else
                    break;
            }
            return idx;
        }

        /// <summary>
        /// Resolve o starting tier (Camada 1) pra um account level. Aplica o teto
        /// seguro: nunca passa de maxStartingTierIndex nem do penúltimo tier
        /// disponível, garantindo que sempre haja tier acima pra acelerar.
        /// </summary>
        public int GetStartingTierIndex(int accountLevel)
        {
            if (startingTierRules == null || startingTierRules.Count == 0)
                return 0;

            int result = 0;
            foreach (var rule in startingTierRules)
            {
                if (accountLevel >= rule.minAccountLevel)
                    result = rule.startingTierIndex;
                else
                    break; // lista em ordem crescente de minAccountLevel
            }

            int safeCeiling = Mathf.Min(maxStartingTierIndex, tiers.Count - 2);
            safeCeiling = Mathf.Max(0, safeCeiling); // proteção se houver poucos tiers
            return Mathf.Clamp(result, 0, safeCeiling);
        }
    }

    /// <summary>Parâmetros dos tiers infinitos (depois do último tier configurado).</summary>
    [System.Serializable]
    public class EndlessScaling
    {
        public bool enabled = true;

        [Tooltip("Distância até o 1º tier infinito. 0 = mesmo intervalo entre os dois últimos tiers configurados.")]
        [Min(0f)] public float firstIntervalMeters = 0f;

        [Tooltip("Cada intervalo é X% maior que o anterior (0.15 = +15%). A progressão desacelera mas nunca para.")]
        [Range(0f, 1f)] public float intervalGrowth = 0.15f;

        [Tooltip("Velocidade máxima assintótica. Os tiers infinitos se aproximam disto sem passar.")]
        [Min(1f)] public float speedCap = 32f;

        [Tooltip("Fração da distância até o teto percorrida a cada tier (0.12 = 12% do que falta).")]
        [Range(0f, 0.5f)] public float speedApproach = 0.12f;

        [Tooltip("Zoom acompanha a velocidade nesta potência (1 = mantém o tempo de leitura idêntico; " +
            "<1 = câmera abre menos, objetos não encolhem tanto).")]
        [Range(0f, 1.5f)] public float zoomPerSpeedExponent = 0.7f;

        [Tooltip("Zoom máximo (distância de câmera) dos tiers infinitos.")]
        [Min(1f)] public float zoomCap = 28f;

        [Tooltip("hazardChanceOnDecoy somado a cada tier infinito.")]
        [Range(0f, 0.2f)] public float hazardPerStep = 0.02f;

        [Range(0f, 1f)] public float hazardCap = 0.85f;
    }
}
