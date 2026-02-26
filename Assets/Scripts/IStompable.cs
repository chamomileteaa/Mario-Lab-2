using UnityEngine;

public interface IStompable
{
    bool TryStomp(MarioController mario, Vector2 hitPoint);
}
