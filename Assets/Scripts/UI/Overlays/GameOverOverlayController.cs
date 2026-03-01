using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class GameOverOverlayController : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float showDuration = 1.6f;

    [Header("Text")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string gameOverText = "GAME OVER";
    [SerializeField] private string timeUpText = "TIME UP";

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

    public void ShowGameOver(Action onFinished = null)
    {
        ShowMessage(gameOverText, onFinished);
    }

    public void ShowTimeUp(Action onFinished = null)
    {
        ShowMessage(timeUpText, onFinished);
    }

    public void ShowGameOverPersistent()
    {
        ShowMessagePersistent(gameOverText);
    }

    public void ShowTimeUpPersistent()
    {
        ShowMessagePersistent(timeUpText);
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

    private void ShowMessage(string message, Action onFinished)
    {
        ValidateReferences();
        if (showRoutine != null) StopCoroutine(showRoutine);
        if (messageText) messageText.text = message;
        SetVisible(true);
        showRoutine = StartCoroutine(ShowRoutine(onFinished));
    }

    private void ShowMessagePersistent(string message)
    {
        ValidateReferences();
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (messageText) messageText.text = message;
        SetVisible(true);
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
        if (!messageText) Debug.LogWarning("GameOverOverlayController missing message text reference.", this);
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var group = GetComponent<CanvasGroup>();
        if (group) return group;
        return gameObject.AddComponent<CanvasGroup>();
    }

}
