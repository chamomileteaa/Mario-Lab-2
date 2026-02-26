using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public UIScript ui;

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "TitleScene")
        {
            GameData.Instance.ResetAll();
        }
        else
        {
            //GameData.Instance.ResetLevel();
            Debug.Log("Reset Level");
        }

        if (ui != null)
            ui.UpdateUI();
    }

    public void Dead()
    {
        Debug.Log("Dead");

        GameData.Instance.LoseLife();

        if (ui != null)
            ui.UpdateUI();

        if (GameData.Instance.lives <= 0)
        {
            GameOver();
        }
        else
        {
            // Reload current scene
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }
    }

    public void GameOver()
    {
        SceneManager.LoadScene("GameOver");
        GameData.Instance.ResetAll();
    }

    public void AddCoin()
    {
        GameData.Instance.AddCoin();
        if (ui != null)
            ui.UpdateUI();
    }

    public void AddLife()
    {
        GameData.Instance.AddLife();
        if (ui != null)
            ui.UpdateUI();
        //add audio
    }
}