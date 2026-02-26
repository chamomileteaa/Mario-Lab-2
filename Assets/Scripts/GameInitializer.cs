using System.Collections.Generic;
using UnityEngine;

public static class GameInitializer
{
    private static readonly HashSet<int> prewarmedPrefabIds = new HashSet<int>();

    public static void ShowScorePopup(GameObject popupPrefab, int score, Vector3 worldPosition)
    {
        if (!popupPrefab) return;

        EnsurePrefabPool(popupPrefab);
        var popupObject = PrefabPoolService.Spawn(popupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(score, worldPosition);
    }

    public static void EnsurePrefabPool(GameObject prefab)
    {
        if (!prefab) return;

        var prefabId = prefab.GetInstanceID();
        if (!prewarmedPrefabIds.Add(prefabId)) return;
        PrefabPoolService.GetOrCreatePool(prefab);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        prewarmedPrefabIds.Clear();
    }
}
