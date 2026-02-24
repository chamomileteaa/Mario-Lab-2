using UnityEngine;

public class EnemyMovementAI : MonoBehaviour
{
    public float speed = 1.5f;
    int direction = -1;

    Rigidbody2D rb;
    Vector2 velocity;

    
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
    }


    // Turn around when hitting wall
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            direction *= -1;
            Flip();
        }

        // Mario contact goomba kills -> 
        // need to add stomp and fireball different death so mario not invincible 
        // and ability for goomba to hurt mario
        if (collision.gameObject.CompareTag("Player"))
        {
            Die();
        }
    }

    void Flip()
    {
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public void Die()
    {
        Destroy(gameObject);
    }
}
