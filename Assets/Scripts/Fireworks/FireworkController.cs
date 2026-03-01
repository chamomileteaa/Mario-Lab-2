using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioPlayer))]
public class FireworkController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip burstSfx;

    [Header("Lifetime")]
    [SerializeField, Min(0.05f)] private float fallbackLifetime = 0.7f;
    [SerializeField, Min(0f)] private float extraDespawnDelay = 0.03f;

    private Animator animatorComponent;
    private AudioPlayer audioPlayer;
    private Coroutine despawnRoutine;
    private float cachedLifetime = -1f;

    private Animator Anim => animatorComponent ? animatorComponent : animatorComponent = GetComponent<Animator>();
    private AudioPlayer Audio => audioPlayer ? audioPlayer : audioPlayer = GetComponent<AudioPlayer>();

    private void OnEnable()
    {
        if (despawnRoutine != null)
            StopCoroutine(despawnRoutine);

        RestartAnimator();
        PlaySfx();
        despawnRoutine = StartCoroutine(DespawnAfter(ResolveLifetime() + extraDespawnDelay));
    }

    private void OnDisable()
    {
        if (despawnRoutine == null) return;
        StopCoroutine(despawnRoutine);
        despawnRoutine = null;
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
        despawnRoutine = null;
        PrefabPoolService.Despawn(gameObject);
    }

    private void PlaySfx()
    {
        if (!burstSfx) return;
        Audio?.PlayOneShot(burstSfx);
    }

    private void RestartAnimator()
    {
        if (!Anim) return;
        Anim.Rebind();
        Anim.Update(0f);
    }

    private float ResolveLifetime()
    {
        if (cachedLifetime > 0f)
            return cachedLifetime;

        cachedLifetime = fallbackLifetime;
        if (!Anim || !Anim.runtimeAnimatorController)
            return cachedLifetime;

        var clips = Anim.runtimeAnimatorController.animationClips;
        for (var i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (!clip) continue;
            cachedLifetime = Mathf.Max(cachedLifetime, clip.length);
        }

        return cachedLifetime;
    }
}
