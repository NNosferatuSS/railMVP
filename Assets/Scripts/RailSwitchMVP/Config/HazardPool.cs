using System.Collections.Generic;
using UnityEngine;
using RailSwitchMVP.Core;

namespace RailSwitchMVP.Config
{
    /// <summary>
    /// Pool ponderado de hazards. Asset compartilhável entre tiers — vários
    /// DifficultyTiers podem apontar pro mesmo pool (ou ter pools distintos).
    /// Pool vazio/null no tier = nenhum hazard, mesmo se chance > 0.
    ///
    /// Pesos &lt;= 0 são ignorados no sorteio (equivalente a omitir a entrada).
    /// Duplicatas do mesmo HazardKind são permitidas (os pesos somam),
    /// mas OnValidate avisa pra evitar confusão.
    /// </summary>
    [CreateAssetMenu(fileName = "HazardPool", menuName = "RailSwitchMVP/Hazard Pool")]
    public class HazardPool : ScriptableObject, IValidatedConfig
    {
        [Tooltip("Tipos de hazard elegíveis neste pool e seus pesos relativos. " +
            "Pesos ≤ 0 são desabilitados (equivalente a remover a entrada).")]
        public List<HazardWeight> entries = new List<HazardWeight>();

        public int Count => entries == null ? 0 : entries.Count;

        public string GetValidationWarnings()
        {
            if (entries == null || entries.Count == 0)
                return null; // empty é válido (= "sem hazards"), tier checa antes

            var sb = new System.Text.StringBuilder();
            float total = 0f;
            int active = 0;
            var seen = new HashSet<HazardKind>();
            var duplicates = new HashSet<HazardKind>();
            bool hasNone = false;

            foreach (var w in entries)
            {
                if (w.kind == HazardKind.None) hasNone = true;
                if (!seen.Add(w.kind)) duplicates.Add(w.kind);
                if (w.weight > 0f) { total += w.weight; active++; }
            }

            if (hasNone)
                sb.AppendLine("• Entrada com kind=None — vai gerar 'sem hazard' aleatoriamente. Remova ou ajuste chance no tier.");
            if (duplicates.Count > 0)
                sb.AppendLine($"• Duplicatas: {string.Join(", ", duplicates)}. Pesos somam, mas costuma ser erro.");
            if (total <= 0f && entries.Count > 0)
                sb.AppendLine("• Soma de pesos = 0. Pool está efetivamente vazio (nenhuma entrada será sorteada).");
            else if (active < entries.Count)
                sb.AppendLine($"• {entries.Count - active} entrada(s) com peso ≤ 0 — ignoradas no sorteio.");

            return sb.Length == 0 ? null : sb.ToString().TrimEnd();
        }

        void OnValidate()
        {
            // Clamp negativos (range já cuida no inspector mas paste pode burlar)
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
