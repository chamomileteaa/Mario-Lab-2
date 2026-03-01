using UnityEngine;

public readonly struct EnemyImpactContext
{
    public readonly EnemyImpactType ImpactType;
    public readonly MarioController Mario;
    public readonly Vector2 ContactPoint;
    public readonly Vector2 SourcePosition;
    public readonly int StompChainIndex;
    public readonly int StompScore;

    public EnemyImpactContext(
        EnemyImpactType impactType,
        MarioController mario,
        Vector2 contactPoint,
        Vector2 sourcePosition,
        int stompChainIndex = 0,
        int stompScore = 0)
    {
        ImpactType = impactType;
        Mario = mario;
        ContactPoint = contactPoint;
        SourcePosition = sourcePosition;
        StompChainIndex = stompChainIndex;
        StompScore = stompScore;
    }
}
