using UnityEngine;

public class CollisionMario : MonoBehaviour
{
    public GameManager gameManager;
    public UIScript ui;

 private void OnTriggerEnter2D(Collider2D collision)
    {
//if mario interacts update UI
    
    if (collision.CompareTag("coin"))
    {
        GameData.Instance.AddCoin();  
        Destroy(collision.gameObject); 
        if (ui != null) ui.UpdateUI();
    }

    if (collision.CompareTag("1up"))
    {
        GameData.Instance.AddLife();  
        Destroy(collision.gameObject); // destroy the 1up
        if (ui != null) ui.UpdateUI();
    }

    if (collision.CompareTag("enemy"))
    {
        // lose life and check death
        GameData.Instance.LoseLife();

        if (GameData.Instance.lives <= 0)
        {
            gameManager.GameOver(); 
        }
        else
        {
            // reload scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }
    }
}
}
