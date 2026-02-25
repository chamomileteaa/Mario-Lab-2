#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MaxAttribute))]
public class MaxAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var maxValue = ((MaxAttribute)attribute).max;

        EditorGUI.BeginProperty(position, label, property);
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                property.floatValue = Mathf.Min(maxValue, EditorGUI.FloatField(position, label, property.floatValue));
                break;
            case SerializedPropertyType.Integer:
                property.intValue = Mathf.Min(Mathf.FloorToInt(maxValue), EditorGUI.IntField(position, label, property.intValue));
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = ClampMax(EditorGUI.Vector2Field(position, label, property.vector2Value), maxValue);
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = ClampMax(EditorGUI.Vector3Field(position, label, property.vector3Value), maxValue);
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = ClampMax(EditorGUI.Vector4Field(position, label, property.vector4Value), maxValue);
                break;
            case SerializedPropertyType.Vector2Int:
                property.vector2IntValue = ClampMax(EditorGUI.Vector2IntField(position, label, property.vector2IntValue), maxValue);
                break;
            case SerializedPropertyType.Vector3Int:
                property.vector3IntValue = ClampMax(EditorGUI.Vector3IntField(position, label, property.vector3IntValue), maxValue);
                break;
            default:
                EditorGUI.PropertyField(position, property, label, true);
                break;
        }

        EditorGUI.EndProperty();
    }

    private static Vector2 ClampMax(Vector2 value, float maxValue)
    {
        value.x = Mathf.Min(maxValue, value.x);
        value.y = Mathf.Min(maxValue, value.y);
        return value;
    }

    private static Vector3 ClampMax(Vector3 value, float maxValue)
    {
        value.x = Mathf.Min(maxValue, value.x);
        value.y = Mathf.Min(maxValue, value.y);
        value.z = Mathf.Min(maxValue, value.z);
        return value;
    }

    private static Vector4 ClampMax(Vector4 value, float maxValue)
    {
        value.x = Mathf.Min(maxValue, value.x);
        value.y = Mathf.Min(maxValue, value.y);
        value.z = Mathf.Min(maxValue, value.z);
        value.w = Mathf.Min(maxValue, value.w);
        return value;
    }

    private static Vector2Int ClampMax(Vector2Int value, float maxValue)
    {
        var maxInt = Mathf.FloorToInt(maxValue);
        value.x = Mathf.Min(maxInt, value.x);
        value.y = Mathf.Min(maxInt, value.y);
        return value;
    }

    private static Vector3Int ClampMax(Vector3Int value, float maxValue)
    {
        var maxInt = Mathf.FloorToInt(maxValue);
        value.x = Mathf.Min(maxInt, value.x);
        value.y = Mathf.Min(maxInt, value.y);
        value.z = Mathf.Min(maxInt, value.z);
        return value;
    }
}
#endif
