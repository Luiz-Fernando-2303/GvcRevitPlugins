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
        public List<(Face face, XYZ point)> results = new List<(Face face, XYZ point)>();

        public ProjectFaces(UIDocument Uidocument, ElementId elementid, Curve[] lines, int subdivision)
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

            utils.Draw._XYZ(Document, ProjectedPoints);
            utils.Draw._XYZ(Document, SlopePoints());
        }

        private XYZ[] SlopePoints()
        {
            List<XYZ> resultPoints = new();

            foreach (var result in results)
            {
                Face face = result.face;
                XYZ projectedPoint = result.point;

                // Normal da face
                BoundingBoxUV bbox = face.GetBoundingBox();
                UV midUV = new UV((bbox.Min.U + bbox.Max.U) / 2, (bbox.Min.V + bbox.Max.V) / 2);
                XYZ faceNormal = face.ComputeNormal(midUV).Normalize();

                // Linha horizontal da base da face
                Line horizontalBaseLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
                if (horizontalBaseLine == null) continue;

                // Ponto projetado na altura 0
                XYZ baseProjected = new XYZ(projectedPoint.X, projectedPoint.Y, 0);

                // Criar raio na direção da normal a partir do ponto base (em Z = 0)
                Line ray = Line.CreateUnbound(baseProjected, faceNormal);

                // Interseção do raio com a linha da base
                SetComparisonResult intersectionResult = horizontalBaseLine.Intersect(ray, out IntersectionResultArray intersectionArray);
                if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                    continue;

                XYZ intersection = intersectionArray.get_Item(0).XYZPoint;

                // Cálculo da distância de deslocamento
                XYZ faceOrigin = face.Evaluate(midUV);
                double heightDifference = Math.Abs(faceOrigin.Z - projectedPoint.Z);
                double x = heightDifference / 2;

                // Direção perpendicular à inclinação da face (no plano XY)
                XYZ horizontalDirection = new XYZ(faceNormal.X, faceNormal.Y, 0).Normalize();
                XYZ horizontalNormal = new XYZ(-horizontalDirection.Y, horizontalDirection.X, 0).Normalize();

                // Ponto deslocado horizontalmente
                XYZ moved = intersection + horizontalNormal * x;

                // Recolocar na altura da linha da face
                double zBase = horizontalBaseLine.GetEndPoint(0).Z; // ou GetEndPoint(1).Z, devem ser iguais
                XYZ finalPoint = new XYZ(moved.X, moved.Y, zBase);

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

                    Line ray = Line.CreateUnbound(startPoint, normal);

                    var resultSet = horizontalLine?.Intersect(ray, out _);
                    if (resultSet != SetComparisonResult.Overlap) continue;

                    XYZ projected = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, startPoint);
                    if (projected != null)
                    {
                        projectedPoints.Add(projected);
                        results.Add((face, projected));
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
