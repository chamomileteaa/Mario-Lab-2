using UnityEngine;

public class GameData : MonoBehaviour
{

    // Singleton instance
    public static GameData Instance;

    public int lives = 3; //static = only one version/copy of this variable
    public int score = 0; //static stays same until game over

    public int coins = 0; //resets at gameover
    public float timer = 400f;


    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

        public  void ResetAll()
    {
        lives = 3;
        score = 0;
        coins = 0;
        timer = 400f;
    }

    public  void ResetLevel()
    {
        coins = 0;
        timer = 400f;
    }

    //
       public void AddCoin()
    {
        coins++;
        if (coins >= 100)
        {
            coins = 0;
            AddLife();
        }
    }

    public void AddLife()
    {
        lives++;
    }

    public void AddScore(int value)
    {
        score += value;
    }

    public void LoseLife()
    {
        lives--;
    }
}
    
