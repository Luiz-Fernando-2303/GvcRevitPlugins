using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    public class SlopeResult
    {
        public XYZ StartPoint { get; set; }
        public XYZ BoundaryPoint { get; set; }
        public XYZ EndPoint { get; set; }
        public double HeightDifference { get; set; }
        public double DistanceToBoundary { get; set; }
        public double OffsetUsed { get; set; }
        public Line WallLine => StartPoint != null && EndPoint != null ? Line.CreateBound(StartPoint, EndPoint) : null;
    }

    public class Plugin
    {
        public UIDocument UiDoc { get; set; }
        public Document Doc => UiDoc?.Document;

        public double PlatformElevation { get; private set; }
        public Level PlatformLevel { get; private set; }
        public ElementId TerrainBoundaryId { get; private set; }
        public int SubdivisionLevel { get; set; } = 10;

        public ProjectedFaceData ProjectedFace { get; private set; }
        public Curve[] TerrainBoundaryLines { get; private set; }
        public Face[] TopoFaces { get; private set; }
        public Toposolid Toposolid { get; private set; }
        public XYZ[] BoundaryPoints { get; private set; }
        public List<SlopeResult> Results { get; private set; } = new();

        public class ProjectedFaceData
        {
            public XYZ[] FaceProjection { get; set; }
            public XYZ FaceNormal { get; set; }
            public Face Face { get; set; }
        }

        public void Initialize(UIApplication uiApp)
        {
            UiDoc = uiApp.ActiveUIDocument;
            SubdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;
        }

        public bool SetPlatformElevation()
        {
            (double platformElevationRaw, Level platformLevel) = GetPlatformElevationWithLevel();
            if (platformElevationRaw == double.NegativeInfinity || platformLevel == null) return false;

            PlatformElevation = UnitUtils.ConvertToInternalUnits(platformElevationRaw, UnitTypeId.Meters);
            PlatformLevel = platformLevel;
            return true;
        }

        public bool SetTerrainBoundary()
        {
            TerrainBoundaryId = GetTerrainBoundaryId();
            TerrainBoundaryLines = GetTerrainBoundaryPath(TerrainBoundaryId, out ElementId toposolidId);
            if (TerrainBoundaryLines == null || TerrainBoundaryLines.All(c => c == null)) return false;

            TopoFaces = FilterTopoFaces(toposolidId, out Toposolid topo);
            Toposolid = topo;
            return TopoFaces != null && TopoFaces.Length > 0;
        }

        public bool SetProjectedFace()
        {
            ProjectedFace = GetFaceReferences();
            return ProjectedFace != null && ProjectedFace.FaceNormal != null && ProjectedFace.FaceProjection.All(p => p != null);
        }

        public bool SetBoundaryPoints()
        {
            BoundaryPoints = FindIntersectionPoints(ProjectedFace.Face, ProjectedFace.FaceNormal, TerrainBoundaryLines, TopoFaces);
            return BoundaryPoints != null && BoundaryPoints.All(p => p != null);
        }

        public void Execute()
        {
            using var transaction = new Transaction(Doc, "EMCCAMP - Terrain Check");
            transaction.Start();

            RunSlopeAnalysis();

            foreach (var result in Results)
            {
                if (result?.WallLine != null)
                {
                    WallType wallType = Shared.Utils.RevitUtils.GetOrCreateWallType(UiDoc, "Resultado Talude Corte", BuiltInCategory.OST_Walls, new Color(255, 0, 0));
                    Wall.Create(Doc, result.WallLine, wallType.Id, PlatformLevel.Id, result.OffsetUsed, 0, false, false);
                }
            }

            transaction.Commit();
        }

        private void RunSlopeAnalysis()
        {
            Results.Clear();
            double baseElevation = PlatformElevation;
            double wallHeight = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);

            double minDist = TerrainCheckApp._thisApp.Store.MinimumDistance;
            minDist = minDist > 2 ? minDist : 2;
            minDist = UnitUtils.ConvertToInternalUnits(minDist, UnitTypeId.Meters);
            if (wallHeight > minDist)
                minDist = wallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);

            int count = Math.Min(ProjectedFace.FaceProjection.Length, BoundaryPoints.Length);
            double maxHeightDiff = 0.0;
            double worstDistance = 0.0;

            for (int i = 0; i < count; i++)
            {
                var start = ProjectedFace.FaceProjection[i];
                var boundary = BoundaryPoints[i];
                if (start == null || boundary == null) continue;

                double heightDiff = boundary.Z - baseElevation;
                double offset = Math.Max(heightDiff / 2, minDist);
                var end = Shared.Utils.XYZUtils.GetEndPoint(start, ProjectedFace.FaceNormal, offset);

                var p1 = new Vector2((float)start.X, (float)start.Y);
                var p2 = new Vector2((float)boundary.X, (float)boundary.Y);
                double distance = Vector2.Distance(p1, p2);

                if (heightDiff > maxHeightDiff)
                {
                    maxHeightDiff = heightDiff;
                    worstDistance = distance;
                }

                Results.Add(new SlopeResult
                {
                    StartPoint = start,
                    BoundaryPoint = boundary,
                    EndPoint = end,
                    HeightDifference = heightDiff,
                    DistanceToBoundary = distance,
                    OffsetUsed = offset
                });
            }

            TerrainCheckApp._thisApp.Store.TerrainCheckCalcHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(maxHeightDiff, UnitTypeId.Meters), 1);
            TerrainCheckApp._thisApp.Store.TerrainCheckCalcDistance = Math.Round(UnitUtils.ConvertFromInternalUnits(worstDistance, UnitTypeId.Meters), 1);
        }

        public virtual XYZ[] FindIntersectionPoints(Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> result = new();
            var startPoints = utils.XYZUtils.DivideCurvesEvenly(boundaryPath, SubdivisionLevel);
            var horizontalLine = GetFaceHorizontalLine(face);
            Draw._Curve(Doc, horizontalLine);

            foreach (var startPoint in startPoints)
            {
                var ray = Line.CreateUnbound(startPoint, normal);
                Draw._Curve(Doc, ray);

                var resultSet = horizontalLine?.Intersect(ray, out IntersectionResultArray _);
                if (resultSet != SetComparisonResult.Overlap) continue;

                var projectedPoint = ProjectPointOntoTopography(terrainFaces, startPoint);
                if (projectedPoint != null)
                    result.Add(projectedPoint);
            }

            return result.ToArray();
        }

        public virtual XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
        {
            foreach (var face in faces)
            {
                var normal = utils.XYZUtils.FaceNormal(face, out UV _);
                if (!FilterPlanes(normal)) continue;

                var verticalLine = Line.CreateUnbound(new XYZ(point.X, point.Y, 0), XYZ.BasisZ);
                var result = face.Intersect(verticalLine, out IntersectionResultArray intersectionResults);
                if (result == SetComparisonResult.Overlap)
                    return intersectionResults.get_Item(0).XYZPoint;
            }

            return null;
        }

        public virtual Face[] FilterTopoFaces(ElementId toposolidId, out Toposolid toposolid)
        {
            toposolid = null;
            var element = Doc.GetElement(toposolidId);
            if (element is not Toposolid ts) return null;
            toposolid = ts;

            var geometry = ts.get_Geometry(new Options());

            return geometry.OfType<Solid>()
                           .Where(s => s.Faces.Size > 0)
                           .SelectMany(s => s.Faces.Cast<Face>())
                           .Where(f => FilterPlanes(utils.XYZUtils.FaceNormal(f, out UV _)))
                           .ToArray();
        }

        public virtual Curve[] GetTerrainBoundaryPath(ElementId railingId, out ElementId toposolidId)
        {
            toposolidId = null;
            if (Doc.GetElement(railingId) is not Railing railing) return null;

            toposolidId = railing.HostId;
            return railing.GetPath()?.ToArray();
        }

        public virtual ProjectedFaceData GetFaceReferences()
        {
            var pickedRef = UiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return null;

            var element = Doc.GetElement(pickedRef.ElementId);
            var geoObject = element.GetGeometryObjectFromReference(pickedRef);
            var transform = (element is FamilyInstance fi) ? fi.GetTransform() : Transform.Identity;

            if (geoObject is not PlanarFace selectedFace) return null;

            var horizontalLine = GetFaceHorizontalLine(selectedFace, false);
            if (horizontalLine == null) return null;

            var points = utils.XYZUtils.DivideEvenly(horizontalLine.GetEndPoint(0), horizontalLine.GetEndPoint(1), SubdivisionLevel);
            if (points == null || points.All(p => p == null)) return null;

            return new ProjectedFaceData
            {
                FaceProjection = points,
                FaceNormal = transform.OfVector(selectedFace.FaceNormal).Normalize(),
                Face = selectedFace
            };
        }

        public virtual (double elevation, Level level) GetPlatformElevationWithLevel()
        {
            var pickedRef = UiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione uma face de referência da elevação do platô");
            if (pickedRef == null) return (double.NegativeInfinity, null);

            var element = Doc.GetElement(pickedRef.ElementId);
            var face = element.GetGeometryObjectFromReference(pickedRef) as Face;
            var normal = utils.XYZUtils.FaceNormal(face, out UV uv);
            var z = normal != null ? face.Evaluate(uv).Z : double.NegativeInfinity;
            var level = Doc.GetElement(element.LevelId) as Level;

            return (z, level);
        }

        public virtual ElementId GetTerrainBoundaryId()
        {
            var pickedRef = UiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Selecione o muro de divisa");
            return pickedRef != null ? UiDoc.Document.GetElement(pickedRef.ElementId).Id : ElementId.InvalidElementId;
        }

        public virtual bool FilterPlanes(XYZ normal)
        {
            return !(Math.Abs(normal.X) == 1 || Math.Abs(normal.Y) == 1 || normal.Z == -1);
        }

        public virtual Line GetFaceHorizontalLine(Face face, bool flat = true)
        {
            if (face == null) return null;

            Mesh mesh = face.Triangulate();
            List<XYZ> vertices = mesh.Vertices.Cast<XYZ>().ToList();
            XYZ center = new XYZ(
                vertices.Average(v => v.X),
                vertices.Average(v => v.Y),
                vertices.Average(v => v.Z)
            );

            double maxWidth = vertices.Max(v => v.X);
            double left = center.X - maxWidth / 2;
            double right = center.X + maxWidth / 2;

            XYZ start = new XYZ(left, center.Y, flat ? 0 : center.Z);
            XYZ end = new XYZ(right, center.Y, flat ? 0 : center.Z);

            return Line.CreateBound(start, end);
        }
    }
}
