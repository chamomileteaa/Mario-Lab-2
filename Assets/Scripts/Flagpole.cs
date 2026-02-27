using UnityEngine;

public class FlagTrigger : MonoBehaviour
{
    public Animator flagAnimator;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Debug.Log("Player hit flag");
            collision.GetComponent<Animator>().SetBool("isTriggered", true);
            flagAnimator.SetBool("isTriggered", true);
            Debug.Log(flagAnimator.GetBool("IsTriggered"));
        }
    }
}