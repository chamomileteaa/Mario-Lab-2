using System.Collections.Generic;
using UnityEngine;

public static class PrefabPoolService
{
    private static readonly Dictionary<int, PrefabPool> poolsByPrefabId = new Dictionary<int, PrefabPool>();
    private static bool autoCreatePools = true;
    private static int autoCreateMaxSize = 128;
    private static int autoCreatePrewarmCount = 2;
    private static Transform runtimePoolRoot;

    public static bool TryGetPool(GameObject prefab, out PrefabPool pool)
    {
        pool = null;
        return prefab && poolsByPrefabId.TryGetValue(prefab.GetInstanceID(), out pool) && pool;
    }

    public static PrefabPool GetOrCreatePool(GameObject prefab)
    {
        if (!prefab) return null;
        if (TryGetPool(prefab, out var existingPool)) return existingPool;
        if (!autoCreatePools) return null;

        return CreateRuntimePool(prefab);
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!prefab) return null;

        var pool = GetOrCreatePool(prefab);
        return pool
            ? pool.Spawn(position, rotation, parent)
            : Object.Instantiate(prefab, position, rotation, parent);
    }

    public static bool Release(GameObject instance)
    {
        if (!instance) return false;
        var pooled = instance.GetComponent<PooledObject>();
        return pooled && pooled.ReleaseToPool();
    }

    public static void Despawn(GameObject instance)
    {
        if (!instance) return;
        if (Release(instance)) return;
        Object.Destroy(instance);
    }

    public static void Despawn(Component instance)
    {
        if (!instance) return;
        Despawn(instance.gameObject);
    }

    public static void ConfigureAutoCreate(bool enabled = true, int defaultMaxSize = 128, int defaultPrewarmCount = 2)
    {
        autoCreatePools = enabled;
        autoCreateMaxSize = Mathf.Max(1, defaultMaxSize);
        autoCreatePrewarmCount = Mathf.Max(0, defaultPrewarmCount);
    }

    internal static void Register(PrefabPool pool)
    {
        if (!pool || !pool.Prefab) return;

        var key = pool.Prefab.GetInstanceID();
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
        autoCreatePools = true;
        autoCreateMaxSize = 128;
        autoCreatePrewarmCount = 2;
        runtimePoolRoot = null;
    }

    private static PrefabPool CreateRuntimePool(GameObject prefab)
    {
        var poolObject = new GameObject(prefab.name + "_Pool");
        poolObject.transform.SetParent(GetRuntimeRoot(), false);

        var pool = poolObject.AddComponent<PrefabPool>();
        pool.Configure(prefab, autoCreatePrewarmCount, autoCreateMaxSize, poolObject.transform);
        return pool;
    }

    private static Transform GetRuntimeRoot()
    {
        if (runtimePoolRoot) return runtimePoolRoot;

        var rootObject = new GameObject("[Runtime Prefab Pools]");
        Object.DontDestroyOnLoad(rootObject);
        runtimePoolRoot = rootObject.transform;
        return runtimePoolRoot;
    }
}
