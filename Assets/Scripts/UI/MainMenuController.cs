using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class MainMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text player1Text;
    [SerializeField] private TMP_Text player2Text;
    [SerializeField] private TMP_Text topScoreText;
    [SerializeField] private Image selectedIndicatorImage;
    [SerializeField] private Button player1Button;
    [SerializeField] private Button player2Button;
    [SerializeField] private Vector2 indicatorOffset = new Vector2(-80f, 0f);

    [Header("Content")]
    [SerializeField] private string player1Label = "1 PLAYER GAME";
    [SerializeField] private string player2Label = "2 PLAYER GAME";
    [SerializeField] private string topScoreFormat = "TOP- {0:000000}";
    [SerializeField] private string unavailableModeMessage = "2P mode is not implemented yet.";
    [SerializeField, Min(0f)] private float inputDebounceOnShow = 0.25f;

    private RectTransform root;
    private CanvasGroup canvasGroup;

    private Action onStart;
    private int selectedOption;
    private bool visible;
    private float acceptInputAt;
    public bool IsVisible => visible && CanvasGroup.alpha > 0.001f;
    private RectTransform Root => root ? root : root = transform as RectTransform;
    private CanvasGroup CanvasGroup => canvasGroup ? canvasGroup : canvasGroup = GetOrAddCanvasGroup();

    private void Awake()
    {
        ValidateReferences();
        selectedOption = 0;
        RefreshText();
        RefreshVisuals();
        HideInstant();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Update()
    {
        if (!visible) return;
        HandleInput();
    }

    private void LateUpdate()
    {
        if (!visible) return;
        RefreshVisuals();
    }

    public void Show(Action startCallback)
    {
        ValidateReferences();
        onStart = startCallback;
        selectedOption = 0;
        acceptInputAt = Time.unscaledTime + Mathf.Max(0f, inputDebounceOnShow);
        SetVisible(true);
        RefreshText();
        Canvas.ForceUpdateCanvases();
        RefreshVisuals();
    }

    public void HideInstant()
    {
        onStart = null;
        SetVisible(false);
    }

    private void HandleInput()
    {
        if (!IsInputReady()) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            selectedOption = 0;
            RefreshVisuals();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            selectedOption = 1;
            RefreshVisuals();
        }

        if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.Space)) return;
        SelectOption();
    }

    private void SetVisible(bool state)
    {
        visible = state;
        Root.gameObject.SetActive(true);
        CanvasGroup.alpha = state ? 1f : 0f;
        CanvasGroup.blocksRaycasts = state;
        CanvasGroup.interactable = state;
    }

    private void SelectOption()
    {
        if (selectedOption == 0)
        {
            onStart?.Invoke();
            return;
        }

        Debug.Log(unavailableModeMessage, this);
    }

    private void RefreshVisuals()
    {
        if (player1Text) player1Text.alpha = selectedOption == 0 ? 1f : 0.75f;
        if (player2Text) player2Text.alpha = selectedOption == 1 ? 1f : 0.75f;

        var targetRect = GetSelectedTargetRect();
        if (targetRect && selectedIndicatorImage)
            MoveIndicatorToTarget(targetRect);
    }

    private void RefreshText()
    {
        if (player1Text) player1Text.text = player1Label;
        if (player2Text) player2Text.text = player2Label;
        if (topScoreText) topScoreText.text = string.Format(topScoreFormat, HighScoreManager.GetHighScore());
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var group = GetComponent<CanvasGroup>();
        if (group) return group;
        return gameObject.AddComponent<CanvasGroup>();
    }

    private void ValidateReferences()
    {
        if (!player1Text) Debug.LogWarning("MainMenuController missing Player 1 text reference.", this);
        if (!player2Text) Debug.LogWarning("MainMenuController missing Player 2 text reference.", this);
        if (!topScoreText) Debug.LogWarning("MainMenuController missing Top Score text reference.", this);
        if (!selectedIndicatorImage) Debug.LogWarning("MainMenuController missing selected indicator image reference.", this);
    }

    private void BindButtons()
    {
        if (player1Button) player1Button.onClick.AddListener(OnPlayer1Pressed);
        if (player2Button) player2Button.onClick.AddListener(OnPlayer2Pressed);
    }

    private void UnbindButtons()
    {
        if (player1Button) player1Button.onClick.RemoveListener(OnPlayer1Pressed);
        if (player2Button) player2Button.onClick.RemoveListener(OnPlayer2Pressed);
    }

    private void OnPlayer1Pressed()
    {
        selectedOption = 0;
        RefreshVisuals();
        if (!IsInputReady()) return;
        SelectOption();
    }

    private void OnPlayer2Pressed()
    {
        selectedOption = 1;
        RefreshVisuals();
        if (!IsInputReady()) return;
        SelectOption();
    }

    private bool IsInputReady()
    {
        return Time.unscaledTime >= acceptInputAt;
    }

    private RectTransform GetSelectedTargetRect()
    {
        if (selectedOption == 0)
        {
            if (player1Button) return player1Button.transform as RectTransform;
            return player1Text ? player1Text.rectTransform : null;
        }

        if (player2Button) return player2Button.transform as RectTransform;
        return player2Text ? player2Text.rectTransform : null;
    }

    private void MoveIndicatorToTarget(RectTransform targetRect)
    {
        var indicatorRect = selectedIndicatorImage.rectTransform;
        var parentRect = indicatorRect.parent as RectTransform;
        if (!parentRect)
        {
            indicatorRect.position = targetRect.position + (Vector3)indicatorOffset;
            return;
        }

        var corners = new Vector3[4];
        targetRect.GetWorldCorners(corners);
        var canvas = parentRect.GetComponentInParent<Canvas>();
        var eventCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        var minX = float.PositiveInfinity;
        for (var i = 0; i < corners.Length; i++)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]),
                eventCamera,
                out var localPoint);
            minX = Mathf.Min(minX, localPoint.x);
            minY = Mathf.Min(minY, localPoint.y);
            maxY = Mathf.Max(maxY, localPoint.y);
        }

        var parentYMin = parentRect.rect.yMin;
        var parentYMax = parentRect.rect.yMax;
        var anchorYMin = Mathf.InverseLerp(parentYMin, parentYMax, minY);
        var anchorYMax = Mathf.InverseLerp(parentYMin, parentYMax, maxY);
        var anchoredXFromLeft = (minX - parentRect.rect.xMin) + indicatorOffset.x;

        var size = indicatorRect.sizeDelta;
        indicatorRect.anchorMin = new Vector2(0f, anchorYMin);
        indicatorRect.anchorMax = new Vector2(0f, anchorYMax);
        indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRect.sizeDelta = new Vector2(size.x, 0f);
        indicatorRect.anchoredPosition = new Vector2(anchoredXFromLeft, indicatorOffset.y);
    }
}
