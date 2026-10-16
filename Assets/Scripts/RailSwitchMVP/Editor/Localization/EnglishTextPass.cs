using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Passe de tradução PT → EN dos textos JÁ SERIALIZADOS (prefabs de UI e cenas).
    /// O código-fonte (controllers e wizards de UI) já foi traduzido; isto alinha os
    /// valores que ficaram gravados nos assets. Varre TODAS as propriedades string de
    /// todos os componentes (TMP_Text.m_text e [SerializeField] string) e troca por
    /// correspondência exata. Reaplicável.
    ///
    /// Menu: Tools → RailSwitch → Localization → Apply English text pass.
    /// </summary>
    public static class EnglishTextPass
    {
        static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "Colete 100 moedas", "Collect 100 coins" },
            { "Complete todas pra abrir", "Complete all to unlock" },
            { "VOCÊ", "YOU" },
            { "Sem recorde ainda", "No record yet" },
            { "EDITAR NOME", "EDIT NAME" },
            { "Música", "Music" },
            { "Efeitos", "Effects" },
            { "Vibração", "Vibration" },
            { "Modo canhoto", "Left-handed mode" },
            { "Respawn no caminho seguro", "Respawn on the safe path" },
            { "Ninguém jogou o desafio de hoje ainda. Seja o primeiro!", "No one has played today's challenge yet. Be the first!" },
            { "Dia 1 de 7", "Day 1 of 7" },
            { "Comprar X por Y coins?", "Buy X for Y coins?" },
            { "Carregando...", "Loading..." },
            { "Baú Grátis +150", "Free Chest +150" },
            { "Baú Grátis", "Free Chest" },
            { "R$ 4,90", "$0.99" },
            { "R$ 9,90", "$1.99" },
            { "Nome muito curto (mínimo {min}).", "Name too short (min {min})." },
            { "Nome muito longo (máximo {max}).", "Name too long (max {max})." },
            { "Digite um nome.", "Enter a name." },
            { "Continuar (Ad)", "Continue (Ad)" },
            { "Recordes Globais — Distância", "Global Records — Distance" },
            { "Sem recorde ainda. Jogue uma run!", "No record yet. Play a run!" },
            { "Reclamar", "Claim" },
            { "Reclamado", "Claimed" },
            { "Equipado", "Equipped" },
            { "Equipar", "Equip" },
            { "Comprar", "Buy" },
            { "BEM-VINDO!\n\nToque na ESQUERDA ou DIREITA da tela pra trocar de trilho.\n\nEvite obstáculos e colete moedas.",
              "WELCOME!\n\nTap LEFT or RIGHT on the screen to switch tracks.\n\nAvoid obstacles and collect coins." },
            { "BEM-VINDO!\n\nUse as setas ← → (ou A/D) pra trocar de trilho.\n\nEvite obstáculos e colete moedas.",
              "WELCOME!\n\nUse the ← → arrows (or A/D) to switch tracks.\n\nAvoid obstacles and collect coins." },
        };

        static readonly string[] PrefabRoots = { "Assets/UI", "Assets/Prefabs" };
        static readonly string[] Scenes = { "Assets/Scenes/HomeScene.unity", "Assets/Scenes/RailSwitchMVP.unity" };

        [MenuItem("Tools/RailSwitch/Localization/Apply English text pass", priority = 400)]
        public static void Apply()
        {
            var log = new StringBuilder("[EnglishTextPass]\n");
            int total = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabRoots))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int n = ReplaceIn(root.GetComponentsInChildren<Component>(true), log, path);
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                total += n;
            }

            foreach (var scenePath in Scenes)
            {
                var s = SceneManager.GetSceneByPath(scenePath);
                bool wasOpen = s.IsValid() && s.isLoaded;
                if (!wasOpen) s = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var comps = s.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
                int n = ReplaceIn(comps, log, scenePath);
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(s);
                    EditorSceneManager.SaveScene(s);
                }
                if (!wasOpen && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(s, true);
                total += n;
            }

            AssetDatabase.SaveAssets();
            log.AppendLine($"TOTAL: {total} textos traduzidos.");
            Debug.Log(log.ToString());
        }

        static int ReplaceIn(Component[] comps, StringBuilder log, string where)
        {
            int n = 0;
            foreach (var c in comps)
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.String) continue;
                    var v = it.stringValue;
                    if (string.IsNullOrEmpty(v)) continue;
                    var key = v.Replace("\r\n", "\n");
                    if (!Map.TryGetValue(key, out var en))
                    {
                        // Tutorial V1 (texto livre editado no Inspector): troca pelo equivalente.
                        if (key.StartsWith("BEM-VINDO!"))
                            en = key.Contains("Toque")
                                ? "WELCOME!\n\nTap LEFT or RIGHT on the screen to switch tracks.\n\nAvoid obstacles and collect coins."
                                : "WELCOME!\n\nUse the ← → arrows (or A/D) to switch tracks.\n\nAvoid obstacles and collect coins.";
                        else continue;
                    }
                    it.stringValue = en;
                    changed = true;
                    n++;
                    log.AppendLine($"  {where} :: {c.gameObject.name}.{it.propertyPath}: \"{Short(v)}\" → \"{Short(en)}\"");
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            return n;
        }

        static string Short(string s) => s.Length > 40 ? s.Substring(0, 40).Replace("\n", " ") + "…" : s.Replace("\n", " ");

        public static void BatchApply()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
