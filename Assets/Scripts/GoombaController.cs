using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EntityController))]
[RequireComponent(typeof(AnimatorCache))]
public class GoombaController : MonoBehaviour, IStompable
{
    private const string SquishedTrigger = "squished";
    private const string LegacySquashTrigger = "squash";

    [Header("Stomp")]
    [SerializeField, Min(0.05f)] private float squishDuration = 0.35f;
    [SerializeField, Range(0.1f, 1f)] private float squishHeightScale = 0.5f;

    [Header("Knock Away")]
    [SerializeField] private bool defeatWhenKnockedAway = true;
    [SerializeField, Min(0.5f)] private float despawnBelowSpawnDistance = 12f;

    private EntityController entityController;
    private Rigidbody2D body2D;
    private AnimatorCache animatorCache;
    private EnemyAudio enemyAudio;
    private Coroutine squishRoutine;
    private Vector3 initialScale;
    private float initialGravityScale = 1f;
    private string initialTag;
    private float spawnY;
    private bool defeated;
    private bool squished;

    private EntityController Entity => entityController ? entityController : entityController = GetComponent<EntityController>();
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private EnemyAudio Audio => enemyAudio ? enemyAudio : enemyAudio = GetComponent<EnemyAudio>();

    private void Awake()
    {
        initialScale = transform.localScale;
        spawnY = transform.position.y;
        if (Body) initialGravityScale = Body.gravityScale;
        initialTag = gameObject.tag;
    }

    private void OnEnable()
    {
        spawnY = transform.position.y;
        defeated = false;
        squished = false;
        transform.localScale = initialScale;
        gameObject.tag = initialTag;
        if (Body) Body.gravityScale = initialGravityScale;
        Entity.KnockedAway += OnEntityKnockedAway;
    }

    private void OnDisable()
    {
        if (entityController)
            entityController.KnockedAway -= OnEntityKnockedAway;

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

    public bool TryStomp(MarioController mario, Vector2 hitPoint)
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
        if (Body)
        {
            Body.linearVelocity = Vector2.zero;
            Body.gravityScale = 0f;
        }

        var squishedScale = initialScale;
        squishedScale.y *= squishHeightScale;
        transform.localScale = squishedScale;

        if (!Anim.TrySetTrigger(SquishedTrigger))
            Anim.TrySetTrigger(LegacySquashTrigger);
        Audio?.PlayDeath();
        squishRoutine = StartCoroutine(DespawnAfter(squishDuration));
    }

    private void SetDefeatedState()
    {
        defeated = true;
        gameObject.tag = "Untagged";
    }

    private void OnEntityKnockedAway(EntityController controller)
    {
        if (!defeatWhenKnockedAway) return;
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
}
