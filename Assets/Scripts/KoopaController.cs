using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;


[DisallowMultipleComponent]
[RequireComponent(typeof(EntityController))]
[RequireComponent(typeof(AnimatorCache))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class KoopaController : MonoBehaviour, IStompHandler, IEnemyImpactHandler
{
    private const string SquashTrigger = "isHit";

    [Header("Stomp")] 
    [SerializeField, Min(0.05f)] private float squishDuration = 5.0f;
    [SerializeField, Min(0)] private int stompScore = 100;
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);


    [Header("Knock Away")]
    [FormerlySerializedAs("defeatWhenKnockedAway")]
    [SerializeField] private bool defeatWhenKnockedBack = true;
    [SerializeField, Min(0.5f)] private float despawnBelowSpawnDistance = 12f;

    [SerializeField] private EnemyAudio enemySFX;

    private EntityController entityController;
    private Rigidbody2D body2D;
    private BoxCollider2D bodyCollider2D;
    private Animator animatorComponent;
    private AnimatorCache animatorCache;
    private EnemyAudio enemyAudio;
    private Coroutine squishRoutine;
    private float initialGravityScale = 1f;
    private string initialTag;
    private float spawnY;
    private bool defeated;
    private bool squished;
    private bool inShell;
    private bool shellMoving;
    
    private EntityController Entity => entityController ? entityController : entityController = GetComponent<EntityController>();
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private BoxCollider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<BoxCollider2D>();
    private Animator Animator => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private EnemyAudio Audio => enemyAudio ? enemyAudio : enemyAudio = GetComponent<EnemyAudio>();
    public GameObject ScorePopupPrefab => scorePopupPrefab;

    private void Awake()
    {
        spawnY = transform.position.y;
        initialGravityScale = Body.gravityScale;
        initialTag = gameObject.tag;
        
        enemySFX = GetComponent<EnemyAudio>();
    }
    
    private void OnEnable()
    {
        spawnY = transform.position.y;
        defeated = false;
        squished = false;
        gameObject.tag = initialTag;
        Body.simulated = true;
        Body.gravityScale = initialGravityScale;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        BodyCollider.enabled = true;
        ResetAnimationState();
        Entity.KnockbackAppliedWithType += OnEntityKnockedBack;
    }
    
    private void OnDisable()
    {
        //Entity.KnockbackAppliedWithType -= OnEntityKnockedBack;

        if (squishRoutine == null) return;
        StopCoroutine(squishRoutine);
        squishRoutine = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (!defeated || squished) return;
        if (transform.position.y > spawnY - despawnBelowSpawnDistance) return;
        PrefabPoolService.Despawn(gameObject);
        
    }
    
    public bool TryHandleStomp(in EnemyImpactContext context)
    {
        if (defeated) return false;
        if (!inShell)
        {
            SquishedOnce();
        }
        else if (!shellMoving)
        {
            SquishedTwice();
        }
        else
        {
            StopShell();
        }

        return true;
    }
    
    private void SquishedOnce()
    {
        if (defeated) return;
        
        inShell = true;
        shellMoving = false;
        
        enemySFX.PlayDeath();

        Entity.SetMovementEnabled(false);

        Body.simulated = true;
        
        //Body.linearVelocity = Vector2.zero;
        //Entity.SetMovementEnabled(true);
        Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
        
        //BodyCollider.enabled = false;

        TriggerSquishAnimation();

        //gameObject.tag = "Untagged";
/*
        if (squishRoutine != null)
        {
            StopCoroutine(squishRoutine);
        }
        squishRoutine = StartCoroutine(IdleShellState(squishDuration));
     */   
    }
    
    
    private void SquishedTwice()
    {
        if (defeated) return;
        shellMoving = true;
        inShell = true;
        
        StartShellMovement(transform.position - Vector3.right);

    }

    private void StopShell()
    {

        shellMoving = false;
        Entity.SetMovementEnabled(false);
        Body.linearVelocity = Vector2.zero;
    }
    
    private void TriggerSquishAnimation()
    {
        if (Anim.TrySetTrigger(SquashTrigger)) return;
        Debug.LogWarning($"Missing animator trigger '{SquashTrigger}' on {name}.", this);
    }
    
    
    private void ResetAnimationState()
    {
        if (!Animator || !Animator.runtimeAnimatorController) return;
        Animator.Rebind();
        Animator.Update(0f);
        Anim.TryResetTrigger(SquashTrigger);
    }
    
    private void SetDefeatedState()
    {
        defeated = true;
        gameObject.tag = "Untagged";
    }
    
    private void OnEntityKnockedBack(EntityController controller, EnemyImpactType impactType)
    {
        if (impactType != EnemyImpactType.Star) return;
        if (defeated) return;
        SetDefeatedState();
        Audio?.PlayDeath();
    }
    

    private IEnumerator IdleShellState(float delay)
    {
        yield return new WaitForSeconds(delay);
        squishRoutine = null;
        
        ResetAnimationState();
        
        squished = false;

        Entity.SetMovementEnabled(true);
        Body.simulated = true;
        Body.linearVelocity = Vector2.zero;
        Body.gravityScale = initialGravityScale;
        BodyCollider.enabled = true;
        Body.angularVelocity = 0f;
        gameObject.tag = initialTag;
        inShell = false;
    }
    

    private void SpawnScorePopup()
    {
        if (!scorePopupPrefab) return;

        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(stompScore, worldPosition);
    }

    public bool TryHandleImpact(in EnemyImpactContext context)
    {
        if (context.ImpactType == EnemyImpactType.Stomp)
            return false;
        if (defeated) 
            return false;
        
        if (!inShell)
            return false;
        
        if (inShell && !shellMoving)
        {
            StartShellMovement(context.SourcePosition);
            return true;
        }
        
        return false;
    }

    private void StartShellMovement(Vector2 sourcePosition)
    {
        shellMoving = true;

        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        Body.linearVelocity = new Vector2(direction * 12f, 0f);
        
        Entity.SetMovementEnabled(false);
    }
}
