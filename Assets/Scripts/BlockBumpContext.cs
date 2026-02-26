using UnityEngine;

public readonly struct BlockBumpContext
{
    public readonly Block Block;
    public readonly MarioController Mario;
    public readonly Vector2 Origin;
    public readonly Vector2 Direction;

    public BlockBumpContext(Block block, MarioController mario, Vector2 origin, Vector2 direction)
    {
        Block = block;
        Mario = mario;
        Origin = origin;
        Direction = direction;
    }
}

public interface IBlockBumpReactive
{
    void OnBlockBumped(BlockBumpContext context);
}
