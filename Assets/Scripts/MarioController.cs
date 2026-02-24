using UnityEngine;
using UnityEngine.InputSystem;

public class MarioController : MonoBehaviour
{
    Rigidbody2D rb;
    float move;
    bool jump;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // old input
    //void Update()
    //{
    //    move = Input.GetAxis("Horizontal");

    //    if (Input.GetButtonDown("Jump"))
    //    {
    //        jump = true;
    //    }
        
    //}

    //new input

    void OnMove(InputValue value)
    {
        move = value.Get<float>(); //left right only
        
    }

    void OnJump()
    {
        jump = true;
        //set so can only jump when detects tilemap ground
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(move * 5f, rb.linearVelocity.y);
        
        if (jump)
        {
            jump = false;
            rb.AddForce(transform.up * 6f, ForceMode2D.Impulse);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("enemy"))
        {
            Debug.Log("lives-1");
            Dead();
        }
        
        
        
    }

        void Dead()
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