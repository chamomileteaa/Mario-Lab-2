using UnityEngine;
using UnityEngine.InputSystem;

public class MarioController : MonoBehaviour
{
    Rigidbody2D rb;
    float move;
    bool jump;
    bool isGrounded;

    //check to see what is ground so mario can't infinite jump
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

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
        move = value.Get<Vector2>().x; //left right only
        
    }

    void OnJump()
    {
        if (isGrounded)
        {
            jump = true;
        }
        //set so can only jump when detects tilemap ground
    }

    void Update()
    {
        //check if touching ground
        isGrounded = Physics2D.OverlapCircle(groundCheck.position,groundCheckRadius,groundLayer);
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(move * 8f, rb.linearVelocity.y);
        
        if (jump)
        {
            jump = false;
            rb.AddForce(transform.up * 7f, ForceMode2D.Impulse);
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