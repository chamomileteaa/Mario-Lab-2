using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class FlagpoleController : MonoBehaviour
{
    [System.Serializable]
    private struct ScoreBand
    {
        [Range(0f, 1f)] public float minNormalizedHeight;
        [Min(0)] public int score;
    }

    [Header("Pole")]
    [SerializeField] private Transform poleBottomPoint;
    [SerializeField] private Transform poleAttachPoint;
    [SerializeField] private float marioPoleXOffset;
    [SerializeField] private Transform poleFlagVisual;
    [SerializeField] private Transform poleFlagTopPoint;
    [SerializeField] private Transform poleFlagBottomPoint;
    [SerializeField] private bool animatePoleFlagDynamically = true;
    [SerializeField, Min(0.1f)] private float poleFlagDropSpeed = 7f;

    [Header("Castle")]
    [SerializeField] private Transform castleDoorPoint;
    [SerializeField] private CastleFlagController castleFlag;
    [SerializeField] private FireworksController fireworksController;
    [SerializeField, Min(0.1f)] private float fireworksCameraPanDuration = 5f;
    [SerializeField, Min(0f)] private float fireworksCameraPanHeight = 7f;

    [Header("Score Popup")]
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private Vector3 scorePopupOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField, Min(0.1f)] private float scorePopupDuration = 1f;

    [Header("Scoring By Contact Height")]
    [SerializeField] private ScoreBand[] scoreBands =
    {
        new ScoreBand { minNormalizedHeight = 0.90f, score = 5000 },
        new ScoreBand { minNormalizedHeight = 0.75f, score = 2000 },
        new ScoreBand { minNormalizedHeight = 0.50f, score = 800 },
        new ScoreBand { minNormalizedHeight = 0.25f, score = 400 },
        new ScoreBand { minNormalizedHeight = 0.00f, score = 100 }
    };

    private bool triggered;
    private MusicPlayer musicPlayer;
    private BoxCollider2D triggerCollider;
    private Coroutine poleFlagRoutine;
    private CameraController cameraController;
    private BoxCollider2D TriggerCollider => triggerCollider ? triggerCollider : triggerCollider = GetComponent<BoxCollider2D>();
    private MusicPlayer Music => musicPlayer ? musicPlayer : musicPlayer = FindFirstObjectByType<MusicPlayer>(FindObjectsInactive.Include);
    private CameraController CameraController => cameraController ? cameraController : cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);

    private void Awake()
    {
        TriggerCollider.isTrigger = true;
        ResolvePoleFlagVisual();
        EnsureDefaultScoreBands();

        if (!scorePopupPrefab)
        {
            var prewarm = FindFirstObjectByType<PoolPrewarmConfig>(FindObjectsInactive.Include);
            if (prewarm) scorePopupPrefab = prewarm.ScorePopupPrefab;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        var mario = other.GetComponentInParent<MarioController>();
        if (!mario || mario.IsDead || mario.IsWinning) return;

        triggered = true;
        TriggerCollider.enabled = false;

        var contactY = ResolveContactY(other);
        var normalizedHeight = ResolveNormalizedHeight(contactY);
        var score = ResolveScore(normalizedHeight);

        var gameData = GameData.GetOrCreate();
        if (gameData) gameData.AddScore(score);
        Music?.PlayWorldClearTheme();

        ShowScorePopup(score, new Vector3(GetPoleX(), contactY, 0f) + scorePopupOffset);
        TriggerPoleFlagAnimation();

        if (!castleDoorPoint)
        {
            Debug.LogError("FlagpoleController requires a Castle Door Point reference.", this);
            return;
        }

        var bottomY = poleBottomPoint ? poleBottomPoint.position.y : TriggerCollider.bounds.min.y;
        var doorX = castleDoorPoint.position.x;
        mario.StartVictoryScreen(
            poleAttachPoint ? poleAttachPoint : transform,
            bottomY,
            doorX,
            marioPoleXOffset,
            OnMarioReachedCastleDoor);
    }

    private void OnMarioReachedCastleDoor()
    {
        if (!castleFlag)
        {
            Debug.LogError("FlagpoleController requires a CastleFlagController reference.", this);
            return;
        }

        castleFlag.TriggerRaise();
        fireworksController?.Play(7);
        CameraController?.StartSkyPan(fireworksCameraPanHeight, fireworksCameraPanDuration, true);
    }

    private float GetPoleX()
    {
        return poleAttachPoint ? poleAttachPoint.position.x : TriggerCollider.bounds.center.x;
    }

    private float ResolveContactY(Collider2D marioCollider)
    {
        if (!marioCollider) return TriggerCollider.bounds.center.y;

        // Use Mario's head height so score maps to where he grabbed the pole.
        var y = marioCollider.bounds.max.y;
        var bounds = TriggerCollider.bounds;
        return Mathf.Clamp(y, bounds.min.y, bounds.max.y);
    }

    private float ResolveNormalizedHeight(float y)
    {
        var bounds = TriggerCollider.bounds;
        if (bounds.size.y <= Mathf.Epsilon) return 0f;
        return Mathf.InverseLerp(bounds.min.y, bounds.max.y, y);
    }

    private int ResolveScore(float normalizedHeight)
    {
        if (scoreBands == null || scoreBands.Length == 0)
            return 100;

        var bestScore = scoreBands[scoreBands.Length - 1].score;
        var bestMin = float.MinValue;

        for (var i = 0; i < scoreBands.Length; i++)
        {
            var band = scoreBands[i];
            if (normalizedHeight < band.minNormalizedHeight) continue;
            if (band.minNormalizedHeight < bestMin) continue;
            bestMin = band.minNormalizedHeight;
            bestScore = band.score;
        }

        return Mathf.Max(0, bestScore);
    }

    private void EnsureDefaultScoreBands()
    {
        if (scoreBands != null && scoreBands.Length > 0) return;
        scoreBands = new[]
        {
            new ScoreBand { minNormalizedHeight = 0.90f, score = 5000 },
            new ScoreBand { minNormalizedHeight = 0.75f, score = 2000 },
            new ScoreBand { minNormalizedHeight = 0.50f, score = 800 },
            new ScoreBand { minNormalizedHeight = 0.25f, score = 400 },
            new ScoreBand { minNormalizedHeight = 0.00f, score = 100 }
        };
    }

    private void ShowScorePopup(int scoreValue, Vector3 worldPosition)
    {
        if (scoreValue <= 0 || !scorePopupPrefab) return;
        var popupObject = PrefabPoolService.Spawn(scorePopupPrefab, worldPosition, Quaternion.identity);
        if (popupObject && popupObject.TryGetComponent<ScorePopup>(out var popup))
            popup.ShowStatic(scoreValue, worldPosition, scorePopupDuration);
    }

    private void TriggerPoleFlagAnimation()
    {
        if (!animatePoleFlagDynamically) return;
        TryStartDynamicFlagDrop();
    }

    private bool TryStartDynamicFlagDrop()
    {
        var visual = ResolvePoleFlagVisual();
        if (!visual) return false;

        var targetY = ResolveFlagBottomY();
        if (poleFlagRoutine != null) StopCoroutine(poleFlagRoutine);
        poleFlagRoutine = StartCoroutine(DropFlagRoutine(visual, targetY));
        return true;
    }

    private IEnumerator DropFlagRoutine(Transform visual, float targetY)
    {
        var speed = Mathf.Max(0.1f, poleFlagDropSpeed);
        while (visual && visual.position.y > targetY)
        {
            var position = visual.position;
            position.y = Mathf.MoveTowards(position.y, targetY, speed * Time.deltaTime);
            visual.position = position;
            yield return null;
        }

        poleFlagRoutine = null;
    }

    private Transform ResolvePoleFlagVisual()
    {
        if (poleFlagVisual) return poleFlagVisual;
        if (poleFlagTopPoint && poleFlagTopPoint.childCount > 0)
            return poleFlagVisual = poleFlagTopPoint.GetChild(0);

        var poleTransform = poleAttachPoint ? poleAttachPoint : transform;
        var named = poleTransform.Find("flag");
        if (named) return poleFlagVisual = named;

        return poleFlagVisual = poleTransform.GetComponentInChildren<SpriteRenderer>(true)?.transform;
    }

    private float ResolveFlagBottomY()
    {
        if (poleFlagBottomPoint) return poleFlagBottomPoint.position.y;
        if (poleBottomPoint) return poleBottomPoint.position.y;
        return TriggerCollider.bounds.min.y;
    }

}
