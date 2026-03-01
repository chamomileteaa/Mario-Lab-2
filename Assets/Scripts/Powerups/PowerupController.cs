using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioPlayer))]
public class PowerupController : MonoBehaviour
{
    [Header("Audio")]
    [FormerlySerializedAs("spawnClip")]
    [SerializeField] private AudioClip spawnSfx;

    [Header("Collect Popup")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Rise")]
    [SerializeField, Min(0f)] private float riseDistance = 1f;
    [SerializeField, Min(0.01f)] private float riseDuration = 0.35f;
    [SerializeField] private int sortingOrderBehindBlock = 1;
    [SerializeField] private bool disableCollidersWhileRising = true;

    private Rigidbody2D body2D;
    private EntityController entityController;
    private AudioPlayer audioPlayer;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D[] ownColliders;
    private SortingState[] sortingStates;
    private bool[] colliderStates;

    private Coroutine riseRoutine;
    private float cachedGravityScale;
    private bool brickSpawnPrepared;

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private EntityController Entity => entityController ? entityController : entityController = GetComponent<EntityController>();
    private AudioPlayer Audio => audioPlayer ? audioPlayer : audioPlayer = GetComponent<AudioPlayer>();
    private SpriteRenderer[] Sprites => spriteRenderers != null && spriteRenderers.Length > 0
        ? spriteRenderers
        : spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    private Collider2D[] Colliders => ownColliders != null && ownColliders.Length > 0
        ? ownColliders
        : ownColliders = GetComponentsInChildren<Collider2D>(true);

    private void OnDisable()
    {
        brickSpawnPrepared = false;
        CancelRiseAndRestore();
    }

    public void PrepareBrickSpawn()
    {
        brickSpawnPrepared = true;
    }

    public void BeginRising(SpriteRenderer blockRenderer)
    {
        if (!brickSpawnPrepared) return;
        brickSpawnPrepared = false;

        if (riseRoutine != null)
            StopCoroutine(riseRoutine);

        if (spawnSfx) Audio?.PlayOneShot(spawnSfx);
        riseRoutine = StartCoroutine(RiseRoutine(blockRenderer));
    }

    public void ShowCollectScorePopup(int value)
    {
        if (value <= 0 || !scorePopupPrefab) return;
        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(value, worldPosition);
    }

    public void ShowCollectLabelPopup(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || !scorePopupPrefab) return;
        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.ShowLabel(label, worldPosition);
    }

    private IEnumerator RiseRoutine(SpriteRenderer blockRenderer)
    {
        CacheSortingState();
        ApplyBehindBlockSorting(blockRenderer);
        SetMovementPaused(true);
        SetBodyStateForRising(true);
        CacheColliderState();
        SetCollidersEnabled(!disableCollidersWhileRising);

        var startPosition = transform.position;
        var targetPosition = startPosition + Vector3.up * riseDistance;
        var elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / riseDuration);
            var position = Vector3.Lerp(startPosition, targetPosition, t);
            SetWorldPosition(position);
            yield return null;
        }

        SetWorldPosition(targetPosition);
        RestoreColliderState();
        SetBodyStateForRising(false);
        SetMovementPaused(false);
        RestoreSortingState();
        riseRoutine = null;
    }

    private void CancelRiseAndRestore()
    {
        if (riseRoutine != null)
        {
            StopCoroutine(riseRoutine);
            riseRoutine = null;
        }

        RestoreColliderState();
        SetBodyStateForRising(false);
        SetMovementPaused(false);
        RestoreSortingState();
    }

    private void SetWorldPosition(Vector3 worldPosition)
    {
        if (Body)
            Body.position = (Vector2)worldPosition;
        else
            transform.position = worldPosition;
    }

    private void SetBodyStateForRising(bool rising)
    {
        if (!Body) return;

        if (rising)
        {
            cachedGravityScale = Body.gravityScale;
            Body.linearVelocity = Vector2.zero;
            Body.gravityScale = 0f;
            return;
        }

        Body.gravityScale = cachedGravityScale;
    }

    private void SetMovementPaused(bool paused)
    {
        if (!Entity) return;

        if (paused)
        {
            Entity.SetMovementEnabled(false);
            return;
        }

        Entity.SetMovementEnabled(true);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        var colliders = Colliders;
        for (var i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider) collider.enabled = enabled;
        }
    }

    private void CacheColliderState()
    {
        var colliders = Colliders;
        colliderStates = new bool[colliders.Length];

        for (var i = 0; i < colliders.Length; i++)
            colliderStates[i] = colliders[i] && colliders[i].enabled;
    }

    private void RestoreColliderState()
    {
        var colliders = Colliders;
        if (colliderStates == null || colliderStates.Length != colliders.Length) return;

        for (var i = 0; i < colliders.Length; i++)
        {
            var collider = colliders[i];
            if (collider) collider.enabled = colliderStates[i];
        }
    }

    private void ApplyBehindBlockSorting(SpriteRenderer blockRenderer)
    {
        if (!blockRenderer) return;

        var sprites = Sprites;
        var behindOrder = blockRenderer.sortingOrder - Mathf.Abs(sortingOrderBehindBlock);
        for (var i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            if (!sprite) continue;
            sprite.sortingLayerID = blockRenderer.sortingLayerID;
            sprite.sortingOrder = behindOrder;
        }
    }

    private void CacheSortingState()
    {
        var sprites = Sprites;
        sortingStates = new SortingState[sprites.Length];

        for (var i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            sortingStates[i] = new SortingState
            {
                Renderer = sprite,
                SortingLayerId = sprite ? sprite.sortingLayerID : 0,
                SortingOrder = sprite ? sprite.sortingOrder : 0
            };
        }
    }

    private void RestoreSortingState()
    {
        if (sortingStates == null || sortingStates.Length == 0) return;

        for (var i = 0; i < sortingStates.Length; i++)
        {
            var state = sortingStates[i];
            if (!state.Renderer) continue;
            state.Renderer.sortingLayerID = state.SortingLayerId;
            state.Renderer.sortingOrder = state.SortingOrder;
        }
    }

    private struct SortingState
    {
        public SpriteRenderer Renderer;
        public int SortingLayerId;
        public int SortingOrder;
    }
}
