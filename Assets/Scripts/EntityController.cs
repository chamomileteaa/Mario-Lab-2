using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
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
    [SerializeField] private Camera visibilityCamera;
    [SerializeField] private TurnMatrix turnMatrix = TurnMatrix.Walls | TurnMatrix.Entities;
    [SerializeField, Min(0f)] private float turnCooldown = 0.1f;
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.06f;

    [Header("Block Bump")]
    [SerializeField] private BlockBumpReaction blockBumpReaction = BlockBumpReaction.Bounce;
    [SerializeField, Min(0f)] private float bumpUpwardSpeed = 3.25f;
    [SerializeField, Min(0f)] private float knockAwayHorizontalSpeed = 2f;
    [SerializeField] private bool keepMovingWhenKnockedAway;

    private Rigidbody2D body2D;
    private Collider2D mainCollider;
    private SpriteFlipper spriteFlipper;
    private bool movementEnabled;
    private bool isKnockedAway;
    private bool hasStartedMovement;
    private float nextTurnTime;

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private Collider2D MainCollider => mainCollider ? mainCollider : mainCollider = ResolveMainCollider();
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);

    public event Action<EntityController> KnockedAway;
    public bool IsKnockedAway => isKnockedAway;

    private void Reset() => EnsureCollider();

    private void Awake()
    {
        EnsureCollider();
        moveDirectionX = NormalizeDirection(moveDirectionX);
        ApplyFacing();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) EnsureCollider();
        moveDirectionX = NormalizeDirection(moveDirectionX);
    }

    private void OnEnable()
    {
        CacheVisibilityCamera();
        if (faceMarioOnSpawn) FaceMarioIfFound();

        hasStartedMovement = !startWhenVisible;
        movementEnabled = moveOnEnable && hasStartedMovement;
        isKnockedAway = false;
        nextTurnTime = 0f;
        Body.WakeUp();
        ApplyFacing();
    }

    private void OnDisable()
    {
        if (!Body) return;
        var velocity = Body.linearVelocity;
        velocity.x = 0f;
        Body.linearVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (!TryActivateMovement()) return;

        if (!movementEnabled) return;

        ApplyHorizontalVelocity();
        if (isKnockedAway) return;
        if (Time.time < nextTurnTime) return;

        if (HasTurn(TurnMatrix.Walls) && HasWallAhead())
            ReverseDirection();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!HasTurn(TurnMatrix.Entities) || isKnockedAway) return;
        if (Time.time < nextTurnTime) return;

        if (TryGetOtherEntity(collision.collider, out var other) && other != this)
        {
            ReverseDirection();
            return;
        }

        if (TryGetOtherEntity(collision.otherCollider, out other) && other != this)
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
        var normalized = NormalizeDirection(directionX);
        if (Mathf.Approximately(normalized, moveDirectionX)) return;
        moveDirectionX = normalized;
        nextTurnTime = Time.time + turnCooldown;
        ApplyFacing();
    }

    public void ReverseDirection()
    {
        SetDirection(-moveDirectionX);
    }

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
                isKnockedAway = true;

                var awayX = Mathf.Sign(transform.position.x - context.Origin.x);
                if (Mathf.Approximately(awayX, 0f)) awayX = moveDirectionX;
                moveDirectionX = NormalizeDirection(awayX);
                ApplyFacing();

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
        if (isKnockedAway && !keepMovingWhenKnockedAway) return;

        var velocity = Body.linearVelocity;
        var targetX = moveDirectionX * moveSpeed;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, acceleration * Time.fixedDeltaTime);
        Body.linearVelocity = velocity;
    }

    private bool HasWallAhead()
    {
        var bounds = MainCollider.bounds;
        var origin = new Vector2(bounds.center.x + moveDirectionX * (bounds.extents.x + 0.01f), bounds.center.y);
        var hit = Physics2D.Raycast(origin, new Vector2(moveDirectionX, 0f), wallCheckDistance);
        return hit.collider && !hit.collider.isTrigger && !IsOwnCollider(hit.collider);
    }

    private bool TryGetOtherEntity(Collider2D collider, out EntityController other)
    {
        other = null;
        if (!collider) return false;
        other = collider.GetComponentInParent<EntityController>();
        return other;
    }

    private bool HasTurn(TurnMatrix turn)
    {
        return (turnMatrix & turn) != 0;
    }

    private void ApplyFacing()
    {
        Flipper?.SetDirection(new Vector2(moveDirectionX, 0f));
    }

    private Collider2D ResolveMainCollider()
    {
        var colliders = GetComponents<Collider2D>();
        for (var i = 0; i < colliders.Length; i++)
            if (colliders[i] && !colliders[i].isTrigger)
                return colliders[i];
        return colliders.Length > 0 ? colliders[0] : null;
    }

    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>()) return;
        gameObject.AddComponent<BoxCollider2D>();
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        if (!collider) return false;
        if (collider == MainCollider) return true;
        return collider.attachedRigidbody && collider.attachedRigidbody == Body;
    }

    private static float NormalizeDirection(float value)
    {
        return value >= 0f ? 1f : -1f;
    }

    private bool TryActivateMovement()
    {
        if (hasStartedMovement) return true;
        if (startWhenVisible && !IsVisibleToStartCamera()) return false;

        hasStartedMovement = true;
        if (moveOnEnable) movementEnabled = true;
        return true;
    }

    private bool IsVisibleToStartCamera()
    {
        var sceneCamera = GetVisibilityCamera();
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

    private Camera GetVisibilityCamera()
    {
        if (visibilityCamera) return visibilityCamera;
        CacheVisibilityCamera();
        return visibilityCamera;
    }

    private void CacheVisibilityCamera()
    {
        if (visibilityCamera) return;
        visibilityCamera = Camera.main;
    }
}
