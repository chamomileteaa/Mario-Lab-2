using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class Block : MonoBehaviour
{
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

    [Serializable]
    private struct ContentStep
    {
        [Min(1)] public int count;
        public GameObject prefab;
    }

    [Header("Type")]
    [SerializeField] private BlockKind kind = BlockKind.Brick;
    [SerializeField] private BreakRule breakRule = BreakRule.BigOnly;

    [SerializeField] private ContentStep[] contentSteps;
    [SerializeField] private bool becomeUsedWhenDepleted = true;

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

    private BoxCollider2D boxCollider2D;
    private float nextHitTime;
    private bool isUsed;
    private bool isHidden;
    private bool initialTriggerState;

    private int currentStep;
    private int remainingInStep;
    private SpriteRenderer spriteRenderer;
    private Coroutine bumpRoutine;
    private Vector3 spriteBaseLocalPosition;
    [SerializeField, HideInInspector] private float spriteBaseAlpha = 1f;
    [SerializeField, HideInInspector] private bool hasSpriteBaseAlpha;
    private bool editorPreviewActive;

    private BoxCollider2D BoxCollider => boxCollider2D ? boxCollider2D : boxCollider2D = GetComponent<BoxCollider2D>();
    private SpriteRenderer Sprite => spriteRenderer ? spriteRenderer : spriteRenderer = ResolveSpriteRenderer();
    private Transform BumpTarget => Sprite ? Sprite.transform : null;
    private Vector3 SpawnPosition => BoxCollider.bounds.center;

    private void Awake()
    {
        initialTriggerState = BoxCollider.isTrigger;
        CacheSpriteBaseAlpha();

        ResetContentProgress();

        isHidden = startsHidden;
        RefreshVisualState();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
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

        var mario = collider.GetComponentInParent<MarioController>();
        if (!mario || !mario.CompareTag("Player")) return false;

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

        if (TryDispense())
        {
            if (!HasContentLeft && becomeUsedWhenDepleted) SetUsed();
            return;
        }

        if (kind == BlockKind.Brick && CanBreak(mario))
        {
            Break();
            return;
        }

        if (kind == BlockKind.Question && becomeUsedWhenDepleted) SetUsed();
    }

    private void Reveal()
    {
        isHidden = false;
        RefreshVisualState();
    }

    private bool TryDispense()
    {
        if (!HasContentLeft) return false;
        Spawn(contentSteps[currentStep]);

        remainingInStep--;
        if (remainingInStep > 0) return true;

        currentStep++;
        AdvanceToNextStep();

        return true;
    }

    private bool HasContentLeft => contentSteps != null && currentStep < contentSteps.Length && remainingInStep > 0;

    private bool CanBreak(MarioController mario)
    {
        return breakRule switch
        {
            BreakRule.Always => true,
            BreakRule.BigOnly => mario && mario.Form != MarioController.MarioForm.Small,
            _ => false
        };
    }

    private void SetUsed()
    {
        if (isUsed) return;

        isUsed = true;
        kind = BlockKind.Solid;
        currentStep = contentSteps != null ? contentSteps.Length : 0;
        remainingInStep = 0;

        RefreshVisualState();
    }

    private void Break()
    {
        if (breakParticles)
        {
            var effect = PrefabPoolService.Spawn(breakParticles.gameObject, BoxCollider.bounds.center, Quaternion.identity);
            if (effect && effect.TryGetComponent<SpriteShardParticles>(out var shardParticles))
                shardParticles.ApplySprite(Sprite ? Sprite.sprite : null);
        }

        Destroy(gameObject);
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

    private void Spawn(ContentStep step)
    {
        if (!step.prefab) return;
        PrefabPoolService.Spawn(step.prefab, SpawnPosition, Quaternion.identity);
    }

    private void ResetContentProgress()
    {
        currentStep = 0;
        remainingInStep = 0;
        AdvanceToNextStep();
    }

    private void AdvanceToNextStep()
    {
        if (contentSteps == null)
        {
            remainingInStep = 0;
            return;
        }

        while (currentStep < contentSteps.Length)
        {
            remainingInStep = Mathf.Max(0, contentSteps[currentStep].count);
            if (remainingInStep > 0) return;
            currentStep++;
        }

        remainingInStep = 0;
    }
}
