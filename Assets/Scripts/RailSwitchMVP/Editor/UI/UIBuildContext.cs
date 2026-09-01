#if UNITY_EDITOR
using UnityEditor;

namespace RailSwitchMVP.EditorTools.UI
{
    /// <summary>
    /// Estado compartilhado da pipeline de build de UI.
    ///
    /// Cada wizard mostra um EditorUtility.DisplayDialog no fim, o que é útil
    /// quando rodado sozinho pelo menu mas vira uma fila de modais quando a
    /// pipeline inteira roda automatizada. <see cref="Quiet"/> suprime esses
    /// diálogos — erros e warnings continuam indo pro Console normalmente.
    /// </summary>
    public static class UIBuildContext
    {
        public static bool Quiet;

        /// <summary>DisplayDialog que respeita o modo Quiet.</summary>
        public static void Info(string title, string message)
        {
            if (Quiet) { UnityEngine.Debug.Log($"[{title}] {message}"); return; }
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
#endif
