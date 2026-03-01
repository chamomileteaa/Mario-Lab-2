using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class Block : MonoBehaviour
{
    private const string PlayerTag = "Player";
    private const string RedMushroomTag = "RedMushroom";
    private const string FireFlowerTag = "FireFlower";
    private const string StarmanTag = "Starman";
    private const string OneUpMushroomTag = "OneUpMushroom";
    private const string TypeParameter = "Type";
    private const string IsDepletedParameter = "IsDepleted";
    private const string OverlayTimeFormat = "{0:0.#}";
    private static readonly int TypeParameterId = Animator.StringToHash(TypeParameter);
    private static readonly int IsDepletedParameterId = Animator.StringToHash(IsDepletedParameter);

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

    [Header("Overlay")]
    [SerializeField, FormerlySerializedAs("overlayRenderer"), FormerlySerializedAs("contentOverlayRenderer")]
    private SpriteRenderer overlayContent;
    [SerializeField, Min(0f), FormerlySerializedAs("overlayY"), FormerlySerializedAs("contentOverlayHeight")]
    private float overlayContentY = 0.6f;
    [SerializeField, FormerlySerializedAs("overlayTimeText"), FormerlySerializedAs("contentOverlayTimerText")]
    private TMP_Text overlayTime;
    [SerializeField, Min(0f), FormerlySerializedAs("overlayTimeY"), FormerlySerializedAs("contentOverlayTimerOffsetY")]
    private float overlayTimeOffsetY = 0.35f;
    [SerializeField, Range(0f, 1f), FormerlySerializedAs("overlayAlpha"), FormerlySerializedAs("contentOverlayAlpha")]
    private float overlayOpacity = 0.9f;

    [Header("Spawns")]
    [SerializeField] private ParticleSystem breakParticles;
    [SerializeField, Min(0f)] private float hitCooldown = 0.05f;
    [SerializeField, Min(0f)] private float bumpHeight = 0.5f;
    [SerializeField, Min(0.01f)] private float bumpDuration = 0.12f;
    [SerializeField, Min(0.05f)] private Vector2 bumpReactionSize = new Vector2(0.95f, 0.5f);
    [SerializeField, Min(0f)] private float bumpReactionHeight = 0.04f;

    private BoxCollider2D boxCollider2D;
    private SpriteRenderer spriteRenderer;
    private Animator animatorComponent;
    private readonly Collider2D[] bumpHits = new Collider2D[12];
    private readonly HashSet<int> bumpNotifiedIds = new HashSet<int>();

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
    private Animator Animator => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();
    private SpriteRenderer OverlayContent => overlayContent ? overlayContent : overlayContent = ResolveOverlayContent();
    private TMP_Text OverlayTime => overlayTime ? overlayTime : overlayTime = ResolveOverlayTime();
    private Vector3 SpawnPosition => BoxCollider.bounds.center;

    private void Awake()
    {
        NormalizeByKind();
        initialTriggerState = BoxCollider.isTrigger;
        CacheSpriteAlpha();

        multiEndTime = -1f;
        isHidden = startsHidden;
        ApplyVisualState();
    }

    private void OnEnable()
    {
        if (Application.isPlaying) return;
        CacheSpriteAlpha();
        ApplyVisualState();
        RefreshEditorPreviewSprite();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        NormalizeByKind();
        CacheSpriteAlpha();
        ApplyVisualState();
        RefreshEditorPreviewSprite();
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
        if (!collider.CompareColliderTag(PlayerTag)) return false;
        if (!TryResolveMario(collider, out var mario, out var body)) return false;
        if (!CanHeadbuttFromBelow(body, collider.bounds, requireUpward)) return false;

        HandleHit(mario);
        return true;
    }

    private bool TryResolveMario(Collider2D collider, out MarioController mario, out Rigidbody2D body)
    {
        mario = null;
        body = collider.attachedRigidbody;
        if (!body) return false;

        if (!body.TryGetComponent(out mario) && !collider.TryGetComponentInParent(out mario))
            return false;

        return true;
    }

    private bool CanHeadbuttFromBelow(Rigidbody2D body, Bounds marioBounds, bool requireUpward)
    {
        var verticalSpeed = body.linearVelocity.y;
        if (requireUpward && verticalSpeed <= 0.01f) return false;

        var blockBounds = BoxCollider.bounds;
        const float sideInset = 0.02f;
        var allowedMinX = blockBounds.min.x + sideInset;
        var allowedMaxX = blockBounds.max.x - sideInset;
        if (marioBounds.max.x <= allowedMinX) return false;
        if (marioBounds.min.x >= allowedMaxX) return false;

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

        foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            if (collider) collider.enabled = false;

        enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void ApplyVisualState()
    {
        SyncAnimatorState();

        var sprite = Sprite;
        var showMainSprite = !isHidden;

        if (sprite)
        {
            if (Application.isPlaying)
            {
                sprite.enabled = showMainSprite;
                SetSpriteAlpha(spriteBaseAlpha);
            }
            else
            {
                sprite.enabled = true;
                SetSpriteAlpha(startsHidden ? hiddenEditorOpacity : 1f);
            }
        }

        UpdateOverlay();
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
        var behaviours = hit.GetComponentsInParent<MonoBehaviour>(true);
        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IBlockBumpHandler handler) continue;
            if (behaviours[i] is not MonoBehaviour behaviour || !behaviour || behaviour == this) continue;
            if (!bumpNotifiedIds.Add(behaviour.GetInstanceID())) continue;
            handler.HandleBlockBump(in context);
        }
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

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && (collider == BoxCollider || collider.transform.IsChildOf(transform));
    }

    private SpriteRenderer ResolveSpriteRenderer()
    {
        var named = transform.Find("Sprite");
        if (named && named.TryGetComponent<SpriteRenderer>(out var namedRenderer))
            return namedRenderer;

        var childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < childRenderers.Length; i++)
        {
            var childRenderer = childRenderers[i];
            if (!childRenderer || childRenderer.transform == transform) continue;
            if (overlayContent && childRenderer == overlayContent) continue;
            if (string.Equals(childRenderer.gameObject.name, "Content", StringComparison.OrdinalIgnoreCase)) continue;
            if (childRenderer.transform.parent && string.Equals(childRenderer.transform.parent.name, "Overlay", StringComparison.OrdinalIgnoreCase)) continue;
                return childRenderer;
        }

        return GetComponent<SpriteRenderer>();
    }

    private SpriteRenderer ResolveOverlayContent()
    {
        var overlayRoot = transform.Find("Overlay");
        if (overlayRoot)
        {
            var named = overlayRoot.Find("Content");
            if (named && named.TryGetComponent<SpriteRenderer>(out var namedRenderer))
                return namedRenderer;

            var fromOverlay = overlayRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (fromOverlay) return fromOverlay;
        }

        var main = Sprite;
        var childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < childRenderers.Length; i++)
        {
            var childRenderer = childRenderers[i];
            if (!childRenderer || childRenderer == main) continue;
            return childRenderer;
        }

        return null;
    }

    private TMP_Text ResolveOverlayTime()
    {
        return GetComponentInChildren<TMP_Text>(true);
    }

    private void RefreshEditorPreviewSprite()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        var sprite = Sprite;
        var previewSprite = ResolveEditorPreviewSprite();
        if (sprite && previewSprite) sprite.sprite = previewSprite;
