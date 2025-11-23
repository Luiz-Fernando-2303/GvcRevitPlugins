using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.CommandFunctions;
using GvcRevitPlugins.TerrainCheck.UI;
using MaterialDesignThemes.Wpf;
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
        public bool IsFaceHigher { get; set; }
        public double HeightDifference_ { get; set; }
        public double DistanceToCenter { get; set; }

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

            HeightDifference_ = HeightDifference();
            IsFaceHigher = HeightDifference_ < 0;
        }

        public double HeightDifference()
        {
            XYZ[] faceVertices = Face.Triangulate().Vertices.Cast<XYZ>().ToArray();
            XYZ lowerVertice = faceVertices.OrderBy(v => v.Z).First();

            return ProjectedPoint.Z - lowerVertice.Z;
        }

        public void Draw(Document doc)
        {
            utils.Draw._Face(doc, Face);
            utils.Draw._XYZ(doc, FlatPoint, color: new Color(255, 0, 0));
            utils.Draw._XYZ(doc, ProjectedPoint, color: new Color(0, 255, 0));
        }
    }

    /// <summary>
    /// Resultado da projeção de uma linha inclinada sobre o terreno, incluindo o ponto projetado e a altura da parede.
    /// </summary>
    public class SlopeResult
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

        public double HeightDifference { get; set; }
        public double DistanceToCenter { get; set; }
        public Face Face { get; set; }

        public SlopeResult(XYZ point, double wallHeight, double totalOffset)
        {
            this.resultPoint = point;
            this.wallHeight = wallHeight;
            this.totalOffset = totalOffset;
        }

        public void Draw(Document doc, bool face = false)
        {
            utils.Draw._XYZ(doc, resultPoint, 0.3, new Color(0, 255, 0));
            utils.Draw._XYZ(doc, PlatoHeightPoint, 0.2, new Color(0, 0, 255));

            if (face) utils.Draw._Face(doc, Face);
        }
    }

    public class SlopeColection : IEnumerable<SlopeResult>
    {
        private List<SlopeResult> slopeResults = new List<SlopeResult>();

        public SlopeColection(IEnumerable<SlopeResult> collection)
        {
            slopeResults.AddRange(collection);
        }

        public void Add(SlopeResult slopeResult)
        {
            slopeResults.Add(slopeResult);
        }
        public void AddRange(IEnumerable<SlopeResult> slopeResults)
        {
            this.slopeResults.AddRange(slopeResults);
        }
        public IEnumerator<SlopeResult> GetEnumerator()
        {
            return slopeResults.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return slopeResults.GetEnumerator();
        }

        public List<Curve> GetCurves(bool nivelated = true)
        {
            double mediumHeight = slopeResults.Average(sr => sr.resultPoint.Z);
            List<Curve> curves = new List<Curve>();

            SlopeResult current = null;
            for (int i = 0; i < slopeResults.Count; i++)
            {
                SlopeResult next = (i < slopeResults.Count - 1) ? slopeResults[i + 1] : null;
                if (current == null)
                {
                    current = slopeResults[i];
                    continue;
                }
                XYZ startPoint = current.resultPoint;
                XYZ endPoint = next != null ? next.resultPoint : current.resultPoint;
                if (nivelated)
                {
                    startPoint = new XYZ(startPoint.X, startPoint.Y, mediumHeight);
                    endPoint = new XYZ(endPoint.X, endPoint.Y, mediumHeight);
                }

                try
                {
                    Line line = Line.CreateBound(startPoint, endPoint);
                    curves.Add(line);
                    current = next;
                } catch(Exception e)
                {
                    continue;
                }
            }

            return curves;
        }

        public List<ElementId> DrawWalls(UIDocument uIDocument, Color wallColor, out List<Curve> ResultLocations, string boundaryType = "", double thicknes = 0.01, double height = 3, bool overrideHeight = true, bool nivalated = true)
        {
            List<Curve> locations = GetCurves(nivalated);
            ResultLocations = locations;
            List<ElementId> ids = new List<ElementId>();

            foreach (Curve location in locations)
            {
                utils.Draw._Wall(
                    out ElementId wallId,
                    uIDocument,
                    location as Line,
                    boundaryType,
                    wallColor,
                    thicknes,
                    height,
                    overrideHeight
                );

                ids.Add(wallId);
            }

            return ids;
        }
    }

    public class WallSegmentResult
    {
        public SlopeResult CenterPoint { get; set; }
        public XYZ StartPoint { get; set; }
        public XYZ EndPoint { get; set; }
        public XYZ Direction { get; set; }
        public SlopeResult[] Points { get; set; }

        public Line GetLine()
        {
            return Line.CreateBound(StartPoint, EndPoint);
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
            List<SlopeResult> wallResults = new List<SlopeResult>();
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

                ProjectionResult[] projectedPoints =
                    InternalUtils.ProjectLinesToFaces(subdivisions, Faces, TerrainFaces, Element);
                //ProjectionResult[] projectedPoints =
                //    InternalUtils.ProjectFacesToLines(LineResults, Faces, TerrainFaces, Element);

                XYZ center = new XYZ(
                    projectedPoints.Average(p => p.ProjectedPoint.X),
                    projectedPoints.Average(p => p.ProjectedPoint.Y),
                    projectedPoints.Average(p => p.ProjectedPoint.Z)
                );

                double[] distances = InternalUtils.GetDistancesAlongNormal(projectedPoints.ToList(), center).ToArray();
                for (int i = 0; i < projectedPoints.Length; i++)
                    projectedPoints[i].DistanceToCenter = distances[i];


                if (projectedPoints == null || projectedPoints.Length == 0)
                {
                    errorTypes.Add(
                        "Algumas linhas não puderam ser projetadas sobre o terreno.\n" +
                        "- Possíveis causas: superfície de terreno não encontrada, faces inacessíveis ou pontos fora da área do terreno.\n" +
                        "- Sugestão: verifique se existe topografia no modelo e se as linhas estão dentro da área do terreno."
                    );
                    continue;
                }

                SlopeResult[] slopePoints = SlopePoints(
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

            List<ElementId> createdElementIds = new List<ElementId>();
            List<Curve> resultLocations = new List<Curve>();
            XYZ[] locations = new XYZ[0];
            double[] offSets = new double[0];
            BoundingBoxXYZ bbox = null;
            SlopeResult worstResult = null;

            using (Transaction t1 = new Transaction(Document, "Create walls and create crop box"))
            {
                t1.Start();

                SlopeColection slopeCollection = new SlopeColection(wallResults);
                var walls = slopeCollection.DrawWalls(
                    Uid,
                    new Color(150, 75, 0),
                    out List<Curve> curves,
                    TerrainCheckApp._thisApp.Store.BoundarySelectionType == "Arrimo" ? "Arrimo" : "Talude"
                );
                createdElementIds.AddRange(walls);
                resultLocations.AddRange(curves);

                double[] rawOffsets = wallResults.Select(wr => wr.totalOffset).ToArray();

                XYZ[] rawLocations = wallResults.Select(wr =>
                    utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, wr.Face.Triangulate().Vertices.First())
                ).ToArray();

                List<(XYZ p, double r)> filtered = new List<(XYZ p, double r)>();
                double minDist = 0.15;

                for (int i = 0; i < rawLocations.Length; i++)
                {
                    XYZ p = rawLocations[i];
                    bool tooClose = filtered.Any(f => f.p.DistanceTo(p) < minDist);
                    if (!tooClose)
                        filtered.Add((p, rawOffsets[i]));
                }

                locations = filtered.Select(f => f.p).ToArray();
                offSets = filtered.Select(f => f.r).ToArray();

                List<ElementId> elementsToInclude = new List<ElementId>(createdElementIds);

                if (TerrainCheckApp._thisApp?.Store?.Element != null)
                    elementsToInclude.Add(TerrainCheckApp._thisApp.Store.Element.Id);

                if (TerrainCheckApp._thisApp?.Store?.TerrainBoundaryIds != null)
                    elementsToInclude.AddRange(TerrainCheckApp._thisApp.Store.TerrainBoundaryIds);

                bbox = null;

                foreach (var id in elementsToInclude)
                {
                    Element e = Document.GetElement(id);
                    if (e == null) continue;

                    BoundingBoxXYZ eBox = e.get_BoundingBox(null);
                    if (eBox == null) continue;

                    if (bbox == null)
                        bbox = new BoundingBoxXYZ { Min = eBox.Min, Max = eBox.Max };
                    else
                    {
                        bbox.Min = new XYZ(
                            Math.Min(bbox.Min.X, eBox.Min.X),
                            Math.Min(bbox.Min.Y, eBox.Min.Y),
                            Math.Min(bbox.Min.Z, eBox.Min.Z)
                        );
                        bbox.Max = new XYZ(
                            Math.Max(bbox.Max.X, eBox.Max.X),
                            Math.Max(bbox.Max.Y, eBox.Max.Y),
                            Math.Max(bbox.Max.Z, eBox.Max.Z)
                        );
                    }
                }

                if (bbox != null)
                {
                    double pad = Math.Max(
                        Math.Max(bbox.Max.X - bbox.Min.X, bbox.Max.Y - bbox.Min.Y),
                        bbox.Max.Z - bbox.Min.Z
                    ) * 0.1;

                    bbox.Min -= new XYZ(pad, pad, pad);
                    bbox.Max += new XYZ(pad, pad, pad);
                }

                ViewFamilyType vft =
                    new FilteredElementCollector(Document)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                View3D newView = View3D.CreateIsometric(Document, vft.Id);
                newView.Name = $"Vista_{TerrainCheckApp._thisApp.Store.BoundarySelectionType} " + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (bbox != null)
                {
                    BoundingBoxXYZ section = new BoundingBoxXYZ();
                    section.Min = bbox.Min;
                    section.Max = bbox.Max;
                    section.Transform = Transform.Identity;

                    newView.SetSectionBox(section);
                }

                t1.Commit();
            }

            using (Transaction t2 = new Transaction(Document, "Create top view and detail curves"))
            {
                t2.Start();

                if (bbox == null)
                {
                    TaskDialog.Show("Erro", "BoundingBox não encontrado.");
                    t2.Commit();
                    return;
                }

                Level level = new FilteredElementCollector(Document)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .FirstOrDefault();

                if (level == null)
                {
                    TaskDialog.Show("Erro", "Nenhum nível encontrado.");
                    t2.Commit();
                    return;
                }

                ViewFamilyType planType = new FilteredElementCollector(Document)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .First(v => v.ViewFamily == ViewFamily.FloorPlan);

                ViewPlan viewPlan = ViewPlan.Create(Document, planType.Id, level.Id);
                viewPlan.Name = $"Resultado_{TerrainCheckApp._thisApp.Store.BoundarySelectionType} " + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (viewPlan.ViewTemplateId != ElementId.InvalidElementId)
                    viewPlan.ViewTemplateId = ElementId.InvalidElementId;

                BoundingBoxXYZ crop = new BoundingBoxXYZ
                {
                    Min = bbox.Min,
                    Max = bbox.Max
                };

                viewPlan.CropBoxActive = true;
                viewPlan.CropBoxVisible = true;
                viewPlan.CropBox = crop;

                viewPlan.Discipline = ViewDiscipline.Coordination;
                viewPlan.get_Parameter(BuiltInParameter.VIEW_DETAIL_LEVEL)?.Set((int)ViewDetailLevel.Fine);

                PhaseArray phases = Document.Phases;
                Phase lastPhase = phases.get_Item(phases.Size - 1);
                viewPlan.get_Parameter(BuiltInParameter.VIEW_PHASE)?.Set(lastPhase.Id);
                viewPlan.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.Set(ElementId.InvalidElementId);

                Categories cats = Document.Settings.Categories;
                foreach (Category cat in cats)
                {
                    try
                    {
                        if (cat != null && cat.get_AllowsVisibilityControl(viewPlan))
                            viewPlan.SetCategoryHidden(cat.Id, false);
                    }
                    catch { }
                }

                FilteredWorksetCollector wCollector = new FilteredWorksetCollector(Document)
                    .OfKind(WorksetKind.UserWorkset);

                foreach (Workset ws in wCollector)
                {
                    try { viewPlan.SetWorksetVisibility(ws.Id, WorksetVisibility.Visible); }
                    catch { }
                }

                PlanViewRange vr = viewPlan.GetViewRange();
                double top = 100;
                double bottom = -100;

                vr.SetOffset(PlanViewPlane.CutPlane, 0);
                vr.SetOffset(PlanViewPlane.TopClipPlane, top);
                vr.SetOffset(PlanViewPlane.BottomClipPlane, bottom);
                vr.SetOffset(PlanViewPlane.ViewDepthPlane, bottom);
                viewPlan.SetViewRange(vr);

                BoundingBoxXYZ cropZ = viewPlan.CropBox;
                cropZ.Min = new XYZ(cropZ.Min.X, cropZ.Min.Y, bottom);
                cropZ.Max = new XYZ(cropZ.Max.X, cropZ.Max.Y, top);
                viewPlan.CropBox = cropZ;

                TextNoteType textType = new FilteredElementCollector(Document)
                    .OfClass(typeof(TextNoteType))
                    .Cast<TextNoteType>()
                    .FirstOrDefault();

                XYZ X = XYZ.BasisX;
                XYZ Y = XYZ.BasisY;

                double offMin = offSets.Min();
                double offMax = offSets.Max();

                for (int i = 0; i < locations.Length; i++)
                {
                    XYZ center = locations[i];
                    double radius = offSets[i];

                    double t = (offSets[i] - offMin) / (offMax - offMin + 1e-6);
                    byte r = (byte)(255 * t);
                    byte g = (byte)(255 * (1 - t));
                    Color dynamicColor = new Color(r, g, 0);

                    OverrideGraphicSettings ogsCircle = new OverrideGraphicSettings();
                    ogsCircle.SetProjectionLineColor(dynamicColor);

                    DetailCurve dCircle =
                        Document.Create.NewDetailCurve(viewPlan,
                        Arc.Create(center, radius, 0, 2 * Math.PI, X, Y));



                    viewPlan.SetElementOverrides(dCircle.Id, ogsCircle);

                    Color blue = new Color(0, 0, 255);

                    double crossLen = Math.Max(radius * 0.05, 0.02);

                    DetailCurve cx =
                        Document.Create.NewDetailCurve(viewPlan,
                        Line.CreateBound(center + X * crossLen, center - X * crossLen));

                    OverrideGraphicSettings ogsCX = new OverrideGraphicSettings();
                    ogsCX.SetProjectionLineColor(blue);
                    viewPlan.SetElementOverrides(cx.Id, ogsCX);

                    DetailCurve cy =
                        Document.Create.NewDetailCurve(viewPlan,
                        Line.CreateBound(center + Y * crossLen, center - Y * crossLen));

                    OverrideGraphicSettings ogsCY = new OverrideGraphicSettings();
                    ogsCY.SetProjectionLineColor(blue);
                    viewPlan.SetElementOverrides(cy.Id, ogsCY);

                    if (textType != null)
                    {
                        XYZ dimPoint = center + X * (radius * 1.3);

                        TextNoteOptions opt = new TextNoteOptions(textType.Id)
                        {
                            HorizontalAlignment = HorizontalTextAlignment.Left,
                            VerticalAlignment = VerticalTextAlignment.Middle
                        };

                        string txt =
                            "⌀ " + Math.Round(radius * 2, 2) +
                            "\nRaio: " + Math.Round(radius, 2) +
                            "\nOffset: " + Math.Round(offSets[i], 2) +
                            "\nCentro:\nX=" + Math.Round(center.X, 3) +
                            "  Y=" + Math.Round(center.Y, 3) +
                            "  Z=" + Math.Round(center.Z, 3);

                        TextNote.Create(Document, viewPlan.Id, dimPoint, txt, opt);
                    }
                }

                // Draw the lines
                foreach (Curve curve in resultLocations)
                {
                    DetailCurve dCurve = Document.Create.NewDetailCurve(viewPlan, curve);
                    OverrideGraphicSettings ogsLine = new OverrideGraphicSettings();
                    ogsLine.SetProjectionLineColor(new Color(255, 0, 255));
                    viewPlan.SetElementOverrides(dCurve.Id, ogsLine);
                }

                t2.Commit();
            }
        }

        private SlopeResult[] SlopePoints(IEnumerable<ProjectionResult> projections, Element reference, double baseElevation, bool project = true)
        {
            List<SlopeResult> resultPoints = new();
            List<XYZ> unprojectedPoints = new();
            int totalPoints = projections.Count();

            ProgressWindow progressWindow = new ProgressWindow();
            progressWindow.Show();

            int currentIndex = 0;

            foreach (ProjectionResult projectionResult in projections)
            {
                try
                {
                    SlopeResult slopePoint = ProcessProjection(projectionResult, reference, baseElevation, project, unprojectedPoints);
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

        private SlopeResult ProcessProjection(ProjectionResult projectionResult, Element reference, double baseElevation, bool project, List<XYZ> unprojectedPoints)
        {
            Face face = projectionResult.Face;
            XYZ projectedPoint = projectionResult.ProjectedPoint;
            XYZ flatPoint = projectionResult.FlatPoint;

            Line baseFlatLine = utils.XYZUtils.GetLongestHorizontalEdge(face);
            Line baseLine = utils.XYZUtils.GetLongestHorizontalEdge(face, false);
            XYZ facePoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, face.Triangulate().Vertices.First());
            XYZ normal = utils.XYZUtils.FaceNormal(face, out _);
            if (normal == null) return null;;

            string boundaryType = TerrainCheckApp._thisApp.Store.BoundarySelectionType;

            Line ray = Line.CreateUnbound(flatPoint, normal);
            SetComparisonResult intersectionResult = baseFlatLine.Intersect(ray, out IntersectionResultArray intersectionArray);
            if (intersectionResult != SetComparisonResult.Overlap || intersectionArray == null || intersectionArray.IsEmpty)
                return null;

            XYZ intersection = intersectionArray.get_Item(0).XYZPoint;
            XYZ transformedIntersection = new XYZ(intersection.X, intersection.Y, baseLine.GetEndPoint(0).Z);

            double wallHeight = UnitUtils.ConvertToInternalUnits(3, UnitTypeId.Feet);
            double totalOffset = 0;

            if (projectionResult.IsFaceHigher && boundaryType != "Arrimo")
                totalOffset = UnitUtils.ConvertToInternalUnits(1.5, UnitTypeId.Meters);
            else
                totalOffset = CalculateOffset(face, facePoint, projectedPoint, reference);

            XYZ movedPoint = utils.XYZUtils.GetEndPoint(transformedIntersection, normal, totalOffset);
            XYZ finalPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));

            if (project)
                finalPoint = utils.XYZUtils.ProjectPointOntoTopography(TerrainFaces, movedPoint);

            SlopeResult slopeResult = new SlopeResult(finalPoint, wallHeight, totalOffset);
            slopeResult.PlatoHeightPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));
            slopeResult.HeightDifference = projectionResult.HeightDifference_;
            slopeResult.DistanceToCenter = projectionResult.DistanceToCenter;
            slopeResult.Face = face;

            if (slopeResult.resultPoint == null || project == false)
            {
                movedPoint = new XYZ(movedPoint.X, movedPoint.Y, UnitUtils.ConvertToInternalUnits(baseElevation, UnitTypeId.Meters));
                unprojectedPoints.Add(movedPoint);
                return null;
            }

            return slopeResult;
        }

        private double CalculateOffset(Face face, XYZ facePoint, XYZ projectedPoint, Element reference)
        {
            string boundaryType = TerrainCheckApp._thisApp.Store.BoundarySelectionType;
            double height = Math.Abs(projectedPoint.Z - facePoint.Z);
            double height_ft = UnitUtils.ConvertToInternalUnits(height, UnitTypeId.Feet);

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

            double offset_ft_;

            if (height_ft <= 3.0)
                offset_ft_ = 1.5;

            else
            {
                if (slopeAngle <= 45.0)
                    offset_ft_ = height_ft / 2.0;
                else
                    offset_ft_ = (2.0 * height_ft) / 3.0;
            }

            if (height_ft > 6.0)
                offset_ft_ += 1.0;

            return offset_ft_ - 5;
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
