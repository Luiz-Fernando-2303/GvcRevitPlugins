using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    /// <summary>
    /// Resultado da projeção de um ponto em uma face do terreno.
    /// </summary>
    public class ProjectionResult
    {
        /// <summary>
        /// Face onde o ponto foi projetado.
        /// </summary>
        public Face Face { get; set; }

        /// <summary>
        /// Ponto na divisa projetado em altura zero (plano horizontal).
        /// </summary>
        public XYZ FlatPoint { get; set; }

        /// <summary>
        /// Ponto na divisa projetado no terreno de referencia (altura do terreno).
        /// </summary>
        public XYZ ProjectedPoint { get; set; }

        public ProjectionResult(Face face, XYZ flatPoint, XYZ projectedPoint)
        {
            Face = face;
            FlatPoint = flatPoint;
            ProjectedPoint = projectedPoint;
        }
    }

    /// <summary>
    /// Resultado da projeção de uma linha inclinada sobre o terreno, incluindo o ponto projetado e a altura da parede.
    /// </summary>
    public class WallResult_
    {
        /// <summary>
        /// Localização do ponto projetado sobre o terreno.
        /// </summary>
        public XYZ resultPoint { get; set; }

        public XYZ PlatoHeightPoint { get; set; }

        /// <summary>
        /// Altura da parede a ser criada a partir do ponto projetado.
        /// </summary>  
        public double wallHeight { get; set; }

        /// <summary>
        /// Localização da parade na vista
        /// </summary>
        public Curve wallCurve { get; set; }

        public WallResult_(XYZ point, double wallHeight)
        {
            this.resultPoint = point;
            this.wallHeight = wallHeight;
        }
    }

    public class ProjectFaces
    {
        Document Document { get; set; }
        Element Element { get; set; }
        Curve[] Lines { get; set; }
        Face[] Faces { get; set; }
        List<XYZ> LinesSubdivions { get; set; } = new List<XYZ>();
        Face[] TerrainFaces { get; set; }
        List<LineResult> LineResults { get; set; }
        public XYZ[] ProjectedPoints { get; set; }
        public List<ProjectionResult> results = new List<ProjectionResult>();

        public ProjectFaces(UIDocument Uidocument, ElementId elementid, Curve[] lines, List<LineResult> LineResults_, double subdivision, double baseElevation)
        {
            Document = Uidocument.Document;
            Element = Document.GetElement(elementid);
            Lines = lines;
            LineResults = LineResults_;

            Toposolid solid = new FilteredElementCollector(Document)
                .OfClass(typeof(Toposolid))
                .Cast<Toposolid>().First();

            TerrainFaces = utils.XYZUtils.FilterTopoFaces(Document, solid.Id, out _);

            if (TerrainCheckApp._thisApp.Store.IntersectionGeometricObject == null)
            {
                Faces = GetElementFaces();
                if (Faces == null || Faces.Length == 0)
                {
                    TaskDialog.Show("Erro", "Nenhuma face válida encontrada no elemento selecionado.");
                    return;
                }
            } 
            else
            {
                CreateDummyFaces();
                if (Faces == null || Faces.Length == 0)
                {
                    TaskDialog.Show("Erro", "Nenhuma face válida encontrada no objeto de interseção.");
                    return;
                }
            }

            if (TerrainFaces == null || TerrainFaces.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhuma face de terreno válida encontrada.");
                return;
            }

            Execute();
        }


        private void Execute()
        {
            List<WallResult_> wallResults = new List<WallResult_>();
            List<string> errorLines = new List<string>();

            foreach (LineResult lineResult in LineResults)
            {
                Element element = lineResult.Element;
                List<XYZ> subdivisions = utils.XYZUtils.DivideCurvesEvenly(
                    new List<Line> { lineResult.line },
                    TerrainCheckApp._thisApp.Store.SubdivisionLevel
                );

                if (subdivisions == null || subdivisions.Count == 0)
                {
                    errorLines.Add($"Linha {lineResult.line.Id}: não foi possível subdividir.");
                    continue;
                }

                LinesSubdivions.AddRange(subdivisions);

                ProjectionResult[] projectedPoints = ProjectLinesToFaces(subdivisions);
                if (projectedPoints == null || projectedPoints.Length == 0)
                {
                    errorLines.Add($"Linha {lineResult.line.Id}: não foi possível projetar os pontos no terreno.");
                    continue;
                }

                WallResult_[] slopePoints = SlopePoints(projectedPoints, element, TerrainCheckApp._thisApp.Store.PlatformElevation, true);
                if (slopePoints == null || slopePoints.Length == 0)
                {
                    errorLines.Add($"Linha {lineResult.line.Id}: não foi possível calcular os pontos de declive.");
                    continue;
                }

                wallResults.AddRange(slopePoints);
            }

            if (wallResults.Count == 0)
            {
                // Nenhuma linha funcionou
                string allErrors = errorLines.Count > 0 ? string.Join("\n", errorLines) : "Nenhuma linha pôde ser processada.";
                TaskDialog.Show("Erros de Processamento", allErrors);
                return;
            }

            if (errorLines.Count > 0)
            {
                TaskDialog.Show("Aviso", $"Algumas linhas não puderam ser processadas:\n{string.Join("\n", errorLines)}");
            }

            List<WallResult_> connectedSlopePoints = ConnectPoints(wallResults.ToArray()).ToList();
            if (connectedSlopePoints == null || connectedSlopePoints.Count == 0)
                return;

            CreateExtrudedWallFromCurves(connectedSlopePoints.ToArray());
        }

        private WallResult_[] SlopePoints(IEnumerable<ProjectionResult> projections, Element reference, double baseElevation, bool project = true)
        {
            List<WallResult_> resultPoints = new();
            List<XYZ> unprojectedPoints = new();

            int totalPoints = projections.Count();

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            int currentIndex = 0;

            foreach (ProjectionResult projectionResult in projections)
            {
                try
                {
                    Face face = projectionResult.Face;
                    XYZ projectedPoint = projectionResult.ProjectedPoint;
                    XYZ flatPoint = projectionResult.FlatPoint;

                    Line baseFlatLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
                    Line baseLine = utils.XYZUtils.GetLongestHorizontalEdge(face, false);
                    XYZ facePoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, face.Triangulate().Vertices.First());

                    XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
                    if (normal == null) continue;

                    Line ray = Line.CreateUnbound(flatPoint, normal);
                    SetComparisonResult intersectionResult = baseFlatLine.Intersect(ray, out IntersectionResultArray intersectionArray);
                    if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                        continue;

                    XYZ intersection = intersectionArray.get_Item(0).XYZPoint;
                    XYZ transformedIntersection = new XYZ(intersection.X, intersection.Y, baseLine.GetEndPoint(0).Z);

                    double wallHeight = UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Meters);
                    double minDistance = UnitUtils.ConvertToInternalUnits(utils.XYZUtils.UpOrDown(facePoint, projectedPoint), UnitTypeId.Meters);
                    double totalOffset = 0;

                    if (TerrainCheckApp._thisApp.Store.BoundarySelectionType == "Arrimo")
                    {
                        ElementType type = reference.Document.GetElement(reference.GetTypeId()) as ElementType;
                        double retainWallHeight = 0;

                        if (type != null)
                        {
                            Parameter heightParam = type.LookupParameter("Altura Arrimo");
                            if (heightParam != null && heightParam.HasValue)
                            {
                                retainWallHeight = heightParam.AsDouble();
                            }
                            totalOffset = retainWallHeight - 3.28;
                        }
                    }
                    else
                    {
                        double verticalOffset = (projectedPoint.Z - baseElevation) / 2;
                        verticalOffset = Math.Max(verticalOffset, minDistance);

                        double inclinationOffset = Math.Abs(transformedIntersection.Z - projectedPoint.Z) / 2;
                        totalOffset = verticalOffset + inclinationOffset;
                    }

                    XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);
                    XYZ finalPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));

                    if (project)
                        finalPoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, movedPoint);

                    WallResult_ slopeResult = new WallResult_(finalPoint, wallHeight);
                    slopeResult.PlatoHeightPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));

                    if (slopeResult.resultPoint == null || project == false)
                    {
                        movedPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));
                        unprojectedPoints.Add(movedPoint);
                        continue;
                    }

                    resultPoints.Add(slopeResult);

                }
                catch (Exception) { continue; }

                currentIndex++;
                double percentage = (double)currentIndex / totalPoints * 100;
                progressWindow.UpdateProgress(percentage, $"Processando pontos: {currentIndex}/{totalPoints}");
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
            }

            progressWindow.Close();

            if (unprojectedPoints.Count > 0 && project)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "Pontos Fora do Sólido",
                    $"Foram encontrados {unprojectedPoints.Count} ponto(s) fora da topografia.\nDeseja desenhá-los no modelo?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (result == TaskDialogResult.Yes)
                {
                    foreach (XYZ point in unprojectedPoints)
                    {
                        utils.Draw._XYZ(Document, point, 0.5, new Color(255, 165, 0));
                    }
                }
            }

            return resultPoints.ToArray();
        }

        private void CreateDummyFaces()
        {
            var dummy = new List<Face>();
            GeometryObject geometryObject = TerrainCheckApp._thisApp.Store.IntersectionGeometricObject;
            Face face = geometryObject as Face;
            Mesh mesh = geometryObject as Mesh;

            if (mesh == null || mesh.Vertices.Count < 4) return;

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
                return;
            }

            normal = normal.Normalize();

            Solid extrusion = GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { faceLoop },
                normal,  
                0.01
            );

            //filter Faces that point to the same normal as the original face
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

            Faces = dummy.ToArray();
        }

        private void CreateExtrudedWallFromCurves(WallResult_[] wallResults)
        {
            using (var tx = new Transaction(Document, "Create extrude wall"))
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
                        Document, wallSolid, color, transparency, out var element, addOnScene: true); // adcionar propriedade de referencia

                    if (element != null)
                        createdIds.Add(element.Id);
                }

                if (createdIds.Count > 0)
                {
                    UIDocument uidoc = new UIDocument(Document);
                    createdIds.Add(TerrainCheckApp._thisApp.Store.Element.Id);
                    // add toposolid on view
                    var search = new FilteredElementCollector(Document)
                        .OfClass(typeof(Toposolid))
                        .Cast<Toposolid>().ToList();
                    if (search != null)
                        createdIds.AddRange(search.Select(s => s.Id));

                    createdIds.AddRange(TerrainCheckApp._thisApp.Store.TerrainBoundaryIds);

                    uidoc.Selection.SetElementIds(createdIds);
                    Document.ActiveView.IsolateElementsTemporary(createdIds);

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

        private WallResult_[] ConnectPoints(WallResult_[] points)
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

        public ProjectionResult[] ProjectLinesToFaces(IEnumerable<XYZ> points)
        {
            List<XYZ> projectedPoints = new();
            List<ProjectionResult> results = new();

            int totalPoints = points.Count();

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            int currentIndex = 0;

            foreach (XYZ startPoint in points)
            {
                foreach (Face face in Faces)
                {
                    XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
                    if (normal == null) continue;

                    if (utils.XYZUtils.IsFacingInside(face, Element))
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

                    XYZ projected = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, startPoint);
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

        private Face[] GetElementFaces()
        {
            if (Element == null) return null;

            GeometryElement geomElement = Element.get_Geometry(new Options());
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
                Material material = Document.GetElement(faces[i].MaterialElementId) as Material;
                if (material == null || !TerrainCheckApp._thisApp.Store.SelectedMaterials.Contains(material.Name))
                {
                    faces.RemoveAt(i);
                }
            }

            BoundingBoxXYZ bbox = Element.get_BoundingBox(Document.ActiveView);
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
    }
}
