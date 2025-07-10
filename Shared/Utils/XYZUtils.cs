using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.Shared.Utils
{
    public static class XYZUtils
    {
        public class SpatialGrid
        {
            public double CellSize { get; }
            private Dictionary<(int, int, int), List<XYZ>> grid;

            public SpatialGrid(double cellSize)
            {
                CellSize = cellSize;
                grid = new Dictionary<(int, int, int), List<XYZ>>();
            }

            public void Add(XYZ point)
            {
                var key = GetCellKey(point);
                if (!grid.ContainsKey(key))
                    grid[key] = new List<XYZ>();
                grid[key].Add(point);
            }

            public List<XYZ> GetPointsInCell(XYZ point)
            {
                var key = GetCellKey(point);
                return grid.ContainsKey(key) ? grid[key] : new List<XYZ>();
            }

            private (int, int, int) GetCellKey(XYZ point)
            {
                int xIndex = (int)Math.Floor(point.X / CellSize);
                int yIndex = (int)Math.Floor(point.Y / CellSize);
                int zIndex = (int)Math.Floor(point.Z / CellSize);
                return (xIndex, yIndex, zIndex);
            }

            public List<XYZ> GetNeighboringPoints(XYZ point, int range = 2)
            {
                var neighbors = new List<XYZ>();
                var (x, y, z) = GetCellKey(point);

                for (int dx = -range; dx <= range; dx++)
                {
                    for (int dy = -range; dy <= range; dy++)
                    {
                        for (int dz = -range; dz <= range; dz++)
                        {
                            var neighborKey = (x + dx, y + dy, z + dz);
                            if (grid.TryGetValue(neighborKey, out var pointsInCell))
                            {
                                neighbors.AddRange(pointsInCell);
                            }
                        }
                    }
                }

                return neighbors;
            }
        }

        public static List<XYZ> TerrainValleys(List<XYZ> terrainPoints, double cellSize = 1, double tolerance = 0.01, int iterations = 2)
        {
            var points = new List<XYZ>(terrainPoints);

            for (int i = 0; i < iterations; i++)
            {
                points = TerrainValleys(points, cellSize * i, tolerance * tolerance);
                if (points.Count == 0) break;
            }

            return points;
        }

        public static List<XYZ> TerrainValleys(List<XYZ> terrainPoints, double cellSize = 1, double tolerance = 0.01)
        {
            SpatialGrid grid = new SpatialGrid(cellSize);
            terrainPoints.ForEach(point => grid.Add(point));

            List<XYZ> valleys = new List<XYZ>();

            foreach (XYZ point in terrainPoints)
            {
                List<XYZ> neighbors = grid.GetNeighboringPoints(point);
                if (neighbors.Count == 0) continue;

                bool isValley = true;

                foreach (XYZ neighbor in neighbors)
                {
                    if (neighbor.IsAlmostEqualTo(point, tolerance))
                        continue;

                    if (neighbor.Z + tolerance < point.Z)
                    {
                        isValley = false;
                        break;
                    }
                }

                if (isValley)
                {
                    valleys.Add(point);
                }
            }

            return valleys;
        }

        public static List<XYZ> TerrainPeaks(List<XYZ> terrainPoints, double cellSize = 1, double tolerance = 0.01, int iterations = 2)
        {
            var points = new List<XYZ>(terrainPoints);

            for (int i = 0; i < iterations; i++)
            {
                points = TerrainPeaks(points, cellSize * i, tolerance * tolerance);
                if (points.Count > 0) break;
            }

            return points;
        }

        public static List<XYZ> TerrainPeaks(List<XYZ> terrainPoints, double cellSize = 1, double tolerance = 0.01)
        {
            SpatialGrid grid = new SpatialGrid(cellSize);
            terrainPoints.ForEach(point => grid.Add(point));
            List<XYZ> peaks = new List<XYZ>();
            foreach (XYZ point in terrainPoints)
            {
                List<XYZ> neighbors = grid.GetNeighboringPoints(point);
                if (neighbors.Count == 0) continue;
                bool isPeak = true;
                foreach (XYZ neighbor in neighbors)
                {
                    if (neighbor.IsAlmostEqualTo(point, tolerance))
                        continue;
                    if (neighbor.Z - tolerance > point.Z)
                    {
                        isPeak = false;
                        break;
                    }
                }
                if (isPeak)
                {
                    peaks.Add(point);
                }
            }
            return peaks;
        }

        public static List<XYZ> TerrainPoints(Document doc, ElementId toposolidId, int subdivision)
        {
            Element toposolidElem = doc.GetElement(toposolidId);
            if (toposolidElem is not Toposolid toposolid)
                return null;

            GeometryElement geomElement = toposolid.get_Geometry(new Options());
            Solid[] solids = geomElement.OfType<Solid>().Where(s => s.Faces.Size > 0).ToArray();

            List<XYZ> points = new List<XYZ>();

            foreach (Solid solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    Mesh mesh = face.Triangulate();
                    if (mesh == null) continue;

                    int triangleCount = mesh.NumTriangles;

                    for (int t = 0; t < triangleCount; t++)
                    {
                        MeshTriangle triangle = mesh.get_Triangle(t);

                        XYZ A = triangle.get_Vertex(0);
                        XYZ B = triangle.get_Vertex(1);
                        XYZ C = triangle.get_Vertex(2);

                        for (int i = 0; i <= subdivision; i++)
                        {
                            for (int j = 0; j <= subdivision - i; j++)
                            {
                                double u = (double)i / subdivision;
                                double v = (double)j / subdivision;
                                double w = 1.0 - u - v;

                                XYZ point = (A * u) + (B * v) + (C * w);

                                if (!points.Any(p => p.IsAlmostEqualTo(point)))
                                    points.Add(point);
                            }
                        }
                    }
                }
            }

            return points;
        }

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

    public static class Draw
    {
        public static List<ModelCurve> modelCurves = new List<ModelCurve>();
        public static List<DirectShape> directShapes = new List<DirectShape>();

        public static void Remove(Document doc)
        {
            using (Transaction tx = new Transaction(doc, "Remover curvas e formas"))
            {
                tx.Start();

                foreach (ModelCurve curve in modelCurves)
                    doc.Delete(curve.Id);

                foreach (DirectShape shape in directShapes)
                    doc.Delete(shape.Id);

                tx.Commit();
            }

            modelCurves.Clear();
            directShapes.Clear();
        }

        public static void Remove<T>(Document doc, T item)
        {
            Remove<T>(doc, new List<T> { item });
        }

        public static void Remove<T>(Document doc, IEnumerable<T> items)
        {
            using (Transaction tx = new Transaction(doc, "Remover elementos"))
            {
                tx.Start();

                foreach (T item in items)
                {
                    if (item is Element element)
                        doc.Delete(element.Id);

                    else if (item is ElementId id)
                        doc.Delete(id);
                }

                tx.Commit();
            }
        }

        public static void _XYZ(Document doc, IEnumerable<XYZ> p, double size = 0.5)
        {
            foreach (XYZ point in p)
                _XYZ(doc, point, size);
        }

        public static void _XYZ(Document doc, XYZ p, double size = 0.5)
        {
            if (!doc.IsModifiable)
            {
                using (Transaction transaction = new Transaction(doc, "Draw XYZ Point"))
                {
                    transaction.Start();

                    Execute();

                    transaction.Commit();
                }
                return;
            }

            Execute();

            void Execute()
            {
                double radius = size;

                Arc arc = Arc.Create(p + new XYZ(0, 0, -radius), p + new XYZ(0, 0, radius), p + new XYZ(radius, 0, 0));

                Line linha1 = Line.CreateBound(arc.GetEndPoint(1), arc.GetEndPoint(0));

                CurveLoop profile = CurveLoop.Create(new List<Curve> { arc, linha1 });

                Autodesk.Revit.DB.Frame eixo = new Autodesk.Revit.DB.Frame(
                    p,
                    XYZ.BasisX,
                    XYZ.BasisY,
                    XYZ.BasisZ
                );

                Solid sphere = GeometryCreationUtilities.CreateRevolvedGeometry(
                    eixo,
                    new List<CurveLoop> { profile },
                    0,
                    2 * Math.PI
                );

                DirectShape shape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                shape.SetShape(new List<GeometryObject> { sphere });
                shape.Name = "Sphere at " + p.ToString();

                directShapes.Add(shape);
            }
        }

        public static void _Curve(Document doc, IEnumerable<Curve> c)
        {
            foreach (Curve curve in c)
                _Curve(doc, curve);
        }

        public static void _Curve(Document doc, Curve curve)
        {
            if (!doc.IsModifiable)

                using (Transaction transaction = new Transaction(doc, "Draw Curve"))
                {
                    transaction.Start();
                    Execute();
                    transaction.Commit();
                }

            else
                Execute();

            void Execute()
            {
                Curve boundCurve = ToBound(curve);

                Plane plane = GetPlane(boundCurve);
                SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                doc.Create.NewModelCurve(boundCurve, sketchPlane);
            }

            Plane GetPlane(Curve curve)
            {
                XYZ p0 = curve.GetEndPoint(0);
                XYZ p1 = curve.GetEndPoint(1);
                XYZ direction = (p1 - p0).Normalize();

                XYZ normal;
                if (direction.CrossProduct(XYZ.BasisZ).IsZeroLength())
                    normal = XYZ.BasisX;

                else
                    normal = direction.CrossProduct(XYZ.BasisZ).Normalize();

                XYZ yVector = normal.CrossProduct(direction).Normalize();

                return Plane.CreateByOriginAndBasis(p0, direction, yVector);
            }

            Curve ToBound(Curve curve)
            {
                if (curve.IsBound)
                    return curve;

                if (curve is Line line)
                {
                    XYZ origin = line.Origin;
                    XYZ dir = line.Direction;

                    double length = 1000;
                    XYZ p0 = origin - dir * (length / 2);
                    XYZ p1 = origin + dir * (length / 2);

                    return Line.CreateBound(p0, p1);
                }
                else
                    throw new ArgumentException("Curve type not supported for unbound conversion.");
            }
        }
    }
}
