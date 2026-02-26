using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AnimatorCache))]
public class MarioController : MonoBehaviour
{
    //updates UI when collision

    public enum MarioForm
    {
        Small = 0,
        Big = 1,
        Fire = 2
    }

    private const float InputDeadzone = 0.01f;
    private const float CrouchThreshold = -0.5f;
    private const float MinAnimMoveSpeed = 0.2f;
    private const float MinDeathBounceSpeed = 11f;
    private const PauseType MarioPauseBypassTypes = PauseType.Physics | PauseType.Animation;
    private const PauseType GameplayPauseTypes = PauseType.Physics | PauseType.Animation | PauseType.Input;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Horizontal")]
    [SerializeField, Min(0.1f)] private float maxMoveSpeed = 7f;
    [SerializeField, Min(0f)] private float acceleration = 30f;
    [SerializeField, Min(0f)] private float deceleration = 10f;
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.85f;

    [Header("Jump")]
    [SerializeField, MinMaxInt(0.1f, 8f)] private MinMaxFloat jumpHeight = new MinMaxFloat(2.4f, 4f);
    [SerializeField, Min(0.05f)] private float timeToApex = 0.44f;
    [SerializeField, Min(1f)] private float jumpReleaseGravityMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

    [Header("Form")]
    [SerializeField] private MarioForm initialForm = MarioForm.Small;
    [SerializeField, Min(0f)] private float damageInvulnerabilityTime = 1f;
    [SerializeField, Range(0.05f, 1f)] private float invulnerabilityMinAlpha = 0.35f;
    [SerializeField, Min(1f)] private float invulnerabilityFlickerSpeed = 18f;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float stompBounceSpeed = 12f;
    [SerializeField, MinMaxInt(-1f, 1f)] private MinMaxFloat stompContactGap = new MinMaxFloat(-0.55f, 0.3f);
    [SerializeField, Min(0f)] private float stompContactPointTolerance = 0.08f;
    [SerializeField, Min(0f)] private float stompSideTolerance = 0.18f;
    [SerializeField, Min(0f)] private float stompMaxUpwardVelocity = 0.75f;

