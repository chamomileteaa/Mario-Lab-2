using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
        }

        else
        {
            //reload current scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }
    }
}
