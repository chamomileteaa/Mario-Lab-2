using Animation;
using Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AnimatorCache))]
public class MarioController : MonoBehaviour
{
    private const float InputDeadzone = 0.01f;
    private const float CrouchThreshold = -0.5f;
    private const float MinAnimMoveSpeed = 0.2f;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Horizontal")]
    [SerializeField] private float maxMoveSpeed = 7f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private float airControlMultiplier = 0.85f;
    [SerializeField] private float skidDeceleration = 14f;

    [Header("Jump")]
    [SerializeField] private float maxJumpHeight = 4f;
    [SerializeField] private float minJumpHeight = 1.8f;
    [SerializeField] private float timeToApex = 0.44f;
    [SerializeField] private float coyoteTime = 0.08f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.12f);
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D body2D;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isDead;
    private float coyoteTimer;
    private float jumpSpeed;
    private float shortJumpSpeed;
    private readonly Collider2D[] groundHits = new Collider2D[4];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);

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
        ReadInput();
        isGrounded = CheckGrounded();
        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);

        if (jumpAction?.action?.WasPressedThisFrame() ?? false) TryStartJump();

        UpdateSpriteDirection();
        SyncAnimator();
    }

    private void FixedUpdate()
    {
        ApplyHorizontalMovement();
        ApplyJumpHold();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("enemy")) return;
        if (isDead) return;
        ResolveDeath();
    }

    private void ReadInput()
    {
        moveInput = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        jumpHeld = jumpAction?.action?.IsPressed() ?? false;
    }

    private void TryStartJump()
    {
        if (coyoteTimer <= 0f) return;

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
        var isSkidding = IsSkidding(inputX, currentSpeedX);

        var control = isGrounded ? 1f : airControlMultiplier;
        if (Mathf.Abs(inputX) > InputDeadzone)
        {
            var targetX = inputX * maxMoveSpeed;
            var rate = isSkidding ? skidDeceleration : acceleration * control;
            velocity.x = Mathf.MoveTowards(currentSpeedX, targetX, rate * Time.fixedDeltaTime);
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

    private void ApplyJumpHold()
    {
        if (jumpHeld) return;

        var velocity = Body.linearVelocity;
        if (velocity.y <= shortJumpSpeed) return;

        velocity.y = shortJumpSpeed;
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
        maxMoveSpeed = Mathf.Max(0.1f, maxMoveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        airControlMultiplier = Mathf.Clamp01(airControlMultiplier);
        skidDeceleration = Mathf.Max(0f, skidDeceleration);

        maxJumpHeight = Mathf.Max(0.1f, maxJumpHeight);
        minJumpHeight = Mathf.Clamp(minJumpHeight, 0.1f, maxJumpHeight);
        timeToApex = Mathf.Max(0.05f, timeToApex);
        groundCheckSize.x = Mathf.Max(0.01f, groundCheckSize.x);
        groundCheckSize.y = Mathf.Max(0.01f, groundCheckSize.y);
        coyoteTime = Mathf.Max(0f, coyoteTime);

        if (!groundCheck) groundCheck = transform.Find("GroundCheck");
        if (!spriteFlipper) spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);

        body2D = GetComponent<Rigidbody2D>();
        animatorCache = GetComponent<AnimatorCache>();
        UpdateJumpPhysics();
    }

    private void OnDrawGizmosSelected()
    {
        var probeCenter = groundCheck ? (Vector2)groundCheck.position : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(probeCenter, groundCheckSize);
    }

    private void UpdateJumpPhysics()
    {
        var apexTime = Mathf.Max(0.05f, timeToApex);
        var gravity = (2f * maxJumpHeight) / (apexTime * apexTime);
        jumpSpeed = gravity * apexTime;
        shortJumpSpeed = Mathf.Sqrt(2f * gravity * minJumpHeight);

        var worldGravity = Mathf.Abs(Physics2D.gravity.y);
        if (worldGravity <= 0.0001f) return;

        Body.gravityScale = gravity / worldGravity;
    }

    private static bool IsSkidding(float inputX, float velocityX)
    {
        if (Mathf.Abs(inputX) <= InputDeadzone) return false;
        if (Mathf.Abs(velocityX) <= InputDeadzone) return false;
        return Mathf.Sign(inputX) != Mathf.Sign(velocityX);
    }
}
