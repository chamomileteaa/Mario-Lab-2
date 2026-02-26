using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class BrickCoin : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float arcHeight = 2f;
    [SerializeField, Min(0.1f)] private float arcSpeed = 1.5f;
    [SerializeField, Min(0f)] private float despawnHeightAboveSpawn = 1f;
    [SerializeField, Min(0)] private int scoreValue = 200;
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private AudioCue collectCue;

    private Rigidbody2D body2D;
    private Animator animatorComponent;
    private Vector3 spawnPosition;
    private bool isDespawning;
    private float baseGravityScale = -1f;
    private Rigidbody2D Body => body2D ? body2D : body2D = GetComponent<Rigidbody2D>();
    private Animator Anim => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();

    private void OnEnable()
    {
        isDespawning = false;
        spawnPosition = transform.position;

        if (collectCue && collectCue.clip)
            AudioSource.PlayClipAtPoint(collectCue.clip, spawnPosition, collectCue.volume);

        RestartAnimation();
        Launch();
    }

    private void FixedUpdate()
    {
        if (isDespawning) return;
        if (Body.linearVelocity.y >= 0f) return;
        if (transform.position.y > spawnPosition.y + despawnHeightAboveSpawn) return;

        isDespawning = true;
        SpawnScorePopup();
        PrefabPoolService.Despawn(gameObject);
    }

    private void OnDisable()
    {
        if (!Body) return;
        Body.linearVelocity = Vector2.zero;
        if (baseGravityScale >= 0f) Body.gravityScale = baseGravityScale;
    }

    private void SpawnScorePopup()
    {
        if (!scorePopupPrefab) return;

        var worldPosition = transform.position + scorePopupOffset;
        GameInitializer.ShowScorePopup(scorePopupPrefab, scoreValue, worldPosition);
    }

    private void Launch()
    {
        if (baseGravityScale < 0f) baseGravityScale = Body.gravityScale;

        var speedScale = Mathf.Max(0.1f, arcSpeed);
        var gravityScale = baseGravityScale * speedScale * speedScale;
        Body.gravityScale = gravityScale;

        var gravity = Mathf.Abs(Physics2D.gravity.y * Mathf.Max(gravityScale, 0.0001f));
        var launchSpeedY = Mathf.Sqrt(2f * gravity * arcHeight);
        Body.linearVelocity = new Vector2(0f, launchSpeedY);
    }

    private void RestartAnimation()
    {
        if (!Anim || !Anim.runtimeAnimatorController) return;
        Anim.enabled = true;
        Anim.Rebind();
        Anim.Update(0f);
    }
}
