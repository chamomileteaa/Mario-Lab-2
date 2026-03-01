using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class FireworksController : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private GameObject fireworksPrefab;

    [Header("Pattern")]
    [SerializeField] private Vector3[] spawnOffsets =
    {
        new Vector3(0f, 4.0f, 0f),
        new Vector3(-1.1f, 4.8f, 0f),
        new Vector3(1.1f, 5.2f, 0f),
        new Vector3(-0.6f, 4.3f, 0f),
        new Vector3(0.6f, 4.6f, 0f),
        new Vector3(-1.5f, 5.1f, 0f),
        new Vector3(1.5f, 4.9f, 0f),
        new Vector3(-0.2f, 5.5f, 0f),
        new Vector3(0.2f, 5.7f, 0f)
    };
    [SerializeField, Min(0.05f)] private float spawnInterval = 0.55f;

    private Coroutine routine;

    private void Awake()
    {
        if (!fireworksPrefab)
        {
            var config = FindFirstObjectByType<PoolPrewarmConfig>(FindObjectsInactive.Include);
            if (config) fireworksPrefab = config.FireworksPrefab;
        }
    }

    public void PlayForScore(int score)
    {
        // Score no longer controls firework count.
        Play(7);
    }

    public void Play(int count)
    {
        if (count <= 0 || !fireworksPrefab) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayRoutine(count));
    }

    private IEnumerator PlayRoutine(int count)
    {
        var delay = Mathf.Max(0.05f, spawnInterval);
        var patternLength = spawnOffsets != null && spawnOffsets.Length > 0 ? spawnOffsets.Length : 1;

        for (var i = 0; i < count; i++)
        {
            var offset = spawnOffsets != null && spawnOffsets.Length > 0
                ? spawnOffsets[i % patternLength]
                : Vector3.zero;
            var worldPosition = transform.position + offset;
            var spawned = PrefabPoolService.Spawn(fireworksPrefab, worldPosition, Quaternion.identity);
            if (spawned && !spawned.TryGetComponent<FireworkController>(out _))
                Debug.LogError($"Fireworks prefab '{fireworksPrefab.name}' must include FireworkController.", fireworksPrefab);
            yield return new WaitForSeconds(delay);
        }

        routine = null;
    }
}
