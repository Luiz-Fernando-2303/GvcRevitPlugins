using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.CommandFunctions
{
    public static class InternalUtils
    {
        public static Face[] CreateDummyFaces()
        {
            var dummy = new List<Face>();
            GeometryObject geometryObject = TerrainCheckApp._thisApp.Store.IntersectionGeometricObject;
            Face face = geometryObject as Face;
            Mesh mesh = geometryObject as Mesh;

            if (mesh == null || mesh.Vertices.Count < 4) return dummy.ToArray();

            XYZ p1 = mesh.Vertices[0];
            XYZ p2 = mesh.Vertices[1];
            XYZ p3 = mesh.Vertices[2];
            XYZ p4 = mesh.Vertices[3];

            var faceLoop = new CurveLoop();
            faceLoop.Append(Line.CreateBound(p1, p2));
            faceLoop.Append(Line.CreateBound(p2, p3));
            faceLoop.Append(Line.CreateBound(p3, p4));
            faceLoop.Append(Line.CreateBound(p4, p1));

            XYZ v1 = p2 - p1;
            XYZ v2 = p3 - p1;
            XYZ normal = v1.CrossProduct(v2);

            if (normal.IsZeroLength())
            {
                TaskDialog.Show("Erro", "A normal da face não pôde ser calculada (pontos coplanares ou degenerados).");
                return dummy.ToArray();
            }

            normal = normal.Normalize();

            Solid extrusion = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { faceLoop },
                normal,
                0.01
            );

            var validFaces = new List<Face>();
            foreach (Face f in extrusion.Faces)
            {
                XYZ faceNormal = utils.XYZUtils.FaceNormal(f, out _);
                if (faceNormal != null && faceNormal.DotProduct(normal) > 0.9)
                {
                    validFaces.Add(f);
                }
            }

            if (validFaces.Count > 0)
                dummy.AddRange(validFaces);

            return dummy.ToArray();
        }

        public static void CreateExtrudedWallFromCurves(WallResult_[] wallResults, Document document)
        {
            using (var tx = new Transaction(document, "Create extrude wall"))
            {
                tx.Start();

                List<ElementId> createdIds = new List<ElementId>();

                foreach (var wall in wallResults)
                {
                    var baseStart = wall.wallCurve.GetEndPoint(0);
                    var baseEnd = wall.wallCurve.GetEndPoint(1);

                    double height = UnitUtils.ConvertToInternalUnits(wall.wallHeight, UnitTypeId.Feet);

                    height = height < 0 ? -height : height;
                    var up = XYZ.BasisZ.Multiply(height);

                    var p1 = baseStart;
                    var p2 = baseEnd;
                    var p3 = baseEnd + up;
                    var p4 = baseStart + up;

                    var faceLoop = new CurveLoop();
                    faceLoop.Append(Line.CreateBound(p1, p2));
                    faceLoop.Append(Line.CreateBound(p2, p3));
                    faceLoop.Append(Line.CreateBound(p3, p4));
                    faceLoop.Append(Line.CreateBound(p4, p1));

                    var loops = new List<CurveLoop> { faceLoop };
                    Solid wallSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
                        loops,
                        (p2 - p1).CrossProduct(up).Normalize(),
                        0.01
                    );

                    var color = new Color(255, 0, 0); // vermelho
                    int transparency = 70;            // 0 = opaco, 100 = invisível

                    var geo = utils.ElementUtils.AddSolidWithColor(
                        document,
                        wallSolid, 
                        color, 
                        transparency, 
                        out var element, 
                        addOnScene: true
                    ); // adcionar propriedade de referencia

                    if (element != null)
                        createdIds.Add(element.Id);
                }

                if (createdIds.Count > 0)
                {
                    UIDocument uidoc = new UIDocument(document);
                    createdIds.Add(TerrainCheckApp._thisApp.Store.Element.Id);
                    // add toposolid on view
                    var search = new FilteredElementCollector(document)
                        .OfClass(typeof(Toposolid))
                        .Cast<Toposolid>().ToList();
                    if (search != null)
                        createdIds.AddRange(search.Select(s => s.Id));

                    createdIds.AddRange(TerrainCheckApp._thisApp.Store.TerrainBoundaryIds);

                    //uidoc.Selection.SetElementIds(createdIds);
                    //Document.ActiveView.IsolateElementsTemporary(createdIds);

                    //var tgm = TemporaryGraphicsManager.GetTemporaryGraphicsManager(Document);
                    //var ogs = new OverrideGraphicSettings()
                    //    .SetProjectionLineColor(new Color(0, 255, 0)) // verde
                    //    .SetSurfaceTransparency(30);                  // semitransparente

                    //foreach (var id in createdIds)
                    //    Document.ActiveView.SetElementOverrides(id, ogs);
                }

                tx.Commit();
            }
        }

        public static WallResult_[] ConnectPoints(WallResult_[] points)
        {
            if (points == null || points.Length < 2)
                return Array.Empty<WallResult_>();

            var curves = new List<WallResult_>();
            double totalDistance = 0;
            int segmentCount = 0;

            const double maxRelativeFactor = 20.0; // Fator multiplicador para definir distância "anormal"
            const double minDistanceTolerance = 0.01; // Tolerância para evitar curvas de comprimento quase zero

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var current = points[i];
                double distance = prev.PlatoHeightPoint.DistanceTo(current.PlatoHeightPoint);

                // Evita distância nula ou quase nula
                if (distance < minDistanceTolerance)
                    continue;

                double average = segmentCount > 0 ? totalDistance / segmentCount : distance;

                // Interrompe o agrupamento se a distância for muito discrepante
                if (segmentCount > 0 && distance > maxRelativeFactor * average)
                {
                    totalDistance = 0;
                    segmentCount = 0;
                    continue;
                }

                // Só considera segmentos curtos (dentro do limite de 20)
                if (distance <= 20)
                {
                    try
                    {
                        var curve = Line.CreateBound(prev.PlatoHeightPoint, current.PlatoHeightPoint);
                        if (curve.Length > minDistanceTolerance)
                        {
                            prev.wallCurve = curve;
                            curves.Add(prev);

                            totalDistance += distance;
                            segmentCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log opcional ou debug
                        Debug.WriteLine($"Erro ao criar linha entre pontos: {ex.Message}");
                        // Continua para evitar que um erro comprometa toda a sequência
                        continue;
                    }
                }
            }

            return curves.ToArray();
        }

        public static ProjectionResult[] ProjectLinesToFaces(IEnumerable<XYZ> points, Face[] faces, Face[] terrainFaces, Element element)
        {
            List<XYZ> projectedPoints = new();
            List<ProjectionResult> results = new();

            int totalPoints = points.Count();

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            int currentIndex = 0;

            foreach (XYZ startPoint in points)
            {
                foreach (Face face in faces)
                {
                    XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
                    if (normal == null) continue;

                    if (utils.XYZUtils.IsFacingInside(face, element))
                        normal = -normal;

                    XYZ vectorToFace = (face.Evaluate(new UV(0.5, 0.5)) - startPoint).Normalize();

                    if (normal.DotProduct(vectorToFace) <= 0)
                    {
                        continue;
                    }

                    Line horizontalLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
                    if (horizontalLine == null) continue;

                    Line ray = Line.CreateUnbound(startPoint, normal);
                    var resultSet = horizontalLine?.Intersect(ray, out _);
                    if (resultSet != SetComparisonResult.Overlap) continue;

                    XYZ projected = utils.XYZUtils.ProjectPointOntoTopography(terrainFaces, startPoint);
                    if (projected != null)
                    {
                        projectedPoints.Add(projected);
                        ProjectionResult projectionResult = new ProjectionResult(face, startPoint, projected);
                        results.Add(projectionResult);
                        break;
                    }
                }

                currentIndex++;
                double percentage = (double)currentIndex / totalPoints * 100;
                progressWindow.UpdateProgress(percentage, $"Projetando pontos: {currentIndex}/{totalPoints}");
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(delegate { })
                );
            }
            progressWindow.Close();

            return results.ToArray();
        }

        public static Face[] GetElementFaces(Element element, Document document)
        {
            if (element == null) return null;

            GeometryElement geomElement = element.get_Geometry(new Options());
            if (geomElement == null) return null;

            List<Face> faces = new();

            foreach (GeometryObject geoObj in geomElement)
            {
                if (geoObj is Solid solid && solid.Faces.Size > 0)
                {
                    faces.AddRange(solid.Faces.Cast<Face>());
                }
                else if (geoObj is Face face)
                {
                    faces.Add(face);
                }
                else if (geoObj is GeometryInstance geoInstance)
                {
                    GeometryElement instanceGeometry = geoInstance.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instanceGeometry)
                    {
                        if (instObj is Solid instSolid && instSolid.Faces.Size > 0)
                        {
                            faces.AddRange(instSolid.Faces.Cast<Face>());
                        }
                        else if (instObj is Face instFace)
                        {
                            faces.Add(instFace);
                        }
                    }
                }
            }

            for (int i = faces.Count - 1; i >= 0; i--)
            {
                Material material = document.GetElement(faces[i].MaterialElementId) as Material;
                if (material == null || !TerrainCheckApp._thisApp.Store.SelectedMaterials.Contains(material.Name))
                {
                    faces.RemoveAt(i);
                }
            }

            BoundingBoxXYZ bbox = element.get_BoundingBox(document.ActiveView);
            XYZ center = (bbox.Min + bbox.Max) / 2;
            for (int i = faces.Count - 1; i >= 0; i--)
            {
                Face face = faces[i];
                UV uv = new UV(0.5, 0.5);
                XYZ faceNormal = face.ComputeNormal(uv).Normalize();
                XYZ faceOrigin = face.Evaluate(uv);

                XYZ directionToCenter = (center - faceOrigin).Normalize();

                double dot = faceNormal.DotProduct(directionToCenter);
                if (dot > 0)
                {
                    faces.RemoveAt(i);
                }
            }

            //utils.Draw._Face(Document, faces, new Color(255, 0, 0), 70);

            return faces.ToArray();
        }

        public static List<WallResult_[]> ConnectSegments(List<WallResult_> wallResults, double maxDist = 100)
        {
            List<WallResult_[]> segments = new List<WallResult_[]>();
            List<WallResult_> currentSegment = new List<WallResult_>();

            for (int i = 0; i < wallResults.Count; i++)
            {
                WallResult_ currentPoint = wallResults[i];

                if (currentSegment.Count == 0)
                {
                    currentSegment.Add(currentPoint);
                    continue;
                }

                WallResult_ lastPoint = currentSegment.Last();
                double distance = lastPoint.PlatoHeightPoint.DistanceTo(currentPoint.PlatoHeightPoint);

                if (distance > maxDist)
                {
                    if (currentSegment.Count >= 2)
                        segments.Add(currentSegment.ToArray());

                    currentSegment = new List<WallResult_> { currentPoint };
                }
                else
                {
                    currentSegment.Add(currentPoint);
                }
            }

            if (currentSegment.Count >= 2)
                segments.Add(currentSegment.ToArray());

            return segments;
        }

        public static List<Line> BuildWorstLines(List<WallResult_> segment, Document doc, double minAngle = 45.0, double maxAngle = 120.0, int maxSkip = 10)
        {
            List<Line> lines = new List<Line>();
            if (segment == null || segment.Count < 2)
                return lines;

            List<XYZ> points = segment.Select(s => s.PlatoHeightPoint).ToList();
            int groupStart = 0;
            XYZ prevDir = null;

            for (int i = 1; i < points.Count; i++)
            {
                XYZ p1 = points[i - 1];
                XYZ p2 = points[i];
                XYZ dir = new XYZ(p2.X - p1.X, p2.Y - p1.Y, 0).Normalize();

                if (prevDir != null)
                {
                    double angle = dir.AngleTo(prevDir) * (180.0 / Math.PI);

                    if (angle > maxAngle || angle < minAngle)
                    {
                        double bestAngle = angle;
                        int bestIndex = i;

                        for (int skip = 1; skip <= maxSkip && (i + skip) < points.Count; skip++)
                        {
                            XYZ pNext = points[i + skip];
                            XYZ nextDir = new XYZ(pNext.X - p1.X, pNext.Y - p1.Y, 0).Normalize();
                            double nextAngle = nextDir.AngleTo(prevDir) * (180.0 / Math.PI);

                            if (nextAngle >= minAngle && nextAngle <= maxAngle)
                            {
                                bestIndex = i + skip;
                                break;
                            }

                            if (Math.Abs(90 - nextAngle) < Math.Abs(90 - bestAngle))
                            {
                                bestAngle = nextAngle;
                                bestIndex = i + skip;
                            }
                        }

                        Line line = Line.CreateBound(points[groupStart], points[bestIndex]);
                        if (line.Length >= doc.Application.ShortCurveTolerance)
                            lines.Add(line);

                        groupStart = bestIndex;
                        i = bestIndex;
                        prevDir = null;
                        continue;
                    }
                }

                prevDir = dir;
            }

            Line lastLine = Line.CreateBound(points[groupStart], points.Last());
            if (lastLine.Length >= doc.Application.ShortCurveTolerance)
                lines.Add(lastLine);

            return lines;
        }
    }
}
