using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AnimatorCache))]
public class MarioController : MonoBehaviour
{
    public enum MarioForm
    {
        Small = 0,
        Big = 1,
        Fire = 2
    }

    private const float InputDeadzone = 0.01f;
    private const float CrouchThreshold = -0.5f;
    private const float MinAnimMoveSpeed = 0.2f;

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

    [Header("Collider")]
    [SerializeField, Min(0.01f)] private Vector2 smallColliderSize = new Vector2(1f, 1f);
    [SerializeField] private Vector2 smallColliderOffset = new Vector2(0f, 0.5f);
    [SerializeField, Min(0.01f)] private Vector2 bigColliderSize = new Vector2(1f, 2f);
    [SerializeField] private Vector2 bigColliderOffset = new Vector2(0f, 1f);

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.12f);
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;

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
    private readonly Collider2D[] groundHits = new Collider2D[4];
    private readonly Collider2D[] resizeHits = new Collider2D[4];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);

    public MarioForm Form => form;
    public bool IsSmall => form == MarioForm.Small;
    public bool IsSuper => isSuper;

    private void Awake()
    {
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
        UpdateJumpPhysics();
        moveAction.SetEnabled(true);
        jumpAction.SetEnabled(true);
    }

    private void OnDisable()
    {
        jumpAction.SetEnabled(false);
        moveAction.SetEnabled(false);
    }

    private void Update()
    {
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

        var jumpPressed = !PauseService.IsPaused(PauseType.Input) && (jumpAction?.action?.WasPressedThisFrame() ?? false);
        jumpBufferTimer = jumpPressed ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);

        TryStartJump();
        UpdateSpriteDirection();
        SyncAnimator();
    }

    private void FixedUpdate()
    {
        TryCompletePendingGrow();
        ApplyHorizontalMovement();
        ApplyJumpRelease();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("enemy")) return;
        if (isDead) return;
        TakeDamage();
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
        if (groundCheckSize.x <= 0f || groundCheckSize.y <= 0f) return false;

        var probeCenter = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
        var hitCount = Physics2D.OverlapBox(probeCenter, groundCheckSize, 0f, filter, groundHits);
        for (var i = 0; i < hitCount; i++)
            if (groundHits[i] && !groundHits[i].isTrigger)
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

        var vertical = Mathf.Abs(moveInput.y) > InputDeadzone ? moveInput.y : Body.linearVelocity.y;
        Flipper.SetDirection(new Vector2(moveInput.x, vertical));
    }

    private void ResolveDeath()
    {
        isDead = true;
        pendingGrow = false;
        Anim.TrySetTrigger("die");

        GameData.lives--;
        if (GameData.lives <= 0)
        {
            SceneManager.LoadScene("GameOver");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void OnValidate()
    {
        if (!groundCheck) groundCheck = transform.Find("GroundCheck");
        if (!spriteFlipper) spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
        if (!Application.isPlaying) form = initialForm;
    }

    private void OnDrawGizmosSelected()
    {
        var probeCenter = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
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
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayer, useTriggers = false };
        var center = (Vector2)transform.position + bigColliderOffset;
        var hitCount = Physics2D.OverlapBox(center, bigColliderSize, 0f, filter, resizeHits);
        for (var i = 0; i < hitCount; i++)
            if (resizeHits[i] && resizeHits[i] != BodyCollider && !resizeHits[i].isTrigger)
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
}
