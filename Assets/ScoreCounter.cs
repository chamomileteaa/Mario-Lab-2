using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ScoreCounter", menuName = "Game/ScoreCounter")]
public class ScoreCounter : ScriptableObject
{
    public int score = 0;

    public event Action OnScoreChanged;

    public void AddScore(int amount)
    {
        score += amount;
        OnScoreChanged?.Invoke();
    }

    public void reset()
    {
        score = 0;
        OnScoreChanged?.Invoke();
    }
}
