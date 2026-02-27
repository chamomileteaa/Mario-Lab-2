using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteFlipper))]
public class EntityController : MonoBehaviour, IBlockBumpHandler, IEnemyImpactHandler, IKnockbackHandler
{
    private const string PlayerTag = "Player";
    private const string CameraBoundaryLayerName = "CameraBoundary";
    private static int cameraBoundaryLayer = int.MinValue;

    [Flags]
    public enum TurnMatrix
    {
        None = 0,
        Walls = 1 << 0,
        Entities = 1 << 1,
        Player = 1 << 2
    }

    public enum BlockBumpReaction
    {
        Ignore,
        Bounce,
        KnockAway
    }

    public enum SpawnFacingMode
    {
        Towards = 0,
        Away = 1,
        Random = 2
    }

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float acceleration = 40f;
    [SerializeField, Range(-1f, 1f)] private float moveDirectionX = -1f;
    [SerializeField] private bool moveOnEnable = true;
    [SerializeField] private bool startWhenVisible = true;
    [SerializeField] private SpawnFacingMode spawnFacingMode = SpawnFacingMode.Towards;
    [SerializeField] private TurnMatrix turnRules = TurnMatrix.Walls | TurnMatrix.Entities | TurnMatrix.Player;
    [SerializeField, Min(0f)] private float turnCooldown = 0.1f;
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.06f;
    [SerializeField] private bool useContinuousCollisionDetection = true;

    [Header("Block Bump")]
    [SerializeField] private BlockBumpReaction blockBumpReaction = BlockBumpReaction.Bounce;
    [SerializeField, Min(0f)] private float bumpUpwardSpeed = 3.25f;
    [SerializeField, Min(0f)] private float knockAwayHorizontalSpeed = 2f;
    [SerializeField, Min(0f)] private float knockAwayGravityScale = 1f;

    [Header("Defeat")]
    [SerializeField] private bool allowKnockbackHit = true;
    [SerializeField] private bool allowStarHit = true;
    [SerializeField] private bool stopAnimatorWhenKnockedBack = true;

    private Rigidbody2D body2D;
    private BoxCollider2D mainCollider2D;
    private Collider2D[] ownColliders;
    private readonly HashSet<int> ownColliderIds = new HashSet<int>(8);
    private SpriteFlipper spriteFlipper;
    private Animator animatorComponent;
    private bool movementEnabled;
    private bool startedMovement;
    private bool knockedAway;
    private float nextTurnTime;
    private float initialGravityScale;
    private readonly RaycastHit2D[] aheadHits = new RaycastHit2D[8];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D MainCollider => mainCollider2D ? mainCollider2D : mainCollider2D = GetComponent<BoxCollider2D>();
    private Collider2D[] OwnColliders => ownColliders != null && ownColliders.Length > 0
        ? ownColliders
        : ownColliders = GetComponentsInChildren<Collider2D>(true);
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponent<SpriteFlipper>();
    private Animator Anim => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();

    public event Action<EntityController> KnockbackApplied;
    public event Action<EntityController, EnemyImpactType> KnockbackAppliedWithType;
    public bool IsKnockedBack => knockedAway;
    [Obsolete("Use IsKnockedBack instead.")]
    public bool IsKnockedAway => IsKnockedBack;

    private void Awake()
    {
        moveDirectionX = ToDirection(moveDirectionX);
        initialGravityScale = Body.gravityScale;
        UpdateFacing(1f);
    }

    private void OnValidate()
    {
        moveDirectionX = ToDirection(moveDirectionX);
    }

    private void OnEnable()
    {
        ApplySpawnFacingMode();

        startedMovement = !startWhenVisible;
        movementEnabled = moveOnEnable && startedMovement;
        knockedAway = false;
        nextTurnTime = 0f;
        Body.gravityScale = initialGravityScale;
        Body.collisionDetectionMode = useContinuousCollisionDetection
            ? CollisionDetectionMode2D.Continuous
            : CollisionDetectionMode2D.Discrete;
        RebuildOwnColliderIds();
        SetCollidersEnabled(true);
        Body.WakeUp();
        ResumeAnimatorIfNeeded();
        UpdateFacing(1f);
    }

