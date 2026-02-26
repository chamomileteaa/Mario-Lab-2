using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class SpriteShardParticles : MonoBehaviour
{
    [SerializeField, Min(1)] private int subdivisions = 2;
    [SerializeField] private bool skipFullyTransparentShards = true;

    private static readonly Dictionary<string, Sprite[]> shardCache = new Dictionary<string, Sprite[]>();
    private ParticleSystem particleSystemComponent;
    private ParticleSystem Particles => particleSystemComponent ? particleSystemComponent : particleSystemComponent = GetComponent<ParticleSystem>();

    public void ApplySprite(Sprite sprite)
    {
        if (!sprite) return;

        var textureSheet = Particles.textureSheetAnimation;
        textureSheet.enabled = true;
        textureSheet.mode = ParticleSystemAnimationMode.Sprites;
        textureSheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        textureSheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 1f);
        ClearSprites(textureSheet);

        var shards = GetOrCreateShards(sprite);
        if (shards.Length == 0)
        {
            textureSheet.AddSprite(sprite);
        }
        else
        {
            foreach (var shard in shards)
                textureSheet.AddSprite(shard);
        }

        // Restart with the latest shard set so pooled effects render the correct source immediately.
        Particles.Clear(true);
        Particles.Play(true);
    }

    private Sprite[] GetOrCreateShards(Sprite sourceSprite)
    {
        var key = sourceSprite.GetInstanceID() + ":" + subdivisions + ":" + skipFullyTransparentShards;
        if (shardCache.TryGetValue(key, out var cached)) return cached;

        var generated = GenerateShards(sourceSprite);
        shardCache[key] = generated;
        return generated;
    }

    private Sprite[] GenerateShards(Sprite sourceSprite)
    {
        var tiles = Mathf.Max(1, subdivisions);
        var result = new List<Sprite>(tiles * tiles);
        var rect = sourceSprite.rect;
        var texture = sourceSprite.texture;
        var stepX = rect.width / tiles;
        var stepY = rect.height / tiles;
        var shardSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(stepX, stepY)));
        var rectXMin = Mathf.RoundToInt(rect.xMin);
        var rectYMin = Mathf.RoundToInt(rect.yMin);
        var rectXMax = Mathf.RoundToInt(rect.xMax) - shardSize;
        var rectYMax = Mathf.RoundToInt(rect.yMax) - shardSize;

        for (var y = tiles - 1; y >= 0; y--)
        {
            var cellYMin = Mathf.RoundToInt(rect.y + rect.height * y / tiles);
            var cellYMax = Mathf.RoundToInt(rect.y + rect.height * (y + 1f) / tiles);
            var cellHeight = Mathf.Max(1, cellYMax - cellYMin);

            for (var x = 0; x < tiles; x++)
            {
                var cellXMin = Mathf.RoundToInt(rect.x + rect.width * x / tiles);
                var cellXMax = Mathf.RoundToInt(rect.x + rect.width * (x + 1f) / tiles);
                var cellWidth = Mathf.Max(1, cellXMax - cellXMin);
                var xMin = Mathf.Clamp(cellXMin + Mathf.RoundToInt((cellWidth - shardSize) * 0.5f), rectXMin, rectXMax);
                var yMin = Mathf.Clamp(cellYMin + Mathf.RoundToInt((cellHeight - shardSize) * 0.5f), rectYMin, rectYMax);

                var shardRect = new Rect(xMin, yMin, shardSize, shardSize);
                if (skipFullyTransparentShards && IsFullyTransparent(texture, shardRect)) continue;

                var shard = Sprite.Create(
                    texture,
                    shardRect,
                    new Vector2(0.5f, 0.5f),
                    sourceSprite.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                result.Add(shard);
            }
        }

        return result.ToArray();
    }

    private static bool IsFullyTransparent(Texture2D texture, Rect rect)
    {
        try
        {
            var pixels = texture.GetPixels(
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height));

            foreach (var pixel in pixels)
                if (pixel.a > 0.001f)
                    return false;

            return true;
        }
        catch (Exception)
        {
            // Non-readable textures cannot be scanned; keep the shard.
            return false;
        }
    }

    private static void ClearSprites(ParticleSystem.TextureSheetAnimationModule textureSheet)
    {
        while (textureSheet.spriteCount > 0)
            textureSheet.RemoveSprite(0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (var pair in shardCache)
        {
            var shards = pair.Value;
            if (shards == null) continue;
            foreach (var shard in shards)
                if (shard) Destroy(shard);
        }

        shardCache.Clear();
    }
}
