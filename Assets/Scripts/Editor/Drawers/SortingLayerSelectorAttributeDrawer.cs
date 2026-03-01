#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SortingLayerSelectorAttribute))]
public class SortingLayerSelectorAttributeDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var layers = SortingLayer.layers;
        if (layers == null || layers.Length == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var sortingLayerNames = new string[layers.Length];
        for (var i = 0; i < layers.Length; i++)
            sortingLayerNames[i] = layers[i].name;

        if (sortingLayerNames == null || sortingLayerNames.Length == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var currentIndex = Array.IndexOf(sortingLayerNames, property.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        EditorGUI.BeginProperty(position, label, property);
        var popupRect = EditorGUI.PrefixLabel(position, label);
        var nextIndex = EditorGUI.Popup(popupRect, currentIndex, sortingLayerNames);
        if (nextIndex >= 0 && nextIndex < sortingLayerNames.Length)
            property.stringValue = sortingLayerNames[nextIndex];
        EditorGUI.EndProperty();
    }
}
#endif
