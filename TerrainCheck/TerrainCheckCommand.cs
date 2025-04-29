using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            double platformElevation = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.PlatformElevation, UnitTypeId.Meters);
            int terrainBoundaryId = TerrainCheckApp._thisApp.Store.TerrainBoundaryId;
            //int subdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;
            int subdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;

            (XYZ[] faceProjection, XYZ faceNormal) = GetSelectedFace(uiDoc, subdivisionLevel);

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

            using (Transaction transaction = new Transaction(doc, "EMCCAMP - Terrain Check"))
            {
                transaction.Start();
                CheckRules.Execute(uiDoc, faceProjection, faceNormal, boundaryPoints, platformElevation, draw);
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
            //else if (element is Wall)
            //    hostId = ((Wall)element);

            if (!(element is Railing))
            {
                TaskDialog.Show("Error", "Elemento selecionado não é um Guarda Corpo");
                return null;
            }

            Railing railing = element as Railing;
            toposolidId = railing.HostId;

            return railing.GetPath().ToArray();
        }
        public static (XYZ[], XYZ) GetSelectedFace(UIDocument uiDoc, int subdivisionLevel)
        {
            Reference pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return (null, null);

            Element element = uiDoc.Document.GetElement(pickedRef.ElementId);

            GeometryObject geoObject = element.GetGeometryObjectFromReference(pickedRef);

            Transform transform = null;
            if (element is FamilyInstance familyInstance)
                transform = familyInstance.GetTransform();

            if (!(geoObject is Face))
            {
                TaskDialog.Show("Error", "The selected object is not a face.");
                return (null, null);
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

            if (startPoint is null || endPoint is null) return (null, null);

            XYZ[] result = Shared.Utils.XYZUtils.DivideEvenly(startPoint, endPoint, subdivisionLevel);

            if (result is null || result.All(x => x is null)) return (null, null);

            XYZ resultNormal = (selectedFace as PlanarFace).FaceNormal;
            if (transform != null)
                resultNormal = transform.OfVector(resultNormal).Normalize();

            return (result, resultNormal);
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
}
