using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SerializedDictionary<,>), true)]
public class SerializedDictionaryDrawer : PropertyDrawer
{
    private const float RowSpacing = 2f;
    private const float ColumnSpacing = 4f;
    private const float RemoveButtonWidth = 24f;
    private const float AddButtonHeight = 20f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var keys = property.FindPropertyRelative("keys");
        var values = property.FindPropertyRelative("values");
        if (keys == null || values == null)
        {
            EditorGUI.LabelField(position, label.text, "Invalid SerializedDictionary");
            return;
        }

        EnsureArraySizes(keys, values);

        var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (!property.isExpanded) return;

        var y = foldoutRect.yMax + RowSpacing;

        for (var i = 0; i < keys.arraySize; i++)
        {
            var keyProp = keys.GetArrayElementAtIndex(i);
            var valueProp = values.GetArrayElementAtIndex(i);

            var keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
            var valueHeight = EditorGUI.GetPropertyHeight(valueProp, true);
            var rowHeight = Mathf.Max(keyHeight, valueHeight, EditorGUIUtility.singleLineHeight);

            var rowRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, rowHeight));
            var keyWidth = (rowRect.width - RemoveButtonWidth - (ColumnSpacing * 2f)) * 0.45f;
            var valueWidth = rowRect.width - RemoveButtonWidth - (ColumnSpacing * 2f) - keyWidth;

            var keyRect = new Rect(rowRect.x, rowRect.y, keyWidth, rowHeight);
            var valueRect = new Rect(keyRect.xMax + ColumnSpacing, rowRect.y, valueWidth, rowHeight);
            var removeRect = new Rect(valueRect.xMax + ColumnSpacing, rowRect.y, RemoveButtonWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);

            if (GUI.Button(removeRect, "-"))
            {
                keys.DeleteArrayElementAtIndex(i);
                values.DeleteArrayElementAtIndex(i);
                break;
            }

            y += rowHeight + RowSpacing;
        }

        var addRect = EditorGUI.IndentedRect(new Rect(position.x, y, position.width, AddButtonHeight));
        if (GUI.Button(addRect, "+ Add Entry"))
        {
            var insertIndex = keys.arraySize;
            keys.InsertArrayElementAtIndex(keys.arraySize);
            values.InsertArrayElementAtIndex(values.arraySize);
            AssignUniqueEnumKey(keys, insertIndex);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;

        var keys = property.FindPropertyRelative("keys");
        var values = property.FindPropertyRelative("values");
        if (keys == null || values == null) return height;

        EnsureArraySizes(keys, values);
        height += RowSpacing;

        for (var i = 0; i < keys.arraySize; i++)
        {
            var keyProp = keys.GetArrayElementAtIndex(i);
            var valueProp = values.GetArrayElementAtIndex(i);
            var keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
            var valueHeight = EditorGUI.GetPropertyHeight(valueProp, true);
            height += Mathf.Max(keyHeight, valueHeight, EditorGUIUtility.singleLineHeight) + RowSpacing;
        }

        height += AddButtonHeight;
        return height;
    }

    private static void EnsureArraySizes(SerializedProperty keys, SerializedProperty values)
    {
        while (values.arraySize < keys.arraySize)
            values.InsertArrayElementAtIndex(values.arraySize);
        while (keys.arraySize < values.arraySize)
            keys.InsertArrayElementAtIndex(keys.arraySize);
    }

    private static void AssignUniqueEnumKey(SerializedProperty keys, int addedIndex)
    {
        if (addedIndex < 0 || addedIndex >= keys.arraySize) return;

        var addedKey = keys.GetArrayElementAtIndex(addedIndex);
        if (addedKey.propertyType != SerializedPropertyType.Enum) return;

        var used = new bool[addedKey.enumDisplayNames.Length];
        for (var i = 0; i < keys.arraySize; i++)
        {
            if (i == addedIndex) continue;
            var key = keys.GetArrayElementAtIndex(i);
            if (key.propertyType != SerializedPropertyType.Enum) continue;
            if (key.enumValueIndex < 0 || key.enumValueIndex >= used.Length) continue;
            used[key.enumValueIndex] = true;
        }

        for (var i = 0; i < used.Length; i++)
        {
            if (used[i]) continue;
            addedKey.enumValueIndex = i;
            return;
        }
    }
}
