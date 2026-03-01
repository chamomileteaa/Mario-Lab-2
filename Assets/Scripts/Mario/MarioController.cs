using System.Collections;
using UnityEngine;
using System;
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
    private const float MinHitInvulnerabilityTime = 1.75f;
    private const string FireThrowTrigger = "throw";
    private const string FireActionName = "Attack";
    private const PauseType MarioPauseBypassTypes = PauseType.Physics | PauseType.Animation;
    private const PauseType GameplayPauseTypes = PauseType.Physics | PauseType.Animation | PauseType.Input;
    private const PauseType FormTransitionPauseTypes = PauseType.Physics | PauseType.Input;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("Horizontal")]
    [SerializeField, Min(0.1f)] private float maxMoveSpeed = 7f;
    [SerializeField, Min(0f)] private float acceleration = 30f;
    [SerializeField, Min(0f)] private float deceleration = 10f;
    [SerializeField, Range(0f, 1f)] private float airControlMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float skidMinSpeed = 3f;
    [SerializeField, Min(0f)] private float skidCooldown = 0.12f;

    [Header("Jump")]
    [SerializeField, MinMaxInt(0.1f, 8f)] private MinMaxFloat jumpHeight = new MinMaxFloat(2.4f, 4f);
    [SerializeField, Min(0.05f)] private float timeToApex = 0.44f;
    [SerializeField, Min(1f)] private float jumpReleaseGravityMultiplier = 2.5f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.08f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

    [Header("Form")]
    [SerializeField] private MarioForm initialForm = MarioForm.Small;
    [SerializeField, Min(0f)] private float damageInvulnerabilityTime = 1f;
    [SerializeField, Min(0f)] private float formTransitionPauseFallback = 0.2f;

    [Header("Power")]
    [SerializeField, Min(0f)] private float defaultFormProtectionDuration = 10f;
    [SerializeField, Min(0f)] private float defaultStarPowerDuration = 10f;

    [Header("Fireball")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private Vector2 fireballSpawnOffset = new Vector2(0.55f, 0.9f);
    [SerializeField, Min(0f)] private float fireballCooldown = 0.15f;
    [SerializeField, Min(1)] private int maxActiveFireballs = 2;

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
    [SerializeField, Min(0f)] private float deathSceneReloadDelay = 2.8f;

    [Header("Victory")]
    [SerializeField, Min(0.1f)] private float victorySlideSpeed = 3f;
    [SerializeField, Min(0.1f)] private float victoryWalkSpeed = 3f;
    [SerializeField, Min(0f)] private float victoryCastleEntryDelay = 1f;
    [SerializeField, Min(0f)] private float victoryDoorReachThreshold = 0.05f;
    [SerializeField, Min(0f)] private float victoryTurnDuration = 0.2f;
    [SerializeField, Min(0f)] private float poleJumpHorizontalSpeed = 2.4f;
    [SerializeField, Min(0f)] private float poleJumpVerticalSpeed = 5f;
    [SerializeField, Min(0f)] private float postFireworksBuffer = 5f;
    [SerializeField] private bool hideMarioOnCastleEntry = true;
    [SerializeField] private bool disableMarioAtCastleEntry = true;
    [SerializeField, Min(0)] private int timerScorePerTick = 50;
    [SerializeField, Min(0.001f)] private float timerTickInterval = 0.01f;

    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private MarioVisuals marioVisuals;
    private AnimatorCache animatorCache;
    private SpriteFlipper spriteFlipper;
    private Camera sceneCamera;
    private InputAction fireAction;
    private MarioAudio marioAudio;
    private MusicPlayer musicPlayer;
    private GameOverOverlayController gameOverOverlay;
    private GameManager gameManager;

    private Vector2 moveInput;
    private bool jumpHeld;
    private bool firePressedThisFrame;
    private bool isGrounded;
    private bool isDead;
    private bool pendingGrow;
    private bool isWinning;
    private bool isOnPole;
    private bool isPipeTravelling;
    private Coroutine winRoutine;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float damageInvulnerabilityTimer;
    private float pipeInvulnerabilityTimer;
    private float formProtectionTimer;
    private float starPowerTimer;
    private float jumpSpeed;
    private float shortJumpSpeed;
    private float lastSkidTime = -999f;
    private float nextFireballTime;
    private float facingDirectionX = 1f;
    private bool forcedCrouch;
    private bool hasForcedMoveInput;
    private Vector2 forcedMoveInput;
    private MarioForm form;
    private MarioForm pendingGrowForm;
    private Coroutine deathRoutine;
    private Coroutine formTransitionRoutine;
    private bool deathPauseActive;
    private bool formPauseActive;
    private bool castleEntryApplied;
    private readonly Collider2D[] groundHits = new Collider2D[4];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private readonly Collider2D[] resizeHits = new Collider2D[4];

    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private MarioVisuals Visuals => marioVisuals ? marioVisuals : marioVisuals = GetComponent<MarioVisuals>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private MarioAudio MarioAudioPlayer => marioAudio ? marioAudio : marioAudio = GetComponent<MarioAudio>();
    private MusicPlayer Music => musicPlayer ? musicPlayer : musicPlayer = FindFirstObjectByType<MusicPlayer>(FindObjectsInactive.Include);
    private SpriteFlipper Flipper => spriteFlipper ? spriteFlipper : spriteFlipper = GetComponentInChildren<SpriteFlipper>(true);
    private Transform GroundCheck => groundCheck ? groundCheck : groundCheck = transform.Find("GroundCheck");
    private Camera SceneCamera => sceneCamera ? sceneCamera : sceneCamera = Camera.main;
    private CameraController SceneCameraController => SceneCamera ? SceneCamera.GetComponent<CameraController>() : null;

    public bool IsWinning => isWinning;
    public bool IsOnPole => isOnPole;
    public bool IsPipeTravelling => isPipeTravelling;
    public MarioForm Form => form;
    public bool IsSmall => form == MarioForm.Small;
    public bool IsFormProtected => formProtectionTimer > 0f;
    public bool IsStarPowered => starPowerTimer > 0f;
    public bool HasStarPower => IsStarPowered;
    public float StarPowerTimeRemaining => starPowerTimer;
    public bool IsDead => isDead;
    public bool IsGrounded => isGrounded;
    public bool IsDamageInvulnerable => damageInvulnerabilityTimer > 0f || pipeInvulnerabilityTimer > 0f;
    public bool IsDamageInvulnerableVisual => damageInvulnerabilityTimer > 0f;
    public bool IsPipeInvulnerable => pipeInvulnerabilityTimer > 0f;
    public bool IsInvincible => IsStarPowered || IsDamageInvulnerable;
    public bool IsCrouching => isGrounded && (forcedCrouch || moveInput.y < CrouchThreshold);
    public bool IsJumpHeld => jumpHeld;
    public float VerticalSpeed => Body.linearVelocity.y;
    public float FullJumpTakeoffSpeed => jumpSpeed;
    public Vector2 MoveInput => hasForcedMoveInput ? forcedMoveInput : moveInput;
    public event Action Spawned;
    public event Action Jumped;
    public event Action<MarioForm, MarioForm> FormChanged;
    public event Action CoinCollected;
    public event Action EnemyStomped;
    public event Action Damaged;
    public event Action ExtraLifeCollected;
    public event Action FireballShot;
    public event Action PipeTravelled;
    public event Action Skidded;
    public event Action Kicked;
    public event Action<bool> StarPowerChanged;
    public event Action Died;

    private void Start()
    {
        form = (MarioForm)Mathf.Clamp((int)initialForm, (int)MarioForm.Small, (int)MarioForm.Fire);
        ApplySmallCollider();
        Spawned?.Invoke();

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
        ResolveFireAction()?.Enable();
    }

    private void OnDisable()
    {
        StopFormTransitionSequence();
        StopDeathSequence();
        isOnPole = false;
        Visuals?.ResetVisuals();
        PauseService.SetPauseBypass(gameObject, MarioPauseBypassTypes, false);
        fireAction?.Disable();
        jumpAction.SetEnabled(false);
        moveAction.SetEnabled(false);
    }

    private void Update()
    {
        var data = ResolveGameData();
        if (data && !data.runActive)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferTimer = 0f;
            firePressedThisFrame = false;
            return;
        }

        if (isDead) return;
        if (isPipeTravelling)
        {
            UpdateTimers();
            Visuals?.RefreshVisualState();
            return;
        }

        if (isWinning)
        {
            UpdateTimers();
            Visuals?.RefreshVisualState();
            return;
        }

        UpdateInputState();
        UpdateGroundState();
        UpdateTimers();
        Visuals?.RefreshVisualState();
        TryStartJump();
        TryShootFireball();
    }

    private void FixedUpdate()
    {
        var data = ResolveGameData();
        if (data && !data.runActive)
        {
            if (Body)
            {
                var velocity = Body.linearVelocity;
                velocity.x = 0f;
                Body.linearVelocity = velocity;
            }
            return;
        }

        if (isDead || isWinning || isPipeTravelling) return;

        TryCompletePendingGrow();
        ApplyHorizontalMovement();
        ApplyJumpRelease();
    }

    public void TakeDamage()
    {
        if (isDead || isPipeTravelling || IsInvincible) return;

        if (IsSmall)
        {
            ResolveDeath();
            return;
        }

        SetForm(MarioForm.Small);
        Damaged?.Invoke();
        damageInvulnerabilityTimer = Mathf.Max(damageInvulnerabilityTime, MinHitInvulnerabilityTime);
    }

    public void KillFromOutOfBounds()
    {
        if (IsPipeInvulnerable) return;
        ResolveDeath();
    }

    public void SetForm(MarioForm targetForm)
    {
        if (isDead) return;

        var previousForm = form;
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

        var transitionGrew = ShouldUseGrowTransitionVisual(previousForm, targetForm, grew);
        Visuals?.PlayFormTransition(transitionGrew);

        form = targetForm;
        FormChanged?.Invoke(previousForm, form);
        StartFormTransitionPause(transitionGrew, previousForm, form);
    }

    public void ActivateFormProtection(float duration = -1f)
    {
        var targetDuration = duration < 0f ? defaultFormProtectionDuration : duration;
        if (targetDuration <= 0f) return;
        formProtectionTimer = Mathf.Max(formProtectionTimer, targetDuration);
    }

    public void ActivateStarPower(float duration = -1f)
    {
        var targetDuration = duration < 0f ? defaultStarPowerDuration : duration;
        if (targetDuration <= 0f) return;
        var wasStarPowered = IsStarPowered;
        starPowerTimer = Mathf.Max(starPowerTimer, targetDuration);
        if (!wasStarPowered && IsStarPowered)
            StarPowerChanged?.Invoke(true);
    }

    public void NotifyExtraLifeCollected()
    {
        ExtraLifeCollected?.Invoke();
    }

    public void NotifyCoinCollected()
    {
        CoinCollected?.Invoke();
    }

    public void NotifyFireballShot()
    {
        FireballShot?.Invoke();
    }

    public void NotifyPipeTravelled()
    {
        PipeTravelled?.Invoke();
    }

    public void ActivatePipeInvulnerability(float duration)
    {
        if (duration <= 0f) return;
        pipeInvulnerabilityTimer = Mathf.Max(pipeInvulnerabilityTimer, duration);
    }

    public void SetPipeTravelState(bool travelling)
    {
        if (isDead && travelling) return;
        if (isPipeTravelling == travelling) return;

        isPipeTravelling = travelling;
        if (travelling)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            return;
        }

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        moveInput = Vector2.zero;
        forcedCrouch = false;
        hasForcedMoveInput = false;
        forcedMoveInput = Vector2.zero;
    }

    public void SetForcedCrouchState(bool state)
    {
        forcedCrouch = state;
    }

    public void SetForcedMoveInput(Vector2 input, bool enabled)
    {
        hasForcedMoveInput = enabled;
        forcedMoveInput = enabled ? input : Vector2.zero;
    }

    public void NotifyKicked()
    {
        Kicked?.Invoke();
    }

    public void ApplyEnemyStompBounce(float bounceSpeed)
    {
        var velocity = Body.linearVelocity;
        velocity.y = Mathf.Max(bounceSpeed, velocity.y);
        Body.linearVelocity = velocity;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        EnemyStomped?.Invoke();
    }

    private void UpdateInputState()
    {
        var inputPaused = PauseService.IsPaused(PauseType.Input);
        if (inputPaused)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferTimer = 0f;
            firePressedThisFrame = false;
            return;
        }

        moveInput = moveAction?.action?.ReadValue<Vector2>() ?? Vector2.zero;
        jumpHeld = jumpAction?.action?.IsPressed() ?? false;
        firePressedThisFrame = ResolveFireAction()?.WasPressedThisFrame() ?? false;
        if (Mathf.Abs(moveInput.x) > InputDeadzone)
            facingDirectionX = Mathf.Sign(moveInput.x);

        var jumpPressed = jumpAction?.action?.WasPressedThisFrame() ?? false;
        jumpBufferTimer = jumpPressed ? jumpBufferTime : Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
    }

    private void TryShootFireball()
    {
        if (!firePressedThisFrame) return;
        firePressedThisFrame = false;

        if (isDead || isWinning) return;
        if (form != MarioForm.Fire) return;
        if (Time.time < nextFireballTime) return;
        if (FireballController.ActiveCount >= maxActiveFireballs) return;

        var prefab = ResolveFireballPrefab();
        if (!prefab) return;

        var directionX = ResolveFireballDirectionX();
        var spawnPosition = (Vector2)transform.position + new Vector2(fireballSpawnOffset.x * directionX, fireballSpawnOffset.y);
        var spawned = PrefabPoolService.Spawn(prefab, spawnPosition, Quaternion.identity);
        if (!spawned) return;

        if (!spawned.TryGetComponent<FireballController>(out var fireball))
        {
            Debug.LogError($"Fireball prefab '{prefab.name}' is missing FireballController.", prefab);
            PrefabPoolService.Despawn(spawned);
            return;
        }

        fireball.Launch(this, spawnPosition, directionX);
        Anim?.TrySetTrigger(FireThrowTrigger);
        NotifyFireballShot();
        nextFireballTime = Time.time + fireballCooldown;
    }

    private InputAction ResolveFireAction()
    {
        if (fireAction != null) return fireAction;

        var actionMap = moveAction?.action?.actionMap ?? jumpAction?.action?.actionMap;
        fireAction = actionMap?.FindAction(FireActionName, false);
        return fireAction;
    }

    private GameObject ResolveFireballPrefab()
    {
        return fireballPrefab;
    }

    private float ResolveFireballDirectionX()
    {
        var flipper = Flipper;
        if (flipper != null && Mathf.Abs(flipper.FacingX) > InputDeadzone)
            return Mathf.Sign(flipper.FacingX);

        return facingDirectionX >= 0f ? 1f : -1f;
    }

    private void UpdateGroundState()
    {
        isGrounded = CheckGrounded();
        coyoteTimer = isGrounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.deltaTime);
    }

    private void UpdateTimers()
    {
        damageInvulnerabilityTimer = Mathf.Max(0f, damageInvulnerabilityTimer - Time.deltaTime);
        pipeInvulnerabilityTimer = Mathf.Max(0f, pipeInvulnerabilityTimer - Time.deltaTime);
        if (PauseService.IsPaused(PauseType.Physics)) return;

        var previousStar = starPowerTimer;
        starPowerTimer = Mathf.Max(0f, starPowerTimer - Time.deltaTime);

        formProtectionTimer = Mathf.Max(0f, formProtectionTimer - Time.deltaTime);
        if (previousStar > 0f && starPowerTimer <= 0f)
            StarPowerChanged?.Invoke(false);
    }

    private void TryStartJump()
    {
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f) return;

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        Jumped?.Invoke();
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
            if (isGrounded &&
                Mathf.Abs(currentSpeedX) >= skidMinSpeed &&
                Mathf.Sign(currentSpeedX) != Mathf.Sign(inputX) &&
                Time.time >= lastSkidTime + skidCooldown)
            {
                lastSkidTime = Time.time;
                Skidded?.Invoke();
            }

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

        StopFormTransitionSequence();
        Died?.Invoke();

        isDead = true;
        isOnPole = false;
        pendingGrow = false;
        damageInvulnerabilityTimer = 0f;
        pipeInvulnerabilityTimer = 0f;
        formProtectionTimer = 0f;
        starPowerTimer = 0f;
        forcedCrouch = false;
        hasForcedMoveInput = false;
        forcedMoveInput = Vector2.zero;

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
        Visuals?.PlayFormTransition(true);
        form = pendingGrowForm;
        FormChanged?.Invoke(MarioForm.Small, form);
        StartFormTransitionPause(true, MarioForm.Small, form);
    }

    private bool TryApplyBigCollider()
    {
        var currentBounds = BodyCollider.bounds;
        var targetCenter = (Vector2)transform.position + bigColliderOffset;
        var targetBounds = new Bounds(targetCenter, bigColliderSize);
        var addedHeight = targetBounds.max.y - currentBounds.max.y;
        if (addedHeight <= 0f)
        {
            SetBodyCollider(bigColliderSize, bigColliderOffset);
            return true;
        }

        var checkCenter = new Vector2(targetBounds.center.x, currentBounds.max.y + addedHeight * 0.5f);
        var checkSize = new Vector2(targetBounds.size.x, addedHeight);
        var filter = new ContactFilter2D { useTriggers = false };
        var hitCount = Physics2D.OverlapBox(checkCenter, checkSize, 0f, filter, resizeHits);

        for (var i = 0; i < hitCount; i++)
        {
            var hit = resizeHits[i];
            if (!hit || hit.isTrigger || IsOwnCollider(hit)) continue;
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
        var timeUp = gameData && gameData.timer <= 0f;
        var outOfLives = gameData && gameData.lives <= 0;
        if (timeUp || outOfLives) Music?.PlayGameOverTheme();
        else Music?.PlayDeathTheme();

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

        deathRoutine = null;

        if (timeUp || outOfLives)
        {
            var overlay = ResolveGameOverOverlay();
            if (overlay)
            {
                if (timeUp) overlay.ShowTimeUpPersistent();
                else overlay.ShowGameOverPersistent();
            }
        }

        if (deathSceneReloadDelay > 0f)
            yield return new WaitForSecondsRealtime(deathSceneReloadDelay);

        if (deathPauseActive)
        {
            PauseService.Resume(GameplayPauseTypes);
            deathPauseActive = false;
        }

        var manager = ResolveGameManager();
        if (manager)
        {
            if (timeUp || outOfLives) manager.ReloadSceneToMainMenu();
            else manager.ReloadSceneForRetry();
            yield break;
        }

        if (timeUp || outOfLives)
            gameData?.ResetAll();

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void StartFormTransitionPause(bool grew, MarioForm fromForm, MarioForm toForm)
    {
        var duration = Visuals ? Visuals.GetFormTransitionDuration(grew) : 0f;
        if (duration <= 0f) duration = formTransitionPauseFallback;
        if (duration <= 0f)
        {
            StopFormTransitionSequence();
            return;
        }

        if (IsBigFireTransition(fromForm, toForm) && toForm > fromForm)
            Visuals?.ForceStarVisualForDuration(duration);

        if (formTransitionRoutine != null)
            StopCoroutine(formTransitionRoutine);

        if (formPauseActive)
        {
            PauseService.Resume(GameplayPauseTypes);
            formPauseActive = false;
        }

        formTransitionRoutine = StartCoroutine(FormTransitionPauseSequence(duration));
    }

    private static bool IsBigFireTransition(MarioForm fromForm, MarioForm toForm)
    {
        return (fromForm == MarioForm.Big && toForm == MarioForm.Fire) ||
               (fromForm == MarioForm.Fire && toForm == MarioForm.Big);
    }

    private static bool ShouldUseGrowTransitionVisual(MarioForm fromForm, MarioForm toForm, bool defaultGrew)
    {
        if (IsBigFireTransition(fromForm, toForm) && toForm > fromForm)
            return true;
        return defaultGrew;
    }

    private void StopFormTransitionSequence()
    {
        if (formTransitionRoutine != null)
        {
            StopCoroutine(formTransitionRoutine);
            formTransitionRoutine = null;
        }

        if (!formPauseActive) return;

        PauseService.Resume(FormTransitionPauseTypes);
        formPauseActive = false;
    }

    private IEnumerator FormTransitionPauseSequence(float duration)
    {
        PauseService.Pause(FormTransitionPauseTypes);
        formPauseActive = true;

        yield return new WaitForSecondsRealtime(duration);

        if (formPauseActive)
        {
            PauseService.Resume(FormTransitionPauseTypes);
            formPauseActive = false;
        }

        formTransitionRoutine = null;
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

    private GameManager ResolveGameManager()
    {
        if (gameManager) return gameManager;
        if (GameManager.Instance)
        {
            gameManager = GameManager.Instance;
            return gameManager;
        }

        gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        return gameManager;
    }

    private GameOverOverlayController ResolveGameOverOverlay()
    {
        if (gameOverOverlay) return gameOverOverlay;
        gameOverOverlay = FindFirstObjectByType<GameOverOverlayController>(FindObjectsInactive.Include);
        return gameOverOverlay;
    }

    public void StartVictoryScreen(Transform poleTransform)
    {
        var fallbackBottomY = (poleTransform ? poleTransform.position.y : transform.position.y) - 3.5f;
        var fallbackDoorX = (poleTransform ? poleTransform.position.x : transform.position.x) + 2.2f;
        StartVictoryScreen(poleTransform, fallbackBottomY, fallbackDoorX, 0f, null);
    }

    public void StartVictoryScreen(Transform poleTransform, float poleBottomY, float castleDoorX, float poleXOffset = 0f, Action onReachedCastleDoor = null)
    {
        if (isDead || isWinning) return;
        isWinning = true;
        castleEntryApplied = false;
        SetOnPoleState(true);

        moveInput = Vector2.zero;
        jumpHeld = false;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        StopFormTransitionSequence();
        
        if (winRoutine != null) StopCoroutine(winRoutine);

        winRoutine = StartCoroutine(WinSequence(poleTransform, poleBottomY, castleDoorX, poleXOffset, onReachedCastleDoor));
    }

    private IEnumerator WinSequence(Transform poleTransform, float poleBottomY, float castleDoorX, float poleXOffset, Action onReachedCastleDoor)
    {
        var anchorX = poleTransform ? poleTransform.position.x + poleXOffset : transform.position.x;

        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.simulated = true;
        BodyCollider.enabled = true;
        var pos = transform.position;
        pos.x = anchorX;
        transform.position = pos;

        while (transform.position.y > poleBottomY)
        {
            var step = victorySlideSpeed * Time.deltaTime;
            pos = transform.position;
            pos.x = anchorX;
            pos.y = Mathf.Max(poleBottomY, pos.y - step);
            transform.position = pos;
            Body.linearVelocity = new Vector2(0f, -victorySlideSpeed);
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        Flipper?.SetDirection(Vector2.left);
        if (victoryTurnDuration > 0f)
            yield return new WaitForSeconds(victoryTurnDuration);

        SetOnPoleState(false);
        Flipper?.SetDirection(Vector2.right);
        Body.linearVelocity = new Vector2(Mathf.Max(0f, poleJumpHorizontalSpeed), Mathf.Max(0f, poleJumpVerticalSpeed));

        while (!isGrounded)
        {
            UpdateGroundState();
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;

        while (Mathf.Abs(transform.position.x - castleDoorX) > victoryDoorReachThreshold)
        {
            var direction = Mathf.Sign(castleDoorX - transform.position.x);
            if (Mathf.Abs(direction) < InputDeadzone) direction = 1f;
            Body.linearVelocity = new Vector2(direction * victoryWalkSpeed, Body.linearVelocity.y);
            if (direction >= 0f) Flipper?.SetDirection(Vector2.right);
            else Flipper?.SetDirection(Vector2.left);
            yield return null;
        }

        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        ApplyCastleEntryState();
        onReachedCastleDoor?.Invoke();

        yield return AwardTimerBonusRoutine();

        if (postFireworksBuffer > 0f)
            yield return new WaitForSeconds(postFireworksBuffer);

        if (victoryCastleEntryDelay > 0f)
            yield return new WaitForSeconds(victoryCastleEntryDelay);

        var manager = ResolveGameManager();
        if (manager)
        {
            manager.ReloadSceneToMainMenu();
            yield break;
        }

        var gameData = ResolveGameData();
        if (gameData) gameData.ResetAll();

        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void SetOnPoleState(bool value)
    {
        isOnPole = value;
        Anim?.TrySet("isOnPole", value);
    }

    private void ApplyCastleEntryState()
    {
        if (castleEntryApplied) return;
        castleEntryApplied = true;

        if (hideMarioOnCastleEntry)
        {
            var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                if (!spriteRenderers[i]) continue;
                spriteRenderers[i].enabled = false;
            }
        }

        if (!disableMarioAtCastleEntry) return;

        if (BodyCollider) BodyCollider.enabled = false;
        if (Body)
        {
            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.simulated = false;
        }
    }

    private IEnumerator AwardTimerBonusRoutine()
    {
        var data = ResolveGameData();
        if (!data) yield break;

        var tickInterval = Mathf.Max(0.001f, timerTickInterval);
        var pointsPerTick = Mathf.Max(0, timerScorePerTick);
        var remaining = Mathf.Max(0, Mathf.CeilToInt(data.timer));
        if (remaining <= 0) yield break;

        while (remaining > 0)
        {
            remaining--;
            data.SetTimer(remaining);
            if (pointsPerTick > 0)
                data.AddScore(pointsPerTick);

            yield return new WaitForSecondsRealtime(tickInterval);
        }
    }
}
