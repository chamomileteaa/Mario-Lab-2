using UnityEngine;
using System.Collections;

public class FlagTrigger : MonoBehaviour
{
    public Animator flagAnimator;
    public bool triggersFireworks = false; // check this only on the green flag
    public GameObject fireworkPrefab;
    public Transform fireworksSpawnPoint;
    public UIScript uiScript;
    public AudioClip fireworkSound;
    private AudioSource audioSource;
    private bool hasTriggered = false;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;

            Animator playerAnimator = collision.GetComponent<Animator>();
            if (HasParameter("isTriggered", playerAnimator))
                playerAnimator.SetBool("isTriggered", true);

            if (flagAnimator != null && HasParameter("isTriggered", flagAnimator))
                flagAnimator.SetBool("isTriggered", true);

            if (triggersFireworks)
            {
                int lastDigit = (int)Mathf.Ceil(uiScript.timeLeft) % 10;

                int fireworkCount = 0;
                if (lastDigit == 1) fireworkCount = 1;
                else if (lastDigit == 3) fireworkCount = 3;
                else if (lastDigit == 6) fireworkCount = 6;

                if (fireworkCount > 0)
                    StartCoroutine(SpawnFireworks(fireworkCount));
            }
        }
    }

    private IEnumerator SpawnFireworks(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 2f), 0f);
            Instantiate(fireworkPrefab, fireworksSpawnPoint.position + randomOffset, Quaternion.identity);

            if (fireworkSound != null)
                audioSource.PlayOneShot(fireworkSound);

            yield return new WaitForSeconds(0.5f);
        }
    }

    private bool HasParameter(string paramName, Animator animator)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }
}