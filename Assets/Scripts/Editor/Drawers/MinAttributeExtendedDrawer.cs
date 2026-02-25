#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MinAttribute))]
public class MinAttributeExtendedDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var minValue = ((MinAttribute)attribute).min;

        EditorGUI.BeginProperty(position, label, property);
        switch (property.propertyType)
        {
            case SerializedPropertyType.Float:
                property.floatValue = Mathf.Max(minValue, EditorGUI.FloatField(position, label, property.floatValue));
                break;
            case SerializedPropertyType.Integer:
                property.intValue = Mathf.Max(Mathf.CeilToInt(minValue), EditorGUI.IntField(position, label, property.intValue));
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = ClampMin(EditorGUI.Vector2Field(position, label, property.vector2Value), minValue);
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = ClampMin(EditorGUI.Vector3Field(position, label, property.vector3Value), minValue);
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = ClampMin(EditorGUI.Vector4Field(position, label, property.vector4Value), minValue);
                break;
            case SerializedPropertyType.Vector2Int:
                property.vector2IntValue = ClampMin(EditorGUI.Vector2IntField(position, label, property.vector2IntValue), minValue);
                break;
            case SerializedPropertyType.Vector3Int:
                property.vector3IntValue = ClampMin(EditorGUI.Vector3IntField(position, label, property.vector3IntValue), minValue);
                break;
            default:
                EditorGUI.PropertyField(position, property, label, true);
                break;
        }

        EditorGUI.EndProperty();
    }

    private static Vector2 ClampMin(Vector2 value, float minValue)
    {
        value.x = Mathf.Max(minValue, value.x);
        value.y = Mathf.Max(minValue, value.y);
        return value;
    }

    private static Vector3 ClampMin(Vector3 value, float minValue)
    {
        value.x = Mathf.Max(minValue, value.x);
        value.y = Mathf.Max(minValue, value.y);
        value.z = Mathf.Max(minValue, value.z);
        return value;
    }

    private static Vector4 ClampMin(Vector4 value, float minValue)
    {
        value.x = Mathf.Max(minValue, value.x);
        value.y = Mathf.Max(minValue, value.y);
        value.z = Mathf.Max(minValue, value.z);
        value.w = Mathf.Max(minValue, value.w);
        return value;
    }

    private static Vector2Int ClampMin(Vector2Int value, float minValue)
    {
        var minInt = Mathf.CeilToInt(minValue);
        value.x = Mathf.Max(minInt, value.x);
        value.y = Mathf.Max(minInt, value.y);
        return value;
    }

    private static Vector3Int ClampMin(Vector3Int value, float minValue)
    {
        var minInt = Mathf.CeilToInt(minValue);
        value.x = Mathf.Max(minInt, value.x);
        value.y = Mathf.Max(minInt, value.y);
        value.z = Mathf.Max(minInt, value.z);
        return value;
    }
}
#endif
