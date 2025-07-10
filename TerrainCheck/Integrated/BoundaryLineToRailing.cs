using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.Integrated
{
    public class BoundaryLineToRailing
    {
        public UIDocument UIDocument { get; set; }
        public Document Document => UIDocument?.Document;
        public UIApplication UIApplication => UIDocument?.Application;

        public Element BoundaryLine { get; private set; }
        public Element ProjectionTerrain { get; private set; }

        // Result items
        public Curve[] Curves { get; private set; }
        public Curve[] FlatCurves { get; private set; }
        public Toposolid Toposolid { get; private set; }
        public Face[] ToposolidFaces { get; private set; }

        public bool ViewIs2D
        {
            get
            {
                var view = UIDocument?.ActiveView;
                return view != null &&
                       (view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan);
            }
        }

        public BoundaryLineToRailing(UIDocument uidoc)
        {
            UIDocument = uidoc;
        }

        public void Execute()
        {
            if (!ViewIs2D)
            {
                TaskDialog.Show("Erro", "A vista ativa deve ser 2D (planta ou forro).");
                return;
            }

            var selectedElements = UIDocument.Selection.GetElementIds()
                                    .Select(id => Document.GetElement(id))
                                    .ToList();

            if (selectedElements.Count != 2)
            {
                TaskDialog.Show("Erro", "Selecione exatamente dois elementos: uma linha e um terreno.");
                return;
            }

            ProjectionTerrain = selectedElements.First(e => e is Toposolid);
            BoundaryLine = selectedElements.First(e => e != ProjectionTerrain);

            if (BoundaryLine == null || ProjectionTerrain == null)
            {
                TaskDialog.Show("Erro", "Certifique-se de selecionar uma linha (LocationCurve) e um terreno (Topografia).");
                return;
            }

            CreateWalls(); 
        }


        // Tamnho das divisoes
        // Altura variavel
        // Espessura variavel
        private void CreateWalls(int subdivisions = 100)
        {
            var geometry = BoundaryLine.get_Geometry(new Options());
            var lines = geometry.OfType<Curve>().ToArray();

            var points = utils.XYZUtils.DivideCurvesEvenly(lines, subdivisions);
            if (points.Count == 0)
            {
                TaskDialog.Show("Erro", "A linha selecionada não possui pontos suficientes para criar uma barreira.");
                return;
            }

            var toposolidId = ProjectionTerrain.Id;
            if (toposolidId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("Erro", "O terreno selecionado não é um Toposolid válido.");
                return;
            }

            var toposolidFaces = FilterTopoFaces(Document, toposolidId, out Toposolid toposolid);
            Toposolid = toposolid ?? throw new InvalidOperationException("Não foi possível obter o Toposolid.");
            ToposolidFaces = toposolidFaces ?? throw new InvalidOperationException("Não foi possível obter as faces do Toposolid.");

            var projectedPoints = new List<XYZ>();

            foreach (var point in points)
            {
                var projected = ProjectPointOntoTopography(toposolidFaces, point);
                if (projected == null)
                    continue;

                projectedPoints.Add(projected);
            }

            var curves = ConnectPoints(projectedPoints.ToArray());
            if (curves == null || curves.Length == 0)
            {
                TaskDialog.Show("Erro", "Não foi possível conectar os pontos projetados.");
                return;
            }

            CreateExtrudedWallFromCurves(curves);

            TaskDialog.Show("Sucesso", "Guarda-corpo criado com sucesso!");
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

            Curves = curves.ToArray();

            // Curves with Z == 0 for flat representation
            FlatCurves = curves
                .Select(c => Line.CreateBound(new XYZ(c.GetEndPoint(0).X, c.GetEndPoint(0).Y, 0),
                                              new XYZ(c.GetEndPoint(1).X, c.GetEndPoint(1).Y, 0)))
                .ToArray();

            return curves.ToArray();
        }

        private XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
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

        private Face[] FilterTopoFaces(Document doc, ElementId toposolidId, out Toposolid toposolid)
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

        private bool FilterPlanes(XYZ normal)
        {
            return !(Math.Abs(normal.X) == 1 || Math.Abs(normal.Y) == 1 || normal.Z == -1);
        }
    }
}
