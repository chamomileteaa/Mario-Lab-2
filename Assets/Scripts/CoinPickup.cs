using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(AnimatorCache))]
public class CoinPickup : MonoBehaviour
{
    [Header("Collect")]
    [SerializeField, Min(0)] private int scoreValue = 200;
    [SerializeField] private AudioClip collectSfx;
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.35f, 0f);

    [Header("Appearance")]
    [SerializeField] private AnimatorCache animatorCache;
    [SerializeField] private string environmentParameter = "environment";

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D triggerCollider;
    private CameraController cameraController;
    private bool collected;

    private SpriteRenderer Sprite => spriteRenderer ? spriteRenderer : spriteRenderer = GetComponent<SpriteRenderer>();
    private BoxCollider2D Trigger => triggerCollider ? triggerCollider : triggerCollider = GetComponent<BoxCollider2D>();
    private AnimatorCache Animator => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();

    private void Awake()
    {
        Trigger.isTrigger = true;
    }

    private void OnEnable()
    {
        collected = false;
        SubscribeCamera(true);
        RefreshAppearance();
    }

    private void OnDisable()
    {
        SubscribeCamera(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (!other.CompareColliderTag("Player")) return;
        if (!other.TryGetComponentInParent<MarioController>(out var mario) || !mario) return;

        collected = true;
        var data = GameData.GetOrCreate();
        data.AddCoin();
        data.AddScore(scoreValue);
        mario.NotifyCoinCollected();

        if (collectSfx)
            AudioPlayer.PlayAtPoint(collectSfx, transform.position);

        ShowScorePopup();
        Destroy(gameObject);
    }

    public void Initialize(
        Sprite sprite,
        int sortingLayerId,
        int sortingOrder,
        int score,
        AudioClip collectClip,
        GameObject popupPrefab)
    {
        if (sprite) Sprite.sprite = sprite;
        Sprite.sortingLayerID = sortingLayerId;
        Sprite.sortingOrder = sortingOrder;
        scoreValue = Mathf.Max(0, score);
        collectSfx = collectClip;
        scorePopupPrefab = popupPrefab;
    }

    private void ShowScorePopup()
    {
        if (!scorePopupPrefab || scoreValue <= 0) return;

        var worldPosition = transform.position + scorePopupOffset;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.Show(scoreValue, worldPosition);
    }

    private void SubscribeCamera(bool subscribe)
    {
        var controller = ResolveCameraController();
        if (!controller) return;

        if (subscribe)
        {
            controller.ActiveEnvironmentChanged += OnActiveEnvironmentChanged;
            return;
        }

        controller.ActiveEnvironmentChanged -= OnActiveEnvironmentChanged;
    }

    private void OnActiveEnvironmentChanged(CameraEnvironmentType _)
    {
        RefreshAppearance();
    }

    private void RefreshAppearance()
    {
        var anim = Animator;
        if (!anim) return;
        if (string.IsNullOrWhiteSpace(environmentParameter)) return;

        var controller = ResolveCameraController();
        var environment = controller ? controller.ActiveEnvironment : CameraEnvironmentType.Overworld;
        anim.TrySet(environmentParameter, (int)environment);
    }

    private CameraController ResolveCameraController()
    {
        if (cameraController) return cameraController;
        cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        return cameraController;
    }
}
