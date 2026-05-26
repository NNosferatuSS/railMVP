using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Entrada do hazardPool de um tier — define que tipos podem aparecer
    /// e com que peso relativo (0 = excluído, equivalente a omitir).
    /// O sorteio é feito quando o tile decoy hit em hazardChanceOnDecoy.
    /// </summary>
    [System.Serializable]
    public struct HazardWeight
    {
        public HazardKind kind;
        [Range(0f, 10f)] public float weight;
    }

    /// <summary>
    /// Entrada do powerUpPool de um tier — quais power-ups podem dropar e
    /// com que peso relativo. Único pool por tier (compartilhado entre
    /// critical e decoy). Type sem prefab registrado no generator é skipado.
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
        [Tooltip("Quantidade de moedas em cada tile do critical path")]
        public int coinsPerCriticalTile;

        [Tooltip("Quantidade de moedas em cada tile decoy (0 = sem moedas em decoys)")]
        public int coinsPerDecoyTile;

        [Header("Hazards")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber QUALQUER hazard. " +
            "Critical path NUNCA recebe hazard (design rule). Se hit, o tipo " +
            "é sorteado por peso em hazardPool. Pool vazio = nenhum hazard, " +
            "mesmo com chance > 0.")]
        public float hazardChanceOnDecoy;

        [Tooltip("Tipos de hazard elegíveis neste tier e seus pesos relativos " +
            "(0 = desabilitado, equivalente a omitir). Permite progressão tipo " +
            "\"tier 1 só letal; tier 3 introduz barrier; tier 5 todos\".")]
        public List<HazardWeight> hazardPool;

        [Header("Power-ups")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile do CRITICAL PATH receber um power-up. " +
            "Se hit, o tipo é sorteado por peso em powerUpPool.")]
        public float powerUpChanceOnCritical;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber um power-up. " +
            "Tile que já recebeu hazard NUNCA recebe power-up.")]
        public float powerUpChanceOnDecoy;

        [Tooltip("Tipos de power-up elegíveis neste tier e seus pesos relativos. " +
            "Pool único compartilhado entre critical e decoy (chance varia, " +
            "distribuição não). Type sem binding no generator é skipado.")]
        public List<PowerUpWeight> powerUpPool;
    }

    /// <summary>
    /// Curva de dificuldade do jogo. Lista ordenada de tiers.
    /// O DifficultyManager avança entre tiers conforme a distância percorrida.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyConfig", menuName = "RailSwitchMVP/Difficulty Config")]
    public class DifficultyConfig : ScriptableObject
    {
        [Tooltip("Lista de tiers em ordem crescente de triggerAtDistance. O tier 0 sempre tem trigger = 0.")]
        public List<DifficultyTier> tiers = new List<DifficultyTier>();

        void OnValidate()
        {
            // Garante que o tier 0 sempre comece em distância 0
            if (tiers != null && tiers.Count > 0)
            {
                var t0 = tiers[0];
                if (t0.triggerAtDistance != 0f)
                {
                    t0.triggerAtDistance = 0f;
                    tiers[0] = t0;
                }
            }
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
