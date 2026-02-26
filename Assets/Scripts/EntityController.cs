using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteFlipper))]
public class EntityController : MonoBehaviour, IBlockBumpReactive
{
    [Flags]
    public enum TurnMatrix
    {
        None = 0,
        Walls = 1 << 0,
        Entities = 1 << 1
    }

    public enum BlockBumpReaction
    {
        Ignore,
        Bounce,
        KnockAway
    }

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float acceleration = 40f;
    [SerializeField, Range(-1f, 1f)] private float moveDirectionX = -1f;
    [SerializeField] private bool moveOnEnable = true;
    [SerializeField] private bool startWhenVisible = true;
    [SerializeField] private bool faceMarioOnSpawn = true;
    [SerializeField] private TurnMatrix turnRules = TurnMatrix.Walls | TurnMatrix.Entities;
    [SerializeField, Min(0f)] private float turnCooldown = 0.1f;
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.06f;
    [SerializeField] private bool useContinuousCollisionDetection = true;

    [Header("Block Bump")]
    [SerializeField] private BlockBumpReaction blockBumpReaction = BlockBumpReaction.Bounce;
    [SerializeField, Min(0f)] private float bumpUpwardSpeed = 3.25f;
    [SerializeField, Min(0f)] private float knockAwayHorizontalSpeed = 2f;
    [SerializeField, Min(0f)] private float knockAwayGravityScale = 1f;

    private Rigidbody2D body2D;
    private Collider2D mainCollider2D;
    private Collider2D[] ownColliders;
    private SpriteFlipper spriteFlipper;
    private bool movementEnabled;
    private bool startedMovement;
    private bool knockedAway;
    private float nextTurnTime;
    private float initialGravityScale;
    private readonly RaycastHit2D[] aheadHits = new RaycastHit2D[8];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private Collider2D MainCollider => mainCollider2D ? mainCollider2D : mainCollider2D = GetComponent<Collider2D>();
    private Collider2D[] OwnColliders => ownColliders != null && ownColliders.Length > 0
        ? ownColliders
        : ownColliders = GetComponentsInChildren<Collider2D>(true);
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponent<SpriteFlipper>();

    public event Action<EntityController> KnockedAway;
    public bool IsKnockedAway => knockedAway;

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
        if (faceMarioOnSpawn) FaceMarioIfFound();

        startedMovement = !startWhenVisible;
        movementEnabled = moveOnEnable && startedMovement;
        knockedAway = false;
        nextTurnTime = 0f;
        Body.gravityScale = initialGravityScale;
        Body.collisionDetectionMode = useContinuousCollisionDetection
            ? CollisionDetectionMode2D.Continuous
            : CollisionDetectionMode2D.Discrete;
        SetCollidersEnabled(true);
        Body.WakeUp();
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
        if ((turnRules & TurnMatrix.Entities) == 0 || knockedAway) return;
        if (Time.time < nextTurnTime) return;

        var other = ResolveOtherEntity(collision);
        if (other)
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

    public void OnBlockBumped(BlockBumpContext context)
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
            {
                startedMovement = true;
                movementEnabled = true;
                knockedAway = true;

                var awayX = Mathf.Sign(transform.position.x - context.Origin.x);
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
                KnockedAway?.Invoke(this);
                return;
            }
        }
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
        if ((turnRules & (TurnMatrix.Walls | TurnMatrix.Entities)) == 0) return false;

        var probeDistance = wallCheckDistance + Mathf.Abs(Body.linearVelocity.x) * Time.fixedDeltaTime;
        if (probeDistance <= 0f) return false;

        var filter = new ContactFilter2D { useTriggers = false };
        var hitCount = MainCollider.Cast(new Vector2(moveDirectionX, 0f), filter, aheadHits, probeDistance);
        for (var i = 0; i < hitCount; i++)
        {
            var hitCollider = aheadHits[i].collider;
            if (ShouldTurnFromCollider(hitCollider)) return true;
        }

        return false;
    }

    private bool ShouldTurnFromCollider(Collider2D collider)
    {
        if (!collider) return false;
        if (IsOwnCollider(collider)) return false;
        if (collider.CompareColliderTag("Player")) return false;

        var otherEntity = ResolveEntityFromCollider(collider);
        if (otherEntity)
            return (turnRules & TurnMatrix.Entities) != 0;

        return (turnRules & TurnMatrix.Walls) != 0;
    }

    private EntityController ResolveOtherEntity(Collision2D collision)
    {
        if (TryResolveOtherEntity(collision.collider, out var other))
            return other;
        if (TryResolveOtherEntity(collision.otherCollider, out other))
            return other;

        return null;
    }

    private EntityController ResolveEntityFromCollider(Collider2D collider)
    {
        return TryResolveOtherEntity(collider, out var other) ? other : null;
    }

    private bool TryResolveOtherEntity(Collider2D collider, out EntityController other)
    {
        other = null;
        if (!collider) return false;
        if (!collider.TryGetComponentInParent(out other)) return false;
        return other && other != this;
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        if (!collider) return false;
        foreach (var localCollider in OwnColliders)
            if (localCollider == collider)
                return true;
        return false;
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

    private void FaceMarioIfFound()
    {
        var mario = GameObject.FindGameObjectWithTag("Player");
        if (!mario) return;

        var delta = mario.transform.position.x - transform.position.x;
        if (Mathf.Abs(delta) <= 0.001f) return;
        moveDirectionX = delta > 0f ? 1f : -1f;
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
}
