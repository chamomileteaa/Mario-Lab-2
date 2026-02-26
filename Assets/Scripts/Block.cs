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
    private float nextHitTime;
    private bool isUsed;
    private bool isHidden;
    private bool initialTriggerState;

    private float multiEndTime = -1f;
    private SpriteRenderer spriteRenderer;
    private Coroutine bumpRoutine;
    private Vector3 spriteBaseLocalPosition;
    [SerializeField, HideInInspector] private float spriteBaseAlpha = 1f;
    [SerializeField, HideInInspector] private bool hasSpriteBaseAlpha;
    private bool editorPreviewActive;
    private readonly Collider2D[] bumpHits = new Collider2D[12];
    private readonly HashSet<int> bumpNotifiedIds = new HashSet<int>();

    private BoxCollider2D BoxCollider => boxCollider2D ? boxCollider2D : boxCollider2D = GetComponent<BoxCollider2D>();
    private SpriteRenderer Sprite => spriteRenderer ? spriteRenderer : spriteRenderer = ResolveSpriteRenderer();
    private Transform BumpTarget => Sprite ? Sprite.transform : null;
    private Vector3 SpawnPosition => BoxCollider.bounds.center;

    private void Awake()
    {
        NormalizeByKind();
        initialTriggerState = BoxCollider.isTrigger;
        CacheSpriteBaseAlpha();
        ResetContentState();

        isHidden = startsHidden;
        RefreshVisualState();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        NormalizeByKind();
        CacheSpriteBaseAlpha();
        RefreshVisualState();
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
        if (!collider.TryGetComponentInParent(out MarioController mario)) return false;

        var body = collider.attachedRigidbody ? collider.attachedRigidbody : mario.GetComponent<Rigidbody2D>();
        if (!body) return false;
        if (!CanHeadbuttFromBelow(body, collider.bounds, requireUpward)) return false;

        Hit(mario);
        return true;
    }

    private bool CanHeadbuttFromBelow(Rigidbody2D body, Bounds marioBounds, bool requireUpward)
    {
        if (!body) return false;

        var blockBounds = BoxCollider.bounds;
        var verticalSpeed = body.linearVelocity.y;
        if (requireUpward && verticalSpeed <= 0.01f) return false;

        const float sideInset = 0.02f;
        if (marioBounds.center.x <= blockBounds.min.x + sideInset) return false;
        if (marioBounds.center.x >= blockBounds.max.x - sideInset) return false;

        const float minHeadGap = -0.3f;
        const float maxHeadGap = 0.3f;
        var headGap = blockBounds.min.y - marioBounds.max.y;
        if (headGap < minHeadGap) return false;
        if (headGap > maxHeadGap) return false;

        if (verticalSpeed > 0.01f) return true;
        return !requireUpward && marioBounds.max.y >= blockBounds.min.y - 0.04f;
    }

    private void Hit(MarioController mario)
    {
        if (Time.time < nextHitTime) return;
        nextHitTime = Time.time + hitCooldown;

        if (isHidden) Reveal();
        StartBump();
        NotifyBumpReactives(mario);

        if (TryDispense())
            return;

        if (kind == BlockKind.Brick && CanBreak(mario))
        {
            Break();
            return;
        }

        if (kind == BlockKind.Question && !HasContentLeft && depletionOutcome == DepletionOutcome.Exhausted)
            SetEmpty();
    }

    private void Reveal()
    {
        isHidden = false;
        RefreshVisualState();
    }

    private bool TryDispense()
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
                if (Time.time >= multiEndTime)
                {
                    DepleteContent();
                    return true;
                }

                SpawnContent();
                return true;

            default:
                return false;
        }
    }

    private bool HasContentLeft
    {
        get
        {
            if (!HasSpawnableContent) return false;

            return contentType switch
            {
                BlockContent.Single => true,
                BlockContent.Multi => multiEndTime < 0f || Time.time <= multiEndTime,
                _ => false
            };
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

        RefreshVisualState();
    }

    private void Break()
    {
        try
        {
            if (breakParticles)
            {
                PrefabPoolService.Spawn(breakParticles.gameObject, BoxCollider.bounds.center, Quaternion.identity);
            }
        }
        finally
        {
            if (bumpRoutine != null)
            {
                StopCoroutine(bumpRoutine);
                bumpRoutine = null;
            }

            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer) renderer.enabled = false;

            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
                if (collider) collider.enabled = false;

            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void RefreshVisualState()
    {
        var sprite = Sprite;
        if (sprite)
        {
            if (isUsed && usedSprite) sprite.sprite = usedSprite;

            if (Application.isPlaying)
            {
                SetSpriteAlpha(spriteBaseAlpha);
                sprite.enabled = !isHidden;
                editorPreviewActive = false;
            }
            else
            {
                sprite.enabled = true;
                var previewAlpha = startsHidden ? spriteBaseAlpha * hiddenEditorOpacity : spriteBaseAlpha;
                SetSpriteAlpha(previewAlpha);
                editorPreviewActive = startsHidden;
            }
        }

        BoxCollider.isTrigger = Application.isPlaying
            ? isHidden
            : initialTriggerState;
    }

    private void StartBump()
    {
        var bumpTarget = BumpTarget;
        if (!bumpTarget) return;

        if (bumpRoutine != null) StopCoroutine(bumpRoutine);
        spriteBaseLocalPosition = bumpTarget.localPosition;
        bumpRoutine = StartCoroutine(BumpSprite(bumpTarget));
    }

    private void NotifyBumpReactives(MarioController mario)
    {
        var bounds = BoxCollider.bounds;
        var center = new Vector2(bounds.center.x, bounds.max.y + bumpReactionHeight + bumpReactionSize.y * 0.5f);
        var context = new BlockBumpContext(this, mario, center, Vector2.up);

        if (bumpReactionSize.x > 0f && bumpReactionSize.y > 0f)
        {
            bumpNotifiedIds.Clear();

            var filter = new ContactFilter2D();
            filter.useTriggers = true;
            var hitCount = Physics2D.OverlapBox(center, bumpReactionSize, 0f, filter, bumpHits);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = bumpHits[i];
                bumpHits[i] = null;
                if (!hit) continue;
                if (IsOwnCollider(hit)) continue;
                NotifyReactiveBehaviours(hit, context);
            }
        }

        Bumped?.Invoke(context);
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

        if (spriteTransform) spriteTransform.localPosition = spriteBaseLocalPosition;
        bumpRoutine = null;
    }

    private void NotifyReactiveBehaviours(Collider2D hit, BlockBumpContext context)
    {
        var behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (!behaviour || behaviour == this) continue;
            if (behaviour is not IBlockBumpReactive reactive) continue;

            var behaviourId = behaviour.GetInstanceID();
            if (!bumpNotifiedIds.Add(behaviourId)) continue;
            reactive.OnBlockBumped(context);
        }
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        if (!collider) return false;
        var owner = collider.GetComponentInParent<Block>();
        return owner && owner == this;
    }

    private SpriteRenderer ResolveSpriteRenderer()
    {
        foreach (var childRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (childRenderer && childRenderer.transform != transform)
                return childRenderer;

        return GetComponent<SpriteRenderer>();
    }

    private void CacheSpriteBaseAlpha()
    {
        var sprite = Sprite;
        if (!sprite) return;
        if (!hasSpriteBaseAlpha)
        {
            spriteBaseAlpha = sprite.color.a <= 0f ? 1f : sprite.color.a;
            hasSpriteBaseAlpha = true;
            return;
        }

        if (Application.isPlaying) return;
        if (startsHidden) return;
        if (editorPreviewActive) return;

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

    private void ResetContentState()
    {
        multiEndTime = -1f;
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

    private void NormalizeByKind()
    {
        if (kind != BlockKind.Brick)
        {
            depletionOutcome = DepletionOutcome.Exhausted;
            breakRule = BreakRule.Never;
        }

        if (kind == BlockKind.Solid)
        {
            contentType = BlockContent.None;
            contentPrefab = null;
            multiEndTime = -1f;
        }
    }
}
