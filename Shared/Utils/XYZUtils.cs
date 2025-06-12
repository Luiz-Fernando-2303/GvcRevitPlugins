using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.Shared.Utils
{
    public static class XYZUtils
    {
        public static XYZ FaceNormal(Face face, out UV surfaceUV)
        {
            surfaceUV = new UV();

            BoundingBoxUV bbox = face.GetBoundingBox();
            if (bbox == null) return null;

            UV uvSample = new UV(
                (bbox.Min.U + bbox.Max.U) / 2,
                (bbox.Min.V + bbox.Max.V) / 2
            );
            surfaceUV = uvSample;

            XYZ faceNormal = face.ComputeNormal(uvSample);
            return faceNormal;
        }

        public static List<XYZ> DivideCurvesEvenly(IEnumerable<Curve> boundaryPath, int subdivisions)
        {
            if (subdivisions < 2) return null;

            var curves = boundaryPath.Where(c => c.IsBound && c.Length > 0).ToList();
            double totalLength = curves.Sum(c => c.Length);
            if (totalLength <= 0) return null;

            double segmentLength = totalLength / subdivisions;
            List<XYZ> points = new();

            double targetLength = 0;
            double accumulatedLength = 0;
            int currentCurveIndex = 0;

            Curve currentCurve = curves[currentCurveIndex];
            double currentCurveStart = currentCurve.GetEndParameter(0);
            double currentCurveEnd = currentCurve.GetEndParameter(1);

            points.Add(currentCurve.GetEndPoint(0));


            for (int i = 1; i <= subdivisions; i++)
            {
                targetLength = segmentLength * i;

                while (accumulatedLength + currentCurve.Length < targetLength)
                {
                    accumulatedLength += currentCurve.Length;
                    currentCurveIndex++;

                    if (currentCurveIndex > curves.Count)
                        return points; 

                    currentCurve = curves[currentCurveIndex];
                    currentCurveStart = currentCurve.GetEndParameter(0);
                    currentCurveEnd = currentCurve.GetEndParameter(1);
                }

                double remaining = targetLength - accumulatedLength;
                double fraction = remaining / currentCurve.Length;

                double param = currentCurveStart + fraction * (currentCurveEnd - currentCurveStart);
                XYZ pt = currentCurve.Evaluate(param, false);

                points.Add(pt);
            }

            return points;
        }

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
