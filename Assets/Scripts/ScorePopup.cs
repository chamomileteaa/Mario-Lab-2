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

    private RectTransform rectTransform;
    private TextMeshProUGUI textLabel;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Vector3 startWorldPosition;
    private Color baseColor;
    private bool hasBaseColor;
    private Coroutine lifeRoutine;

    private RectTransform Rect => rectTransform ? rectTransform : rectTransform = GetComponent<RectTransform>();
    private TextMeshProUGUI Label => textLabel ? textLabel : textLabel = GetComponent<TextMeshProUGUI>();

    private void OnEnable()
    {
        startWorldPosition = transform.position;
        CacheBaseColor();
        Label.color = baseColor;
        Label.text = score.ToString();
        StartLife();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        lifeRoutine = null;
    }

    public void SetScore(int value)
    {
        score = Mathf.Max(0, value);
        Label.text = score.ToString();
    }

    public void Show(int value, Vector3 worldPosition)
    {
        SetScore(value);
        startWorldPosition = worldPosition;
        StartLife();
    }

    private void StartLife()
    {
        if (lifeRoutine != null) StopCoroutine(lifeRoutine);
        lifeRoutine = StartCoroutine(Life());
    }

    private IEnumerator Life()
    {
        if (!ResolveCanvas()) yield break;

        var elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / lifetime);
            SetCanvasPosition(startWorldPosition, Mathf.SmoothStep(0f, riseDistance, t));

            if (t >= fadeStartNormalized)
            {
                var fadeT = Mathf.InverseLerp(fadeStartNormalized, 1f, t);
                var color = baseColor;
                color.a = Mathf.Lerp(baseColor.a, 0f, fadeT);
                Label.color = color;
            }

            yield return null;
        }

        PrefabPoolService.Despawn(gameObject);
        lifeRoutine = null;
    }

    private bool ResolveCanvas()
    {
        if (!canvas)
            canvas = GetComponentInParent<Canvas>();
        if (!canvas)
            canvas = FindFirstObjectByType<Canvas>();

        if (!canvas) return false;
        canvasRect = canvas.transform as RectTransform;
        if (!canvasRect) return false;
        if (Rect.parent == canvasRect) return true;

        Rect.SetParent(canvasRect, false);
        return true;
    }

    private void SetCanvasPosition(Vector3 worldPosition, float riseY)
    {
        var worldCamera = Camera.main;
        var uiCamera = canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas && canvas.worldCamera
                ? canvas.worldCamera
                : worldCamera;
        var sourceCamera = worldCamera ? worldCamera : uiCamera;

        var screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out var localPoint);
        Rect.anchoredPosition = localPoint + Vector2.up * riseY;
    }

    private void CacheBaseColor()
    {
        if (hasBaseColor) return;
        baseColor = Label.color;
        hasBaseColor = true;
    }
}
