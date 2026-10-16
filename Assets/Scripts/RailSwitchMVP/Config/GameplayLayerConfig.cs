using System.Text;
using UnityEngine;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Config da camada de jogo Combo/Fever + Zonas (Docs/Gameplay_ComboFeverZonas.md).
    /// Lido por ComboFeverManager e ZoneDirector (objeto Aurora_Gameplay na cena).
    /// Defaults = valores testados em Play na 1ª versão da camada.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplayLayerConfig", menuName = "RailSwitchMVP/Gameplay Layer Config")]
    public class GameplayLayerConfig : ScriptableObject, IValidatedConfig
    {
        // ================================================================ Combo / Fever

        [Header("Fever — Barra")]
        [Min(1f)] public float meterMax = 100f;
        [Tooltip("Segundos sem ação antes da barra começar a escoar.")]
        [Min(0f)] public float decayDelay = 3.5f;
        [Tooltip("Pontos de barra perdidos por segundo enquanto escoa.")]
        [Min(0f)] public float decayPerSecond = 4.5f;

        [Header("Fever — Ganhos por ação")]
        [Tooltip("CLUTCH: trocar o conector no último terço do trilho e pousar vivo.")]
        [Min(0f)] public float clutchGain = 12f;
        [Tooltip("RISCO: pousar num decoy (fora do caminho crítico) e seguir.")]
        [Min(0f)] public float riskyGain = 14f;
        [Tooltip("RASPÃO: passar colado num hazard da lane vizinha.")]
        [Min(0f)] public float nearMissGain = 8f;
        [Min(0f)] public float coinGain = 2.5f;
        [Min(0f)] public float bonusCoinGain = 10f;
        [Min(0f)] public float gemGain = 25f;
        [Min(0f)] public float powerUpGain = 6f;
        [Tooltip("Barra perdida num impacto (shake ≥ 0.25). Também quebra a corrente.")]
        [Min(0f)] public float impactPenalty = 15f;

        [Header("Fever — Corrente")]
        [Tooltip("Segundos máximos entre ações pra continuar a corrente.")]
        [Min(0.1f)] public float chainWindow = 2.6f;
        [Tooltip("Ganho extra por elo da corrente (0.08 = +8% por elo).")]
        [Min(0f)] public float chainBonusPerLink = 0.08f;
        [Tooltip("Teto do bônus da corrente (1.5 = até +150%).")]
        [Min(0f)] public float chainBonusMax = 1.5f;

        [Header("Fever — Regras")]
        [Tooltip("Progresso no trilho (0..1) a partir do qual a troca conta como CLUTCH.")]
        [Range(0.3f, 0.95f)] public float clutchThreshold = 0.68f;
        [Tooltip("Distância lateral máxima (em lanes) pra contar RASPÃO.")]
        [Min(0f)] public float nearMissLanes = 1.05f;

        [Header("Fever — Ativo")]
        [Tooltip("Segundos de Fever depois que o jogador solta.")]
        [Min(0.5f)] public float feverDuration = 8f;
        [Tooltip("Multiplicador de coins durante o Fever (o ímã automático vem junto).")]
        [Min(1)] public int feverCoinMultiplier = 2;

        // ================================================================ Zonas

        [Header("Zonas — Agenda")]
        [Tooltip("Metros de corrida (pós-GO) até a 1ª zona.")]
        [Min(0f)] public float firstZoneAt = 260f;
        [Tooltip("Metros entre o FIM de uma zona e o início da próxima.")]
        [Min(0f)] public float zoneInterval = 420f;
        [Tooltip("Segundos de duração de cada zona.")]
        [Min(1f)] public float zoneDuration = 20f;
        [Tooltip("Tier mínimo (índice) pra zonas começarem.")]
        [Min(0)] public int minTierIndex = 2;

        [Header("Zonas — Rush")]
        [Tooltip("Multiplicador de velocidade do player durante o Rush (coins ×2 fixo).")]
        [Min(1f)] public float rushSpeedMultiplier = 1.18f;

        [Header("Zonas — Recompensa (completar viva)")]
        [Min(0)] public int clearBonusCoins = 25;
        [Tooltip("Barra de Fever somada ao completar a zona.")]
        [Min(0f)] public float clearFeverGain = 30f;

        [Header("Zonas — Moeda bônus (isca em decoy)")]
        [Tooltip("Chance por decoy sem coins, a partir do tier 2.")]
        [Range(0f, 1f)] public float baseBonusCoinChance = 0.1f;
        [Tooltip("Chance por decoy durante a Chuva de Ouro.")]
        [Range(0f, 1f)] public float goldRainBonusCoinChance = 0.45f;

        // ============================================================ Tutorial

        [Header("Cometa (power-up overpower)")]
        [Tooltip("Duração em trilhos.")]
        [Min(1)] public int cometTiles = 10;
        [Tooltip("Multiplicador de velocidade enquanto é cometa.")]
        [Range(1f, 2.5f)] public float cometSpeedMultiplier = 1.6f;
        [Tooltip("Segundos pra chegar na velocidade de cometa / voltar ao normal.")]
        [Min(0.05f)] public float cometRampIn = 0.35f;
        [Min(0.05f)] public float cometRampOut = 0.8f;
        [Tooltip("Raio do ímã gigante (mundo). O ímã normal é 4.")]
        [Min(0f)] public float cometMagnetRadius = 9f;
        [Tooltip("Moedas por hazard esmagado (× multiplicadores de moeda).")]
        [Min(0)] public int cometSmashCoins = 5;
        [Tooltip("Barra de Fever por hazard esmagado.")]
        [Min(0f)] public float cometFeverPerSmash = 4f;
        [Tooltip("Alcance (Z, mundo) da onda de choque no fim: hazards à frente viram moedas.")]
        [Min(0f)] public float cometShockwaveRange = 45f;
        [Tooltip("Trilhos de carência (sem queda) depois que o cometa acaba.")]
        [Min(0)] public int cometGraceTiles = 2;

        [Header("Tutorial — Slow-motion nas decisões")]
        [Tooltip("Time.timeScale durante o slow-motion do tutorial (quase parado, o mundo segue vivo).")]
        [Range(0.02f, 1f)] public float tutorialSlowTimeScale = 0.12f;
        [Tooltip("Progresso no trilho de decisão (0..1) em que o slow-motion entra.")]
        [Range(0.1f, 0.8f)] public float tutorialSlowTriggerProgress = 0.4f;
        [Tooltip("Tempo real (s) da rampa pra entrar no slow-motion.")]
        [Min(0.01f)] public float tutorialSlowRampIn = 0.25f;
        [Tooltip("Tempo real (s) da rampa pra voltar ao normal.")]
        [Min(0.01f)] public float tutorialSlowRampOut = 0.2f;
        [Tooltip("Se o jogador não agir em X s (tempo real), o jogo segue no ritmo de assistência.")]
        [Min(0.5f)] public float tutorialSlowMaxHold = 6f;
        [Tooltip("Velocidade do player durante o tutorial fora do slow-motion.")]
        [Range(0.3f, 1f)] public float tutorialSpeedMultiplier = 0.7f;

        public string GetValidationWarnings()
        {
            var sb = new StringBuilder();

            float maxGain = Mathf.Max(clutchGain, riskyGain, nearMissGain, bonusCoinGain, gemGain);
            if (maxGain <= 0f)
                sb.AppendLine("• Todos os ganhos de ação são 0: a barra de Fever nunca enche.");
            else if (maxGain >= meterMax)
                sb.AppendLine($"• Um único ganho ({maxGain:0.#}) já enche a barra ({meterMax:0.#}) — Fever pronto numa ação só.");

            if (decayDelay < chainWindow)
                sb.AppendLine($"• decayDelay ({decayDelay:0.##}s) < chainWindow ({chainWindow:0.##}s): " +
                    "a barra começa a escoar antes da corrente quebrar.");

            if (clearFeverGain >= meterMax)
                sb.AppendLine("• clearFeverGain >= meterMax: completar uma zona sempre deixa o Fever pronto.");

            if (zoneInterval <= 0f)
                sb.AppendLine("• zoneInterval = 0: as zonas vão emendar uma na outra.");

            if (goldRainBonusCoinChance < baseBonusCoinChance)
                sb.AppendLine("• goldRainBonusCoinChance < baseBonusCoinChance: a Chuva de Ouro dá MENOS moeda bônus que o normal.");

            return sb.ToString();
        }
    }
}
