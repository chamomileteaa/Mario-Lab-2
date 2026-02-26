using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(EntityController))]
[RequireComponent(typeof(AnimatorCache))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GoombaController : MonoBehaviour, IStompHandler
{
    private const string SquashTrigger = "squash";

    [Header("Stomp")]
    [SerializeField, Min(0.05f)] private float squishDuration = 0.35f;
    [SerializeField, Min(0)] private int stompScore = 100;
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Knock Away")]
    [FormerlySerializedAs("defeatWhenKnockedAway")]
    [SerializeField] private bool defeatWhenKnockedBack = true;
    [SerializeField, Min(0.5f)] private float despawnBelowSpawnDistance = 12f;

    private EntityController entityController;
    private Rigidbody2D body2D;
    private Collider2D bodyCollider2D;
    private Animator animatorComponent;
    private AnimatorCache animatorCache;
    private EnemyAudio enemyAudio;
    private Coroutine squishRoutine;
    private float initialGravityScale = 1f;
    private string initialTag;
    private float spawnY;
    private bool defeated;
    private bool squished;

    private EntityController Entity => entityController ? entityController : entityController = GetComponent<EntityController>();
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private Collider2D BodyCollider => bodyCollider2D ? bodyCollider2D : bodyCollider2D = GetComponent<Collider2D>();
    private Animator Animator => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private EnemyAudio Audio => enemyAudio ? enemyAudio : enemyAudio = GetComponent<EnemyAudio>();
    public GameObject ScorePopupPrefab => scorePopupPrefab;

    private void Awake()
    {
        spawnY = transform.position.y;
        initialGravityScale = Body.gravityScale;
        initialTag = gameObject.tag;
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
        Entity.KnockbackAppliedWithType -= OnEntityKnockedBack;

        if (squishRoutine == null) return;
        StopCoroutine(squishRoutine);
        squishRoutine = null;
    }

    private void Update()
    {
        if (!defeated || squished) return;
        if (transform.position.y > spawnY - despawnBelowSpawnDistance) return;
        PrefabPoolService.Despawn(gameObject);
    }

    public bool TryHandleStomp(in EnemyImpactContext context)
    {
        if (defeated) return false;
        DefeatBySquish();
        return true;
    }

    private void DefeatBySquish()
    {
        if (defeated) return;
        SetDefeatedState();
        squished = true;

        Entity.SetMovementEnabled(false);
        Body.linearVelocity = Vector2.zero;
        Body.gravityScale = 0f;
        Body.simulated = false;
        BodyCollider.enabled = false;

        TriggerSquishAnimation();
        SpawnScorePopup();
        Audio?.PlayDeath();
        squishRoutine = StartCoroutine(DespawnAfter(squishDuration));
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
        if (impactType != EnemyImpactType.Star && !defeatWhenKnockedBack) return;
        if (defeated) return;
        SetDefeatedState();
        Audio?.PlayDeath();
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        PrefabPoolService.Despawn(gameObject);
        squishRoutine = null;
    }

    private void SpawnScorePopup()
    {
        if (!scorePopupPrefab) return;

        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(stompScore, worldPosition);
    }
}
