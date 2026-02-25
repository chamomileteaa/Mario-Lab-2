using System.Collections.Generic;
using UnityEngine;

public static class PrefabPoolLocator
{
    private static readonly Dictionary<int, PrefabPool> poolsByPrefabId = new Dictionary<int, PrefabPool>();

    public static bool TryGet(GameObject prefab, out PrefabPool pool)
    {
        pool = null;
        if (!prefab) return false;
        return poolsByPrefabId.TryGetValue(prefab.GetInstanceID(), out pool) && pool;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!prefab) return null;
        return TryGet(prefab, out var pool)
            ? pool.Spawn(position, rotation, parent)
            : Object.Instantiate(prefab, position, rotation, parent);
    }

    public static bool Release(GameObject instance)
    {
        if (!instance) return false;
        var pooled = instance.GetComponent<PooledObject>();
        return pooled && pooled.ReleaseToPool();
    }

    internal static void Register(PrefabPool pool)
    {
        if (!pool || !pool.Prefab) return;

        var key = pool.Prefab.GetInstanceID();
        if (poolsByPrefabId.TryGetValue(key, out var existing) && existing && existing != pool)
            Debug.LogWarning($"Duplicate pool for prefab '{pool.Prefab.name}'. Using newest registration.", pool);

        poolsByPrefabId[key] = pool;
    }

    internal static void Unregister(PrefabPool pool)
    {
        if (!pool || !pool.Prefab) return;

        var key = pool.Prefab.GetInstanceID();
        if (poolsByPrefabId.TryGetValue(key, out var existing) && existing == pool)
            poolsByPrefabId.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        poolsByPrefabId.Clear();
    }
}
