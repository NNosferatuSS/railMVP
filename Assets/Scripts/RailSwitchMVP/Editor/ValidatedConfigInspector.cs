using UnityEditor;
using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Inspector base que renderiza um HelpBox de warnings no topo
    /// (vindo de IValidatedConfig.GetValidationWarnings) antes do
    /// inspector default. Subclasses só precisam declarar [CustomEditor].
    ///
    /// Warnings != errors — são heurísticas (chance > 0 sem pool, ranges
    /// invertidos, etc). Asset continua salvando normalmente.
    /// </summary>
    public class ValidatedConfigInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (target is IValidatedConfig validated)
            {
                string warnings = validated.GetValidationWarnings();
                if (!string.IsNullOrEmpty(warnings))
                {
                    EditorGUILayout.HelpBox(warnings, MessageType.Warning);
                    EditorGUILayout.Space(2);
                }
            }
            DrawDefaultInspector();

            // Descrição do que cada hazard/power-up do pool faz (Inspector normal).
            // No Control Panel/Odin o mesmo texto aparece via [OnInspectorGUI].
            string info = null;
            if (target is HazardPool hp) info = hp.GetEntriesInfo();
            else if (target is PowerUpPool pp) info = pp.GetEntriesInfo();
            if (!string.IsNullOrEmpty(info))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(info, MessageType.Info);
            }
        }
    }

    [CustomEditor(typeof(DifficultyConfig))]
    public class DifficultyConfigEditor : ValidatedConfigInspector { }

    [CustomEditor(typeof(RailGenConfig))]
    public class RailGenConfigEditor : ValidatedConfigInspector { }

    [CustomEditor(typeof(HazardPool))]
    public class HazardPoolEditor : ValidatedConfigInspector { }

    [CustomEditor(typeof(PowerUpPool))]
    public class PowerUpPoolEditor : ValidatedConfigInspector { }
}
