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
