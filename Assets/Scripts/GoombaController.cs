using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EntityController))]
[RequireComponent(typeof(AnimatorCache))]
[RequireComponent(typeof(Rigidbody2D))]
public class GoombaController : MonoBehaviour, IStompable
{
    private const string SquishedTrigger = "squished";

    [Header("Stomp")]
    [SerializeField, Min(0.05f)] private float squishDuration = 0.35f;
    [SerializeField, Min(0)] private int stompScore = 100;
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Knock Away")]
    [SerializeField] private bool defeatWhenKnockedAway = true;
    [SerializeField, Min(0.5f)] private float despawnBelowSpawnDistance = 12f;

    private EntityController entityController;
    private Rigidbody2D body2D;
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
    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();
    private EnemyAudio Audio => enemyAudio ? enemyAudio : enemyAudio = GetComponent<EnemyAudio>();

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
        Body.gravityScale = initialGravityScale;
        Entity.KnockedAway += OnEntityKnockedAway;
    }

    private void OnDisable()
    {
        Entity.KnockedAway -= OnEntityKnockedAway;

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
        Body.linearVelocity = Vector2.zero;
        Body.gravityScale = 0f;

        Anim.TrySetTrigger(SquishedTrigger);
        SpawnScorePopup();
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

    private void SpawnScorePopup()
    {
        if (!scorePopupPrefab) return;

        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(stompScore, worldPosition);
    }
}
