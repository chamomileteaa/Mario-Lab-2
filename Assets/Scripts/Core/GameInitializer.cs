using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameInitializer
{
    private sealed class CoroutineHost : MonoBehaviour
    {
        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);
    }

    private const string HostObjectName = "[Game Initializer]";
    private const bool DefaultUseAsyncPrewarm = true;
    private const int DefaultPoolPrewarmCount = 2;
    private const int DefaultPoolCreatesPerFrame = 1;

    private static readonly HashSet<int> prewarmedPrefabIds = new HashSet<int>();

    private static bool initialized;
    private static bool useAsyncPrewarm = DefaultUseAsyncPrewarm;
    private static int poolPrewarmCount = DefaultPoolPrewarmCount;
    private static int poolCreatesPerFrame = DefaultPoolCreatesPerFrame;
    private static CoroutineHost host;
    private static Coroutine scenePrewarmRoutine;
    private static PoolPrewarmConfig prewarmConfig;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeOnLoad() => Initialize();

    private static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        ApplyPoolDefaults();
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScheduleScenePoolPrewarm();
    }

    public static void ConfigurePrewarm(bool enableAsync, int targetPoolPrewarmCount = DefaultPoolPrewarmCount, int targetPoolCreatesPerFrame = DefaultPoolCreatesPerFrame)
    {
        useAsyncPrewarm = enableAsync;
        poolPrewarmCount = Mathf.Max(0, targetPoolPrewarmCount);
        poolCreatesPerFrame = Mathf.Max(1, targetPoolCreatesPerFrame);

        if (!initialized) return;
        ApplyPoolDefaults();
    }

    public static void PrewarmPool(GameObject prefab)
    {
        Initialize();
        if (!prefab) return;

        var prefabId = prefab.GetInstanceID();
        if (!prewarmedPrefabIds.Add(prefabId)) return;
        StartPrewarm(prefab);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleScenePoolPrewarm();
    }

    private static void ScheduleScenePoolPrewarm()
    {
        if (scenePrewarmRoutine != null)
            Host.StopCoroutine(scenePrewarmRoutine);

        scenePrewarmRoutine = Host.Run(PrewarmScenePoolsNextFrame());
    }

    private static IEnumerator PrewarmScenePoolsNextFrame()
    {
        // Defer prewarm work so first rendered frame is not blocked.
        yield return null;

        var config = ResolvePrewarmConfig();
        if (!config)
        {
            scenePrewarmRoutine = null;
            yield break;
        }

        PrewarmPool(config.ScorePopupPrefab);

        // Split work across frames to reduce startup spikes.
        yield return null;

        PrewarmPool(config.FireballPrefab);

        yield return null;

        PrewarmPool(config.FireworksPrefab);

        scenePrewarmRoutine = null;
    }

    private static PoolPrewarmConfig ResolvePrewarmConfig()
    {
        prewarmConfig ??= Object.FindFirstObjectByType<PoolPrewarmConfig>(FindObjectsInactive.Include);
        return prewarmConfig;
    }

    private static void StartPrewarm(GameObject prefab)
    {
        if (!useAsyncPrewarm)
        {
            var pool = PrefabPoolService.GetOrCreatePool(prefab);
            if (poolPrewarmCount > 0) pool?.Prewarm(poolPrewarmCount);
            return;
        }

        Host.Run(PrefabPoolService.PrewarmRoutine(prefab, poolPrewarmCount, poolCreatesPerFrame));
    }

    private static CoroutineHost Host
    {
        get
        {
            if (host) return host;

            var hostObject = new GameObject(HostObjectName);
            Object.DontDestroyOnLoad(hostObject);
            host = hostObject.AddComponent<CoroutineHost>();
            return host;
        }
    }

    private static void ApplyPoolDefaults()
    {
        var autoPrewarmCount = useAsyncPrewarm ? 0 : poolPrewarmCount;
        PrefabPoolService.ConfigureAutoCreate(true, 128, autoPrewarmCount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (host) Object.Destroy(host.gameObject);
        host = null;
        scenePrewarmRoutine = null;
        prewarmConfig = null;

        if (initialized) SceneManager.sceneLoaded -= OnSceneLoaded;
        initialized = false;
        useAsyncPrewarm = DefaultUseAsyncPrewarm;
        poolPrewarmCount = DefaultPoolPrewarmCount;
        poolCreatesPerFrame = DefaultPoolCreatesPerFrame;
        prewarmedPrefabIds.Clear();
    }
}
