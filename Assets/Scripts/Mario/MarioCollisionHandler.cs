using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(MarioController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MarioCollisionHandler : MonoBehaviour
{
    [Header("Collectibles")]
    [SerializeField] private UIScript ui;

    [Header("Stomp")]
    [SerializeField, Min(0.1f)] private float stompBounceSpeed = 12f;
    [SerializeField, MinMaxInt(-1f, 1f)] private MinMaxFloat stompContactGap = new MinMaxFloat(-0.55f, 0.3f);
    [SerializeField, Min(0f)] private float stompContactPointTolerance = 0.08f;
    [SerializeField, Min(0f)] private float stompSideTolerance = 0.18f;
    [SerializeField, Min(0f)] private float stompMaxUpwardVelocity = 0.75f;

    private MarioController marioController;
    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private readonly Dictionary<int, CachedEnemyHandlers> enemyHandlersByColliderId = new Dictionary<int, CachedEnemyHandlers>(64);

    private MarioController Mario => marioController ? marioController : marioController = GetComponent<MarioController>();
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();

    private void OnValidate()
    {
        stompContactGap.ClampAndOrder(-1f, 1f);
    }

    private void OnDisable()
    {
        enemyHandlersByColliderId.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (TryHandleCollectible(collision)) return;
        HandleEnemyTrigger(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleEnemyTrigger(collision);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryHandleCollectible(collision.collider)) return;
        if (TryHandleCollectible(collision.otherCollider)) return;
        HandleEnemyCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleEnemyCollision(collision);
    }

    private bool TryHandleCollectible(Collider2D collision)
    {
        if (!collision) return false;

        if (collision.CompareTag("coin"))
        {
            GameData.Instance.AddCoin();
            DespawnCollectible(collision.gameObject);
            ui?.UpdateUI();
            return true;
        }

        if (!collision.CompareTag("1up")) return false;
        GameData.Instance.AddLife();
        DespawnCollectible(collision.gameObject);
        ui?.UpdateUI();
        return true;
    }

    private void HandleEnemyCollision(Collision2D collision)
    {
        if (!Mario || Mario.IsDead) return;
        var enemyCollider = ResolveEnemyCollider(collision);
        if (!enemyCollider) return;
        var isStompContact = IsStompCollision(collision, enemyCollider.bounds);
        TryHandleEnemyContact(enemyCollider, isStompContact);
    }

    private void HandleEnemyTrigger(Collider2D collision)
    {
        if (!Mario || Mario.IsDead) return;
        if (!collision || IsOwnCollider(collision)) return;

        var isStompContact = Body.linearVelocity.y <= stompMaxUpwardVelocity && IsStompContact(collision.bounds);
        TryHandleEnemyContact(collision, isStompContact);
    }

    private bool TryHandleEnemyContact(Collider2D collider, bool isStompContact)
    {
        if (!collider) return false;
        if (Mario.IsDead) return true;

        var cachedHandlers = GetCachedEnemyHandlers(collider);
        var impactHandler = cachedHandlers.ImpactHandler;
        var stompHandler = cachedHandlers.StompHandler;
        var knockbackHandler = cachedHandlers.KnockbackHandler;
        var isEnemy = IsEnemyCollider(collider);
        if (!isEnemy && impactHandler == null && stompHandler == null && knockbackHandler == null) return false;

        var contactPoint = (Vector2)BodyCollider.bounds.center;
        var sourcePosition = (Vector2)transform.position;

        if (Mario.IsStarPowered)
        {
            var starContext = new EnemyImpactContext(EnemyImpactType.Star, Mario, contactPoint, sourcePosition);
            TryResolveImpact(collider, in starContext, impactHandler, stompHandler, knockbackHandler);
            return true;
        }

        if (isStompContact &&
            TryResolveImpact(
                collider,
                new EnemyImpactContext(EnemyImpactType.Stomp, Mario, contactPoint, sourcePosition),
                impactHandler,
                stompHandler,
                knockbackHandler))
        {
            Mario.ApplyEnemyStompBounce(stompBounceSpeed);
            return true;
        }

        if (!isEnemy) return true;
        Mario.TakeDamage();
        return true;
    }

    private Collider2D ResolveEnemyCollider(Collision2D collision)
    {
        var primary = collision.collider;
        if (primary && !IsOwnCollider(primary)) return primary;

        var secondary = collision.otherCollider;
        if (secondary && !IsOwnCollider(secondary)) return secondary;

        return null;
    }

    private bool IsStompCollision(Collision2D collision, Bounds enemyBounds)
    {
        if (Body.linearVelocity.y > stompMaxUpwardVelocity) return false;
        if (BodyCollider.bounds.center.y <= enemyBounds.center.y + 0.01f) return false;
        if (HasFeetContact(collision, enemyBounds)) return true;
        return IsStompContact(enemyBounds);
    }

    private bool IsStompContact(Bounds enemyBounds)
    {
        var marioBounds = BodyCollider.bounds;
        var horizontalOverlap = Mathf.Min(marioBounds.max.x, enemyBounds.max.x) - Mathf.Max(marioBounds.min.x, enemyBounds.min.x);
        if (horizontalOverlap < -stompSideTolerance) return false;

        var feetGap = marioBounds.min.y - enemyBounds.max.y;
        if (feetGap > stompContactGap.max) return false;
        if (marioBounds.min.y <= enemyBounds.min.y) return false;
        return true;
    }

    private bool HasFeetContact(Collision2D collision, Bounds enemyBounds)
    {
        var marioBounds = BodyCollider.bounds;
        var marioFeetY = marioBounds.min.y + stompContactPointTolerance;
        var minContactX = enemyBounds.min.x - stompSideTolerance;
        var maxContactX = enemyBounds.max.x + stompSideTolerance;
        var minContactY = enemyBounds.center.y - stompContactPointTolerance;
        var count = collision.contactCount;
        for (var i = 0; i < count; i++)
        {
            var point = collision.GetContact(i).point;
            if (point.y <= marioFeetY && point.y >= minContactY && point.x >= minContactX && point.x <= maxContactX)
                return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && collider == BodyCollider;
    }

    private static bool IsEnemyCollider(Collider2D collider)
    {
        return collider.CompareColliderTag("enemy");
    }

    private bool TryResolveImpact(
        Collider2D enemyCollider,
        in EnemyImpactContext context,
        IEnemyImpactHandler cachedImpactHandler,
        IStompHandler cachedStompHandler,
        IKnockbackHandler cachedKnockbackHandler)
    {
        var impactHandler = cachedImpactHandler ?? ResolveResponder<IEnemyImpactHandler>(enemyCollider);
        if (impactHandler != null &&
            impactHandler.TryHandleImpact(in context))
            return true;

        if (context.ImpactType == EnemyImpactType.Stomp)
        {
            var stompHandler = cachedStompHandler ?? ResolveResponder<IStompHandler>(enemyCollider);
            return stompHandler != null && stompHandler.TryHandleStomp(in context);
        }

        if (context.ImpactType == EnemyImpactType.Star || context.ImpactType == EnemyImpactType.Knockback)
        {
            var knockbackHandler = cachedKnockbackHandler ?? ResolveResponder<IKnockbackHandler>(enemyCollider);
            return knockbackHandler != null && knockbackHandler.TryHandleKnockback(in context);
        }

        return false;
    }

    private CachedEnemyHandlers GetCachedEnemyHandlers(Collider2D collider)
    {
        var colliderId = collider.GetInstanceID();
        if (enemyHandlersByColliderId.TryGetValue(colliderId, out var cachedHandlers))
            return cachedHandlers;

        cachedHandlers = new CachedEnemyHandlers
        {
            ImpactHandler = ResolveResponder<IEnemyImpactHandler>(collider),
            StompHandler = ResolveResponder<IStompHandler>(collider),
            KnockbackHandler = ResolveResponder<IKnockbackHandler>(collider)
        };
        enemyHandlersByColliderId[colliderId] = cachedHandlers;
        return cachedHandlers;
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

    private static void DespawnCollectible(GameObject collectible)
    {
        if (!collectible) return;
        PrefabPoolService.Despawn(collectible);
    }

    private struct CachedEnemyHandlers
    {
        public IEnemyImpactHandler ImpactHandler;
        public IStompHandler StompHandler;
        public IKnockbackHandler KnockbackHandler;
    }
}
