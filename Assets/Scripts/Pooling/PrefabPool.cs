using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PrefabPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField, Min(0)] private int prewarmCount;
    [SerializeField, Min(1)] private int maxSize = 128;
    [SerializeField] private Transform inactiveContainer;

    private readonly Stack<PooledObject> inactive = new Stack<PooledObject>();
    private int activeCount;

    public GameObject Prefab => prefab;
    public int ActiveCount => activeCount;
    public int InactiveCount => inactive.Count;

    private Transform InactiveContainer => inactiveContainer ? inactiveContainer : transform;

    public void Configure(GameObject targetPrefab, int targetPrewarmCount = 0, int targetMaxSize = 128, Transform targetInactiveContainer = null)
    {
        var previousPrefab = prefab;
        if (previousPrefab && targetPrefab && previousPrefab != targetPrefab)
        {
            Debug.LogWarning($"PrefabPool '{name}' is already bound to '{previousPrefab.name}'. Create a new pool for '{targetPrefab.name}'.", this);
            return;
        }

        prefab = targetPrefab;
        prewarmCount = Mathf.Max(0, targetPrewarmCount);
        maxSize = Mathf.Max(1, targetMaxSize);
        inactiveContainer = targetInactiveContainer;

        if (!isActiveAndEnabled) return;
        PrefabPoolService.Register(this);
        Prewarm(prewarmCount);
    }

    private void OnEnable()
    {
        PrefabPoolService.Register(this);
        Prewarm(prewarmCount);
    }

    private void OnDisable()
    {
        PrefabPoolService.Unregister(this);
    }

    public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (!prefab) return null;

        var pooled = PopOrCreate();
        if (!pooled) return null;

        pooled.SetInPool(false);
        activeCount++;

        var instance = pooled.gameObject;
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public bool Release(GameObject instance)
    {
        if (!instance) return false;
        return Release(instance.GetComponent<PooledObject>());
    }

    public bool Release(PooledObject pooled)
    {
        if (!pooled) return false;
        if (pooled.OwnerPool != this) return false;
        if (pooled.IsInPool) return false;

        if (inactive.Count >= maxSize)
        {
            if (!pooled.HandleReleaseFailure()) return false;
            activeCount = Mathf.Max(0, activeCount - 1);
            return true;
        }

        var instance = pooled.gameObject;
        pooled.SetInPool(true);
        instance.SetActive(false);
        instance.transform.SetParent(InactiveContainer, false);
        inactive.Push(pooled);

        activeCount = Mathf.Max(0, activeCount - 1);
        return true;
    }

    public void Prewarm(int count)
    {
        if (!prefab || count <= 0) return;
        if (count <= inactive.Count) return;

        var needed = Mathf.Min(count - inactive.Count, Mathf.Max(0, maxSize - inactive.Count));
        for (var i = 0; i < needed; i++) inactive.Push(CreateInstance());
    }

    public void ClearInactive()
    {
        while (inactive.Count > 0)
        {
            var pooled = inactive.Pop();
            if (pooled) Destroy(pooled.gameObject);
        }
    }

    private PooledObject PopOrCreate()
    {
        while (inactive.Count > 0)
        {
            var pooled = inactive.Pop();
            if (pooled) return pooled;
        }

        return CreateInstance();
    }

    private PooledObject CreateInstance()
    {
        if (!prefab) return null;

        var instance = Instantiate(prefab, InactiveContainer);
        instance.name = prefab.name;
        instance.SetActive(false);

        var pooled = instance.GetComponent<PooledObject>();
        if (!pooled) pooled = instance.AddComponent<PooledObject>();
        pooled.Bind(this);
        pooled.SetInPool(true);
        return pooled;
    }
}
