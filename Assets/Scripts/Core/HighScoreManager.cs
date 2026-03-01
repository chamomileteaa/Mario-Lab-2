using UnityEngine;

public static class HighScoreManager
{
    private const string HighScoreKey = "high_score";

    public static int GetHighScore()
    {
        return PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    public static bool TrySetHighScore(int score)
    {
        var sanitized = Mathf.Max(0, score);
        if (sanitized <= GetHighScore()) return false;
        SetHighScore(sanitized);
        return true;
    }

    public static void SetHighScore(int score)
    {
        var sanitized = Mathf.Max(0, score);
        PlayerPrefs.SetInt(HighScoreKey, sanitized);
        PlayerPrefs.Save();
    }

    public static void ClearHighScore()
    {
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
    }
}
