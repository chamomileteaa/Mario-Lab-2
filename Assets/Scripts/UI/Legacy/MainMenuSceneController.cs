using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MainMenuSceneController : MonoBehaviour
{
    [SerializeField] private RectTransform arrow;
    [SerializeField] private RectTransform player1Pos;
    [SerializeField] private RectTransform player2Pos;
    [SerializeField] private TMP_Text[] optionLabels;
    [SerializeField] private Vector2 arrowOffset = new Vector2(-240f, 15f);
    [SerializeField] private string startSceneName = "Transition2GameScene";
    [SerializeField] private string[] disabledOptionMessages = { "2P mode is not implemented yet." };

    private int selectedOption;

    private void Start()
    {
        selectedOption = 0;
        RefreshVisuals();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
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

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            SelectOption();
    }

    private void RefreshVisuals()
    {
        if (arrow)
        {
            var target = selectedOption == 0 ? player1Pos : player2Pos;
            if (target)
                arrow.position = target.position + (Vector3)arrowOffset;
        }

        for (var i = 0; i < optionLabels.Length; i++)
        {
            var label = optionLabels[i];
            if (!label) continue;
            label.alpha = i == selectedOption ? 1f : 0.75f;
        }
    }

    private void SelectOption()
    {
        if (selectedOption == 0)
        {
            SceneManager.LoadScene(startSceneName);
            return;
        }

        if (disabledOptionMessages != null && selectedOption - 1 < disabledOptionMessages.Length)
            Debug.Log(disabledOptionMessages[selectedOption - 1], this);
    }
}
