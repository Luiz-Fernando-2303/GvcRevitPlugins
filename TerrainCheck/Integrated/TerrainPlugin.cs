using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    public class SlopeResult
    {
        public XYZ StartPoint { get; set; }
        public XYZ BoundaryPoint { get; set; }
        public XYZ EndPoint { get; set; }
        public XYZ Middle => StartPoint != null && EndPoint != null ? new XYZ((StartPoint.X + EndPoint.X) / 2, (StartPoint.Y + EndPoint.Y) / 2, (StartPoint.Z + EndPoint.Z) / 2) : null;
        public Face ReferenceFace { get; set; }
        public double HeightDifference { get; set; }
        public double DistanceToBoundary { get; set; }
        public double DistanceFaceToBoundary => StartPoint != null && BoundaryPoint != null ? utils.XYZUtils.FaceNormal(ReferenceFace, out UV _).DistanceTo(BoundaryPoint) : 0.0;
        public double DistanceSlopeToBoundary => StartPoint != null && BoundaryPoint != null ? Middle.DistanceTo(BoundaryPoint) : 0.0;
        public double WallExtension => EndPoint != null && StartPoint != null ? EndPoint.DistanceTo(StartPoint) : 0.0;
        public double OffsetUsed { get; set; }
        public Wall Wall { get; set; }
        public Line WallLine => StartPoint != null && EndPoint != null ? Line.CreateBound(StartPoint, EndPoint) : null;
    }

    public class TerrainPlugin
    {
        public UIDocument UiDoc { get; set; }
        public Document Doc => UiDoc?.Document;

        public double PlatformElevation { get; private set; }
        public Level PlatformLevel { get; private set; }
        public ElementId TerrainBoundaryId { get; private set; }
        public int SubdivisionLevel { get; set; } = 10;
        public double wallHeight_ { get; set; } = 2.0; // Default wall height in meters
        public double minimumDistance_ { get; set; } = 2.0; // Default minimum distance in meters

        public ProjectedFaceData ProjectedFace { get; private set; }
        public Curve[] TerrainBoundaryLines { get; private set; }
        public Face[] TopoFaces { get; private set; }
        public Toposolid Toposolid { get; private set; }
        public XYZ[] BoundaryPoints { get; private set; }
        public XYZ[] startPoints { get; private set; }
        public List<SlopeResult> Results { get; private set; } = new();

        // Pre made items
        public Curve[] PreMadePath { get; set; } = null;
        public ElementId PreMadeTopoSolidId { get; set; } = null;
        public Face[] PreMadeTopoFaces { get; set; } = null;

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
            startPoints = ProjectedFace?.FaceProjection;
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

            SetBoundaryPoints();
            RunSlopeAnalysis();

            foreach (var result in Results)
            {
                if (result?.WallLine != null)
                {
                    WallType wallType = Shared.Utils.RevitUtils.GetOrCreateWallType(UiDoc, "Resultado Talude Corte", BuiltInCategory.OST_Walls, new Color(255, 0, 0));
                    var wall = Wall.Create(
                        Doc,
                        result.WallLine,
                        wallType.Id,
                        PlatformLevel.Id,
                        UnitUtils.ConvertToInternalUnits(wallHeight_, UnitTypeId.Meters), 
                        0,
                        false,
                        false
                    );
                    result.Wall = wall;
                    result.ReferenceFace = ProjectedFace.Face;
                }
            }

            transaction.Commit();
        }

        public void RunSlopeAnalysis()
        {
            Results.Clear();

            double baseElevation = UnitUtils.ConvertToInternalUnits(PlatformElevation, UnitTypeId.Meters);
            double wallHeight = UnitUtils.ConvertToInternalUnits(wallHeight_, UnitTypeId.Meters);
            double minDistance = minimumDistance_;
            minDistance = minDistance > 2 ? minDistance : 2;
            minDistance = UnitUtils.ConvertToInternalUnits(minDistance, UnitTypeId.Meters);

            if (wallHeight > minDistance)
            {
                minDistance = wallHeight - UnitUtils.ConvertToInternalUnits(1.0, UnitTypeId.Meters);
            }

            List<Curve> wallCurves = new();
            List<XYZ> validStarts = new();
            List<XYZ> validBoundaries = new();
            List<XYZ> endPoints = new();

            int count = Math.Min(startPoints?.Length ?? 0, BoundaryPoints?.Length ?? 0);

            for (int i = 0; i < count; i++)
            {
                var start = startPoints[i];
                var boundary = BoundaryPoints[i];
                if (start == null || boundary == null) continue;

                validStarts.Add(start);
                validBoundaries.Add(boundary);

                // Calcula o deslocamento com base na diferença de altura
                double offset = (boundary.Z - baseElevation) / 2;
                offset = offset < minDistance ? minDistance : offset;

                var end = Shared.Utils.XYZUtils.GetEndPoint(start, ProjectedFace.FaceNormal, offset);
                endPoints.Add(end);

                // Cria linha entre o ponto atual e o anterior válido
                if (end != null && endPoints.Count > 1 && endPoints[^2] != null)
                {
                    wallCurves.Add(Line.CreateBound(endPoints[^2], end));

                    Results.Add(new SlopeResult
                    {
                        StartPoint = endPoints[^2],
                        BoundaryPoint = boundary,
                        EndPoint = end,
                        HeightDifference = Math.Abs(boundary.Z - baseElevation),
                        DistanceToBoundary = start.DistanceTo(boundary),
                        OffsetUsed = offset
                    });
                }
            }
        }

        public virtual XYZ[] FindIntersectionPoints(Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> result = new();
            var startPoints = utils.XYZUtils.DivideCurvesEvenly(boundaryPath, SubdivisionLevel);
            var horizontalLine = GetFaceHorizontalLine(face);

            foreach (var startPoint in startPoints)
            {
                var ray = Line.CreateUnbound(startPoint, normal);

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