#endif
    }

    private void CacheSpriteAlpha()
    {
        var sprite = Sprite;
        if (!sprite)
        {
            spriteBaseAlpha = 1f;
            return;
        }

        var alpha = sprite.color.a;
        if (startsHidden && hiddenEditorOpacity > 0f)
            alpha /= hiddenEditorOpacity;

        spriteBaseAlpha = alpha <= 0f ? 1f : Mathf.Clamp01(alpha);
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
        var spawned = PrefabPoolService.Spawn(contentPrefab, SpawnPosition, Quaternion.identity);
        TryStartPowerupRise(spawned);
    }

    private void TryStartPowerupRise(GameObject spawned)
    {
        if (!spawned) return;
        if (!IsPowerupSpawn(spawned)) return;

        var riseController = spawned.GetComponent<PowerupController>();
        if (!riseController)
            riseController = spawned.AddComponent<PowerupController>();

        riseController.PrepareBrickSpawn();
        riseController.BeginRising(Sprite);
    }

    private static bool IsPowerupSpawn(GameObject spawned)
    {
        if (!spawned) return false;

        return spawned.CompareTag(RedMushroomTag) ||
               spawned.CompareTag(FireFlowerTag) ||
               spawned.CompareTag(StarmanTag) ||
               spawned.CompareTag(OneUpMushroomTag);
    }

    private void SyncAnimatorState()
    {
        if (!TryGetUsableAnimator(out var animator)) return;

        animator.SetFloat(TypeParameterId, (float)kind);
        animator.SetBool(IsDepletedParameterId, isUsed);
    }

    private void UpdateOverlay()
    {
        var show = !Application.isPlaying && !isUsed && contentType != BlockContent.None;
        var icon = show ? GetOverlaySprite() : null;
        var content = OverlayContent;

        if (content)
        {
            content.sprite = icon;
            content.enabled = show && icon;
            if (content.enabled)
            {
                content.transform.localPosition = new Vector3(0f, overlayContentY, 0f);
                var color = content.color;
                color.a = Mathf.Clamp01(overlayOpacity);
                content.color = color;
                var main = Sprite;
                if (main)
                {
                    content.sortingLayerID = main.sortingLayerID;
                    content.sortingOrder = main.sortingOrder + 1;
                }
            }
        }

        var timer = OverlayTime;
        if (!timer) return;

        var showTime = show && contentType == BlockContent.Multi;
        timer.enabled = showTime;
        if (!showTime)
        {
            timer.SetText(string.Empty);
            return;
        }

        if (timer.rectTransform)
        {
            var rect = timer.rectTransform;
            var anchored = rect.anchoredPosition;
            anchored.y = overlayTimeOffsetY;
            rect.anchoredPosition = anchored;
        }
        else
        {
            timer.transform.localPosition = Vector3.up * overlayTimeOffsetY;
        }

        var textColor = timer.color;
        textColor.a = Mathf.Clamp01(overlayOpacity);
        timer.color = textColor;
        timer.SetText(OverlayTimeFormat, multiDuration);
    }

    private Sprite GetOverlaySprite()
    {
        if (contentType == BlockContent.None || !contentPrefab) return null;

        if (contentPrefab.TryGetComponent(out SpriteRenderer root) && root.sprite)
            return root.sprite;

        var child = contentPrefab.GetComponentInChildren<SpriteRenderer>(true);
        return child ? child.sprite : null;
    }

    private bool TryGetUsableAnimator(out Animator animator)
    {
        animator = Animator;
        if (!animator) return false;
        if (!animator.runtimeAnimatorController) return false;
        if (!animator.isActiveAndEnabled) return false;
        if (!animator.gameObject.activeInHierarchy) return false;
        return true;
    }

