using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RailSwitchMVP.Config;

namespace RailSwitchMVP.EditorTools
{
    [CustomPropertyDrawer(typeof(DifficultyTier))]
    public class DifficultyTierDrawer : PropertyDrawer
    {
        static readonly Dictionary<string, bool> _foldouts = new();

        // cameraTilt/cameraFieldOfView só valem com overrideCameraAngle ligado — escondemos
        // quando off (espelha o [ShowIf] do Odin no Control Panel).
        static bool IsCameraOverrideField(string name)
            => name == "cameraTilt" || name == "cameraFieldOfView";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!_foldouts.TryGetValue(property.propertyPath, out bool expanded) || !expanded)
                return EditorGUIUtility.singleLineHeight;

            bool hideCam = !property.FindPropertyRelative("overrideCameraAngle").boolValue;
            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var child = property.Copy();
            var end = property.GetEndProperty();
            child.NextVisible(true);
            while (!SerializedProperty.EqualContents(child, end))
            {
                if (!(hideCam && IsCameraOverrideField(child.name)))
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                child.NextVisible(false);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var distProp     = property.FindPropertyRelative("triggerAtDistance");
            var speedProp    = property.FindPropertyRelative("playerSpeed");
            var minRowProp   = property.FindPropertyRelative("minLanesPerRow");
            var maxRowProp   = property.FindPropertyRelative("maxLanesPerRow");
            var zoomProp     = property.FindPropertyRelative("cameraZoom");
            var overrideProp = property.FindPropertyRelative("overrideCameraAngle");
            var fovProp      = property.FindPropertyRelative("cameraFieldOfView");

            // "Element 0" → "Tier 0 — 0 m | speed 6 | 2-3/row | zoom 12 | FOV 70"
            // (FOV só entra no header quando o tier sobrescreve o ângulo da câmera.)
            string tierLabel = label.text.Replace("Element", "Tier")
                + $"  —  {distProp.floatValue:0} m"
                + $"  |  speed {speedProp.floatValue:0.#}"
                + $"  |  {minRowProp.intValue}-{maxRowProp.intValue}/row"
                + $"  |  zoom {zoomProp.floatValue:0.#}"
                + (overrideProp.boolValue ? $"  |  FOV {fovProp.floatValue:0}" : "");

            string path = property.propertyPath;
            if (!_foldouts.TryGetValue(path, out bool expanded))
                expanded = false;

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            expanded = EditorGUI.Foldout(headerRect, expanded, tierLabel, true, EditorStyles.foldoutHeader);
            _foldouts[path] = expanded;

            if (!expanded) return;

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            bool hideCam = !overrideProp.boolValue;
            var child = property.Copy();
            var end = property.GetEndProperty();
            child.NextVisible(true);
            while (!SerializedProperty.EqualContents(child, end))
            {
                if (hideCam && IsCameraOverrideField(child.name))
                {
                    child.NextVisible(false);
                    continue;
                }
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                child.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }
    }
}
