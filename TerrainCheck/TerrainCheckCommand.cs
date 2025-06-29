using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using utils = GvcRevitPlugins.Shared.Utils;

// Formas de divisa: Parede normal, Parade cortina, Linhas do modelo, guarda corpo, **linha de divisa, superfice topografica e vinculo AutoCad 2D

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
            TerrainCheckCommand_Wall wallCommand = new();
            wallCommand.Execute(uiApp);
        }
    }

    public class TerrainCheckCommand_Wall : BaseTerrainCheckCommand { }

    public class BaseTerrainCheckCommand
    {
        public class ProjectedFaceData
        {
            public XYZ[] FaceProjection { get; set; }
            public XYZ FaceNormal { get; set; }
            public Level Level { get; set; }
            public Face Face { get; set; }
        }

        public virtual void Execute(UIApplication uiApp)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            double platformElevation = UnitUtils.ConvertToInternalUnits(GetPlatformElevation(uiDoc), UnitTypeId.Meters);
            if (platformElevation == double.NegativeInfinity) return;

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

            CheckRules.Execute(uiDoc, projectedFaceData.FaceProjection, projectedFaceData.FaceNormal, boundaryPoints, platformElevation, true, projectedFaceData.Level);

            transaction.Commit();
        }

        public virtual XYZ[] FindIntersectionPoints(Document doc, Face face, XYZ normal, IEnumerable<Curve> boundaryPath, Face[] terrainFaces, int subdivisionsPerCurve)
        {
            if (face == null || normal == null || boundaryPath == null || !boundaryPath.Any()) return null;

            List<XYZ> result = new();
            var startPoints = utils.XYZUtils.DivideCurvesEvenly(boundaryPath, subdivisionsPerCurve);
            var horizontalLine = GetFaceHorizontalLine(face);

            foreach (var startPoint in startPoints)
            {
                var ray = Line.CreateUnbound(startPoint, normal); // Simula um "raio" perpendicular
                Draw._Curve(doc, ray);

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

            // Seleciona somente faces com inclinação útil (exclui horizontais e verticais)
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

            var horizontalLine = selectedFace
                .EdgeLoops.Cast<EdgeArray>()
                .SelectMany(ea => ea.Cast<Edge>())
                .Select(e => e.AsCurve())
                .OfType<Line>()
                .FirstOrDefault(l => l.Direction.Z == 0);

            if (horizontalLine == null) return null;

            // Projeta a linha horizontal para o plano XY (Z = 0)
            var transformedLine = horizontalLine.CreateTransformed(transform) as Line;
            var start = new XYZ(transformedLine.GetEndPoint(0).X, transformedLine.GetEndPoint(0).Y, 0);
            var end = new XYZ(transformedLine.GetEndPoint(1).X, transformedLine.GetEndPoint(1).Y, 0);

            var points = utils.XYZUtils.DivideEvenly(start, end, subdivisionLevel);
            if (points == null || points.All(p => p == null)) return null;

            return new ProjectedFaceData
            {
                FaceProjection = points,
                FaceNormal = transform.OfVector(selectedFace.FaceNormal).Normalize(),
                Level = doc.GetElement(element.LevelId) as Level,
                Face = selectedFace
            };
        }

        public virtual double GetPlatformElevation(UIDocument uiDoc)
        {
            var pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Face, "Selecione uma face de referência da elevação do platô");
            if (pickedRef == null) return double.NegativeInfinity;

            var face = uiDoc.Document.GetElement(pickedRef.ElementId).GetGeometryObjectFromReference(pickedRef) as Face;
            var normal = utils.XYZUtils.FaceNormal(face, out UV uv);
            return normal != null ? face.Evaluate(uv).Z : double.NegativeInfinity;
        }

        public virtual ElementId GetTerrainBoundaryId(UIDocument uiDoc)
        {
            var pickedRef = uiDoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "Selecione o muro de divisa");
            return pickedRef != null ? uiDoc.Document.GetElement(pickedRef.ElementId).Id : ElementId.InvalidElementId;
        }

        public virtual bool FilterPlanes(XYZ normal)
        {
            // Ignora planos totalmente verticais ou horizontais
            return !(Math.Abs(normal.X) == 1 || Math.Abs(normal.Y) == 1 || normal.Z == -1);
        }

        public virtual Line GetFaceHorizontalLine(Face face)
        {
            if (face == null) return null;

            var line = face
                .EdgeLoops.Cast<EdgeArray>()
                .SelectMany(ea => ea.Cast<Edge>())
                .Select(e => e.AsCurve())
                .OfType<Line>()
                .FirstOrDefault(l => l.Direction.Z == 0);

            if (line == null) return null;

            var start = line.GetEndPoint(0);
            var end = line.GetEndPoint(1);
            return Line.CreateBound(new XYZ(start.X, start.Y, 0), new XYZ(end.X, end.Y, 0));
        }
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
