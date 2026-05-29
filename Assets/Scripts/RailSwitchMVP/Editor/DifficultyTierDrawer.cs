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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!_foldouts.TryGetValue(property.propertyPath, out bool expanded) || !expanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var child = property.Copy();
            var end = property.GetEndProperty();
            child.NextVisible(true);
            while (!SerializedProperty.EqualContents(child, end))
            {
                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                child.NextVisible(false);
            }
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var distProp    = property.FindPropertyRelative("triggerAtDistance");
            var speedProp   = property.FindPropertyRelative("playerSpeed");
            var minRowProp  = property.FindPropertyRelative("minLanesPerRow");
            var maxRowProp  = property.FindPropertyRelative("maxLanesPerRow");
            var popProp     = property.FindPropertyRelative("lanePopulationChance");

            // "Element 0" → "Tier 0 — 0 m | speed 6 | 3 lanes | 2-3/row | pop 60%"
            string tierLabel = label.text.Replace("Element", "Tier")
                + $"  —  {distProp.floatValue:0} m"
                + $"  |  speed {speedProp.floatValue:0.#}"
                + $"  |  {minRowProp.intValue}-{maxRowProp.intValue}/row"
                + $"  |  pop {popProp.floatValue * 100f:0}%";

            string path = property.propertyPath;
            if (!_foldouts.TryGetValue(path, out bool expanded))
                expanded = false;

            Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            expanded = EditorGUI.Foldout(headerRect, expanded, tierLabel, true, EditorStyles.foldoutHeader);
            _foldouts[path] = expanded;

            if (!expanded) return;

            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var child = property.Copy();
            var end = property.GetEndProperty();
            child.NextVisible(true);
            while (!SerializedProperty.EqualContents(child, end))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                child.NextVisible(false);
            }
            EditorGUI.indentLevel--;
        }
    }
}
