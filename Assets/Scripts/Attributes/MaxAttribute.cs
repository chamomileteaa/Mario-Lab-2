using UnityEngine;

public sealed class MaxAttribute : PropertyAttribute
{
    public readonly float max;

    public MaxAttribute(float max)
    {
        this.max = max;
    }
}
