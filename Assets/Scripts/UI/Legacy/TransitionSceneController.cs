using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TransitionSceneController : MonoBehaviour
{
    [SerializeField] private TMP_Text[] textsToFade;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float stayDuration = 3f;
    [SerializeField] private string nextSceneName = "GameScene";

    private void Start()
    {
        for (var i = 0; i < textsToFade.Length; i++)
        {
            if (textsToFade[i])
                textsToFade[i].alpha = 0f;
        }

        StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(stayDuration);
        yield return Fade(1f, 0f);
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);

            for (var i = 0; i < textsToFade.Length; i++)
            {
                if (textsToFade[i])
                    textsToFade[i].alpha = alpha;
            }

            yield return null;
        }

        for (var i = 0; i < textsToFade.Length; i++)
        {
            if (textsToFade[i])
                textsToFade[i].alpha = endAlpha;
        }
    }
}
