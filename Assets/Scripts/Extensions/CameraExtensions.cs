using UnityEngine;

public static class CameraExtensions
{
    public static Rect GetOrthographicWorldRect(this Camera camera)
    {
        var halfHeight = camera.orthographicSize;
        var halfWidth = halfHeight * camera.aspect;
        var position = camera.transform.position;

        return new Rect(
            position.x - halfWidth,
            position.y - halfHeight,
            halfWidth * 2f,
            halfHeight * 2f);
    }

    public static bool OverlapsOrthographicView(this Camera camera, Bounds bounds, bool allowInverse = true)
    {
        var cameraRect = camera.GetOrthographicWorldRect();
        var boundsRect = new Rect(bounds.min, bounds.size);
        return cameraRect.Overlaps(boundsRect, allowInverse);
    }

    public static bool ContainsOrthographicPoint(this Camera camera, Vector2 worldPoint)
    {
        var cameraRect = camera.GetOrthographicWorldRect();
        return cameraRect.Contains(worldPoint);
    }

    public static Vector3 ClampToOrthographicBounds(this Camera camera, Rect worldBounds, Vector3 position)
    {
        var cameraRect = camera.GetOrthographicWorldRect();
        var halfWidth = cameraRect.width * 0.5f;
        var halfHeight = cameraRect.height * 0.5f;

        var minX = worldBounds.xMin + halfWidth;
        var maxX = worldBounds.xMax - halfWidth;
        var minY = worldBounds.yMin + halfHeight;
        var maxY = worldBounds.yMax - halfHeight;

        position.x = minX > maxX ? worldBounds.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? worldBounds.center.y : Mathf.Clamp(position.y, minY, maxY);
        return position;
    }
}
