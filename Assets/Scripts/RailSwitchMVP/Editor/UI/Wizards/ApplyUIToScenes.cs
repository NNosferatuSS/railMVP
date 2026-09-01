#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using RailSwitchMVP.Meta;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Reconstrói os prefabs e sincroniza as cenas: remove as instâncias antigas
    /// dos nossos canvases e coloca as novas.
    ///
    /// Por que é necessário: PrefabUtility.SaveAsPrefabAsset reconstrói o prefab
    /// do zero, então os fileIDs internos mudam e as instâncias já na cena ficam
    /// com referências órfãs (componentes soltos, campos null). Re-arrastar na
    /// mão a cada iteração é mecânico e fácil de esquecer.
    ///
    /// SÓ remove instâncias dos prefabs listados em <see cref="Map"/> — casa por
    /// nome OU por prefab de origem. Nada mais na cena é tocado: managers, player,
    /// câmeras, e o Canvas legado desativado ficam como estão.
    /// </summary>
    public static class ApplyUIToScenes
    {
        static readonly (string prefab, string scene)[] Map =
        {
            ("Assets/UI/Prefabs/HUD/Canvas_HUD.prefab",           SceneNames.Game),
            ("Assets/UI/Prefabs/Overlays/Canvas_Warmup.prefab",   SceneNames.Game),
            ("Assets/UI/Prefabs/Overlays/Canvas_GameOver.prefab", SceneNames.Game),
            ("Assets/UI/Prefabs/Overlays/Canvas_Pause.prefab",    SceneNames.Game),
            ("Assets/UI/Prefabs/Meta/Canvas_Meta.prefab",         SceneNames.Home),
        };

        [MenuItem("Tools/RailSwitch/Build UI/REBUILD + APPLY TO SCENES", priority = 1)]
        public static void RebuildAndApply() => Run(rebuild: true, includeAssets: false);

        [MenuItem("Tools/RailSwitch/Build UI/REBUILD (com sprites+fontes) + APPLY", priority = 2)]
        public static void FullRebuildAndApply() => Run(rebuild: true, includeAssets: true);

        [MenuItem("Tools/RailSwitch/Build UI/9. Apply to Scenes (sem rebuild)", priority = 90)]
        public static void ApplyOnly() => Run(rebuild: false, includeAssets: false);

        static void Run(bool rebuild, bool includeAssets)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Rail Switch UI",
                    "Saia do Play Mode antes de rodar isto.", "OK");
                return;
            }

            var sceneNames = Map.Select(m => m.scene).Distinct().ToArray();
            if (!EditorUtility.DisplayDialog("Rail Switch UI",
                (rebuild ? "1. Reconstrói os prefabs" + (includeAssets ? " (incluindo sprites e fontes)" : "") + "\n" : "") +
                $"{(rebuild ? "2" : "1")}. Abre e SALVA estas cenas:\n" +
                string.Join("\n", sceneNames.Select(s => "     • " + s)) + "\n\n" +
                "Em cada uma, remove as instâncias antigas dos canvases da UI nova\n" +
                "e coloca as atualizadas. Nada mais é tocado.\n\n" +
                "As cenas serão SALVAS. Commite antes se quiser poder desfazer.",
                "Rodar", "Cancelar")) return;

            // Não perde trabalho não salvo ao trocar de cena.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string originalScene = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();

            if (rebuild)
            {
                report.AppendLine("PREFABS");
                BuildAll.RunPipeline(report, includeAssets);
                report.AppendLine();
            }

            report.AppendLine("CENAS");
            foreach (var group in Map.GroupBy(m => m.scene))
            {
                string path = FindScenePath(group.Key);
                if (path == null)
                {
                    report.AppendLine($"[FAIL] {group.Key} — cena não encontrada no projeto");
                    Debug.LogError($"[ApplyUI] Cena '{group.Key}' não encontrada. Confira SceneNames.");
                    continue;
                }
                ApplyToScene(path, group.Select(g => g.prefab).ToArray(), report);
            }

            // Volta pra cena que estava aberta.
            if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            Debug.Log("[ApplyUI]\n" + report);
            EditorUtility.DisplayDialog("Rail Switch UI — Apply", report.ToString(), "OK");
        }

        static void ApplyToScene(string scenePath, string[] prefabPaths, StringBuilder report)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine($"— {scene.name}");

            var wantedNames = new HashSet<string>(
                prefabPaths.Select(Path.GetFileNameWithoutExtension));

            // 1. Remove instâncias antigas. Casa por nome OU por prefab de origem
            //    (cobre o caso do usuário ter renomeado a instância).
            int removed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!IsOurs(root, wantedNames, prefabPaths)) continue;
                Object.DestroyImmediate(root);
                removed++;
            }

            // 2. Instancia os atuais.
            int added = 0;
            foreach (var p in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null)
                {
                    report.AppendLine($"    [FAIL] prefab ausente: {p}");
                    Debug.LogError("[ApplyUI] Prefab ausente: " + p);
                    continue;
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.name = prefab.name;
                added++;
            }

            report.AppendLine($"    removidos {removed}, adicionados {added}");
            report.AppendLine("    " + DescribeEventSystems(scene));

            var legacy = FindDisabledCanvases(scene, wantedNames);
            if (legacy.Count > 0)
                report.AppendLine($"    (canvas legado desativado, intocado: {string.Join(", ", legacy)})");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool IsOurs(GameObject root, HashSet<string> names, string[] prefabPaths)
        {
            if (names.Contains(root.name)) return true;
            var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (src == null) return false;
            return System.Array.IndexOf(prefabPaths, AssetDatabase.GetAssetPath(src)) >= 0;
        }

        // Só REPORTA. Um EventSystem a mais (ou a menos) costuma ser configuração
        // deliberada do usuário — apagar por conta própria seria destrutivo, e
        // criar exigiria adivinhar o input module (o projeto usa o novo Input System).
        static string DescribeEventSystems(Scene scene)
        {
            var found = new List<EventSystem>();
            foreach (var root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<EventSystem>(true));

            if (found.Count == 1) return "EventSystem: OK (1)";
            if (found.Count == 0) return "EventSystem: NENHUM — botões não vão responder!";
            return $"EventSystem: {found.Count} encontrados — deixe apenas 1";
        }

        static List<string> FindDisabledCanvases(Scene scene, HashSet<string> ours)
        {
            var list = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.activeSelf || ours.Contains(root.name)) continue;
                if (root.GetComponentInChildren<Canvas>(true) != null) list.Add(root.name);
            }
            return list;
        }

        static string FindScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:Scene {sceneName}"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == sceneName) return p;
            }
            return null;
        }
    }
}
#endif
