using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class HudController : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text worldText;

    [Header("Formatting")]
    [SerializeField, TextArea] private string worldFormat = "WORLD\n{0}";
    [SerializeField, TextArea] private string timeFormat = "TIME\n{0:000}";
    [SerializeField, TextArea] private string scoreFormat = "MARIO\n{0:000000}";
    [SerializeField, TextArea] private string coinsFormat = "x {0:00}";

    [Header("Timer")]
    [SerializeField, Min(1f)] private float levelStartTime = 400f;
    [SerializeField] private bool timerEnabled = true;

    private CanvasGroup canvasGroup;
    private GameData gameData;
    private MarioController mario;
    private bool timerExpired;
    private CanvasGroup CanvasGroup => canvasGroup ? canvasGroup : canvasGroup = GetOrAddCanvasGroup();

    private void OnEnable()
    {
        _ = CanvasGroup;
        gameData = GameData.GetOrCreate();
        SubscribeData(true);
        ValidateReferences();
        RefreshAll();
    }

    private void OnDisable()
    {
        SubscribeData(false);
    }

    private void Update()
    {
        if (!timerEnabled) return;
        if (!gameData || !gameData.runActive) return;
        if (PauseService.IsPaused(PauseType.Physics)) return;

        var remaining = Mathf.Max(0f, gameData.timer - Time.deltaTime);
        gameData.SetTimer(remaining);

        if (timerExpired || remaining > 0f) return;
        timerExpired = true;
        ResolveMario()?.KillFromOutOfBounds();
    }

    public void BeginLevel()
    {
        timerExpired = false;
        gameData = GameData.GetOrCreate();
        gameData.SetTimer(levelStartTime);
        RefreshAll();
    }

    public float GetLevelStartTime()
    {
        return levelStartTime;
    }

    public void UpdateUI()
    {
        RefreshAll();
    }

    private void SubscribeData(bool subscribe)
    {
        if (!gameData) return;
        if (subscribe) gameData.Changed += RefreshAll;
        else gameData.Changed -= RefreshAll;
    }

    private void RefreshAll()
    {
        if (!gameData) return;

        var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(gameData.timer));

        SetFormatted(scoreText, scoreFormat, gameData.score);
        SetFormatted(coinsText, coinsFormat, gameData.coins);
        SetFormatted(timeText, timeFormat, totalSeconds);
        SetFormatted(worldText, worldFormat, GetWorldValue());
    }

    private static void SetFormatted(TMP_Text text, string format, params object[] values)
    {
        if (!text) return;
        var resolvedFormat = string.IsNullOrEmpty(format) ? "{0}" : format;
        var primaryValue = values != null && values.Length > 0 ? values[0] : null;

        if (!resolvedFormat.Contains("{0"))
        {
            var label = resolvedFormat.TrimEnd();
            if (primaryValue == null)
            {
                text.text = label;
                return;
            }

            if (string.IsNullOrEmpty(label))
            {
                text.text = primaryValue.ToString();
                return;
            }

            text.text = $"{label}\n{primaryValue}";
            return;
        }

        try
        {
            text.text = string.Format(resolvedFormat, values);
        }
        catch (System.FormatException)
        {
            var label = resolvedFormat.Replace("{0}", string.Empty).TrimEnd();
            var fallback = primaryValue?.ToString() ?? string.Empty;
            text.text = string.IsNullOrEmpty(label) ? fallback : $"{label}\n{fallback}";
        }
    }

    private void ValidateReferences()
    {
        if (!scoreText) Debug.LogWarning("HudController missing score text reference.", this);
        if (!coinsText) Debug.LogWarning("HudController missing coins text reference.", this);
        if (!timeText) Debug.LogWarning("HudController missing time text reference.", this);
        if (!worldText) Debug.LogWarning("HudController missing world text reference.", this);
    }

    private MarioController ResolveMario()
    {
        if (mario) return mario;
        mario = FindFirstObjectByType<MarioController>(FindObjectsInactive.Exclude);
        return mario;
    }

    private string GetWorldValue()
    {
        if (!gameData || string.IsNullOrWhiteSpace(gameData.world)) return "-";
        return gameData.world;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(true);
        CanvasGroup.alpha = visible ? 1f : 0f;
        CanvasGroup.blocksRaycasts = visible;
        CanvasGroup.interactable = visible;
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var group = GetComponent<CanvasGroup>();
        if (group) return group;
        return gameObject.AddComponent<CanvasGroup>();
    }
}
