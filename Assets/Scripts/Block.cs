using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class Block : MonoBehaviour
{
    public static event Action<BlockBumpContext> Bumped;

    public enum BlockKind
    {
        Brick,
        Question,
        Solid
    }

    public enum BreakRule
    {
        Never,
        BigOnly,
        Always
    }

    public enum BlockContent
    {
        None,
        Single,
        Multi
    }

    public enum DepletionOutcome
    {
        Break = 0,
        Exhausted = 1
    }

    [Header("Type")]
    [SerializeField] private BlockKind kind = BlockKind.Brick;

    [Header("Content")]
    [SerializeField, ConditionalField(nameof(kind), (int)BlockKind.Solid, true)] private BlockContent contentType = BlockContent.None;
    [SerializeField, ConditionalField(nameof(contentType), (int)BlockContent.None, true)] private GameObject contentPrefab;
    [SerializeField, ConditionalField(nameof(contentType), (int)BlockContent.Multi), Min(0.1f)] private float multiDuration = 5f;

    [SerializeField, ConditionalField(nameof(kind), (int)BlockKind.Brick)] private DepletionOutcome depletionOutcome = DepletionOutcome.Exhausted;
    [SerializeField, ConditionalField(nameof(depletionOutcome), (int)DepletionOutcome.Break)] private BreakRule breakRule = BreakRule.BigOnly;

    [Header("State")]
    [SerializeField] private bool startsHidden;
    [SerializeField, Range(0.05f, 1f)] private float hiddenEditorOpacity = 0.35f;

    [Header("Visuals")]
    [SerializeField] private Sprite usedSprite;

    [Header("Spawns")]
    [SerializeField] private ParticleSystem breakParticles;
    [SerializeField, Min(0f)] private float hitCooldown = 0.05f;
    [SerializeField, Min(0f)] private float bumpHeight = 0.5f;
    [SerializeField, Min(0.01f)] private float bumpDuration = 0.12f;
    [SerializeField, Min(0.05f)] private Vector2 bumpReactionSize = new Vector2(0.95f, 0.5f);
    [SerializeField, Min(0f)] private float bumpReactionHeight = 0.04f;

    private BoxCollider2D boxCollider2D;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] ownColliders = Array.Empty<Collider2D>();
    private readonly HashSet<int> ownColliderIds = new HashSet<int>();
    private readonly Collider2D[] bumpHits = new Collider2D[12];
    private readonly HashSet<int> bumpNotifiedIds = new HashSet<int>();
    private readonly Dictionary<int, IBlockBumpHandler[]> bumpHandlersByColliderId = new Dictionary<int, IBlockBumpHandler[]>(32);
    private readonly Dictionary<int, MarioController> marioByColliderId = new Dictionary<int, MarioController>(8);

    private float nextHitTime;
    private float multiEndTime = -1f;
    private bool isUsed;
    private bool isHidden;
    private bool initialTriggerState;
    private float spriteBaseAlpha = 1f;
    private Coroutine bumpRoutine;
    private Vector3 spriteBaseLocalPosition;

    private BoxCollider2D BoxCollider => boxCollider2D ? boxCollider2D : boxCollider2D = GetComponent<BoxCollider2D>();
    private SpriteRenderer Sprite => spriteRenderer ? spriteRenderer : spriteRenderer = ResolveSpriteRenderer();
    private Vector3 SpawnPosition => BoxCollider.bounds.center;

    private void Awake()
    {
        NormalizeByKind();
        initialTriggerState = BoxCollider.isTrigger;
        RebuildOwnColliderCache();
        CacheSpriteAlpha();

        multiEndTime = -1f;
        isHidden = startsHidden;
        ApplyVisualState();
    }

    private void OnEnable()
    {
        RebuildOwnColliderCache();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        NormalizeByKind();
        CacheSpriteAlpha();
        ApplyVisualState();
    }

    private void OnDisable()
    {
        bumpHandlersByColliderId.Clear();
        marioByColliderId.Clear();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryHitFromCollider(collision.collider, false)) return;
        TryHitFromCollider(collision.otherCollider, false);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (TryHitFromCollider(collision.collider, false)) return;
        TryHitFromCollider(collision.otherCollider, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isHidden) return;
        TryHitFromCollider(other, true);
    }

    private bool TryHitFromCollider(Collider2D collider, bool requireUpward)
    {
        if (!collider) return false;
        if (!collider.CompareColliderTag("Player")) return false;
        if (!TryResolveMario(collider, out var mario, out var body)) return false;
        if (!CanHeadbuttFromBelow(body, collider.bounds, requireUpward)) return false;

        HandleHit(mario);
        return true;
    }

    private bool TryResolveMario(Collider2D collider, out MarioController mario, out Rigidbody2D body)
    {
        mario = null;
        body = null;

        var colliderId = collider.GetInstanceID();
        if (marioByColliderId.TryGetValue(colliderId, out mario) && mario)
        {
            body = mario.GetComponent<Rigidbody2D>();
            return body;
        }

        body = collider.attachedRigidbody;
        if (!body) return false;

        if (!body.TryGetComponent(out mario) && !collider.TryGetComponentInParent(out mario))
            return false;

        marioByColliderId[colliderId] = mario;
        return true;
    }

    private bool CanHeadbuttFromBelow(Rigidbody2D body, Bounds marioBounds, bool requireUpward)
    {
        var verticalSpeed = body.linearVelocity.y;
        if (requireUpward && verticalSpeed <= 0.01f) return false;

        var blockBounds = BoxCollider.bounds;
        const float sideInset = 0.02f;
        if (marioBounds.center.x <= blockBounds.min.x + sideInset) return false;
        if (marioBounds.center.x >= blockBounds.max.x - sideInset) return false;

        const float minHeadGap = -0.3f;
        const float maxHeadGap = 0.3f;
        var headGap = blockBounds.min.y - marioBounds.max.y;
        if (headGap < minHeadGap || headGap > maxHeadGap) return false;

        if (verticalSpeed > 0.01f) return true;
        return !requireUpward && marioBounds.max.y >= blockBounds.min.y - 0.04f;
    }

    private void HandleHit(MarioController mario)
    {
        if (Time.time < nextHitTime) return;
        nextHitTime = Time.time + hitCooldown;

        if (isHidden)
        {
            isHidden = false;
            ApplyVisualState();
        }

        StartBump();
        NotifyBumpHandlers(mario);

        if (TryDispenseContent()) return;

        if (kind == BlockKind.Brick && CanBreak(mario))
        {
            Break();
            return;
        }

        if (kind == BlockKind.Question && depletionOutcome == DepletionOutcome.Exhausted)
            SetEmpty();
    }

    private bool TryDispenseContent()
    {
        if (!HasSpawnableContent) return false;

        switch (contentType)
        {
            case BlockContent.Single:
                SpawnContent();
                DepleteContent();
                return true;

            case BlockContent.Multi:
                if (multiEndTime < 0f)
                    multiEndTime = Time.time + multiDuration;

                if (Time.time <= multiEndTime)
                {
                    SpawnContent();
                    return true;
                }

                DepleteContent();
                return true;

            default:
                return false;
        }
    }

    private bool HasSpawnableContent => contentType != BlockContent.None && contentPrefab;

    private bool CanBreak(MarioController mario)
    {
        if (kind != BlockKind.Brick) return false;

        return breakRule switch
        {
            BreakRule.Always => true,
            BreakRule.BigOnly => mario && mario.Form != MarioController.MarioForm.Small,
            _ => false
        };
    }

    private void SetEmpty()
    {
        if (isUsed) return;

        isUsed = true;
        kind = BlockKind.Solid;
        contentType = BlockContent.None;
        multiEndTime = -1f;
        ApplyVisualState();
    }

    private void DepleteContent()
    {
        contentType = BlockContent.None;
        multiEndTime = -1f;

        if (kind == BlockKind.Brick && depletionOutcome == DepletionOutcome.Break)
        {
            Break();
            return;
        }

        SetEmpty();
    }

    private void Break()
    {
        if (breakParticles)
            PrefabPoolService.Spawn(breakParticles.gameObject, BoxCollider.bounds.center, Quaternion.identity);

        if (bumpRoutine != null)
        {
            StopCoroutine(bumpRoutine);
            bumpRoutine = null;
        }

        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer) renderer.enabled = false;

        foreach (var collider in ownColliders)
            if (collider) collider.enabled = false;

        enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void ApplyVisualState()
    {
        var sprite = Sprite;
        if (sprite)
        {
            if (isUsed && usedSprite) sprite.sprite = usedSprite;

            if (Application.isPlaying)
            {
                sprite.enabled = !isHidden;
                SetSpriteAlpha(spriteBaseAlpha);
            }
            else
            {
                sprite.enabled = true;
                var previewAlpha = startsHidden ? spriteBaseAlpha * hiddenEditorOpacity : spriteBaseAlpha;
                SetSpriteAlpha(previewAlpha);
            }
        }

        BoxCollider.isTrigger = Application.isPlaying ? isHidden : initialTriggerState;
    }

    private void StartBump()
    {
        var sprite = Sprite;
        if (!sprite) return;

        if (bumpRoutine != null)
            StopCoroutine(bumpRoutine);

        var spriteTransform = sprite.transform;
        spriteBaseLocalPosition = spriteTransform.localPosition;
        bumpRoutine = StartCoroutine(BumpSprite(spriteTransform));
    }

    private void NotifyBumpHandlers(MarioController mario)
    {
        var bounds = BoxCollider.bounds;
        var center = new Vector2(bounds.center.x, bounds.max.y + bumpReactionHeight + bumpReactionSize.y * 0.5f);
        var context = new BlockBumpContext(this, mario, center, Vector2.up);

        if (bumpReactionSize.x > 0f && bumpReactionSize.y > 0f)
        {
            bumpNotifiedIds.Clear();
            var filter = new ContactFilter2D { useTriggers = true };
            var hitCount = Physics2D.OverlapBox(center, bumpReactionSize, 0f, filter, bumpHits);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = bumpHits[i];
                bumpHits[i] = null;
                if (!hit || IsOwnCollider(hit)) continue;
                NotifyHandlersForCollider(hit, in context);
            }
        }

        Bumped?.Invoke(context);
    }

    private void NotifyHandlersForCollider(Collider2D hit, in BlockBumpContext context)
    {
        var handlers = GetCachedBumpHandlers(hit);
        for (var i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is not MonoBehaviour behaviour || !behaviour || behaviour == this) continue;
            if (!bumpNotifiedIds.Add(behaviour.GetInstanceID())) continue;
            handlers[i].HandleBlockBump(in context);
        }
    }

    private IBlockBumpHandler[] GetCachedBumpHandlers(Collider2D hit)
    {
        var colliderId = hit.GetInstanceID();
        if (bumpHandlersByColliderId.TryGetValue(colliderId, out var cachedHandlers))
            return cachedHandlers;

        var behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        var handlers = new List<IBlockBumpHandler>(2);
        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBlockBumpHandler handler)
                handlers.Add(handler);
        }

        cachedHandlers = handlers.Count == 0 ? Array.Empty<IBlockBumpHandler>() : handlers.ToArray();
        bumpHandlersByColliderId[colliderId] = cachedHandlers;
        return cachedHandlers;
    }

    private IEnumerator BumpSprite(Transform spriteTransform)
    {
        var elapsed = 0f;
        while (elapsed < bumpDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / bumpDuration);
            var y = Mathf.Sin(t * Mathf.PI) * bumpHeight;
            spriteTransform.localPosition = spriteBaseLocalPosition + Vector3.up * y;
            yield return null;
        }

        if (spriteTransform)
            spriteTransform.localPosition = spriteBaseLocalPosition;

        bumpRoutine = null;
    }

    private void RebuildOwnColliderCache()
    {
        ownColliders = GetComponentsInChildren<Collider2D>(true);
        ownColliderIds.Clear();
        for (var i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i])
                ownColliderIds.Add(ownColliders[i].GetInstanceID());
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && ownColliderIds.Contains(collider.GetInstanceID());
    }

    private SpriteRenderer ResolveSpriteRenderer()
    {
        var childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < childRenderers.Length; i++)
        {
            var childRenderer = childRenderers[i];
            if (childRenderer && childRenderer.transform != transform)
                return childRenderer;
        }

        return GetComponent<SpriteRenderer>();
    }

    private void CacheSpriteAlpha()
    {
        var sprite = Sprite;
        if (!sprite)
        {
            spriteBaseAlpha = 1f;
            return;
        }

        spriteBaseAlpha = sprite.color.a <= 0f ? 1f : sprite.color.a;
    }

    private void SetSpriteAlpha(float alpha)
    {
        var sprite = Sprite;
        if (!sprite) return;

        var color = sprite.color;
        color.a = alpha;
        sprite.color = color;
    }

    private void SpawnContent()
    {
        PrefabPoolService.Spawn(contentPrefab, SpawnPosition, Quaternion.identity);
    }

    private void NormalizeByKind()
    {
        if (kind != BlockKind.Brick)
        {
            depletionOutcome = DepletionOutcome.Exhausted;
            breakRule = BreakRule.Never;
        }

        if (kind != BlockKind.Solid) return;
        contentType = BlockContent.None;
        contentPrefab = null;
        multiEndTime = -1f;
    }
}
