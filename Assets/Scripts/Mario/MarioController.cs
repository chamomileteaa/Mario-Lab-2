using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(MarioVisuals))]
[RequireComponent(typeof(MarioCollisionHandler))]
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

    [Header("Power")]
    [SerializeField, Min(0f)] private float defaultSuperDuration = 10f;
    [SerializeField, Min(0f)] private float defaultStarPowerDuration = 10f;

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

    [SerializeField] private MarioAudio marioSFX;
    [SerializeField] private MusicPlayer LevelMusic;

    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private MarioVisuals marioVisuals;
    private Camera sceneCamera;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isDead;
    private bool pendingGrow;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float damageInvulnerabilityTimer;
    private float superTimer;
    private float starPowerTimer;
    private float jumpSpeed;
    private float shortJumpSpeed;
    private MarioForm form;
    private MarioForm pendingGrowForm;
    private Coroutine deathRoutine;
    private bool deathPauseActive;
    private readonly Collider2D[] groundHits = new Collider2D[4];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private readonly Collider2D[] resizeHits = new Collider2D[4];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private MarioVisuals Visuals => marioVisuals ? marioVisuals : marioVisuals = GetComponent<MarioVisuals>();
    private Transform GroundCheck => groundCheck ? groundCheck : groundCheck = transform.Find("GroundCheck");
    private Camera SceneCamera => sceneCamera ? sceneCamera : sceneCamera = Camera.main;
    private CameraController SceneCameraController => SceneCamera ? SceneCamera.GetComponent<CameraController>() : null;

    public MarioForm Form => form;
    public bool IsSmall => form == MarioForm.Small;
    public bool IsSuper => superTimer > 0f;
    public bool IsStarPowered => starPowerTimer > 0f;
    public bool HasStarPower => IsStarPowered;
    public float StarPowerTimeRemaining => starPowerTimer;
    public bool IsDead => isDead;
    public bool IsGrounded => isGrounded;
    public bool IsDamageInvulnerable => damageInvulnerabilityTimer > 0f;
    public bool IsInvincible => IsSuper || IsStarPowered || IsDamageInvulnerable;
    public bool IsCrouching => isGrounded && moveInput.y < CrouchThreshold;
    public Vector2 MoveInput => moveInput;

    private void Start()
    {
        LevelMusic.PlayGroundTheme();
        form = (MarioForm)Mathf.Clamp((int)initialForm, (int)MarioForm.Small, (int)MarioForm.Fire);
        ApplySmallCollider();

        marioSFX = GetComponent<MarioAudio>();
        if (form == MarioForm.Small) return;
        pendingGrow = true;
        pendingGrowForm = form;
        form = MarioForm.Small;
    }

    private void OnEnable()
    {
        UpdateJumpPhysics();
        Body.simulated = true;
        BodyCollider.enabled = true;
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        moveAction.SetEnabled(true);
        jumpAction.SetEnabled(true);
    }

    private void OnDisable()
    {
        StopDeathSequence();
        Visuals?.ResetVisuals();
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        jumpAction.SetEnabled(false);
        moveAction.SetEnabled(false);
    }

    private void Update()
    {
        if (isDead) return;

        UpdateInputState();
        UpdateGroundState();
        UpdateTimers();
        Visuals?.RefreshVisualState();
        TryStartJump();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        TryCompletePendingGrow();
        ApplyHorizontalMovement();
        ApplyJumpRelease();
    }

    public void TakeDamage()
    {
        if (isDead || IsInvincible) return;

        if (IsSmall)
        {
            ResolveDeath();
            return;
        }

        SetForm((MarioForm)((int)form - 1));
        damageInvulnerabilityTimer = damageInvulnerabilityTime;
    }

    public void KillFromOutOfBounds()
    {
        ResolveDeath();
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

        if (!grew && targetForm == MarioForm.Small)
            ApplySmallCollider();

        form = targetForm;
        Visuals?.PlayFormTransition(grew);
    }

    public void ActivateSuper(float duration = -1f)
    {
        var targetDuration = duration < 0f ? defaultSuperDuration : duration;
        if (targetDuration <= 0f) return;
        superTimer = Mathf.Max(superTimer, targetDuration);
    }

    public void ActivateStarPower(float duration = -1f)
    {
        var targetDuration = duration < 0f ? defaultStarPowerDuration : duration;
        if (targetDuration <= 0f) return;
        LevelMusic.PlayInvincibilityCue();
        starPowerTimer = Mathf.Max(starPowerTimer, targetDuration);
    }

    public void ApplyEnemyStompBounce(float bounceSpeed)
    {
        var velocity = Body.linearVelocity;
        velocity.y = Mathf.Max(bounceSpeed, velocity.y);
        Body.linearVelocity = velocity;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private void UpdateInputState()
    {
        var inputPaused = PauseService.IsPaused(PauseType.Input);
        if (inputPaused)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferTimer = 0f;
            return;
        }

        moveInput = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        jumpHeld = jumpAction?.action?.IsPressed() ?? false;

        var jumpPressed = jumpAction?.action?.WasPressedThisFrame() ?? false;
        jumpBufferTimer = jumpPressed ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
    }

    private void UpdateGroundState()
    {
        isGrounded = CheckGrounded();
        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private void UpdateTimers()
    {
        damageInvulnerabilityTimer = Mathf.Max(0f, damageInvulnerabilityTimer - Time.deltaTime);
        if (PauseService.IsPaused(PauseType.Physics)) return;
        
        float previousStar = starPowerTimer;
        starPowerTimer = Mathf.Max(0f, starPowerTimer - Time.deltaTime);

        if (previousStar > 0f && starPowerTimer == 0f)
        {
            LevelMusic.PlayGroundTheme();  
        }

        superTimer = Mathf.Max(0f, superTimer - Time.deltaTime);
        starPowerTimer = Mathf.Max(0f, starPowerTimer - Time.deltaTime);
    }

    private void TryStartJump()
    {
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f) return;

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        marioSFX.PlayJump();
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
        {
            if (groundHits[i] && !groundHits[i].isTrigger && !IsOwnCollider(groundHits[i]))
                return true;
        }

        return false;
    }

    private void ResolveDeath()
    {
        if (isDead) return;

        LevelMusic.PlayDeathCue();

        isDead = true;
        pendingGrow = false;
        damageInvulnerabilityTimer = 0f;
        superTimer = 0f;
        starPowerTimer = 0f;

        moveInput = Vector2.zero;
        jumpHeld = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        Visuals?.ResetVisuals();
        Visuals?.PlayDeath();
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, true);
        
        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DeathSequence());
    }

    private void StopDeathSequence()
    {
        if (deathPauseActive)
        {
            PauseService.Resume(GameplayPauseTypes);
            deathPauseActive = false;
        }

        if (deathRoutine == null) return;
        StopCoroutine(deathRoutine);
        deathRoutine = null;
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
        Visuals?.PlayFormTransition(true);
    }

    private bool TryApplyBigCollider()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        var center = (Vector2)transform.position + bigColliderOffset;
        var hitCount = Physics2D.OverlapBox(center, bigColliderSize, 0f, filter, resizeHits);
        for (var i = 0; i < hitCount; i++)
        {
            if (resizeHits[i] && !resizeHits[i].isTrigger && !IsOwnCollider(resizeHits[i]))
                return false;
        }

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

    private bool IsOwnCollider(Collider2D collider)
    {
        return collider && collider == BodyCollider;
    }

    private IEnumerator DeathSequence()
    {
        var gameData = ResolveGameData();
        if (gameData) gameData.LoseLife();

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
        if (gameData && gameData.lives <= 0)
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

    private static GameData ResolveGameData()
    {
        if (GameData.Instance) return GameData.Instance;

        var found = FindFirstObjectByType<GameData>(FindObjectsInactive.Include);
        if (found && !GameData.Instance) GameData.Instance = found;
        return found;
    }
}