    private void OnDisable()
    {
        var velocity = Body.linearVelocity;
        velocity.x = 0f;
        Body.linearVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (!TryStartMovement()) return;
        if (!movementEnabled) return;

        ApplyHorizontalVelocity();
        if (knockedAway) return;
        if (Time.time < nextTurnTime) return;

        if (ShouldTurnFromAheadProbe())
            ReverseDirection();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (knockedAway) return;
        if (Time.time < nextTurnTime) return;
        if (!HasHorizontalContact(collision)) return;
        if (!ShouldTurnFromCollider(collision.collider)) return;
        ReverseDirection();
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (enabled) return;

        var velocity = Body.linearVelocity;
        velocity.x = 0f;
        Body.linearVelocity = velocity;
    }

    public void SetDirection(float directionX)
    {
        var normalized = ToDirection(directionX);
        if (Mathf.Approximately(normalized, moveDirectionX)) return;
        moveDirectionX = normalized;
        nextTurnTime = Time.time + turnCooldown;
        UpdateFacing(knockedAway ? -1f : 1f);
    }

    public void ReverseDirection() => SetDirection(-moveDirectionX);

    public void HandleBlockBump(in BlockBumpContext context)
    {
        switch (blockBumpReaction)
        {
            case BlockBumpReaction.Ignore:
                return;

            case BlockBumpReaction.Bounce:
            {
                var velocity = Body.linearVelocity;
                velocity.y = Mathf.Max(velocity.y, bumpUpwardSpeed);
                Body.linearVelocity = velocity;
                return;
            }

            case BlockBumpReaction.KnockAway:
                var knockbackContext = new EnemyImpactContext(EnemyImpactType.Knockback, context.Mario, context.Origin, context.Origin);
                TryHandleKnockback(in knockbackContext);
                return;
        }
    }

    public bool TryHandleImpact(in EnemyImpactContext context)
    {
        return context.ImpactType switch
        {
            EnemyImpactType.Star when allowStarHit => TryHandleKnockback(in context),
            EnemyImpactType.Knockback when allowKnockbackHit => TryHandleKnockback(in context),
            _ => false
        };
    }

    public bool TryHandleKnockback(in EnemyImpactContext context)
    {
        var impactType = context.ImpactType;
        if (impactType != EnemyImpactType.Star && impactType != EnemyImpactType.Knockback)
            return false;
        return TryKnockAway(context.SourcePosition, impactType);
    }

    [Obsolete("Use TryHandleKnockback(in EnemyImpactContext) instead.")]
    public bool TryKnockAway(Vector2 origin) => TryKnockAway(origin, EnemyImpactType.Knockback);

    private bool TryKnockAway(Vector2 origin, EnemyImpactType impactType)
    {
        if (knockedAway) return false;

        startedMovement = true;
        movementEnabled = true;
        knockedAway = true;

        var awayX = Mathf.Sign(transform.position.x - origin.x);
        if (Mathf.Approximately(awayX, 0f)) awayX = moveDirectionX;
        moveDirectionX = ToDirection(awayX);
        UpdateFacing(-1f);

        SetCollidersEnabled(false);
        Body.gravityScale = Mathf.Max(initialGravityScale, knockAwayGravityScale);
        var velocity = Body.linearVelocity;
        velocity.x = moveDirectionX * knockAwayHorizontalSpeed;
        velocity.y = Mathf.Max(velocity.y, bumpUpwardSpeed);
        Body.linearVelocity = velocity;
        nextTurnTime = Time.time + turnCooldown;
        KnockbackApplied?.Invoke(this);
        KnockbackAppliedWithType?.Invoke(this, impactType);
        StopAnimatorIfNeeded();
        return true;
    }

