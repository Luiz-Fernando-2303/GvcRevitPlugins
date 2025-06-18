using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            TerrainCheckCommand_.Execute(uiApp, draw);
        }
    }

    public static class TerrainCheckCommand_
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            double platformElevation = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.PlatformElevation, UnitTypeId.Meters);
            int terrainBoundaryId = TerrainCheckApp._thisApp.Store.TerrainBoundaryId;
            int subdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;

            (XYZ[] faceProjection, XYZ faceNormal, Level level, Face face) = GetSelectedFace(uiDoc, subdivisionLevel);
            if (faceNormal is null || faceProjection.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Face normal is null");
                return;
            }

            Curve[] terrainBoundaryLines = GetTerrainBoundaryLines(doc, terrainBoundaryId, out ElementId toposolidId);
            if (terrainBoundaryLines == null || terrainBoundaryLines.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Terrain boundary lines are null");
                return;
            }
            Draw._Curve(doc, terrainBoundaryLines);

            Face[] filteredTopoFaces = FilterTopoFaces(doc, toposolidId, out Toposolid toposolid);
            if (filteredTopoFaces == null || filteredTopoFaces.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Filtered topo faces are null");
                return;
            }

            XYZ[] boundaryPoints = FindIntersectionPoints(doc, face, faceNormal, terrainBoundaryLines, filteredTopoFaces, subdivisionLevel);
            if (boundaryPoints is null || boundaryPoints.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Boundary points are null");
                return;
            }
            TerrainCheckApp._thisApp.Store.RailingPoins = boundaryPoints.ToList();

            using (Transaction transaction = new Transaction(doc, "EMCCAMP - Terrain Check"))
            {
                transaction.Start();
                CheckRules.Execute(uiDoc, faceProjection, faceNormal, boundaryPoints, platformElevation, draw, level);
                transaction.Commit();
            }
        }

        internal static Curve[] GetTerrainBoundaryLines(Document doc, int railingId, out ElementId toposolidId)
        {
            toposolidId = null;
            Element element = doc.GetElement(new ElementId(railingId)) as Element;

            FamilyInstance familyInstance = element as FamilyInstance;
            ElementId hostId = null;

            if (element is Railing)
                hostId = ((Railing)element).HostId;

            if (!(element is Railing))
            {
                TaskDialog.Show("Error", "Elemento selecionado não é um Guarda Corpo");
                return null;
            }

            Railing railing = element as Railing;
            toposolidId = railing.HostId;

            return railing.GetPath().ToArray();
        }

        public static (XYZ[], XYZ, Level, Face) GetSelectedFace(UIDocument uiDoc, int subdivisionLevel)
        {
            Reference pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return (null, null, null, null);

            Element element = uiDoc.Document.GetElement(pickedRef.ElementId);

            GeometryObject geoObject = element.GetGeometryObjectFromReference(pickedRef);

            Transform transform = null;
            if (element is FamilyInstance familyInstance)
                transform = familyInstance.GetTransform();

            if (!(geoObject is Face))
            {
                TaskDialog.Show("Error", "The selected object is not a face.");
                return (null, null, null, null);
            }

            Face selectedFace = geoObject as Face;

            EdgeArrayArray edgeLoops = selectedFace.EdgeLoops;
            List<Line> boundaryLines = new List<Line>();

            foreach (EdgeArray edgeArray in edgeLoops)
                foreach (Edge edge in edgeArray)
                    if (edge.AsCurve() is Line line)
                        boundaryLines.Add(line);

            XYZ startPoint = null;
            XYZ endPoint = null;
            foreach (Line line in boundaryLines) //TODO: Refactor this
            {
                if (line.Direction.Z != 0) continue;

                Line actualLine = line;

                if (transform != null) actualLine = actualLine.CreateTransformed(transform) as Line;

                XYZ start = actualLine.GetEndPoint(0);
                XYZ end = actualLine.GetEndPoint(1);
                startPoint = new XYZ(start.X, start.Y, 0);
                endPoint = new XYZ(end.X, end.Y, 0);

                break;
            }

            if (startPoint is null || endPoint is null) return (null, null, null, null);

            XYZ[] result = utils.XYZUtils.DivideEvenly(startPoint, endPoint, subdivisionLevel);

            if (result is null || result.All(x => x is null)) return (null, null, null, null);

            XYZ resultNormal = (selectedFace as PlanarFace).FaceNormal;
            if (transform != null)
                resultNormal = transform.OfVector(resultNormal).Normalize();

            ElementId levelId = uiDoc.Document.GetElement(pickedRef.ElementId).LevelId;
            Level level = uiDoc.Document.GetElement(levelId) as Level;

            return (result, resultNormal, level, selectedFace);
        }

        public static XYZ[] FindIntersectionPoints(Document doc, Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces, int subdivisionsPerCurve)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> projectedPoints = new();

            List<XYZ> startPoints = utils.XYZUtils.DivideCurvesEvenly(boundaryPath, subdivisionsPerCurve);

            foreach (XYZ startPoint in startPoints)
            {
                Line ray = Line.CreateUnbound(startPoint, normal);

                SetComparisonResult result = face.Intersect(ray, out IntersectionResultArray intersectionResults);
                if (result != SetComparisonResult.Overlap) continue;

                XYZ projectedPoint = ProjectPointOntoTopography(terrainFaces, startPoint);
                projectedPoints.Add(projectedPoint);

                Draw._Curve(doc, ray);
                Draw._XYZ(doc, intersectionResults.get_Item(0).XYZPoint, 0.2);
            }

            return projectedPoints.ToArray();
        }

        private static XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
        {
            foreach (Face face in faces)
            {
                XYZ faceNormal = utils.XYZUtils.FaceNormal(face, out UV _);

                if (!FilterPlanes(faceNormal)) continue;

                Line infinityCurve = Line.CreateUnbound(new XYZ(point.X, point.Y, 0), new XYZ(0, 0, 1));
                SetComparisonResult inftest = face.Intersect(infinityCurve, out IntersectionResultArray intersectionResults);
                if (inftest == SetComparisonResult.Overlap) return intersectionResults.get_Item(0).XYZPoint;
            }
            return null;
        }

        private static Face[] FilterTopoFaces(Document doc, ElementId toposolidId, out Toposolid toposolid)
        {
            toposolid = null;

            Element toposolidElem = doc.GetElement(toposolidId);
            if (toposolidElem is not Toposolid ts) return null;

            toposolid = ts;

            GeometryElement geomElement = toposolid.get_Geometry(new Options());
            Solid[] solids = geomElement.OfType<Solid>().Where(s => s.Faces.Size > 0).ToArray();

            List<Face> result = new List<Face>();

            foreach (Solid solid in solids)
            {
                foreach (Face face in solid.Faces)
                {;
                    XYZ faceNormal = utils.XYZUtils.FaceNormal(face, out UV _);

                    if (FilterPlanes(faceNormal))
                        result.Add(face);
                }
            }

            return result.ToArray();
        }

        private static bool FilterPlanes(XYZ normal) => !(normal.X == 1 || normal.X == -1 || normal.Y == 1 || normal.Y == -1 || normal.Z == -1);
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
