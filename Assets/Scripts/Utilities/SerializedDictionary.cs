using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializedDictionary<TKey, TValue> : ISerializationCallbackReceiver, IEnumerable<KeyValuePair<TKey, TValue>>
{
    [SerializeField] private List<TKey> keys = new List<TKey>();
    [SerializeField] private List<TValue> values = new List<TValue>();

    private readonly Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();

    public int Count => dictionary.Count;
    public IEnumerable<TKey> Keys => dictionary.Keys;
    public IEnumerable<TValue> Values => dictionary.Values;

    public TValue this[TKey key]
    {
        get => dictionary[key];
        set => dictionary[key] = value;
    }

    public bool TryGetValue(TKey key, out TValue value) => dictionary.TryGetValue(key, out value);
    public bool ContainsKey(TKey key) => dictionary.ContainsKey(key);
    public void Set(TKey key, TValue value) => dictionary[key] = value;

    public bool Remove(TKey key) => dictionary.Remove(key);
    public void Clear() => dictionary.Clear();

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => dictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (var pair in dictionary)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        dictionary.Clear();
        var count = Math.Min(keys.Count, values.Count);
        for (var i = 0; i < count; i++)
        {
            var key = keys[i];
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = values[i];
                continue;
            }

            dictionary.Add(key, values[i]);
        }
    }
}

[Serializable]
public class SerializedEnumDictionary<TEnum, TValue> : SerializedDictionary<TEnum, TValue>
    where TEnum : struct, Enum
{
}
