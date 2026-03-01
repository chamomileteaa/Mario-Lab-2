using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AudioPlayer))]
public class FireballController : MonoBehaviour
{
    private const string EnemyTag = "Enemy";
    private const float HorizontalNormalThreshold = 0.6f;
    private const float UpwardNormalThreshold = 0.45f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float horizontalSpeed = 9f;
    [SerializeField, Min(0f)] private float launchVerticalSpeed = 2.2f;
    [SerializeField, Min(0f)] private float bounceVerticalSpeed = 4.75f;
    [SerializeField, Min(0f)] private float maxLifetime = 3f;
    [SerializeField, Min(0)] private int maxBounces = 6;
    [SerializeField, Min(0f)] private float offscreenDespawnPadding = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioClip hitClip;

    private static int activeCount;

    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private AudioPlayer audioPlayer;
    private MarioController owner;
    private float directionX = 1f;
    private float lifeTimer;
    private int bounceCount;
    private bool launched;
    private bool countedAsActive;
    private bool hasBeenVisibleToMainCamera;
    private bool despawnQueued;
    private Camera mainCamera;
    private Coroutine despawnRoutine;
    private Collider2D[] ownColliders = new Collider2D[0];
    private SpriteRenderer[] spriteRenderers = new SpriteRenderer[0];
    private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>(8);

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private AudioPlayer Audio => audioPlayer ? audioPlayer : audioPlayer = GetComponent<AudioPlayer>();

    public static int ActiveCount => activeCount;

    private void OnEnable()
    {
        if (!countedAsActive)
        {
            activeCount++;
            countedAsActive = true;
        }

        launched = false;
        lifeTimer = 0f;
        bounceCount = 0;
        owner = null;
        directionX = 1f;
        despawnQueued = false;
        hasBeenVisibleToMainCamera = false;
        mainCamera = Camera.main;
        Body.gravityScale = 1f;
        Body.simulated = true;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BodyCollider.enabled = true;
        CacheOwnColliders();
        CacheSpriteRenderers();
        SetRenderersVisible(true);
    }

    private void OnDisable()
    {
        if (countedAsActive)
        {
            activeCount = Mathf.Max(0, activeCount - 1);
            countedAsActive = false;
        }

        ClearOwnerCollisionIgnores();
        launched = false;
        owner = null;
        despawnQueued = false;
        if (despawnRoutine != null)
        {
            StopCoroutine(despawnRoutine);
            despawnRoutine = null;
        }
    }

    private void FixedUpdate()
    {
        if (!launched || despawnQueued) return;

        lifeTimer += Time.fixedDeltaTime;
        if (lifeTimer >= maxLifetime)
        {
            Despawn();
            return;
        }

        var velocity = Body.linearVelocity;
        velocity.x = directionX * horizontalSpeed;
        Body.linearVelocity = velocity;

        if (ShouldDespawnOffscreen())
        {
            Despawn(false);
            return;
        }
    }

    public void Launch(MarioController fireOwner, Vector2 worldPosition, float direction)
    {
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;
        owner = fireOwner;
        directionX = Mathf.Abs(direction) <= 0.001f ? 1f : Mathf.Sign(direction);

        ClearOwnerCollisionIgnores();
        CacheOwnColliders();
        ApplyOwnerCollisionIgnores();

        var velocity = Body.linearVelocity;
        velocity.x = directionX * horizontalSpeed;
        velocity.y = launchVerticalSpeed;
        Body.linearVelocity = velocity;
        launched = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!launched || despawnQueued) return;
        if (!collision) return;
        if (IsIgnoredCollider(collision)) return;

        if (TryHandleEnemyImpact(collision))
            Despawn(true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!launched || despawnQueued) return;
        if (collision == null) return;
        if (!collision.collider) return;
        if (IsIgnoredCollider(collision.collider)) return;

        if (TryHandleEnemyImpact(collision.collider))
        {
            Despawn(true);
            return;
        }

        var contactCount = collision.contactCount;
        var shouldBounce = false;
        var shouldDespawn = false;

        for (var i = 0; i < contactCount; i++)
        {
            var normal = collision.GetContact(i).normal;
            if (normal.y >= UpwardNormalThreshold && Body.linearVelocity.y <= 0.1f)
                shouldBounce = true;

            if (Mathf.Abs(normal.x) >= HorizontalNormalThreshold)
                shouldDespawn = true;

            if (normal.y <= -UpwardNormalThreshold)
                shouldDespawn = true;
        }

        if (shouldDespawn)
        {
            Despawn(true);
            return;
        }

        if (!shouldBounce) return;

        bounceCount++;
        if (bounceCount > maxBounces)
        {
            Despawn(true);
            return;
        }

