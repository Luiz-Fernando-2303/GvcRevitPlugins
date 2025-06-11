using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            //TerrainCheckCommand_.Execute(uiApp, draw);
            TerrainCheckCommand_reversed.Execute(uiApp, draw);
        }
    }

    public static class TerrainCheckCommand_reversed
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

            // Test
            XYZ[] test = FindIntersectionPoints(faceProjection, faceNormal, terrainBoundaryLines, filteredTopoFaces);
            //Draw._XYZ(doc, test);

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

            XYZ[] result = Shared.Utils.XYZUtils.DivideEvenly(startPoint, endPoint, subdivisionLevel);

            if (result is null || result.All(x => x is null)) return (null, null, null, null);

            XYZ resultNormal = (selectedFace as PlanarFace).FaceNormal;
            if (transform != null)
                resultNormal = transform.OfVector(resultNormal).Normalize();

            ElementId levelId = uiDoc.Document.GetElement(pickedRef.ElementId).LevelId;
            Level level = uiDoc.Document.GetElement(levelId) as Level;

            return (result, resultNormal, level, selectedFace);
        }

        // Interseção reversa Terreno -> parede
        public static XYZ[] FindIntersectionPoints(Document doc, Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces, int subdivisionsPerCurve)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> projectedPoints = new();

            List<XYZ> startPoints = Shared.Utils.XYZUtils.DivideCurvesEvenly(boundaryPath, subdivisionsPerCurve);

            foreach (XYZ startPoint in startPoints)
            {
                Line ray = Line.CreateUnbound(startPoint, normal);
                Draw._Line(doc, ray);

                SetComparisonResult result = face.Intersect(ray, out IntersectionResultArray intersectionResults);
                if (result != SetComparisonResult.Overlap) continue;

                XYZ projectedPoint = ProjectPointOntoTopography(terrainFaces, intersectionResults.get_Item(0).XYZPoint); //sem efeito
                projectedPoints.Add(projectedPoint);
            }

            Draw._XYZ(doc, projectedPoints);
            return projectedPoints.ToArray();
        }

        private static XYZ[] FindIntersectionPoints(XYZ[] startPoints, XYZ normal, Curve[] boundaryPath, Face[] terrainFaces)
        {
            XYZ[] boundaryPoints = new XYZ[startPoints.Length];

            for (int i = 0; i < startPoints.Length; i++)
            {
                Line rayPath = Line.CreateUnbound(startPoints[i], normal);
                XYZ foundIntersection = null;

                for (int j = 0; j < boundaryPath.Length; j++)
                {
                    SetComparisonResult result = rayPath.Intersect(boundaryPath[j], out IntersectionResultArray intersectionResults);
                    if (result != SetComparisonResult.Overlap) continue;

                    XYZ intersectionResultPoint = intersectionResults.get_Item(0).XYZPoint;
                    double angle = normal.AngleTo(intersectionResultPoint - startPoints[i]) * 180 / Math.PI;

                    if (angle > 1) continue;

                    foundIntersection = intersectionResultPoint;
                    break;
                }

                if (foundIntersection is null) continue;

                XYZ projected = ProjectPointOntoTopography(terrainFaces, foundIntersection);
                if (projected is null) continue;

                boundaryPoints[i] = projected;
            }

            if (boundaryPoints.All(x => x is null)) return null;

            return boundaryPoints;
        }

        private static XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
        {
            foreach (Face face in faces)
            {
                if (!FilterPlanes((face as PlanarFace).FaceNormal)) continue;
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

            if (!(toposolidElem is Toposolid)) return null;

            toposolid = toposolidElem as Toposolid;

            GeometryElement geomElement = toposolid.get_Geometry(new Options());

            Solid[] solids = geomElement.OfType<Solid>().ToArray();

            List<Face> result = new List<Face>();

            foreach (Solid solid in solids)
                foreach (Face face in solid.Faces)
                    if (FilterPlanes((face as PlanarFace).FaceNormal))
                        result.Add(face);

            return result.ToArray();
        }

        private static bool FilterPlanes(XYZ normal) => !(normal.X == 1 || normal.X == -1 || normal.Y == 1 || normal.Y == -1 || normal.Z == -1);
    }

    public static class TerrainCheckCommand_new
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            Reference faceRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (faceRef == null) return;

            Element element = doc.GetElement(faceRef.ElementId);
            Face selectedFace = element.GetGeometryObjectFromReference(faceRef) as Face;
            if (selectedFace == null) return;   

            XYZ faceNormal = (selectedFace as PlanarFace).FaceNormal;
            if (faceNormal == null) return;
            Draw._XYZ(doc, faceNormal);

            Line horizontalEdge = null;
            foreach (EdgeArray edgeArray in selectedFace.EdgeLoops)
            {
                foreach (Edge edge in edgeArray)
                {
                    Curve curve = edge.AsCurve();
                    if (curve is Line line && Math.Abs(line.Direction.Z) < 0.01)
                    {
                        horizontalEdge = line;
                        break;
                    }
                }
                if (horizontalEdge != null) break;
            }

            if (horizontalEdge == null) return;
            Draw._Line(doc, horizontalEdge);

            int subdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;

            List<XYZ> samplePoints = new List<XYZ>();
            XYZ start = horizontalEdge.GetEndPoint(0);
            XYZ end = horizontalEdge.GetEndPoint(1);

            for (int i = 0; i <= subdivisionLevel; i++)
            {
                double t = (double)i / subdivisionLevel;
                XYZ pt = new XYZ(
                    start.X + t * (end.X - start.X),
                    start.Y + t * (end.Y - start.Y),
                    start.Z + t * (end.Z - start.Z)
                );
                samplePoints.Add(pt);
            }
            Draw._XYZ(doc, samplePoints);

            Toposolid topo = new FilteredElementCollector(doc)
                .OfClass(typeof(Toposolid))
                .Cast<Toposolid>()
                .FirstOrDefault();
            if (topo == null) return;

            List<Face> topoFaces = new List<Face>();
            foreach (Solid solid in topo.get_Geometry(new Options()).OfType<Solid>())
                foreach (Face face in solid.Faces)
                {
                    if (face is PlanarFace planarFace && !planarFace.FaceNormal.IsAlmostEqualTo(faceNormal, 0.01))
                        continue;

                    topoFaces.Add(face);
                }
            if (topoFaces.Count == 0) return;

            List<XYZ> intersectionPoints = new List<XYZ>();
            foreach (XYZ point in samplePoints)
            {
                Line ray = Line.CreateUnbound(point, faceNormal);
                Draw._Line(doc, ray);
                foreach (Face topoFace in topoFaces)
                {
                    SetComparisonResult result = topoFace.Intersect(ray, out IntersectionResultArray intersectionResults);
                    if (result == SetComparisonResult.Overlap && intersectionResults.Size > 0)
                    {
                        XYZ intersectionPoint = intersectionResults.get_Item(0).XYZPoint;
                        if (!intersectionPoints.Any(p => p.IsAlmostEqualTo(intersectionPoint, 0.01)))
                            intersectionPoints.Add(intersectionPoint);
                    }
                }
            }
            if (intersectionPoints.Count == 0) return;
            Draw._XYZ(doc, intersectionPoints);

            using (Transaction transaction = new Transaction(doc, "EMCCAMP - Terrain Check"))
            {
                ElementId ElementId = faceRef.ElementId;
                ElementId levelId = uiDoc.Document.GetElement(ElementId).LevelId;
                Level level = doc.GetElement(levelId) as Level;

                transaction.Start();
                CheckRules.Execute(uiDoc,
                    samplePoints.ToArray(),
                    faceNormal,
                    intersectionPoints.ToArray(), 
                    UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.PlatformElevation, UnitTypeId.Meters),
                    draw,
                    level
                );
                transaction.Commit();
            }
        }

        private class XYZComparer : IEqualityComparer<XYZ>
        {
            private double _tolerance;
            public XYZComparer(double tolerance) => _tolerance = tolerance;

            public bool Equals(XYZ a, XYZ b)
            {
                return a.IsAlmostEqualTo(b, _tolerance);
            }

            public int GetHashCode(XYZ obj)
            {
                return 0;
            }
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

            (XYZ[] faceProjection, XYZ faceNormal, Level level) = GetSelectedFace(uiDoc, subdivisionLevel);
            if (faceNormal is null || faceProjection.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Face normal is null");
                return;
            }
            Draw._XYZ(doc, faceProjection);
            Draw._XYZ(doc, faceNormal);

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

            XYZ[] boundaryPoints = FindIntersectionPoints(faceProjection, faceNormal, terrainBoundaryLines, filteredTopoFaces);
            if (boundaryPoints is null || boundaryPoints.All(x => x is null))
            {
                TaskDialog.Show("Null Error", "Boundary points are null");
                return;
            }
            Draw._XYZ(doc, boundaryPoints);

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

        public static (XYZ[], XYZ, Level) GetSelectedFace(UIDocument uiDoc, int subdivisionLevel)
        {
            Reference pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return (null, null, null);

            Element element = uiDoc.Document.GetElement(pickedRef.ElementId);

            GeometryObject geoObject = element.GetGeometryObjectFromReference(pickedRef);

            Transform transform = null;
            if (element is FamilyInstance familyInstance)
                transform = familyInstance.GetTransform();

            if (!(geoObject is Face))
            {
                TaskDialog.Show("Error", "The selected object is not a face.");
                return (null, null, null);
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

            if (startPoint is null || endPoint is null) return (null, null, null);

            XYZ[] result = Shared.Utils.XYZUtils.DivideEvenly(startPoint, endPoint, subdivisionLevel);

            if (result is null || result.All(x => x is null)) return (null, null, null);

            XYZ resultNormal = (selectedFace as PlanarFace).FaceNormal;
            if (transform != null)
                resultNormal = transform.OfVector(resultNormal).Normalize();

            ElementId levelId = uiDoc.Document.GetElement(pickedRef.ElementId).LevelId;
            Level level = uiDoc.Document.GetElement(levelId) as Level;

            return (result, resultNormal, level);
        }

        private static XYZ[] FindIntersectionPoints(XYZ[] startPoints, XYZ normal, Curve[] boundaryPath, Face[] terrainFaces)
        {
            XYZ[] boundaryPoints = new XYZ[startPoints.Length];

            for (int i = 0; i < startPoints.Length; i++)
            {
                Line rayPath = Line.CreateUnbound(startPoints[i], normal);
                XYZ foundIntersection = null;

                for (int j = 0; j < boundaryPath.Length; j++)
                {
                    var tst1 = boundaryPath[j].GetEndPoint(0);
                    var tst2 = boundaryPath[j].GetEndPoint(1);
                    SetComparisonResult result = rayPath.Intersect(boundaryPath[j], out IntersectionResultArray intersectionResults);

                    if (result != SetComparisonResult.Overlap) continue;

                    XYZ intersectionResultPoint = intersectionResults.get_Item(0).XYZPoint;
                    double angle = normal.AngleTo(intersectionResultPoint - startPoints[i]) * 180 / Math.PI;

                    if (angle > 1) continue;

                    foundIntersection = intersectionResultPoint;
                    break;
                }

                if (foundIntersection is null) continue;

                XYZ projected = ProjectPointOntoTopography(terrainFaces, foundIntersection);

                if (projected is null) continue;

                boundaryPoints[i] = projected;
            }

            if (boundaryPoints.All(x => x is null)) return null;

            return boundaryPoints;
        }

        private static XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
        {
            foreach (Face face in faces)
            {
                if (!FilterPlanes((face as PlanarFace).FaceNormal)) continue;
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

            if (!(toposolidElem is Toposolid)) return null;

            toposolid = toposolidElem as Toposolid;

            GeometryElement geomElement = toposolid.get_Geometry(new Options());

            Solid[] solids = geomElement.OfType<Solid>().ToArray();

            List<Face> result = new List<Face>();

            foreach (Solid solid in solids)
                foreach (Face face in solid.Faces)
                    if (FilterPlanes((face as PlanarFace).FaceNormal))
                        result.Add(face);

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

        public static void _Line(Document doc, IEnumerable<Line> l)
        {
            foreach (Line line in l)
                _Line(doc, line);
        }

        public static void _Line(Document doc, Line line)
        {
            using (Transaction tx = new Transaction(doc, "Desenhar Linha"))
            {
                tx.Start();

                // Converte unbound para bound
                Line drawLine = line;
                if (!line.IsBound)
                {
                    double halfLength = 50;
                    XYZ p0 = line.Origin - line.Direction * halfLength;
                    XYZ p1 = line.Origin + line.Direction * halfLength;
                    drawLine = Line.CreateBound(p0, p1);
                }

                XYZ p0_draw = drawLine.GetEndPoint(0);
                XYZ p1_draw = drawLine.GetEndPoint(1);
                XYZ dir = (p1_draw - p0_draw).Normalize();

                // Garante que a direção do plano seja ortogonal à linha
                XYZ up = XYZ.BasisZ;
                if (Math.Abs(dir.DotProduct(up)) > 0.99)
                    up = XYZ.BasisX; // evita degeneração se a linha for vertical

                XYZ right = dir.CrossProduct(up).Normalize();
                XYZ normal = right.CrossProduct(dir).Normalize();

                Plane plane = Plane.CreateByNormalAndOrigin(normal, p0_draw);
                SketchPlane sketch = SketchPlane.Create(doc, plane);

                ModelCurve mc = doc.Create.NewModelCurve(drawLine, sketch);
                Draw.modelCurves.Add(mc); // armazena se necessário

                tx.Commit();
            }
        }

        public static void _XYZ(Document doc, IEnumerable<XYZ> p)
        {
            foreach (XYZ point in p)
                _XYZ(doc, point);
        }

        public static void _XYZ(Document doc, XYZ p)
        {
            using (Transaction transaction = new Transaction(doc, "Draw XYZ Point"))
            {
                transaction.Start();

                if (p == null)
                {
                    transaction.RollBack();
                    return;
                }

                double radius = 0.5;

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

                transaction.Commit();
            }
        }

        public static void _Curve(Document doc, IEnumerable<Curve> c)
        {
            foreach (Curve curve in c) 
                _Curve(doc, curve);
        }

        public static void _Curve(Document doc, Curve c)
        {
            using (Transaction transaction = new Transaction(doc, "Draw Curve"))
            {
                transaction.Start();

                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, c.GetEndPoint(0));
                SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                ModelCurve modelCurve = doc.Create.NewModelCurve(c, sketchPlane);

                transaction.Commit();
            }
        }
    }
}
