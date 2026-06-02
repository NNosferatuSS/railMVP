using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Pool ponderado de power-ups. Asset compartilhável entre tiers.
    /// Pool único por tier — chance varia entre critical e decoy, distribuição não.
    ///
    /// Types sem prefab registrado no ProceduralRailGenerator são skipados em runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUpPool", menuName = "RailSwitchMVP/PowerUp Pool")]
    public class PowerUpPool : ScriptableObject, IValidatedConfig
    {
        [Tooltip("Tipos de power-up elegíveis neste pool e seus pesos relativos. " +
            "Pesos ≤ 0 são desabilitados.")]
        public List<PowerUpWeight> entries = new List<PowerUpWeight>();

        public int Count => entries == null ? 0 : entries.Count;

        /// <summary>
        /// Descrição central do que cada power-up faz. Fonte única — usada pelo
        /// InfoBox do editor (e disponível pra qualquer outra UI).
        /// </summary>
        public static string Describe(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    return "Shield — absorve 1 hit de Barrier (não salva de Lethal). Sem stack: renova 1 carga.";
                case PowerUpType.SlowDown:
                    return "SlowDown — reduz a velocidade do player (~0.7x) por N tiles. Mais tempo de reação.";
                case PowerUpType.Magnet:
                    return "Magnet — atrai coins próximas automaticamente por N tiles.";
                case PowerUpType.DifficultyReset:
                    return "DifficultyReset — reseta a dificuldade pro início (instantâneo).";
                case PowerUpType.DoubleCoins:
                    return "2x Coins — dobra as coins coletadas por N tiles.";
                case PowerUpType.Ghost:
                    return "Ghost — atravessa hazards / dead-ends / bordas por N tiles (voa por cima).";
                case PowerUpType.LanePreview:
                    return "Lane Preview — mostra a direção do critical path na próxima row por N tiles.";
                case PowerUpType.CoinRadar:
                    return "Coin Radar — destaca/pulsa as coins por N tiles.";
                case PowerUpType.Teleport:
                    return "Teleport — abre uma janela (N tiles) pra teleportar ±1 lane com Shift+←/→.";
                case PowerUpType.AutoCriticalFollow:
                    return "AutoFollow — o jogo segue o critical path sozinho por N tiles.";
                case PowerUpType.TimeFreeze:
                    return "TimeFreeze — slow-mo (timeScale baixo) por alguns segundos. Consumido na colisão.";
                case PowerUpType.MysteryBox:
                    return "Mystery Box — caixa surpresa: concede um power-up aleatório do mysteryBoxPool do tier.";
                case PowerUpType.SpeedUpDebuff:
                    return "SpeedUp (DEBUFF) — acelera o player; normalmente vem de hazard, não de pool de power-up.";
                case PowerUpType.LaneSwapDebuff:
                    return "LaneSwap (DEBUFF) — inverte ←/→; normalmente vem de hazard, não de pool de power-up.";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// Texto (multi-linha) listando o que cada power-up presente no pool faz.
        /// Usado pelo InfoBox do Inspector normal (ValidatedConfigInspector) e
        /// pelo [OnInspectorGUI] do Odin (Control Panel). null = pool vazio.
        /// </summary>
        public string GetEntriesInfo()
        {
            if (entries == null || entries.Count == 0) return null;
            var seen = new HashSet<PowerUpType>();
            var sb = new System.Text.StringBuilder("O que cada power-up deste pool faz:\n");
            foreach (var e in entries)
                if (seen.Add(e.type))
                    sb.AppendLine("• " + Describe(e.type));
            return sb.ToString().TrimEnd();
        }

#if UNITY_EDITOR
        // InfoBox no Control Panel / Odin (que processa [OnInspectorGUI]).
        // PropertyOrder alto = depois da lista de entries.
        [Sirenix.OdinInspector.OnInspectorGUI, Sirenix.OdinInspector.PropertyOrder(100)]
        void DrawPowerUpDescriptions()
        {
            var info = GetEntriesInfo();
            if (!string.IsNullOrEmpty(info))
                UnityEditor.EditorGUILayout.HelpBox(info, UnityEditor.MessageType.Info);
        }
#endif

        public string GetValidationWarnings()
        {
            if (entries == null || entries.Count == 0)
                return null;

            var sb = new System.Text.StringBuilder();
            float total = 0f;
            int active = 0;
            var seen = new HashSet<PowerUpType>();
            var duplicates = new HashSet<PowerUpType>();

            foreach (var w in entries)
            {
                if (!seen.Add(w.type)) duplicates.Add(w.type);
                if (w.weight > 0f) { total += w.weight; active++; }
            }

            if (duplicates.Count > 0)
                sb.AppendLine($"• Duplicatas: {string.Join(", ", duplicates)}. Pesos somam, mas costuma ser erro.");
            if (total <= 0f && entries.Count > 0)
                sb.AppendLine("• Soma de pesos = 0. Pool está efetivamente vazio.");
            else if (active < entries.Count)
                sb.AppendLine($"• {entries.Count - active} entrada(s) com peso ≤ 0 — ignoradas no sorteio.");

            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }

        void OnValidate()
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].weight < 0f)
                    {
                        var e = entries[i];
                        e.weight = 0f;
                        entries[i] = e;
                    }
                }
            }
        }
    }
}
