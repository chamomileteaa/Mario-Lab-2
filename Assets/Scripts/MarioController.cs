using UnityEngine;

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

    // Update is called once per frame
    void Update()
    {
        move = Input.GetAxis("Horizontal");

        if (Input.GetButtonDown("Jump"))
        {
            jump = true;
        }
        
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(move * 5, rb.linearVelocity.y);
        
        if (jump)
        {
            jump = false;
            rb.AddForce(transform.up * 6, ForceMode2D.Impulse);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("enemy"))
        {
            Debug.Log("lives-1");
            GameManager.Dead();
        }
          
    }


}