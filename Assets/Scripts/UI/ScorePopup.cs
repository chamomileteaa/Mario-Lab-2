using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class ScorePopup : MonoBehaviour
{
    [SerializeField, Min(0)] private int score = 200;
    [SerializeField, Min(1f)] private float riseSpeed = 150f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
    [SerializeField, Min(0.01f)] private float staticLifetime = 0.9f;
    [SerializeField, Range(0f, 1f)] private float fadeStartNormalized = 0.45f;

    private static Canvas cachedRootCanvas;
    private static Camera cachedWorldCamera;

    private RectTransform rectTransform;
    private TextMeshProUGUI textLabel;
    private RectTransform canvasRect;
    private Camera sceneCamera;
    private Camera uiCamera;
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

    public void SetLabel(string text)
    {
        Label.text = string.IsNullOrWhiteSpace(text) ? "0" : text;
    }

    public void Show(int value, Vector3 worldPosition)
    {
        if (!PrepareForShow(worldPosition)) return;
        SetScore(value);
        lifeRoutine = StartCoroutine(Life(true, lifetime));
    }

    public void ShowLabel(string label, Vector3 worldPosition)
    {
        if (!PrepareForShow(worldPosition)) return;
        SetLabel(label);
        lifeRoutine = StartCoroutine(Life(true, lifetime));
    }

    public void ShowStatic(int value, Vector3 worldPosition, float duration = -1f)
    {
        if (!PrepareForShow(worldPosition)) return;
        SetScore(value);
        lifeRoutine = StartCoroutine(Life(false, duration > 0f ? duration : staticLifetime));
    }

    private bool PrepareForShow(Vector3 worldPosition)
    {
        if (!TryAttachToCanvas())
        {
            PrefabPoolService.Despawn(gameObject);
            return false;
        }

        Label.color = baseColor;

        if (!TryWorldToAnchored(worldPosition, out startAnchoredPosition))
        {
            PrefabPoolService.Despawn(gameObject);
            return false;
        }

        Rect.anchoredPosition = startAnchoredPosition;
        if (lifeRoutine != null) StopCoroutine(lifeRoutine);
        return true;
    }

    private IEnumerator Life(bool floatUp, float duration)
    {
        var elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            if (floatUp)
                Rect.anchoredPosition = startAnchoredPosition + Vector2.up * (riseSpeed * elapsed);

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

        sceneCamera = ResolveSceneCamera();
        uiCamera = ResolveUiCamera(rootCanvas, sceneCamera);

        if (!sceneCamera) return false;
        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) return true;
        return uiCamera;
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

    private static Camera ResolveSceneCamera()
    {
        if (cachedWorldCamera && cachedWorldCamera.isActiveAndEnabled) return cachedWorldCamera;

        if (Camera.main) cachedWorldCamera = Camera.main;
        if (cachedWorldCamera && cachedWorldCamera.isActiveAndEnabled) return cachedWorldCamera;

        var cameras = Camera.allCameras;
        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (!camera || !camera.isActiveAndEnabled) continue;
            cachedWorldCamera = camera;
            break;
        }

        return cachedWorldCamera;
    }

    private static Camera ResolveUiCamera(Canvas canvas, Camera fallbackCamera)
    {
        if (!canvas || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        if (canvas.worldCamera) return canvas.worldCamera;
        return fallbackCamera;
    }

    private bool TryWorldToAnchored(Vector3 worldPosition, out Vector2 anchoredPosition)
    {
        if (!sceneCamera)
        {
            anchoredPosition = default;
            return false;
        }

        var screenPoint = RectTransformUtility.WorldToScreenPoint(sceneCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out anchoredPosition);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedRootCanvas = null;
        cachedWorldCamera = null;
    }
}
