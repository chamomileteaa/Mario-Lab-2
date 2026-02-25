using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AnimatorCache))]
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

    [Header("Contents (Linear Order)")]
    [SerializeField] private ContentStep[] contentSteps;
    [SerializeField] private bool becomeUsedWhenDepleted = true;

    [Header("State")]
    [SerializeField] private bool startsHidden;
    [SerializeField] private bool hiddenNonSolidUntilReveal = true;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite usedSprite;
    [SerializeField] private AnimatorCache animatorCache;

    [Header("Spawns")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject breakPrefab;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float bounceHeight = 0.18f;
    [SerializeField, Min(0.01f)] private float bounceDuration = 0.1f;
    [SerializeField, Min(0f)] private float hitCooldown = 0.05f;

    private BoxCollider2D boxCollider2D;
    private Vector3 startLocalPosition;
    private Coroutine bounceRoutine;
    private float nextHitTime;
    private bool isUsed;
    private bool isRevealed;
    private bool initialTriggerState;

    private int currentStep;
    private int remainingInStep;

    private BoxCollider2D BoxCollider => boxCollider2D ? boxCollider2D : boxCollider2D = GetComponent<BoxCollider2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private Vector3 SpawnPosition => spawnPoint ? spawnPoint.position : transform.position + Vector3.up * 0.6f;

    private void Awake()
    {
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        startLocalPosition = transform.localPosition;
        initialTriggerState = BoxCollider.isTrigger;

        ResetContentProgress();

        isRevealed = !startsHidden;
        ApplyHidden(!isRevealed);
        SyncAnimatorState();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var body = collision.rigidbody;
        if (!body) return;

        var mario = body.GetComponent<MarioController>();
        if (!mario) return;
        if (!IsFromBelow(collision)) return;
        if (!IsMovingUp(body)) return;

        Hit(mario);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!startsHidden || isRevealed || !hiddenNonSolidUntilReveal) return;
        if (!other.attachedRigidbody) return;

        var mario = other.attachedRigidbody.GetComponent<MarioController>();
        if (!mario) return;
        if (!IsMovingUp(other.attachedRigidbody)) return;
        if (other.attachedRigidbody.position.y >= transform.position.y - 0.05f) return;

        Hit(mario);
    }

    private void Hit(MarioController mario)
    {
        if (Time.time < nextHitTime) return;
        nextHitTime = Time.time + hitCooldown;

        if (!isRevealed) Reveal();
        PlayHitAnimation();

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
        isRevealed = true;
        ApplyHidden(false);
        TryTrigger("reveal");
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

        if (usedSprite && spriteRenderer) spriteRenderer.sprite = usedSprite;
        TryTrigger("used");
        SyncAnimatorState();
    }

    private void Break()
    {
        TryTrigger("break");
        if (breakPrefab) PrefabPoolService.Spawn(breakPrefab, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    private void PlayHitAnimation()
    {
        if (TryTrigger("hit")) return;

        if (bounceRoutine != null) StopCoroutine(bounceRoutine);
        bounceRoutine = StartCoroutine(Bounce());
    }

    private IEnumerator Bounce()
    {
        var elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / bounceDuration);
            var y = t < 0.5f
                ? Mathf.Lerp(0f, bounceHeight, t * 2f)
                : Mathf.Lerp(bounceHeight, 0f, (t - 0.5f) * 2f);

            transform.localPosition = startLocalPosition + Vector3.up * y;
            yield return null;
        }

        transform.localPosition = startLocalPosition;
        bounceRoutine = null;
    }

    private void ApplyHidden(bool hidden)
    {
        if (spriteRenderer) spriteRenderer.enabled = !hidden;
        if (hiddenNonSolidUntilReveal) BoxCollider.isTrigger = hidden ? true : initialTriggerState;
        SyncAnimatorState();
    }

    private void SyncAnimatorState()
    {
        if (!Anim) return;
        Anim.TrySet("isUsed", isUsed);
        Anim.TrySet("isHidden", !isRevealed);
    }

    private bool TryTrigger(string triggerName) => Anim && Anim.TrySetTrigger(triggerName);

    private static bool IsMovingUp(Rigidbody2D body) => body && body.linearVelocity.y > 0f;

    private static bool IsFromBelow(Collision2D collision)
    {
        for (var i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y < -0.5f)
                return true;

        return false;
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
