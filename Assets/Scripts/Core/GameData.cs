using UnityEngine;
using System;

public class GameData : MonoBehaviour
{

    // Singleton instance
    public static GameData Instance;
    [SerializeField] private string defaultWorld = "1-1";

    public int lives = 3; //static = only one version/copy of this variable
    public int score = 0; //static stays same until game over

    public int coins = 0; //resets at gameover
    public float timer = 400f;
    public string world;
    public bool runActive;
    public event Action Changed;


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
        if (string.IsNullOrWhiteSpace(world))
            world = defaultWorld;
        NotifyChanged();
    }

    public static GameData GetOrCreate()
    {
        if (Instance) return Instance;
        var existing = FindFirstObjectByType<GameData>(FindObjectsInactive.Include);
        if (existing)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("GameData");
        Instance = go.AddComponent<GameData>();
        return Instance;
    }

    public void ResetAll()
    {
        lives = 3;
        score = 0;
        coins = 0;
        timer = 400f;
        world = defaultWorld;
        runActive = false;
        NotifyChanged();
    }

    public void ResetLevel()
    {
        coins = 0;
        timer = 400f;
        NotifyChanged();
    }

    public void BeginRun()
    {
        runActive = true;
        NotifyChanged();
    }

    public void EndRun()
    {
        runActive = false;
        NotifyChanged();
    }

    //
    public void AddCoin()
    {
        coins++;
        if (coins >= 100)
        {
            coins = 0;
            AddLife();
            return;
        }

        NotifyChanged();
    }

    public void AddLife()
    {
        lives++;
        NotifyChanged();
    }

    public void AddScore(int value)
    {
        score += Mathf.Max(0, value);
        HighScoreManager.TrySetHighScore(score);
        NotifyChanged();
    }

    public void LoseLife()
    {
        lives = Mathf.Max(0, lives - 1);
        NotifyChanged();
    }

    public void SetTimer(float remainingTime)
    {
        var clamped = Mathf.Max(0f, remainingTime);
        if (Mathf.Approximately(timer, clamped)) return;
        timer = clamped;
        NotifyChanged();
    }

    public void SetWorld(string worldId)
    {
        var sanitized = string.IsNullOrWhiteSpace(worldId) ? defaultWorld : worldId.Trim();
        if (world == sanitized) return;
        world = sanitized;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
    