#if UNITY_EDITOR
    private Sprite ResolveEditorPreviewSprite()
    {
        var controller = Animator ? Animator.runtimeAnimatorController as AnimatorController : null;
        if (!controller || controller.layers == null || controller.layers.Length == 0) return null;

        var stateMachine = controller.layers[0].stateMachine;
        var motion = ResolveEditorPreviewMotion(stateMachine);
        var clip = ResolveEditorPreviewAnimationClip(motion);
        return clip ? GetFirstSpriteFromClip(clip) : null;
    }

    private Motion ResolveEditorPreviewMotion(AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null) return null;

        if (isUsed)
        {
            for (var i = 0; i < stateMachine.states.Length; i++)
            {
                var state = stateMachine.states[i].state;
                if (state == null || !string.Equals(state.name, "Depleted", StringComparison.OrdinalIgnoreCase)) continue;
                return state.motion;
            }
        }

        var defaultState = stateMachine.defaultState;
        return defaultState ? defaultState.motion : null;
    }

    private AnimationClip ResolveEditorPreviewAnimationClip(Motion motion)
    {
        if (!motion) return null;
        if (motion is AnimationClip clip) return clip;
        if (motion is not BlendTree tree || tree.children == null || tree.children.Length == 0) return null;

        var target = (float)kind;
        var bestIndex = 0;
        var bestDiff = Mathf.Abs(tree.children[0].threshold - target);
        for (var i = 1; i < tree.children.Length; i++)
        {
            var diff = Mathf.Abs(tree.children[i].threshold - target);
            if (diff >= bestDiff) continue;
            bestDiff = diff;
            bestIndex = i;
        }

        return ResolveEditorPreviewAnimationClip(tree.children[bestIndex].motion);
    }

    private static Sprite GetFirstSpriteFromClip(AnimationClip clip)
    {
        if (!clip) return null;

        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite") continue;

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length == 0) continue;
            if (keys[0].value is Sprite sprite) return sprite;
        }

        return null;
    }
#endif

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
