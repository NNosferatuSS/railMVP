using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("Camera — Perspective (altura Y)")]
        [Tooltip("Altura Y mínima da câmera (player parado / speed = speedAtMinZoom). " +
            "Usado apenas em modo Perspective.")]
        public float cameraZoomMin;
        [Tooltip("Altura Y máxima da câmera (speed = speedAtMaxZoom). Usado apenas em Perspective.")]
        public float cameraZoomMax;

        [Header("Camera — Orthographic (orthoSize)")]
        [Tooltip("orthographicSize mínimo deste tier (player lento). " +
            "Metade da altura vertical da view em unidades de mundo. " +
            "Usado apenas em modo Orthographic.")]
        public float cameraOrthoSizeMin;
        [Tooltip("orthographicSize máximo deste tier (player rápido). Maior = zoom out. " +
            "Usado apenas em modo Orthographic.")]
        public float cameraOrthoSizeMax;

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

                // Camera ranges
                if (t.cameraZoomMin > t.cameraZoomMax)
                    sb.AppendLine($"{prefix}cameraZoomMin ({t.cameraZoomMin}) > cameraZoomMax ({t.cameraZoomMax}).");
                if (t.cameraOrthoSizeMin > t.cameraOrthoSizeMax)
                    sb.AppendLine($"{prefix}cameraOrthoSizeMin ({t.cameraOrthoSizeMin}) > cameraOrthoSizeMax ({t.cameraOrthoSizeMax}).");

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
    }
}
