using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.CommandFunctions;
using GvcRevitPlugins.TerrainCheck.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck
{
    /// <summary>
    /// Resultado da projeção de um ponto em uma face do terreno.
    /// </summary>
    public class ProjectionResult
    {
        /// <summary>
        /// Face onde o ponto foi projetado.
        /// </summary>
        public Face Face { get; set; }

        /// <summary>
        /// Ponto na divisa projetado em altura zero (plano horizontal).
        /// </summary>
        public XYZ FlatPoint { get; set; }

        /// <summary>
        /// Ponto na divisa projetado no terreno de referencia (altura do terreno).
        /// </summary>
        public XYZ ProjectedPoint { get; set; }

        public ProjectionResult(Face face, XYZ flatPoint, XYZ projectedPoint)
        {
            Face = face;
            FlatPoint = flatPoint;
            ProjectedPoint = projectedPoint;
        }
    }

    /// <summary>
    /// Resultado da projeção de uma linha inclinada sobre o terreno, incluindo o ponto projetado e a altura da parede.
    /// </summary>
    public class WallResult_
    {
        /// <summary>
        /// Localização do ponto projetado sobre o terreno.
        /// </summary>
        public XYZ resultPoint { get; set; }

        public XYZ PlatoHeightPoint { get; set; }

        /// <summary>
        /// Altura da parede a ser criada a partir do ponto projetado.
        /// </summary>  
        public double wallHeight { get; set; }

        /// <summary>
        /// O offset total aplicado ao ponto projetado para determinar a altura da parede.
        /// </summary>
        public double totalOffset { get; set; }

        /// <summary>
        /// Localização da parade na vista
        /// </summary>
        public Curve wallCurve { get; set; }

        public WallResult_(XYZ point, double wallHeight, double totalOffset)
        {
            this.resultPoint = point;
            this.wallHeight = wallHeight;
            this.totalOffset = totalOffset;
        }
    }

    public class ProjectFaces
    {
        Document Document { get; set; }
        UIDocument Uid { get; set; }
        Element Element { get; set; }
        Curve[] Lines { get; set; }
        Face[] Faces { get; set; }
        List<XYZ> LinesSubdivions { get; set; } = new List<XYZ>();
        Face[] TerrainFaces { get; set; }
        List<LineResult> LineResults { get; set; }
        public XYZ[] ProjectedPoints { get; set; }
        public List<ProjectionResult> results = new List<ProjectionResult>();

        public ProjectFaces(UIDocument Uidocument, ElementId elementid, Curve[] lines, List<LineResult> LineResults_, double subdivision, double baseElevation)
        {
            Document = Uidocument.Document;
            Uid = Uidocument;
            Element = Document.GetElement(elementid);
            Lines = lines;
            LineResults = LineResults_;

            Toposolid solid = new FilteredElementCollector(Document)
                .OfClass(typeof(Toposolid))
                .Cast<Toposolid>().First();

            //TerrainFaces = utils.XYZUtils.FilterTopoFaces(Document, solid.Id, out _);
            TerrainFaces = utils.XYZUtils.FilterTopoFaces(Document, null, out _);

            if (TerrainCheckApp._thisApp.Store.IntersectionGeometricObject == null)
            {
                //Faces = GetElementFaces();
                Faces = InternalUtils.GetElementFaces(Element, Document);
                if (Faces == null || Faces.Length == 0)
                {
                    TaskDialog.Show("Erro", "Nenhuma face válida encontrada no elemento selecionado.");
                    return;
                }
            }
            else
            {
                //CreateDummyFaces();
                Faces = InternalUtils.CreateDummyFaces();
                if (Faces == null || Faces.Length == 0)
                {
                    TaskDialog.Show("Erro", "Nenhuma face válida encontrada no objeto de interseção.");
                    return;
                }
            }

            if (TerrainFaces == null || TerrainFaces.Length == 0)
            {
                TaskDialog.Show("Erro", "Nenhuma face de terreno válida encontrada.");
                return;
            }

            Execute();
        }

        private void Execute()
        {
            List<WallResult_> wallResults = new List<WallResult_>();
            HashSet<string> errorTypes = new HashSet<string>();

            foreach (LineResult lineResult in LineResults)
            {
                Element element = lineResult.Element;
                string lineId = lineResult.line?.Id.ToString() ?? "(ID desconhecido)";
                double lineLength = lineResult.line?.Length ?? 0;

                List<XYZ> subdivisions = utils.XYZUtils.DivideCurvesEvenly(
                    new List<Line> { lineResult.line },
                    TerrainCheckApp._thisApp.Store.SubdivisionLevel
                );

                if (subdivisions == null || subdivisions.Count == 0)
                {
                    errorTypes.Add(
                        "Algumas linhas não puderam ser subdivididas.\n" +
                        "- Possíveis causas: linha muito curta, inválida ou subdivisão muito densa.\n" +
                        "- Verifique se as linhas estão em um plano válido e se o nível de subdivisão não é exagerado."
                    );
                    continue;
                }

                LinesSubdivions.AddRange(subdivisions);

                //ProjectionResult[] projectedPoints = ProjectLinesToFaces(subdivisions);
                ProjectionResult[] projectedPoints = InternalUtils.ProjectLinesToFaces(subdivisions, Faces, TerrainFaces, Element);
                if (projectedPoints == null || projectedPoints.Length == 0)
                {
                    errorTypes.Add(
                        "Algumas linhas não puderam ser projetadas sobre o terreno.\n" +
                        "- Possíveis causas: superfície de terreno não encontrada, faces inacessíveis ou pontos fora da área do terreno.\n" +
                        "- Sugestão: verifique se existe topografia no modelo e se as linhas estão dentro da área do terreno."
                    );
                    continue;
                }

                WallResult_[] slopePoints = SlopePoints(
                    projectedPoints,
                    element,
                    TerrainCheckApp._thisApp.Store.PlatformElevation,
                    true
                );
                if (slopePoints == null || slopePoints.Length == 0)
                {
                    errorTypes.Add(
                        "Algumas linhas não tiveram seus pontos de declive calculados.\n" +
                        "- Possíveis causas: projeções em níveis inválidos ou falha no cálculo de declividade.\n" +
                        "- Sugestão: verifique se a elevação de plataforma está correta e se as linhas não estão totalmente planas."
                    );
                    continue;
                }

                wallResults.AddRange(slopePoints);
            }

            if (wallResults.Count == 0)
            {
                string allErrors = errorTypes.Count > 0
                    ? string.Join("\n\n", errorTypes)
                    : "Nenhuma linha pôde ser processada. Verifique se há linhas válidas no modelo.";
                TaskDialog.Show("Falha no Processamento", allErrors);
                return;
            }

            if (errorTypes.Count > 0)
            {
                TaskDialog.Show(
                    "Aviso - Processamento Parcial",
                    string.Join("\n\n", errorTypes)
                );
            }

            List<WallResult_> connectedSlopePoints = InternalUtils.ConnectPoints(wallResults.ToArray()).ToList();
            if (connectedSlopePoints == null || connectedSlopePoints.Count == 0)
            {
                TaskDialog.Show(
                    "Aviso",
                    "Nenhum ponto conectado foi gerado a partir das linhas processadas. Verifique se as linhas estão contínuas e próximas entre si."
                );
                return;
            }

            using (Transaction transaction = new Transaction(Document, "Draw Line"))
            {
                transaction.Start();
                var segments = InternalUtils.ConnectSegments(connectedSlopePoints, 5);
                List<ElementId> results = new List<ElementId>();
                foreach (var segment in segments)
                {
                    var worstLines = InternalUtils.BuildWorstLines(segment.ToList(), Document, 0);
                    if (worstLines != null)
                    {
                        foreach (var line in worstLines)
                        {
                            utils.Draw._Wall(
                                out ElementId id,
                                Uid,
                                line,
                                TerrainCheck.TerrainCheckApp._thisApp.Store.BoundarySelectionType == "Arrimo" ? "Arrimo" : "Talude",
                                new Color(255, 0, 0),
                                0.01,
                                3
                            );
                            results.Add(id);
                        }
                    }
                }

                var isolate = results;
                isolate.Add(TerrainCheckApp._thisApp.Store.Element.Id);
                isolate.AddRange(TerrainCheckApp._thisApp.Store.TerrainBoundaryIds);
                Document.ActiveView.IsolateElementsTemporary(isolate);

                transaction.Commit();
            }
        }

        private WallResult_[] SlopePoints(IEnumerable<ProjectionResult> projections, Element reference, double baseElevation, bool project = true)
        {
            List<WallResult_> resultPoints = new();
            List<XYZ> unprojectedPoints = new();
            int totalPoints = projections.Count();

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            int currentIndex = 0;

            foreach (ProjectionResult projectionResult in projections)
            {
                try
                {
                    WallResult_ slopePoint = ProcessProjection(projectionResult, reference, baseElevation, project, unprojectedPoints);
                    if (slopePoint != null)
                        resultPoints.Add(slopePoint);
                }
                catch
                {
                    continue;
                }

                currentIndex++;
                UpdateProgress(progressWindow, currentIndex, totalPoints);
            }

            progressWindow.Close();

            HandleUnprojectedPoints(unprojectedPoints, project);
            return resultPoints.ToArray();
        }

        private WallResult_ ProcessProjection(ProjectionResult projectionResult, Element reference, double baseElevation, bool project, List<XYZ> unprojectedPoints)
        {
            Face face = projectionResult.Face;
            XYZ projectedPoint = projectionResult.ProjectedPoint;
            XYZ flatPoint = projectionResult.FlatPoint;

            Line baseFlatLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
            Line baseLine = utils.XYZUtils.GetLongestHorizontalEdge(face, false);
            XYZ facePoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, face.Triangulate().Vertices.First());
            XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
            if (normal == null) return null;

            Line ray = Line.CreateUnbound(flatPoint, normal);
            SetComparisonResult intersectionResult = baseFlatLine.Intersect(ray, out IntersectionResultArray intersectionArray);
            if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                return null;

            XYZ intersection = intersectionArray.get_Item(0).XYZPoint;
            XYZ transformedIntersection = new XYZ(intersection.X, intersection.Y, baseLine.GetEndPoint(0).Z);

            double wallHeight = UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Feet);
            double totalOffset = CalculateOffset(face, facePoint, projectedPoint, reference, baseElevation, transformedIntersection);

            XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);
            XYZ finalPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));

            //utils.Draw._XYZ(Document, finalPoint, 0.2, new Color(0, 0, 255));

            if (project)
                finalPoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, movedPoint);

            WallResult_ slopeResult = new WallResult_(finalPoint, wallHeight, totalOffset);
            slopeResult.PlatoHeightPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));

            if (slopeResult.resultPoint == null || project == false)
            {
                movedPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));
                unprojectedPoints.Add(movedPoint);
                return null;
            }

            return slopeResult;
        }

        private double CalculateOffset(Face face, XYZ facePoint, XYZ projectedPoint, Element reference, double baseElevation, XYZ transformedIntersection)
        {
            string boundaryType = TerrainCheckApp._thisApp.Store.BoundarySelectionType;
            double height = Math.Abs(projectedPoint.Z - baseElevation);
            double heightMeters = UnitUtils.ConvertToInternalUnits(height, UnitTypeId.Meters);

            double slopeAngle = utils.XYZUtils.GetFaceSlopeAngle(face);
            slopeAngle = Math.Abs(slopeAngle);

            if (boundaryType == "Arrimo")
            {
                ElementType type = reference.Document.GetElement(reference.GetTypeId()) as ElementType;
                double arrimoHeight_m = 0;

                if (type != null)
                {
                    Parameter heightParam = type.LookupParameter("Altura Arrimo");
                    if (heightParam != null && heightParam.HasValue)
                    {
                        double arrimoHeight_ft = heightParam.AsDouble();
                        arrimoHeight_m = UnitUtils.ConvertFromInternalUnits(arrimoHeight_ft, UnitTypeId.Meters);
                    }
                }

                double offset_m = arrimoHeight_m - 1.0;

                if (offset_m < 1.5)
                    offset_m = 1.5;

                double offset_ft = UnitUtils.ConvertToInternalUnits(offset_m, UnitTypeId.Meters);

                return offset_ft;
            }

            double offsetMeters;

            if (heightMeters <= 3.0)
            {
                offsetMeters = 1.5;
            }
            else
            {
                if (slopeAngle <= 45.0)
                    offsetMeters = heightMeters / 2.0;
                else
                    offsetMeters = (2.0 * heightMeters) / 3.0;
            }

            if (heightMeters > 6.0)
                offsetMeters += 1.0;

            return UnitUtils.ConvertToInternalUnits(offsetMeters, UnitTypeId.Feet);
        }

        private void UpdateProgress(ProgressWindow progressWindow, int currentIndex, int totalPoints)
        {
            double percentage = (double)currentIndex / totalPoints * 100;
            progressWindow.UpdateProgress(percentage, $"Processando pontos: {currentIndex}/{totalPoints}");
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(delegate { })
            );
        }

        private void HandleUnprojectedPoints(List<XYZ> unprojectedPoints, bool project)
        {
            if (unprojectedPoints.Count == 0 || !project) return;

            TaskDialogResult result = TaskDialog.Show(
                "Pontos Fora do Sólido",
                $"Foram encontrados {unprojectedPoints.Count} ponto(s) fora da topografia.\nDeseja desenhá-los no modelo?",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

            if (result == TaskDialogResult.Yes)
            {
                foreach (XYZ point in unprojectedPoints)
                    utils.Draw._XYZ(Document, point, 0.5, new Color(255, 165, 0));
            }
        }
    }

}
