using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameData.Reset();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public static void Dead()
    {
        print("Dead");

        GameData.lives--; 
        if (GameData.lives == 0)
        {
            GameOver();
            //do gameManager.NewGame() on the gameover scene if player presses replay
        }

        else
        {
            //reload current scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }
    }

    public static void GameOver()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        GameData.Reset();
    }

    public void AddCoin()
    {
        GameData.coins++;
        //play coin audio

        if (GameData.coins == 100)
        {
            GameData.coins = 0;
            AddLife();
        }
    }

    public void AddLife()
    {
        GameData.lives++;
        //play audio
    }
}
