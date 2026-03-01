using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class PauseOverlayController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private string pausedText = "PAUSED";

    private RectTransform root;
    private CanvasGroup canvasGroup;
    public bool IsVisible => CanvasGroup.alpha > 0.001f;
    private RectTransform Root => root ? root : root = transform as RectTransform;
    private CanvasGroup CanvasGroup => canvasGroup ? canvasGroup : canvasGroup = GetOrAddCanvasGroup();

    private void Awake()
    {
        ValidateReferences();
        HideInstant();
    }

    public void Show()
    {
        ValidateReferences();
        if (label) label.text = pausedText;
        SetVisible(true);
    }

    public void HideInstant()
    {
        SetVisible(false);
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
        if (!label) Debug.LogWarning("PauseOverlayController missing paused label reference.", this);
        if (label) label.text = pausedText;
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var group = GetComponent<CanvasGroup>();
        if (group) return group;
        return gameObject.AddComponent<CanvasGroup>();
    }

}
