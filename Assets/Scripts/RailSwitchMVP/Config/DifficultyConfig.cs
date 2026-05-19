using System.Collections.Generic;
using UnityEngine;

namespace RailSwitchMVP.Config
{
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

        [Header("Camera")]
        public float cameraZoomMin;
        public float cameraZoomMax;

        [Header("Coins")]
        [Tooltip("Quantidade de moedas em cada tile do critical path")]
        public int coinsPerCriticalTile;

        [Tooltip("Quantidade de moedas em cada tile decoy (0 = sem moedas em decoys)")]
        public int coinsPerDecoyTile;

        [Header("Obstacles (MVP2)")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber um obstáculo letal. " +
            "Critical path NUNCA recebe obstáculo (decisão de design MVP2 — " +
            "reforça \"moeda = caminho seguro\").")]
        public float obstacleChanceOnDecoy;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber uma barreira (absorvida por Shield). " +
            "Independente de obstacleChanceOnDecoy — mas tiles nunca recebem AMBOS no mesmo lugar.")]
        public float barrierChanceOnDecoy;

        [Header("Power-ups (MVP2)")]
        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile do CRITICAL PATH receber um power-up.")]
        public float powerUpChanceOnCritical;

        [Range(0f, 1f)]
        [Tooltip("Probabilidade de um tile DECOY receber um power-up. " +
            "Tile com obstáculo OU barreira não recebe power-up.")]
        public float powerUpChanceOnDecoy;
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
