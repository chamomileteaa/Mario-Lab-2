using UnityEngine;

[DisallowMultipleComponent]
public class SpriteFlipper : MonoBehaviour
{
[SerializeField] private SpriteRenderer[] targets;
[SerializeField] private Collider2D referenceCollider;

    [Header("Flip")]
    [SerializeField] private bool useLocalScale = true;
    [SerializeField] private bool flipX = true;
    [SerializeField] private bool flipY = true;
    [SerializeField, Min(0f)] private float deadzone = 0.01f;

    [Header("Offset")]
    [SerializeField, HideInInspector] private bool hasReferenceCollider;
    [SerializeField, ConditionalField(nameof(hasReferenceCollider), false)] private Vector2 fallbackFlipOffset = Vector2.zero;

    private float facingX = 1f;
    private float facingY = 1f;
    private Vector3[] baseScales;
    private Vector3[] baseLocalPositions;

    private void Awake() => Initialize();

    public void SetDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > deadzone) facingX = Mathf.Sign(direction.x);
        if (Mathf.Abs(direction.y) > deadzone) facingY = Mathf.Sign(direction.y);
        Apply();
    }

    private void OnValidate()
    {
        deadzone = Mathf.Max(0f, deadzone);
        Initialize();
    }

    private void Initialize()
    {
        RefreshColliderReference();

        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<SpriteRenderer>(true);

        baseScales = new Vector3[targets.Length];
        baseLocalPositions = new Vector3[targets.Length];
        for (var i = 0; i < targets.Length; i++)
        {
            if (!targets[i])
            {
                baseScales[i] = Vector3.one;
                baseLocalPositions[i] = Vector3.zero;
                continue;
            }

            baseScales[i] = targets[i].transform.localScale;
            baseLocalPositions[i] = targets[i].transform.localPosition;
        }
    }

    private void Apply()
    {
        if (targets == null || targets.Length == 0) return;
        if (baseScales == null || baseLocalPositions == null || baseScales.Length != targets.Length || baseLocalPositions.Length != targets.Length)
            Initialize();

        var flipOffset = GetFlipOffset();

        if (useLocalScale)
        {
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (!target) continue;

                var scale = baseScales[i];
                if (flipX) scale.x = Mathf.Abs(scale.x) * facingX;
                if (flipY) scale.y = Mathf.Abs(scale.y) * facingY;
                target.transform.localScale = scale;
                target.transform.localPosition = baseLocalPositions[i] + (Vector3)flipOffset;
            }
        }
        else
        {
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (!target) continue;
                if (flipX) target.flipX = facingX < 0f;
                if (flipY) target.flipY = facingY < 0f;
                target.transform.localPosition = baseLocalPositions[i] + (Vector3)flipOffset;
            }
        }
    }

    private Vector2 GetFlipOffset()
    {
        var applyX = flipX && facingX < 0f;
        var applyY = flipY && facingY < 0f;
        if (!applyX && !applyY) return Vector2.zero;

        Vector2 sourceOffset;
        if (referenceCollider)
        {
            sourceOffset = referenceCollider.offset * 2f;
            hasReferenceCollider = true;
        }
        else
        {
            sourceOffset = fallbackFlipOffset;
            hasReferenceCollider = false;
        }

        return new Vector2(applyX ? sourceOffset.x : 0f, applyY ? sourceOffset.y : 0f);
    }

    private void RefreshColliderReference()
    {
        if (!referenceCollider) referenceCollider = GetComponent<Collider2D>();
        hasReferenceCollider = referenceCollider;
    }
}
