using UnityEngine;

public readonly struct EnemyImpactContext
{
    public readonly EnemyImpactType ImpactType;
    public readonly MarioController Mario;
    public readonly Vector2 ContactPoint;
    public readonly Vector2 SourcePosition;

    public EnemyImpactContext(EnemyImpactType impactType, MarioController mario, Vector2 contactPoint, Vector2 sourcePosition)
    {
        ImpactType = impactType;
        Mario = mario;
        ContactPoint = contactPoint;
        SourcePosition = sourcePosition;
    }
}
