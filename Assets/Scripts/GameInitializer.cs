using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameInitializer
{
    private static readonly HashSet<int> prewarmedPrefabIds = new HashSet<int>();
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeOnLoad()
    {
        Initialize();
    }

    private static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        PrefabPoolService.ConfigureAutoCreate(true, 128, 2);
        SceneManager.sceneLoaded += OnSceneLoaded;
        PrewarmScenePools();
    }

    public static void PrewarmPool(GameObject prefab)
    {
        Initialize();
        if (!prefab) return;

        var prefabId = prefab.GetInstanceID();
        if (!prewarmedPrefabIds.Add(prefabId)) return;
        PrefabPoolService.GetOrCreatePool(prefab);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PrewarmScenePools();
    }

    private static void PrewarmScenePools()
    {
        var goombas = Object.FindObjectsByType<GoombaController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < goombas.Length; i++)
            PrewarmPool(goombas[i].ScorePopupPrefab);

        var brickCoins = Object.FindObjectsByType<BrickCoin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < brickCoins.Length; i++)
            PrewarmPool(brickCoins[i].ScorePopupPrefab);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (initialized) SceneManager.sceneLoaded -= OnSceneLoaded;
        initialized = false;
        prewarmedPrefabIds.Clear();
    }
}
