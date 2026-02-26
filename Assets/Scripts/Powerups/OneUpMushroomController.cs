using System;
using UnityEngine;

public class OneUpMushroomController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.GetComponent<MarioController>())
        {
            Debug.Log("Mario has been hit!");
            if (GameData.lives < 3)
            {
                GameData.lives++;
                Destroy(gameObject);
            }
            
        }
    }
}
