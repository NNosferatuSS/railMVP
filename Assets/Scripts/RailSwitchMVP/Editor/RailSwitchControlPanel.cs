using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Painel central de parâmetros do jogo — Tools → RailSwitch → Control Panel.
    ///
    /// OdinMenuEditorWindow: árvore/menu à esquerda, inspector Odin do item
    /// selecionado à direita. Agrega os ScriptableObjects de config num lugar só
    /// pra ver/identificar/alterar fácil, sem caçar assets pela pasta. Como a janela
    /// usa o sistema de desenho do Odin, ela é independente do Inspector normal
    /// (os custom editors/drawers existentes continuam intocados).
    ///
    /// v1: lista os configs (Difficulty, Generation, Revive, Pools). Tabela curada de
    /// tiers (v2) e Play Tools/ações de runtime (v3) vêm depois.
    /// </summary>
    public class RailSwitchControlPanel : OdinMenuEditorWindow
    {
        [MenuItem("Tools/RailSwitch/Control Panel")]
        private static void Open()
        {
            var window = GetWindow<RailSwitchControlPanel>();
            window.titleContent = new GUIContent("RailSwitch");
            window.minSize = new Vector2(880f, 520f);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree(supportsMultiSelect: false);
            tree.Config.DrawSearchToolbar = true;

            // Configs principais (1 asset cada).
            AddFirst<DifficultyConfig>(tree, "Difficulty");
            AddFirst<RailGenConfig>(tree, "Generation");
            AddFirst<ReviveConfig>(tree, "Revive");

            // Pools (vários assets — um item por asset).
            AddAll<HazardPool>(tree, "Pools/Hazards");
            AddAll<PowerUpPool>(tree, "Pools/Power-ups");

            return tree;
        }

        // ============ Helpers ============

        private static void AddFirst<T>(OdinMenuTree tree, string path) where T : ScriptableObject
        {
            var asset = LoadAll<T>().FirstOrDefault();
            if (asset != null) tree.Add(path, asset);
        }

        private static void AddAll<T>(OdinMenuTree tree, string basePath) where T : ScriptableObject
        {
            foreach (var asset in LoadAll<T>())
                tree.Add(basePath + "/" + asset.name, asset);
        }

        private static IEnumerable<T> LoadAll<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null);
        }
    }
}
