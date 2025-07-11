using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    public class ProjectFaces
    {
        Document Document { get; set; }
        Element Element { get; set; }
        Curve[] Lines { get; set; }
        Face[] Faces { get; set; }
        List<XYZ> LinesSubdivions { get; set; }
        Toposolid Toposolid { get; set; }
        Face[] TerrainFaces { get; set; }

        public XYZ[] ProjectedPoints { get; set; }
        public List<(Face face, XYZ flatPoint, XYZ projectedPoint)> results = new List<(Face face, XYZ flatPoint, XYZ projectedPoint)>();

        public ProjectFaces(UIDocument Uidocument, ElementId elementid, Curve[] lines, int subdivision, double baseElevation)
        {
            Document = Uidocument.Document;
            Element = Document.GetElement(elementid);

            TaskDialog.Show("Seleção de Terreno", "Por favor, selecione o terreno (Toposolid) para projetar as linhas.");
            Reference pickedRef = Uidocument.Selection.PickObject(ObjectType.Element, "Selecione o Terreno");
            if (pickedRef == null) return;

            Element element = Document.GetElement(pickedRef.ElementId);
            if (element is Toposolid toposolid)
            {
                Toposolid = toposolid;
            }
            else
            {
                TaskDialog.Show("Erro", "O elemento selecionado não é um Terreno válido.");
                return;
            }

            TerrainFaces = utils.XYZUtils.FilterTopoFaces(Document, element.Id, out _);
            Lines = lines;
            LinesSubdivions = utils.XYZUtils.DivideCurvesEvenly(lines, subdivision);
            Faces = GetElementFaces();

            if (Faces == null || Faces.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhuma face válida encontrada no elemento selecionado.");
                return;
            }

            if (TerrainFaces == null || TerrainFaces.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhuma face de terreno válida encontrada.");
                return;
            }

            ProjectLinesToFaces();

            Curve[] connected = ConnectPoints(ProjectedPoints);
            CreateExtrudedWallFromCurves(connected);

            var slopePoints = SlopePoints(baseElevation);
            Curve[] connectedSlopePoints = ConnectPoints(slopePoints);
            CreateExtrudedWallFromCurves(connectedSlopePoints);
        }

        private XYZ[] SlopePoints(double baseElevation)
        {
            List<XYZ> resultPoints = new();

            foreach (var result in results)
            {
                Face face = result.face;
                XYZ projectedPoint = result.projectedPoint;
                XYZ flatPoint = result.flatPoint;

                Line baseFlatLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
                Line baseLine = utils.XYZUtils.GetLongestHorizontalEdge(face, false);
                XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
                if (normal == null) continue;

                // Encontrar interseção com linha base do plano
                Line ray = Line.CreateUnbound(flatPoint, normal);
                SetComparisonResult intersectionResult = baseFlatLine.Intersect(ray, out IntersectionResultArray intersectionArray);
                if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                    continue;

                XYZ intersection = intersectionArray.get_Item(0).XYZPoint;
                XYZ transformedIntersection = new XYZ(intersection.X, intersection.Y, baseLine.GetEndPoint(0).Z);

                // Parâmetros de altura e distância mínima
                double wallHeight = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);
                double minDistance = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.MinimumDistance, UnitTypeId.Meters);
                if (wallHeight > minDistance)
                    minDistance = wallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);

                // Calcula offset real
                double verticalOffset = (projectedPoint.Z - baseElevation) / 2;
                verticalOffset = Math.Max(verticalOffset, minDistance);

                // Inclinação simulada adicional proporcional à diferença de altura
                double inclinationOffset = Math.Abs(transformedIntersection.Z - projectedPoint.Z) / 2;

                // Soma total de deslocamento na direção da normal (real + inclinação)
                double totalOffset = verticalOffset + inclinationOffset;

                // Deslocar ponto ao longo da normal (ambos componentes juntos)
                XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);

                // Projeta o ponto deslocado de volta no terreno
                XYZ finalPoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, movedPoint);
                resultPoints.Add(finalPoint);
            }

            return resultPoints.ToArray();
        }

        private void CreateExtrudedWallFromCurves(Curve[] curves)
        {
            using (var tx = new Transaction(Document, "Criar forma 3D inclinada"))
            {
                tx.Start();

                foreach (var curve in curves)
                {
                    var baseStart = curve.GetEndPoint(0);
                    var baseEnd = curve.GetEndPoint(1);

                    double altura = UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Meters); //TODO: seletor da altura

                    var up = XYZ.BasisZ.Multiply(altura);

                    var p1 = baseStart;
                    var p2 = baseEnd;
                    var p3 = baseEnd + up;
                    var p4 = baseStart + up;

                    // forma um retângulo
                    var faceLoop = new CurveLoop();
                    faceLoop.Append(Line.CreateBound(p1, p2));
                    faceLoop.Append(Line.CreateBound(p2, p3));
                    faceLoop.Append(Line.CreateBound(p3, p4));
                    faceLoop.Append(Line.CreateBound(p4, p1));

                    // cria sólido a partir do perfil
                    var loops = new List<CurveLoop> { faceLoop };
                    Solid wallSolid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, (p2 - p1).CrossProduct(up).Normalize(), 0.1);

                    // cria DirectShape
                    var ds = DirectShape.CreateElement(Document, new ElementId(BuiltInCategory.OST_GenericModel));
                    ds.ApplicationId = "WallExtrusion3D";
                    ds.ApplicationDataId = Guid.NewGuid().ToString();
                    ds.SetShape(new List<GeometryObject> { wallSolid });
                }

                tx.Commit();
            }
        }

        private Curve[] ConnectPoints(XYZ[] points)
        {
            if (points == null || points.Length < 2)
                return Array.Empty<Curve>();

            var curves = new List<Curve>();
            var remaining = new HashSet<XYZ>(points);
            var visited = new HashSet<XYZ>();

            var current = points[0];
            visited.Add(current);
            remaining.Remove(current);

            while (remaining.Count > 0)
            {
                var next = remaining
                    .Where(p => !p.IsAlmostEqualTo(current))
                    .OrderBy(p => p.DistanceTo(current))
                    .FirstOrDefault();

                if (next == null)
                    break;

                curves.Add(Line.CreateBound(current, next));
                visited.Add(next);
                remaining.Remove(next);
                current = next;
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

                    // Permitir apenas ângulos entre 90° e 180° → dot entre -1 e 0
                    if (dot >= 0) continue;

                    Line ray = Line.CreateUnbound(startPoint, normal);
                    var resultSet = horizontalLine?.Intersect(ray, out _);
                    if (resultSet != SetComparisonResult.Overlap) continue;

                    XYZ projected = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, startPoint);
                    if (projected != null)
                    {
                        projectedPoints.Add(projected);
                        results.Add((face, startPoint, projected));
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

            return faces.ToArray();
        }
    }
}
