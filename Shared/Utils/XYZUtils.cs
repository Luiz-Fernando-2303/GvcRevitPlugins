using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.Shared.Utils
{
    public static class XYZUtils
    {
        /// <summary>
        /// R(t) = P + t*D
        /// t -> Number of divisions
        /// P -> Start point
        /// D -> Direction vector (end - start)
        /// 
        internal static XYZ[] DivideEvenly(XYZ start, XYZ end, int number)
        {
            XYZ[] points = new XYZ[number + 1];
            XYZ step = (end - start) / number;

            for (int i = 0; i <= number; i++)
                points[i] = start + step * i;

            return points;
        }

        public static XYZ[] RemoveAlmostEquals(List<XYZ> points, double tolerance)
        {
            List<XYZ> result = new List<XYZ>();

            foreach (var point in points)
                if (!result.Any(p => AreAlmostEqual(p, point, tolerance)))
                    result.Add(point);

            return result.ToArray();
        }
        public static bool AreAlmostEqual(XYZ point1, XYZ point2, double tolerance) => point1.IsAlmostEqualTo(point2, tolerance);
        public static XYZ[] ReorderRefFacePoints(XYZ[] refFacePoints, XYZ[] refBoundaryPoints)
        {
            double dist1 = refFacePoints[0].DistanceTo(refBoundaryPoints[0]);
            double dist2 = refFacePoints[0].DistanceTo(refBoundaryPoints[1]);
            if (dist1 > dist2)
                return refFacePoints;
            return new XYZ[] { refFacePoints[1], refFacePoints[0] };
        }
        public static XYZ GetEndPoint(XYZ origin, XYZ direction, double length)
        {
            if (length == 0) return origin;
            XYZ normalizedDirection = direction.Normalize();
            XYZ displacement = normalizedDirection * length;
            XYZ endPoint = origin + displacement;
            return endPoint;
        }
    }

}
