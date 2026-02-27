using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScript : MonoBehaviour
{
    public RectTransform arrow;

    public RectTransform player1Pos;
    public RectTransform player2Pos;

    int selectedOption = 0; // 0 = player1, 1 = player2

    void Start()
    {
        UpdateArrow();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name != "MainMenuScene")
            return;
        HandleInput();
    }

    void HandleInput()
    {
        
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            selectedOption = 0;
            UpdateArrow();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            selectedOption = 1;
            UpdateArrow();
        }

        
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SelectOption();
        }
    }

    void UpdateArrow()
    {
        Vector3 offset = new Vector3(-240f, 15f, 0f);
        if (selectedOption == 0)
        {
            
            arrow.position = player1Pos.position + offset;
        }
        else
        {
            arrow.position = player2Pos.position + offset;
        }
    }

    void SelectOption()
    {
        if (selectedOption == 0)
        {

            SceneManager.LoadScene("Transition2GameScene");
        }
        else
        {
            Debug.Log("This does nothing!!!)");
        }
    }
}
