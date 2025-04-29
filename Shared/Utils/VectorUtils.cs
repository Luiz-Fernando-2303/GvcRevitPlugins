using System.Numerics;

namespace GvcRevitPlugins.Shared.Utils
{
    internal static class VectorUtils
    {
        internal static Vector2[] DivideBy(Vector2 start, Vector2 end, int number)
        {
            Vector2[] points = new Vector2[number + 1];
            Vector2 step = (end - start) / number;

            for (int i = 0; i <= number; i++)
                points[i] = start + step * i;

            return points;
        }
        internal static float DistanceTo(this Vector2 v1, Vector2 v2) => Vector2.Subtract(v1, v2).Length();
        internal static Vector2[] ReorderRefFacePoints(Vector2[] refFacePoints, Vector2[] refBoundaryPoints)
        {
            float dist1 = refFacePoints[0].DistanceTo(refBoundaryPoints[0]);
            float dist2 = refFacePoints[0].DistanceTo(refBoundaryPoints[1]);
            if (dist1 > dist2)
                return refFacePoints;
            return new Vector2[] { refFacePoints[1], refFacePoints[0] };
        }
        internal static Vector2 GetEndPoint(Vector2 origin, Vector2 direction, double length)
        {
            Vector2 normalizedDirection = Vector2.Normalize(direction);
            Vector2 scaledDirection = normalizedDirection * (float)length;
            Vector2 endPoint = origin + scaledDirection;
            return endPoint;
        }
    }
}
