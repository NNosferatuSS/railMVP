#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Roda toda a pipeline de build em ordem. Cada passo tem try/catch pra que
    /// uma falha isolada não bloqueie os subsequentes — no fim mostra um sumário
    /// com quais passos completaram e quais falharam. Detalhes do erro sempre
    /// vão pro Console.
    /// Menu: Tools -> RailSwitch -> Build UI -> BUILD ALL.
    /// </summary>
    public static class BuildAll
    {
        [MenuItem("Tools/RailSwitch/Build UI/BUILD ALL", priority = 0)]
        public static void RunAll()
        {
            if (!EditorUtility.DisplayDialog("Rail Switch UI",
                "Isso vai:\n" +
                "1. Copiar sprites\n2. Baixar fontes\n3. Prefabs base\n" +
                "4. Canvas HUD\n5. Canvas Overlays (GameOver/Pause/Warmup)\n" +
                "6. Canvas Meta\n7. Canvas Splash\n\n" +
                "Cada passo é isolado — falha de um não bloqueia os outros.",
                "BUILD", "Cancelar")) return;

            var report = new System.Text.StringBuilder();
            RunPipeline(report, includeAssets: true);
            EditorUtility.DisplayDialog("Rail Switch UI — Report", report.ToString(), "OK");
        }

        /// <summary>
        /// Pipeline sem diálogos de confirmação — usado pelo BUILD ALL interativo
        /// e pelo ApplyUIToScenes (que roda tudo em sequência e não pode parar
        /// numa fila de modais).
        /// </summary>
        /// <param name="includeAssets">
        /// Passos 1 e 2 (copiar sprites, baixar fontes). Só precisam rodar quando
        /// os assets do handoff mudam — pular economiza bastante tempo no loop
        /// normal de iteração de layout.
        /// </param>
        public static void RunPipeline(System.Text.StringBuilder report, bool includeAssets)
        {
            bool prevQuiet = UIBuildContext.Quiet;
            UIBuildContext.Quiet = true;
            try
            {
                if (includeAssets)
                {
                    RunStep("1. Import Sprites", UIAssetImporter.ImportAll, report);
                    RunStep("2. Import Fonts",   UIFontImporter.ImportAll,  report);
                }
                RunStep("3. Base Prefabs",   BuildBasePrefabs.BuildAll, report);
                RunStep("4. HUD",            BuildHUD.Build,            report);
                RunStep("5. Overlays",       BuildOverlays.BuildAll,    report);
                RunStep("6. Meta Canvas",    BuildMetaCanvas.Build,     report);
                RunStep("7. Splash",         BuildSplash.Build,         report);
                AssetDatabase.SaveAssets();
            }
            finally { UIBuildContext.Quiet = prevQuiet; }
        }

        static void RunStep(string label, System.Action step, System.Text.StringBuilder report)
        {
            try
            {
                step();
                report.AppendLine("[OK] " + label);
                Debug.Log("[BuildAll][OK] " + label);
            }
            catch (System.Exception ex)
            {
                report.AppendLine("[FAIL] " + label + " — " + ex.Message);
                Debug.LogError("[BuildAll][FAIL] " + label + "\n" + ex);
            }
        }
    }
}
#endif
