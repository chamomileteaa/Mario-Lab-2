using UnityEngine;

public class HighScoreTools : MonoBehaviour
{
    [SerializeField, Min(0)] private int highScoreValue;
    [SerializeField] private bool logActions = true;

    [Button("Get High Score")]
    public void GetHighScore()
    {
        highScoreValue = HighScoreManager.GetHighScore();
        if (logActions)
            Debug.Log($"High score: {highScoreValue}", this);
    }

    [Button("Set High Score")]
    public void SetHighScore()
    {
        HighScoreManager.SetHighScore(highScoreValue);
        if (logActions)
            Debug.Log($"High score set to {highScoreValue}", this);
    }

    [Button("Clear High Score")]
    public void ClearHighScore()
    {
        HighScoreManager.ClearHighScore();
        highScoreValue = 0;
        if (logActions)
            Debug.Log("High score cleared.", this);
    }
}
