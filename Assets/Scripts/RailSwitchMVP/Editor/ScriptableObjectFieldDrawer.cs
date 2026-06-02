using UnityEditor;
using UnityEngine;

namespace RailSwitchMVP.EditorTools
{
    /// <summary>
    /// Drawer GLOBAL para qualquer campo que referencie um ScriptableObject.
    /// Desenha o object field normal + um botão "Open" ao lado que abre as
    /// properties do asset numa janela, sem precisar caçá-lo no Project e
    /// selecioná-lo. Poupa cliques ao editar configs que apontam pra SOs
    /// (ex.: DifficultyTier.hazardPool/powerUpPool/mysteryBoxPool, refs de
    /// RailGenConfig/DifficultyConfig/ReviveConfig, etc.).
    ///
    /// Registrado com useForChildren=true → vale pra TODA subclasse de
    /// ScriptableObject, em qualquer Inspector (e dentro de drawers que usam
    /// EditorGUI.PropertyField, como o DifficultyTierDrawer). O botão só aparece
    /// quando há um asset atribuído.
    ///
    /// Observação: no Control Panel (janela Odin) os object fields são desenhados
    /// pelo próprio Odin, então este botão pode não aparecer lá — mas no Control
    /// Panel os pools/configs já são itens diretos da árvore (1 clique).
    /// </summary>
    [CustomPropertyDrawer(typeof(ScriptableObject), true)]
    public class ScriptableObjectFieldDrawer : PropertyDrawer
    {
        const float ButtonWidth = 46f;
        const float Gap = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Só tratamos referências de objeto. Qualquer outro caso cai no default.
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            bool hasValue = property.objectReferenceValue != null;
            float fieldWidth = hasValue ? position.width - ButtonWidth - Gap : position.width;

            var fieldRect = new Rect(position.x, position.y, fieldWidth, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(fieldRect, property, label, true);

            if (hasValue)
            {
                var btnRect = new Rect(position.x + fieldWidth + Gap, position.y,
                    ButtonWidth, EditorGUIUtility.singleLineHeight);
                var content = new GUIContent("Open", "Abrir as properties deste asset numa janela");
                if (GUI.Button(btnRect, content))
                    OpenInspector(property.objectReferenceValue);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Object reference é sempre uma linha; o botão fica na mesma linha.
            return EditorGUIUtility.singleLineHeight;
        }

        static void OpenInspector(Object obj)
        {
            if (obj == null) return;

#if ODIN_INSPECTOR
            // Odin é o padrão do projeto: abre uma janela inspecionando o asset.
            Sirenix.OdinInspector.Editor.OdinEditorWindow.InspectObject(obj);
#else
            // Fallback nativo: Property Editor flutuante (Unity 2021.2+), via reflection
            // pra não quebrar build se a API mudar. Último recurso: ping no Project.
            var open = typeof(EditorUtility).GetMethod("OpenPropertyEditor",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(Object) }, null);
            if (open != null) open.Invoke(null, new object[] { obj });
            else { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
#endif
        }
    }
}
