using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AnimatorCache))]
public class CastleFlagController : MonoBehaviour
{
    private const string RaiseTrigger = "trigger";

    private AnimatorCache animatorCache;
    private bool triggered;

    private AnimatorCache Anim => animatorCache ? animatorCache : animatorCache = GetComponent<AnimatorCache>();

    public void TriggerRaise()
    {
        if (triggered) return;
        triggered = true;
        Anim.TrySetTrigger(RaiseTrigger);
    }

    public void ResetRaiseState()
    {
        triggered = false;
        Anim.TryResetTrigger(RaiseTrigger);
    }
}
