using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class IntroOverlayController : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float showDuration = 1.2f;

    [Header("Text")]
    [SerializeField] private TMP_Text worldText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField, TextArea] private string worldFormat = "WORLD\n{0}";
    [SerializeField, TextArea] private string livesFormat = "MARIO x {0}";

    private RectTransform root;
    private CanvasGroup canvasGroup;
    private Coroutine showRoutine;
    public bool IsVisible => CanvasGroup.alpha > 0.001f;
    private RectTransform Root => root ? root : root = transform as RectTransform;
    private CanvasGroup CanvasGroup => canvasGroup ? canvasGroup : canvasGroup = GetOrAddCanvasGroup();

    private void Awake()
    {
        ValidateReferences();
        HideInstant();
    }

    public void Show(int lives, string world, Action onFinished)
    {
        ValidateReferences();
        if (showRoutine != null) StopCoroutine(showRoutine);
        SetVisible(true);
        SetFormatted(worldText, worldFormat, string.IsNullOrWhiteSpace(world) ? "-" : world);
        SetFormatted(livesText, livesFormat, lives);
        showRoutine = StartCoroutine(ShowRoutine(onFinished));
    }

    public void HideInstant()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        SetVisible(false);
    }

    private IEnumerator ShowRoutine(Action onFinished)
    {
        yield return new WaitForSecondsRealtime(showDuration);
        SetVisible(false);
        showRoutine = null;
        onFinished?.Invoke();
    }

    private void SetVisible(bool state)
    {
        Root.gameObject.SetActive(true);
        CanvasGroup.alpha = state ? 1f : 0f;
        CanvasGroup.blocksRaycasts = state;
        CanvasGroup.interactable = state;
    }

    private void ValidateReferences()
    {
        _ = Root;
        _ = CanvasGroup;
        if (!worldText) Debug.LogWarning("IntroOverlayController missing world text reference.", this);
        if (!livesText) Debug.LogWarning("IntroOverlayController missing lives text reference.", this);
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var group = GetComponent<CanvasGroup>();
        if (group) return group;
        return gameObject.AddComponent<CanvasGroup>();
    }

    private static void SetFormatted(TMP_Text text, string format, params object[] values)
    {
        if (!text) return;
        var resolvedFormat = string.IsNullOrEmpty(format) ? "{0}" : format;
        text.text = string.Format(resolvedFormat, values);
    }
}
