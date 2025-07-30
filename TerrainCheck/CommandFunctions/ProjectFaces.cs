using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
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
        public XYZ point { get; set; }

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
            this.point = point;
            this.wallHeight = wallHeight;
        }
    }

    public class ProjectFaces
    {
        Document Document { get; set; }
        Element Element { get; set; }
        Curve[] Lines { get; set; }
        Face[] Faces { get; set; }
        List<XYZ> LinesSubdivions { get; set; }
        Face[] TerrainFaces { get; set; }

        public XYZ[] ProjectedPoints { get; set; }
        public List<ProjectionResult> results = new List<ProjectionResult>();

        public ProjectFaces(
            UIDocument Uidocument,
            ElementId elementid, 
            Curve[] lines,
            double subdivision,
            double baseElevation)
        {
            Document = Uidocument.Document;
            Element = Document.GetElement(elementid);

            Toposolid solid = new FilteredElementCollector(Document)
                .OfClass(typeof(Toposolid))
                .Cast<Toposolid>().First();

            TerrainFaces = utils.XYZUtils.FilterTopoFaces(Document, solid.Id, out _);
            Lines = lines;
            LinesSubdivions = utils.XYZUtils.DivideCurvesEvenly(lines, subdivision);

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

            ProjectLinesToFaces();
            if (ProjectedPoints == null || ProjectedPoints.Length == 0)
            {
                TaskDialog.Show("Erro", "Não foi possivel projetar os pontos da linha de divisa");
                return;
            }

            var slopePoints = SlopePoints(baseElevation);
            if (slopePoints == null || slopePoints.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhum resultado encontrado no terreno.");
                return;
            }

            var connectedSlopePoints = ConnectPoints(slopePoints);
            if (connectedSlopePoints == null || connectedSlopePoints.Length == 0)
            {
                TaskDialog.Show("Erro", "Não foi possivel gerar os resultados finais");
                return;
            }

            CreateExtrudedWallFromCurves(connectedSlopePoints);
        }

        /// <summary>
        /// Calcula os pontos projetados sobre o terreno a partir dos resultados de interseção com as faces.<br/>
        /// Etapas executadas para cada resultado:<br/>
        /// 1. Obtém a aresta horizontal mais longa da face na altura zero (<c>baseFlatLine</c>) e na altura real (<c>baseLine</c>).<br/>
        /// 2. Calcula a normal da face (<c>normal</c>).<br/>
        /// 3. Projeta o ponto base (<c>flatPoint</c>) sobre a linha da base em altura zero para obter a interseção (<c>intersection</c>).<br/>
        /// 4. Transfere a interseção para a altura da base real da face (<c>transformedIntersection</c>).<br/>
        /// 5. Calcula os deslocamentos vertical (<c>verticalOffset</c>) e de inclinação (<c>inclinationOffset</c>), considerando altura da parede e distância mínima.<br/>
        /// 6. Move o ponto transformado na direção da normal da face, aplicando o deslocamento total.<br/>
        /// 7. Projeta esse ponto final sobre a topografia e o adiciona à lista de resultados.<br/>
        /// </summary>
        private WallResult_[] SlopePoints(double baseElevation)
        {
            List<WallResult_> resultPoints = new();
            List<XYZ> unprojectedPoints = new();

            foreach (var result in results)
            {
                Face face = result.Face;
                XYZ projectedPoint = result.ProjectedPoint;
                XYZ flatPoint = result.FlatPoint;

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

                double minDistance = utils.XYZUtils.UpOrDown(facePoint, projectedPoint);
                minDistance = UnitUtils.ConvertToInternalUnits(utils.XYZUtils.UpOrDown(facePoint, projectedPoint), UnitTypeId.Meters);

                if (wallHeight > minDistance)
                    minDistance = wallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);

                double verticalOffset = (projectedPoint.Z - baseElevation) / 2;
                verticalOffset = Math.Max(verticalOffset, minDistance);

                double inclinationOffset = Math.Abs(transformedIntersection.Z - projectedPoint.Z) / 2;
                double totalOffset = verticalOffset + inclinationOffset;

                XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);
                XYZ finalPoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, movedPoint);
                
                WallResult_ slopeResult = new WallResult_(finalPoint, wallHeight);

                // No Terrain projection
                if (slopeResult.point == null)
                {
                    movedPoint = new XYZ(movedPoint.X, movedPoint.Y, projectedPoint.Z);
                    unprojectedPoints.Add(movedPoint);
                    continue;
                }

                if (!IsInvadingElement(slopeResult))
                {
                    resultPoints.Add(slopeResult);
                    continue;
                }
            }

            if (unprojectedPoints.Count > 0)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "Pontos Fora do Sólido",
                    $"Foram encontrados {unprojectedPoints.Count} ponto(s) fora do topografia.\nDeseja desenhá-los no modelo?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (result == TaskDialogResult.Yes)
                {
                    foreach (XYZ point in unprojectedPoints)
                    {
                        utils.Draw._XYZ(Document, point, 0.8, new Color(255, 165, 0));
                    }
                }
            }

            return resultPoints.ToArray();
        }

        private void CreateDummyFaces()
        {
            var dummy = new List<Face>();
            GeometryObject geometryObject = TerrainCheckApp._thisApp.Store.IntersectionGeometricObject;
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
                0.1
            );

            var faces = extrusion.Faces;
            dummy.AddRange(faces.OfType<Face>());
            Faces = dummy.ToArray();
        }

        private bool IsInvadingElement(WallResult_ result)
        {
            BoundingBoxXYZ elementBox = Element.get_BoundingBox(null);
            if (elementBox == null) return false;

            XYZ point = result.point;

            double tolerance = UnitUtils.ConvertToInternalUnits(-1, UnitTypeId.Meters);

            return point.X >= elementBox.Min.X - tolerance &&
                   point.Y >= elementBox.Min.Y - tolerance &&
                   point.Z >= elementBox.Min.Z - tolerance &&
                   point.X <= elementBox.Max.X + tolerance &&
                   point.Y <= elementBox.Max.Y + tolerance &&
                   point.Z <= elementBox.Max.Z + tolerance;
        }


        private void CreateExtrudedWallFromCurves(WallResult_[] wallResults)
        {
            using (var tx = new Transaction(Document, "Create extrude wall"))
            {
                tx.Start();

                foreach (var wall in wallResults)
                {
                    var baseStart = wall.wallCurve.GetEndPoint(0);
                    var baseEnd = wall.wallCurve.GetEndPoint(1);

                    double altura = wall.wallHeight;

                    var up = XYZ.BasisZ.Multiply(altura);

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
                    Solid wallSolid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, (p2 - p1).CrossProduct(up).Normalize(), 0.1);

                    var ds = DirectShape.CreateElement(Document, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "TerrainCheckApp";
                    ds.ApplicationDataId = Guid.NewGuid().ToString();
                    ds.SetShape(new List<GeometryObject> { wallSolid });
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

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var current = points[i];
                double distance = prev.point.DistanceTo(current.point);

                double average = segmentCount > 0 ? totalDistance / segmentCount : distance;

                if (segmentCount > 0 && distance > 20 * average)
                {
                    totalDistance = 0;
                    segmentCount = 0;
                }
                else if (distance <= 20)
                {
                    prev.wallCurve = Line.CreateBound(prev.point, current.point);
                    curves.Add(prev);

                    totalDistance += distance;
                    segmentCount++;
                }
            }

            return curves.ToArray();
        }

        private void ProjectLinesToFaces()
        {
            List<XYZ> projectedPoints = new();

            foreach (XYZ startPoint in LinesSubdivions)
            {
                foreach (Face face in Faces)
                {
                    XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
                    if (normal == null) continue;

                    Line horizontalLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
                    if (horizontalLine == null) continue;

                    XYZ faceCentroid = (horizontalLine.GetEndPoint(0) + horizontalLine.GetEndPoint(1)) / 2;
                    XYZ directionToFace = (faceCentroid - startPoint).Normalize();

                    double dot = normal.Normalize().DotProduct(directionToFace);

                    //if (dot >= 0) 
                    //    continue;

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
            }

            ProjectedPoints = projectedPoints.Count > 0 ? projectedPoints.ToArray() : Array.Empty<XYZ>();
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

            return faces.ToArray();
        }
    }
}
