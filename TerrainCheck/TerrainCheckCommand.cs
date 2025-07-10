using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using utils = GvcRevitPlugins.Shared.Utils;

// Formas de divisa: Parede normal, Parade cortina, Linhas do modelo, guarda corpo, **linha de divisa, superfice topografica e vinculo AutoCad 2D
// Clicar na linha de divisa gerar os guarda corpos


// passo 1: configurar parametros (talude ou arrimo, norma tecnica (CEF, execucaco, estudal, municipal, federal, dnit e der) e [tipo de espaco])
// passo 2: clicar nos objetos de referencia (linha de divisa(mostra para o usuario e obrigatorio), divisa de analise(guarda corpo), face(do edificio), piso(plato acabado))
// passo 3: visualizacao dos resultados (vista 3D, planta, corte, quantitativos e graficos)
// passo 4: validacao dos resultados (area permeavel, voluem de concreto, corte\aterro e drenagem)
// passo 5: publicacao de resultados (automacao de prancha, cotas e detalhamentos executivos)

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            TerrainCheckCommand_ wallCommand = new();
            wallCommand.Execute(uiApp);
        }
    }

    public class TerrainCheckCommand_
    {
        public class ProjectedFaceData
        {
            public XYZ[] FaceProjection { get; set; }
            public XYZ FaceNormal { get; set; }
            public Face Face { get; set; }
        }

        public virtual void Execute(UIApplication uiApp)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            SelectionToLines selectionToLines = new SelectionToLines(TerrainCheckApp._thisApp.Store.TerrainBoundaryIds, doc);
            ProjectFaces projectFaces = new ProjectFaces(uiDoc, TerrainCheckApp._thisApp.Store.IntersectionElementId, selectionToLines.Lines, TerrainCheckApp._thisApp.Store.SubdivisionLevel);

            return;

            (double platformElevationRaw, Level platformLevel) = GetPlatformElevationWithLevel(uiDoc);
            double platformElevation = UnitUtils.ConvertToInternalUnits(platformElevationRaw, UnitTypeId.Meters);
            if (platformElevation == double.NegativeInfinity || platformLevel == null) return;

            ElementId terrainBoundaryId = GetTerrainBoundaryId(uiDoc);
            int subdivisionLevel = TerrainCheckApp._thisApp.Store.SubdivisionLevel;

            ProjectedFaceData projectedFaceData = GetFaceReferences(uiDoc, subdivisionLevel);
            if (projectedFaceData == null || projectedFaceData.FaceNormal == null || projectedFaceData.FaceProjection.All(p => p == null)) return;

            Curve[] terrainBoundaryLines = GetTerrainBoundaryPath(doc, terrainBoundaryId, out ElementId toposolidId);
            if (terrainBoundaryLines == null || terrainBoundaryLines.All(c => c == null)) return;

            Face[] filteredTopoFaces = FilterTopoFaces(doc, toposolidId, out Toposolid toposolid);
            if (filteredTopoFaces == null || filteredTopoFaces.All(f => f == null)) return;

            XYZ[] boundaryPoints = FindIntersectionPoints(doc, projectedFaceData.Face, projectedFaceData.FaceNormal, terrainBoundaryLines, filteredTopoFaces, subdivisionLevel);
            if (boundaryPoints == null || boundaryPoints.All(p => p == null)) return;

            using var transaction = new Transaction(doc, "EMCCAMP - Terrain Check");
            transaction.Start();

            CheckRules.Execute(uiDoc, projectedFaceData.FaceProjection, projectedFaceData.FaceNormal, boundaryPoints, platformElevation, true, platformLevel);

            transaction.Commit();
        }


        public virtual XYZ[] FindIntersectionPoints(Document doc, Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces, int subdivisionsPerCurve)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> result = new();
            var startPoints = utils.XYZUtils.DivideCurvesEvenly(boundaryPath, subdivisionsPerCurve);
            var horizontalLine = GetFaceHorizontalLine(face);
            utils.Draw._Curve(doc, horizontalLine);

            foreach (var startPoint in startPoints)
            {
                var ray = Line.CreateUnbound(startPoint, normal);
                utils.Draw._Curve(doc, ray);

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

        public virtual Face[] FilterTopoFaces(Document doc, ElementId toposolidId, out Toposolid toposolid)
        {
            toposolid = null;
            var element = doc.GetElement(toposolidId);
            if (element is not Toposolid ts) return null;
            toposolid = ts;

            var geometry = ts.get_Geometry(new Options());

            return geometry.OfType<Solid>()
                           .Where(s => s.Faces.Size > 0)
                           .SelectMany(s => s.Faces.Cast<Face>())
                           .Where(f => FilterPlanes(utils.XYZUtils.FaceNormal(f, out UV _)))
                           .ToArray();
        }

        public virtual Curve[] GetTerrainBoundaryPath(Document doc, ElementId railingId, out ElementId toposolidId)
        {
            toposolidId = null;
            if (doc.GetElement(railingId) is not Railing railing) return null;

            toposolidId = railing.HostId;
            return railing.GetPath()?.ToArray();
        }

        public virtual ProjectedFaceData GetFaceReferences(UIDocument uiDoc, int subdivisionLevel)
        {
            var doc = uiDoc.Document;
            var pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return null;

            var element = doc.GetElement(pickedRef.ElementId);
            var geoObject = element.GetGeometryObjectFromReference(pickedRef);
            var transform = (element is FamilyInstance fi) ? fi.GetTransform() : Transform.Identity;

            if (geoObject is not PlanarFace selectedFace) return null;

            var horizontalLine = GetFaceHorizontalLine(selectedFace, false);

            if (horizontalLine == null) return null;

            var points = utils.XYZUtils.DivideEvenly(horizontalLine.GetEndPoint(0), horizontalLine.GetEndPoint(1), subdivisionLevel);
            if (points == null || points.All(p => p == null)) return null;

            return new ProjectedFaceData
            {
                FaceProjection = points,
                FaceNormal = transform.OfVector(selectedFace.FaceNormal).Normalize(),
                Face = selectedFace
            };
        }

        public virtual (double elevation, Level level) GetPlatformElevationWithLevel(UIDocument uiDoc)
        {
            var pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione uma face de referência da elevação do platô");
            if (pickedRef == null) return (double.NegativeInfinity, null);

            var element = uiDoc.Document.GetElement(pickedRef.ElementId);
            var face = element.GetGeometryObjectFromReference(pickedRef) as Face;
            var normal = utils.XYZUtils.FaceNormal(face, out UV uv);
            var z = normal != null ? face.Evaluate(uv).Z : double.NegativeInfinity;
            var level = uiDoc.Document.GetElement(element.LevelId) as Level;

            return (z, level);
        }

        public virtual ElementId GetTerrainBoundaryId(UIDocument uiDoc)
        {
            var pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Selecione o muro de divisa");
            return pickedRef != null ? uiDoc.Document.GetElement(pickedRef.ElementId).Id : ElementId.InvalidElementId;
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

            XYZ start = new XYZ(left, center.Y, flat? 0 : center.Z);
            XYZ end = new XYZ(right, center.Y, flat ? 0 : center.Z);

            return Line.CreateBound(start, end);
        }
    }
}
