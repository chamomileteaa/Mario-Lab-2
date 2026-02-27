using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class TransitionSceneScript : MonoBehaviour
{
    public TMP_Text[] textsToFade; 
    public float fadeDuration = 0.5f;
    public float stayDuration = 3f;

    void Start()
    {
        foreach (var txt in textsToFade)
        {
            if (txt != null)
                txt.alpha = 0f; // start invisible
        }

        StartCoroutine(FadeSequence());
    }

    IEnumerator FadeSequence()
    {
        yield return StartCoroutine(Fade(0f, 0.5f));

        yield return new WaitForSeconds(stayDuration);

        yield return StartCoroutine(Fade(0.5f, 0f));
        SceneManager.LoadScene("GameScene");
    }

    IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / fadeDuration);

            foreach (var txt in textsToFade)
            {
                if (txt != null)
                    txt.alpha = alpha;
            }

            yield return null;
        }

        foreach (var txt in textsToFade)
        {
            if (txt != null)
                txt.alpha = endAlpha; // ensure final alpha
        }
    }
}
