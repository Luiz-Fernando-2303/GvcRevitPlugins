using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public ProjectFaces(
            UIDocument Uidocument,
            ElementId elementid, Curve[] lines,
            int subdivision,
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

            if (TerrainFaces == null || TerrainFaces.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhuma face de terreno válida encontrada.");
                return;
            }

            ProjectLinesToFaces();

            var slopePoints = SlopePoints(baseElevation);
            var connectedSlopePoints = ConnectPoints(slopePoints);
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

                Line ray = Line.CreateUnbound(flatPoint, normal);
                SetComparisonResult intersectionResult = baseFlatLine.Intersect(ray, out IntersectionResultArray intersectionArray);
                if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                    continue;

                XYZ intersection = intersectionArray.get_Item(0).XYZPoint;
                XYZ transformedIntersection = new XYZ(intersection.X, intersection.Y, baseLine.GetEndPoint(0).Z);

                double wallHeight = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);
                double minDistance = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.MinimumDistance, UnitTypeId.Meters);
                if (wallHeight > minDistance)
                    minDistance = wallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);

                double verticalOffset = (projectedPoint.Z - baseElevation) / 2;
                verticalOffset = Math.Max(verticalOffset, minDistance);

                double inclinationOffset = Math.Abs(transformedIntersection.Z - projectedPoint.Z) / 2;

                double totalOffset = verticalOffset + inclinationOffset;

                XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);

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

                    double altura = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters); //TODO: seletor da altura

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

        private Curve[] ConnectPoints(XYZ[] points)
        {
            if (points == null || points.Length < 2)
                return Array.Empty<Curve>();

            var curves = new List<Curve>();
            double totalDistance = 0;
            int segmentCount = 0;

            for (int i = 1; i < points.Length; i++)
            {
                var prev = points[i - 1];
                var current = points[i];
                double distance = prev.DistanceTo(current);

                double average = segmentCount > 0 ? totalDistance / segmentCount : distance;

                if (segmentCount > 0 && distance > 2 * average)
                {
                    totalDistance = 0;
                    segmentCount = 0;
                }
                else if (distance <= 20)
                {
                    curves.Add(Line.CreateBound(prev, current));
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