    private void ApplyHorizontalVelocity()
    {
        if (knockedAway) return;

        var velocity = Body.linearVelocity;
        var targetX = moveDirectionX * moveSpeed;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, acceleration * Time.fixedDeltaTime);
        Body.linearVelocity = velocity;
    }

    private bool ShouldTurnFromAheadProbe()
    {
        if (!MainCollider) return false;
        if ((turnRules & (TurnMatrix.Walls | TurnMatrix.Entities | TurnMatrix.Player)) == 0) return false;

        var probeDistance = wallCheckDistance + Mathf.Abs(Body.linearVelocity.x) * Time.fixedDeltaTime;
        if (probeDistance <= 0f) return false;

        var filter = new ContactFilter2D { useTriggers = false };
        var hitCount = MainCollider.Cast(new Vector2(moveDirectionX, 0f), filter, aheadHits, probeDistance);
        for (var i = 0; i < hitCount; i++)
        {
            var hit = aheadHits[i];
            if (Mathf.Abs(hit.normal.x) < 0.2f) continue;

            var hitCollider = hit.collider;
            if (ShouldTurnFromCollider(hitCollider)) return true;
        }

        return false;
    }

    private bool ShouldTurnFromCollider(Collider2D collider)
    {
        if (!collider) return false;
        if (IsOwnCollider(collider)) return false;
        if (collider.CompareColliderTag(PlayerTag))
            return (turnRules & TurnMatrix.Player) != 0;
        if (IsCameraBoundaryLayer(collider)) return (turnRules & TurnMatrix.Walls) != 0;

        if (TryGetOtherEntity(collider, out _))
            return (turnRules & TurnMatrix.Entities) != 0;

        return (turnRules & TurnMatrix.Walls) != 0;
    }

    private static bool HasHorizontalContact(Collision2D collision)
    {
        var contactCount = collision.contactCount;
        for (var i = 0; i < contactCount; i++)
        {
            var normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.x) > 0.2f)
                return true;
        }

        return false;
    }

    private static bool IsCameraBoundaryLayer(Collider2D collider)
    {
        if (cameraBoundaryLayer == int.MinValue)
            cameraBoundaryLayer = LayerMask.NameToLayer(CameraBoundaryLayerName);
        return collider.IsInLayer(cameraBoundaryLayer);
    }

    private bool TryGetOtherEntity(Collider2D collider, out EntityController other)
    {
        other = null;
        if (!collider) return false;
        if (!collider.TryGetComponentInParent(out other)) return false;
        return other && other != this;
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && ownColliderIds.Contains(collider.GetInstanceID());
    }

    private void RebuildOwnColliderIds()
    {
        ownColliderIds.Clear();
        var localColliders = OwnColliders;
        for (var i = 0; i < localColliders.Length; i++)
        {
            var collider = localColliders[i];
            if (collider) ownColliderIds.Add(collider.GetInstanceID());
        }
    }

    private void UpdateFacing(float directionY)
    {
        Flipper.SetDirection(new Vector2(moveDirectionX, directionY));
    }

    private static float ToDirection(float value)
    {
        return value >= 0f ? 1f : -1f;
    }

    private bool TryStartMovement()
    {
        if (startedMovement) return true;
        if (startWhenVisible && !IsVisibleToStartCamera()) return false;

        startedMovement = true;
        if (moveOnEnable) movementEnabled = true;
        return true;
    }

    private bool IsVisibleToStartCamera()
    {
        var sceneCamera = Camera.main;
        if (!sceneCamera || !sceneCamera.orthographic) return true;

        if (MainCollider)
            return sceneCamera.OverlapsOrthographicView(MainCollider.bounds);

        return sceneCamera.ContainsOrthographicPoint(transform.position);
    }

    private void ApplySpawnFacingMode()
    {
        var mario = GameObject.FindGameObjectWithTag(PlayerTag);
        if (!mario)
        {
            if (spawnFacingMode == SpawnFacingMode.Random)
                moveDirectionX = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            return;
        }

        var delta = mario.transform.position.x - transform.position.x;
        if (Mathf.Abs(delta) <= 0.001f)
        {
            if (spawnFacingMode == SpawnFacingMode.Random)
                moveDirectionX = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            return;
        }

        switch (spawnFacingMode)
        {
            case SpawnFacingMode.Towards:
                moveDirectionX = delta > 0f ? 1f : -1f;
                break;

            case SpawnFacingMode.Away:
                moveDirectionX = delta > 0f ? -1f : 1f;
                break;

            case SpawnFacingMode.Random:
                moveDirectionX = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                break;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        var localColliders = OwnColliders;
        for (var i = 0; i < localColliders.Length; i++)
        {
            var collider = localColliders[i];
            if (!collider) continue;
            collider.enabled = enabled;
        }
    }

    private void StopAnimatorIfNeeded()
    {
        if (!stopAnimatorWhenKnockedBack) return;
        if (!Anim) return;
        Anim.speed = 0f;
    }

    private void ResumeAnimatorIfNeeded()
    {
        if (!Anim) return;
        Anim.speed = 1f;
    }
}
