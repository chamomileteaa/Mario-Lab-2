using UnityEngine;

public static class CameraExtensions
{
    public static Vector3 ClampToOrthographicBounds(this Camera camera, Rect worldBounds, Vector3 position)
    {
        var halfHeight = camera.orthographicSize;
        var halfWidth = halfHeight * camera.aspect;

        var minX = worldBounds.xMin + halfWidth;
        var maxX = worldBounds.xMax - halfWidth;
        var minY = worldBounds.yMin + halfHeight;
        var maxY = worldBounds.yMax - halfHeight;

        position.x = minX > maxX ? worldBounds.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? worldBounds.center.y : Mathf.Clamp(position.y, minY, maxY);
        return position;
    }
}
