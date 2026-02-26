using UnityEngine;

public class GameData 
{

    public static int lives = 3; //static = only one version/copy of this variable
    public static int startLives = 3; 

    public static int coins = 0;
    public static int startCoins = 0;

    public static int score = 0;
    public static int startScore = 0;


        public static void Reset()
    {
        lives = startLives;
        coins = startCoins;
        score = startScore;
    }
    
}