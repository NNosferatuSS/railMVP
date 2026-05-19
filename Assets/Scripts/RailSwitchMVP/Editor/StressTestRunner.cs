using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using RailSwitchMVP.Config;
using RailSwitchMVP.Core;
using Debug = UnityEngine.Debug;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Iter 5 stress test (headless). Gera N linhas em memória usando o
    /// ProceduralRailGenerator e valida invariantes do MVP:
    ///   1. Toda linha tem pelo menos 1 critical lane.
    ///   2. Critical lanes ficam dentro de [0, globalMax-1].
    ///   3. Total de tiles por linha respeita [minPerRow, maxPerRow] do tier ativo
    ///      (em geração normal — durante transição de reset, pode estourar pra cima
    ///      pela expansão do active range, o que é aceitável).
    ///   4. Continuidade ±1: critical lanes da linha N podem chegar à linha N+1
    ///      via switch (existe sempre pelo menos uma critical lane na N+1 cujo
    ///      offset em relação a alguma critical lane da N é ≤ 1).
    ///
    /// Roda em Edit Mode via menu: <b>Tools → RailSwitchMVP → Run Stress Test</b>.
    /// Não toca em GameObjects da cena — usa GameObject temporário destruído ao fim.
    /// </summary>
    public static class StressTestRunner
    {
        private const int DefaultRowCount = 10_000;
        private const int RowsPerTierStep = 1_500; // muda tier a cada 1500 linhas

        [MenuItem("Tools/RailSwitchMVP/Run Stress Test (10k rows)")]
        public static void Run10k() => RunStressTest(DefaultRowCount);

        [MenuItem("Tools/RailSwitchMVP/Run Stress Test (100k rows)")]
        public static void Run100k() => RunStressTest(100_000);

        public static void RunStressTest(int rowCount)
        {
            var config = FindFirstAssetOfType<RailGenConfig>();
            var difficulty = FindFirstAssetOfType<DifficultyConfig>();

            if (config == null)
            {
                Debug.LogError("[StressTest] No RailGenConfig asset found in project. Create one first.");
                return;
            }
            if (difficulty == null || difficulty.tiers == null || difficulty.tiers.Count == 0)
            {
                Debug.LogError("[StressTest] No DifficultyConfig (com pelo menos 1 tier) encontrado.");
                return;
            }

            Debug.Log($"[StressTest] Starting — config={config.name}, difficulty={difficulty.name} ({difficulty.tiers.Count} tiers), {rowCount} rows.");

            // Cria um generator temporário (não instancia tiles porque vamos chamar PlanRow).
            var hostGo = new GameObject("__StressTestHost__") { hideFlags = HideFlags.HideAndDontSave };
            var gen = hostGo.AddComponent<ProceduralRailGenerator>();
            gen.Configure(config, null);
            gen.ResetState();

            var stopwatch = Stopwatch.StartNew();

            int failures = 0;
            int transitionRows = 0;
            int tierTransitions = 0;
            int[] previousCritical = null;
            var sb = new StringBuilder();

            try
            {
                for (int row = 0; row < rowCount; row++)
                {
                    // Simula progressão de tier: muda a cada RowsPerTierStep até o último.
                    int tierIdx = Mathf.Min(row / RowsPerTierStep, difficulty.tiers.Count - 1);
                    var tier = difficulty.tiers[tierIdx];

                    // Detecta tier change pra contar
                    if (row > 0 && row % RowsPerTierStep == 0 && tierIdx > 0)
                        tierTransitions++;

                    var plan = gen.PlanRow(row, tier);

                    if (!ValidateRow(plan, tier, previousCritical, ref failures, sb))
                    {
                        // Continua o teste mesmo após falha — junta tudo no relatório.
                    }

                    if (plan.WasInTransition) transitionRows++;

                    previousCritical = plan.CriticalLanes;
                }
            }
            finally
            {
                stopwatch.Stop();
                Object.DestroyImmediate(hostGo);
            }

            // ===== Report =====
            Debug.Log(
                $"[StressTest] Completed in {stopwatch.ElapsedMilliseconds} ms\n" +
                $"  Rows generated   : {rowCount}\n" +
                $"  Tier transitions : {tierTransitions}\n" +
                $"  Rows w/ critical : {rowCount - failures}\n" +
                $"  Transition rows  : {transitionRows} (sempre 0 — não testamos resets aqui)\n" +
                $"  Failures         : {failures}"
            );

            if (failures == 0)
            {
                Debug.Log("<color=#00cc44>[StressTest] === ALL CHECKS PASSED ===</color>");
            }
            else
            {
                Debug.LogError($"[StressTest] === {failures} FAILURE(S) ===\n{sb}");
            }
        }

        static bool ValidateRow(RowPlan plan, DifficultyTier tier, int[] previousCritical, ref int failures, StringBuilder log)
        {
            bool ok = true;

            // 1. Pelo menos 1 critical lane
            if (plan.CriticalLanes == null || plan.CriticalLanes.Length == 0)
            {
                Append(log, $"Row {plan.RowIndex}: NO critical lane.");
                failures++; ok = false;
            }

            // 2. Critical lanes dentro de [0, globalMax)
            if (plan.CriticalLanes != null)
            {
                foreach (int L in plan.CriticalLanes)
                {
                    if (L < 0 || L >= plan.GlobalMax)
                    {
                        Append(log, $"Row {plan.RowIndex}: critical lane {L} out of global bounds [0, {plan.GlobalMax}).");
                        failures++; ok = false;
                    }
                }
            }

            // 3. Total de tiles respeita min/maxPerRow (apenas fora de transição —
            //    durante transição, active range expande e pode aumentar o total).
            if (!plan.WasInTransition && plan.LanePopulated != null)
            {
                int tierMin = Mathf.Clamp(tier.minLanesPerRow, 1, Mathf.Max(1, tier.maxLanes));
                int tierMax = Mathf.Clamp(tier.maxLanesPerRow, tierMin, Mathf.Max(1, tier.maxLanes));

                if (plan.TotalTiles < tierMin)
                {
                    Append(log, $"Row {plan.RowIndex}: tile count {plan.TotalTiles} < minPerRow {tierMin}.");
                    failures++; ok = false;
                }
                if (plan.TotalTiles > tierMax)
                {
                    Append(log, $"Row {plan.RowIndex}: tile count {plan.TotalTiles} > maxPerRow {tierMax}.");
                    failures++; ok = false;
                }
            }

            // 4. Continuidade ±1 com linha anterior — pula a primeira linha (sem anterior).
            if (previousCritical != null && previousCritical.Length > 0 && plan.CriticalLanes != null)
            {
                bool foundReachable = false;
                foreach (int prev in previousCritical)
                {
                    foreach (int now in plan.CriticalLanes)
                    {
                        if (Mathf.Abs(now - prev) <= 1)
                        {
                            foundReachable = true;
                            break;
                        }
                    }
                    if (foundReachable) break;
                }

                if (!foundReachable)
                {
                    string prevList = string.Join(",", previousCritical);
                    string nowList = string.Join(",", plan.CriticalLanes);
                    Append(log, $"Row {plan.RowIndex}: no critical reachable from previous (prev={prevList}, now={nowList}).");
                    failures++; ok = false;
                }
            }

            return ok;
        }

        static void Append(StringBuilder sb, string msg)
        {
            // Limita ao primeiro N pra não estourar o Console
            if (sb.Length < 4_000) sb.AppendLine(msg);
            else if (sb.Length < 4_100) sb.AppendLine("... (more failures suppressed)");
        }

        static T FindFirstAssetOfType<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids == null || guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
