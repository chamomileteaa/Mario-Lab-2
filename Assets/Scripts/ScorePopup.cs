using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class ScorePopup : MonoBehaviour
{
    [SerializeField, Min(0)] private int score = 200;
    [SerializeField, Min(0f)] private float riseDistance = 48f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
    [SerializeField, Range(0f, 1f)] private float fadeStartNormalized = 0.45f;

    private static Canvas cachedRootCanvas;
    private static Camera cachedWorldCamera;

    private RectTransform rectTransform;
    private TextMeshProUGUI textLabel;
    private RectTransform canvasRect;
    private Camera worldCamera;
    private Color baseColor;
    private Vector2 startAnchoredPosition;
    private Coroutine lifeRoutine;

    private RectTransform Rect => rectTransform ? rectTransform : rectTransform = GetComponent<RectTransform>();
    private TextMeshProUGUI Label => textLabel ? textLabel : textLabel = GetComponent<TextMeshProUGUI>();

    private void Awake()
    {
        baseColor = Label.color;
    }

    private void OnDisable()
    {
        if (lifeRoutine != null)
        {
            StopCoroutine(lifeRoutine);
            lifeRoutine = null;
        }
    }

    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        Label.text = score.ToString();
    }

    public void Show(int value, Vector3 worldPosition)
    {
        if (!TryAttachToCanvas())
        {
            PrefabPoolService.Despawn(gameObject);
            return;
        }

        SetScore(value);
        Label.color = baseColor;

        if (!TryWorldToAnchored(worldPosition, out startAnchoredPosition))
        {
            PrefabPoolService.Despawn(gameObject);
            return;
        }

        Rect.anchoredPosition = startAnchoredPosition;
        if (lifeRoutine != null) StopCoroutine(lifeRoutine);
        lifeRoutine = StartCoroutine(Life());
    }

    private IEnumerator Life()
    {
        var elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / lifetime);
            Rect.anchoredPosition = startAnchoredPosition + Vector2.up * Mathf.SmoothStep(0f, riseDistance, t);

            if (t >= fadeStartNormalized)
            {
                var fadeT = Mathf.InverseLerp(fadeStartNormalized, 1f, t);
                var color = baseColor;
                color.a = Mathf.Lerp(baseColor.a, 0f, fadeT);
                Label.color = color;
            }

            yield return null;
        }

        lifeRoutine = null;
        PrefabPoolService.Despawn(gameObject);
    }

    private bool TryAttachToCanvas()
    {
        var rootCanvas = ResolveRootCanvas();
        if (!rootCanvas) return false;

        canvasRect = rootCanvas.transform as RectTransform;
        if (!canvasRect) return false;

        if (Rect.parent != canvasRect)
            Rect.SetParent(canvasRect, false);

        worldCamera = ResolveWorldCamera(rootCanvas);
        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return true;
        return worldCamera;
    }

    private static Canvas ResolveRootCanvas()
    {
        if (cachedRootCanvas && cachedRootCanvas.isActiveAndEnabled) return cachedRootCanvas;

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if (!canvas || !canvas.isActiveAndEnabled || !canvas.isRootCanvas) continue;
            if (canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                cachedRootCanvas = canvas;
                return cachedRootCanvas;
            }

            if (!cachedRootCanvas) cachedRootCanvas = canvas;
        }

        return cachedRootCanvas;
    }

    private static Camera ResolveWorldCamera(Canvas canvas)
    {
        if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        if (canvas.worldCamera) return canvas.worldCamera;

        if (cachedWorldCamera) return cachedWorldCamera;
        cachedWorldCamera = Camera.main;
        return cachedWorldCamera;
    }

    private bool TryWorldToAnchored(Vector3 worldPosition, out Vector2 anchoredPosition)
    {
        var screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, worldCamera, out anchoredPosition);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedRootCanvas = null;
        cachedWorldCamera = null;
    }
}