    [Header("Collider")]
    [SerializeField, Min(0.01f)] private Vector2 smallColliderSize = new Vector2(1f, 1f);
    [SerializeField] private Vector2 smallColliderOffset = new Vector2(0f, 0.5f);
    [SerializeField, Min(0.01f)] private Vector2 bigColliderSize = new Vector2(1f, 2f);
    [SerializeField] private Vector2 bigColliderOffset = new Vector2(0f, 1f);

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.12f);
    [SerializeField, Range(0f, 1f)] private float groundNormalMinY = 0.55f;

    [Header("Death")]
    [SerializeField, Min(0f)] private float deathPauseDuration = 0.15f;
    [SerializeField, Min(0f)] private float deathBounceDelay = 0.1f;
    [SerializeField, Min(0.1f)] private float deathBounceSpeed = 8f;
    [SerializeField, Min(0f)] private float deathOffscreenBuffer = 1f;
    [SerializeField, Min(0.1f)] private float deathFallbackFallDistance = 8f;

    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;
    private Camera sceneCamera;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isDead;
    private bool isSuper;
    private bool pendingGrow;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float damageInvulnerabilityTimer;
    private float jumpSpeed;
    private float shortJumpSpeed;
    private MarioForm form;
    private MarioForm pendingGrowForm;
    private Coroutine deathRoutine;
    private bool deathPauseActive;
    private SpriteRenderer[] spriteRenderers;
    private readonly Collider2D[] groundHits = new Collider2D[4];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private readonly Collider2D[] resizeHits = new Collider2D[4];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private Transform GroundCheck => groundCheck ? groundCheck : groundCheck = transform.Find("GroundCheck");
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
    private SpriteRenderer[] SpriteRenderers => spriteRenderers != null && spriteRenderers.Length > 0
        ? spriteRenderers
        : spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    private Camera SceneCamera => sceneCamera ? sceneCamera : sceneCamera = Camera.main;
    private CameraController SceneCameraController => SceneCamera ? SceneCamera.GetComponent<CameraController>() : null;

    public MarioForm Form => form;
    public bool IsSmall => form == MarioForm.Small;
    public bool IsSuper => isSuper;

    private void Awake()
    {
        stompContactGap.ClampAndOrder(-1f, 1f);
        form = (MarioForm)Mathf.Clamp((int)initialForm, (int)MarioForm.Small, (int)MarioForm.Fire);
        ApplySmallCollider();
        if (form != MarioForm.Small)
        {
            pendingGrow = true;
            pendingGrowForm = form;
            form = MarioForm.Small;
        }
    }

    private void OnEnable()
    {
        stompContactGap.ClampAndOrder(-1f, 1f);
        UpdateJumpPhysics();
        Body.simulated = true;
        BodyCollider.enabled = true;
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        moveAction.SetEnabled(true);
        jumpAction.SetEnabled(true);
    }

    private void OnDisable()
    {
        if (deathPauseActive)
        {
            PauseService.Resume(GameplayPauseTypes);
            deathPauseActive = false;
        }

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        SetSpriteOpacity(1f);
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        jumpAction.SetEnabled(false);
        moveAction.SetEnabled(false);
    }

    private void Update()
    {
        if (isDead) return;

        if (PauseService.IsPaused(PauseType.Input))
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferTimer = 0f;
        }
        else
            ReadInput();

        isGrounded = CheckGrounded();
        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        damageInvulnerabilityTimer = Mathf.Max(0f, damageInvulnerabilityTimer - Time.deltaTime);
        UpdateInvulnerabilityVisual();

        var jumpPressed = !PauseService.IsPaused(PauseType.Input) && (jumpAction?.action?.WasPressedThisFrame() ?? false);
        jumpBufferTimer = jumpPressed ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);

        TryStartJump();
        UpdateSpriteDirection();
        SyncAnimator();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        TryCompletePendingGrow();
        ApplyHorizontalMovement();
        ApplyJumpRelease();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {

        if (!collision) return;
        if (isDead) return;

        var stompable = collision.GetComponentInParent<IStompable>();
        if (stompable != null) return;
        if (!IsEnemyCollider(collision)) return;
        TakeDamage();

//if mario interacts update UI


        HandleEnemyTrigger(collision);

    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleEnemyCollision(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleEnemyTrigger(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleEnemyCollision(collision);
    }

    public void TakeDamage()
    {
        if (isDead || isSuper || damageInvulnerabilityTimer > 0f) return;
        if (IsSmall)
        {
            ResolveDeath();
            return;
        }

        SetForm((MarioForm)((int)form - 1));
        damageInvulnerabilityTimer = damageInvulnerabilityTime;
    }

    public void SetForm(MarioForm targetForm)
    {
        if (isDead) return;

        targetForm = (MarioForm)Mathf.Clamp((int)targetForm, (int)MarioForm.Small, (int)MarioForm.Fire);
        if (targetForm == form) return;

        var grew = targetForm > form;
        if (targetForm == MarioForm.Small) pendingGrow = false;

        if (grew && form == MarioForm.Small && !TryApplyBigCollider())
        {
            pendingGrow = true;
            pendingGrowForm = targetForm;
            return;
        }

        if (!grew && targetForm == MarioForm.Small) ApplySmallCollider();

        form = targetForm;
        Anim.TrySetTrigger(grew ? "grow" : "shrink");
    }

    public void SetSuper(bool value) => isSuper = value;

    private bool TryHandleEnemyContact(Collider2D collider, bool isStompContact)
    {
        if (!collider) return false;
        if (isDead) return true;

        var stompable = collider.GetComponentInParent<IStompable>();
        var isEnemy = IsEnemyCollider(collider);
        if (!isEnemy && stompable == null) return false;

        if (TryStompEnemy(collider, isStompContact, stompable)) return true;

        if (!isEnemy) return true;
        TakeDamage();
        return true;
    }

    private bool TryStompEnemy(Collider2D enemyCollider, bool isStompContact, IStompable cachedStompable = null)
    {
        if (!isStompContact) return false;

        var stompable = cachedStompable ?? enemyCollider.GetComponentInParent<IStompable>();
        if (stompable == null) return false;
        if (!stompable.TryStomp(this, BodyCollider.bounds.center)) return false;

        BounceFromStomp();
        return true;
    }

    private bool IsStompContact(Bounds enemyBounds)
    {
        var marioBounds = BodyCollider.bounds;
        var horizontalOverlap = Mathf.Min(marioBounds.max.x, enemyBounds.max.x) - Mathf.Max(marioBounds.min.x, enemyBounds.min.x);
        if (horizontalOverlap < -stompSideTolerance) return false;

        var feetGap = marioBounds.min.y - enemyBounds.max.y;
        if (feetGap < stompContactGap.min) return false;
        if (feetGap > stompContactGap.max) return false;

        if (marioBounds.min.y <= enemyBounds.min.y) return false;

        return true;
    }

    private void HandleEnemyCollision(Collision2D collision)
    {
        if (isDead) return;
        var enemyCollider = ResolveEnemyCollider(collision);
        if (!enemyCollider) return;
        var isStompContact = IsStompCollision(collision, enemyCollider.bounds);
        TryHandleEnemyContact(enemyCollider, isStompContact);
    }

    private void HandleEnemyTrigger(Collider2D collision)
    {
        if (!collision || isDead) return;
        if (IsOwnCollider(collision)) return;

        var isStompContact = Body.linearVelocity.y <= stompMaxUpwardVelocity && IsStompContact(collision.bounds);
        TryHandleEnemyContact(collision, isStompContact);
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

    private void BounceFromStomp()
    {
        var velocity = Body.linearVelocity;
        velocity.y = Mathf.Max(stompBounceSpeed, velocity.y);
        Body.linearVelocity = velocity;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private void ReadInput()
    {
        moveInput = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        jumpHeld = jumpAction?.action?.IsPressed() ?? false;
    }

    private void TryStartJump()
    {
        if (jumpBufferTimer <= 0f) return;
        if (coyoteTimer <= 0f) return;

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        var velocity = Body.linearVelocity;
        if (velocity.y < 0f) velocity.y = 0f;
        velocity.y = jumpSpeed;
        Body.linearVelocity = velocity;
    }

    private void ApplyHorizontalMovement()
    {
        var inputX = IsCrouching ? 0f : moveInput.x;
        if (Mathf.Abs(inputX) < InputDeadzone) inputX = 0f;

        var velocity = Body.linearVelocity;
        var currentSpeedX = velocity.x;
        var control = isGrounded ? 1f : airControlMultiplier;

        if (Mathf.Abs(inputX) > InputDeadzone)
        {
            var targetX = inputX * maxMoveSpeed;
            velocity.x = Mathf.MoveTowards(currentSpeedX, targetX, acceleration * control * Time.fixedDeltaTime);
        }
        else
        {
            if (!isGrounded)
            {
                Body.linearVelocity = velocity;
                return;
            }

            velocity.x = Mathf.MoveTowards(currentSpeedX, 0f, deceleration * Time.fixedDeltaTime);
        }

        Body.linearVelocity = velocity;
    }

    private void ApplyJumpRelease()
    {
        if (jumpHeld) return;

        var velocity = Body.linearVelocity;
        if (velocity.y <= 0f) return;
        if (velocity.y > shortJumpSpeed) velocity.y = shortJumpSpeed;

        var extraGravity = Mathf.Abs(Physics2D.gravity.y) * Body.gravityScale * (jumpReleaseGravityMultiplier - 1f);
        velocity.y -= extraGravity * Time.fixedDeltaTime;
        Body.linearVelocity = velocity;
    }

    private bool CheckGrounded()
    {
        var contactCount = Body.GetContacts(groundContacts);
        for (var i = 0; i < contactCount; i++)
        {
            var contact = groundContacts[i];
            if (!contact.collider || contact.collider.isTrigger) continue;
            if (contact.normal.y >= groundNormalMinY) return true;
        }

        if (groundCheckSize.x <= 0f || groundCheckSize.y <= 0f) return false;

        var probeCenter = GroundCheck ? (Vector2)GroundCheck.position : (Vector2)transform.position;
        var filter = new ContactFilter2D { useTriggers = false };
        var hitCount = Physics2D.OverlapBox(probeCenter, groundCheckSize, 0f, filter, groundHits);
        for (var i = 0; i < hitCount; i++)
            if (groundHits[i] && !groundHits[i].isTrigger && !IsOwnCollider(groundHits[i]))
                return true;

        return false;
    }

    private bool IsCrouching => isGrounded && moveInput.y < CrouchThreshold;

    private void SyncAnimator()
    {
        var velocity = Body.linearVelocity;
        var inputX = IsCrouching ? 0f : moveInput.x;
        if (Mathf.Abs(inputX) < InputDeadzone) inputX = 0f;

        var absVelocityX = Mathf.Abs(velocity.x);
        if (Mathf.Abs(inputX) > InputDeadzone) absVelocityX = Mathf.Max(absVelocityX, MinAnimMoveSpeed);

        Anim.TrySet("absVelocityX", absVelocityX);
        Anim.TrySet("velocityX", velocity.x);
        Anim.TrySet("inputX", inputX);
        Anim.TrySet("velocityY", velocity.y);
        Anim.TrySet("isGrounded", isGrounded);
        Anim.TrySet("isCrouching", IsCrouching);
        Anim.TrySet("isSkidding", IsSkidding(inputX, velocity.x));
        Anim.TrySet("form", (float)form);
    }

    private void UpdateSpriteDirection()
    {
        if (!Flipper) return;
        if (!isGrounded) return;
        if (Mathf.Abs(moveInput.x) <= InputDeadzone) return;
        Flipper.SetDirection(new Vector2(moveInput.x, 0f));
    }

    private void ResolveDeath()
    {
        if (isDead) return;

        isDead = true;
        pendingGrow = false;
        SetSpriteOpacity(1f);
        Anim.TrySetTrigger("die");
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, true);

        moveInput = Vector2.zero;
        jumpHeld = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DeathSequence());
    }

    private void OnDrawGizmosSelected()
    {
        var probeCenter = GroundCheck ? (Vector2)GroundCheck.position : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(probeCenter, groundCheckSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube((Vector2)transform.position + smallColliderOffset, smallColliderSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube((Vector2)transform.position + bigColliderOffset, bigColliderSize);
    }

    private void UpdateJumpPhysics()
    {
        var gravity = (2f * jumpHeight.max) / (timeToApex * timeToApex);
        jumpSpeed = gravity * timeToApex;
        shortJumpSpeed = Mathf.Sqrt(2f * gravity * jumpHeight.min);

        var worldGravity = Mathf.Abs(Physics2D.gravity.y);
        if (worldGravity <= 0.0001f) return;

        Body.gravityScale = gravity / worldGravity;
    }

    private void TryCompletePendingGrow()
    {
        if (!pendingGrow || isDead || !IsSmall) return;
        if (!TryApplyBigCollider()) return;

        pendingGrow = false;
        form = pendingGrowForm;
        Anim.TrySetTrigger("grow");
    }

    private bool TryApplyBigCollider()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        var center = (Vector2)transform.position + bigColliderOffset;
        var hitCount = Physics2D.OverlapBox(center, bigColliderSize, 0f, filter, resizeHits);
        for (var i = 0; i < hitCount; i++)
            if (resizeHits[i] && !resizeHits[i].isTrigger && !IsOwnCollider(resizeHits[i]))
                return false;

        SetBodyCollider(bigColliderSize, bigColliderOffset);
        return true;
    }

    private void ApplySmallCollider()
    {
        SetBodyCollider(smallColliderSize, smallColliderOffset);
    }

    private void SetBodyCollider(Vector2 size, Vector2 offset)
    {
        BodyCollider.size = size;
        BodyCollider.offset = offset;
    }

    private static bool IsSkidding(float inputX, float velocityX)
    {
        if (Mathf.Abs(inputX) <= InputDeadzone) return false;
        if (Mathf.Abs(velocityX) <= InputDeadzone) return false;
        return Mathf.Sign(inputX) != Mathf.Sign(velocityX);
    }

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && collider == BodyCollider;
    }

    private static bool IsEnemyCollider(Collider2D collider)
    {
        if (!collider) return false;
        if (collider.CompareTag("enemy")) return true;

        if (collider.attachedRigidbody && collider.attachedRigidbody.CompareTag("enemy"))
            return true;

        var transform = collider.transform;
        return transform && transform.root && transform.root.CompareTag("enemy");
    }

    private void UpdateInvulnerabilityVisual()
    {
        if (damageInvulnerabilityTimer <= 0f)
        {
            SetSpriteOpacity(1f);
            return;
        }

        var pulse = Mathf.PingPong(Time.time * invulnerabilityFlickerSpeed, 1f);
        var alpha = Mathf.Lerp(invulnerabilityMinAlpha, 1f, pulse);
        SetSpriteOpacity(alpha);
    }

    private void SetSpriteOpacity(float alpha)
    {
        var renderers = SpriteRenderers;
        for (var i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (!renderer) continue;
            var color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }

    private void OnValidate()
    {
        stompContactGap.ClampAndOrder(-1f, 1f);
    }

    private IEnumerator DeathSequence()
    {
        GameData.Instance.lives--;

        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.simulated = false;
        SceneCameraController?.SetAxisLocks(false, true, true);

        PauseService.Pause(GameplayPauseTypes);
        deathPauseActive = true;
        yield return new WaitForSecondsRealtime(deathPauseDuration);

        if (deathBounceDelay > 0f)
            yield return new WaitForSecondsRealtime(deathBounceDelay);

        BodyCollider.enabled = false;
        var verticalSpeed = Mathf.Max(deathBounceSpeed, MinDeathBounceSpeed);
        var gravity = Physics2D.gravity.y * Body.gravityScale;

        var fallbackCutoffY = transform.position.y - deathFallbackFallDistance;
        var cutoffY = GetDeathCutoffY(fallbackCutoffY);

        while (transform.position.y > cutoffY)
        {
            verticalSpeed += gravity * Time.unscaledDeltaTime;
            transform.position += Vector3.up * (verticalSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        if (deathPauseActive)
        {
            PauseService.Resume(GameplayPauseTypes);
            deathPauseActive = false;
        }

        deathRoutine = null;
        if (GameData.Instance.lives <= 0)
        {
            SceneManager.LoadScene("GameOver");
            yield break;
        }

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private float GetDeathCutoffY(float fallbackCutoffY)
    {
        var sceneCamera = SceneCamera;
        if (!sceneCamera || !sceneCamera.orthographic) return fallbackCutoffY;
        return sceneCamera.transform.position.y - sceneCamera.orthographicSize - deathOffscreenBuffer;
    }
}