        var velocity = Body.linearVelocity;
        velocity.y = bounceVerticalSpeed;
        Body.linearVelocity = velocity;
    }

    private bool TryHandleEnemyImpact(Collider2D collider)
    {
        if (!IsEnemyCollider(collider)) return false;

        var context = new EnemyImpactContext(EnemyImpactType.Knockback, owner, transform.position, transform.position);
        var impactHandler = ResolveResponder<IEnemyImpactHandler>(collider);
        if (impactHandler != null)
            impactHandler.TryHandleImpact(in context);

        var knockbackHandler = ResolveResponder<IKnockbackHandler>(collider);
        if (knockbackHandler != null)
            knockbackHandler.TryHandleKnockback(in context);

        return true;
    }

    private static bool IsEnemyCollider(Collider2D collider)
    {
        if (!collider) return false;
        if (collider.CompareColliderTag(EnemyTag)) return true;
        if (collider.attachedRigidbody && collider.attachedRigidbody.CompareTag(EnemyTag))
            return true;

        var root = collider.transform ? collider.transform.root : null;
        return root && root.CompareTag(EnemyTag);
    }

    private static T ResolveResponder<T>(Collider2D collider) where T : class
    {
        if (!collider) return null;

        var direct = collider.GetComponentInParent<T>();
        if (direct != null) return direct;

        if (collider.attachedRigidbody)
        {
            var rigidbodyResponder = collider.attachedRigidbody.GetComponentInParent<T>();
            if (rigidbodyResponder != null) return rigidbodyResponder;
        }

        var root = collider.transform ? collider.transform.root : null;
        return root ? root.GetComponentInChildren<T>(true) : null;
    }

    private bool IsIgnoredCollider(Collider2D collider)
    {
        if (!collider) return true;
        if (owner && collider.transform && collider.transform.IsChildOf(owner.transform)) return true;
        return false;
    }

    private void CacheOwnColliders()
    {
        ownColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void CacheSpriteRenderers()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0) return;
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            var renderer = spriteRenderers[i];
            if (renderer) renderer.enabled = visible;
        }
    }

    private void ApplyOwnerCollisionIgnores()
    {
        if (!owner) return;
        if (ownColliders == null || ownColliders.Length == 0) return;

        var ownerColliders = owner.GetComponentsInChildren<Collider2D>(true);
        ignoredOwnerColliders.Clear();

        for (var i = 0; i < ownerColliders.Length; i++)
        {
            var ownerCollider = ownerColliders[i];
            if (!ownerCollider) continue;

            ignoredOwnerColliders.Add(ownerCollider);
            for (var j = 0; j < ownColliders.Length; j++)
            {
                var ownCollider = ownColliders[j];
                if (!ownCollider) continue;
                Physics2D.IgnoreCollision(ownCollider, ownerCollider, true);
            }
        }
    }

    private void ClearOwnerCollisionIgnores()
    {
        if (ignoredOwnerColliders.Count == 0) return;
        if (ownColliders == null || ownColliders.Length == 0)
        {
            ignoredOwnerColliders.Clear();
            return;
        }

        for (var i = 0; i < ignoredOwnerColliders.Count; i++)
        {
            var ownerCollider = ignoredOwnerColliders[i];
            if (!ownerCollider) continue;

            for (var j = 0; j < ownColliders.Length; j++)
            {
                var ownCollider = ownColliders[j];
                if (!ownCollider) continue;
                Physics2D.IgnoreCollision(ownCollider, ownerCollider, false);
            }
        }

        ignoredOwnerColliders.Clear();
    }

    private bool ShouldDespawnOffscreen()
    {
        var camera = mainCamera ? mainCamera : mainCamera = Camera.main;
        if (!camera || !camera.orthographic || !BodyCollider) return false;

        var cameraPosition = camera.transform.position;
        var halfHeight = camera.orthographicSize + offscreenDespawnPadding;
        var halfWidth = halfHeight * camera.aspect + offscreenDespawnPadding;
        var viewRect = new Rect(
            cameraPosition.x - halfWidth,
            cameraPosition.y - halfHeight,
            halfWidth * 2f,
            halfHeight * 2f);

        var bounds = BodyCollider.bounds;
        var overlapsView =
            bounds.max.x >= viewRect.xMin &&
            bounds.min.x <= viewRect.xMax &&
            bounds.max.y >= viewRect.yMin &&
            bounds.min.y <= viewRect.yMax;

        if (overlapsView)
        {
            hasBeenVisibleToMainCamera = true;
            return false;
        }

        return hasBeenVisibleToMainCamera;
    }

    private void Despawn(bool playHitSound = false)
    {
        if (despawnQueued) return;

        if (!playHitSound || !hitClip)
        {
            PrefabPoolService.Despawn(gameObject);
            return;
        }

        despawnQueued = true;
        launched = false;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.simulated = false;
        if (ownColliders != null)
        {
            for (var i = 0; i < ownColliders.Length; i++)
            {
                var collider = ownColliders[i];
                if (collider) collider.enabled = false;
            }
        }
        SetRenderersVisible(false);

        despawnRoutine = StartCoroutine(PlayHitThenDespawn());
    }

    private IEnumerator PlayHitThenDespawn()
    {
        Audio?.PlayOneShot(hitClip);
        var delay = Mathf.Clamp(hitClip.length, 0.02f, 0.25f);
        yield return new WaitForSeconds(delay);
        despawnRoutine = null;
        PrefabPoolService.Despawn(gameObject);
    }
}
