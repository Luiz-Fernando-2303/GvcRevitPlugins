using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.TerrainCheck
{
    public class LineResult 
    {
        public Line line { get; set; }
        public Element Element { get; set; }
    }

    public class SelectionToLines
    {
        public Curve[]          Lines { get; set; }
        public List<LineResult> LineResults { get; set; } = new();
        IEnumerable<ElementId>  ElementIds { get; set; }
        IEnumerable<Element>    Elements { get; set; }
        Document                Document_ { get; set; }

        public SelectionToLines(IEnumerable<ElementId> elementIds, Document document)
        {
            Document_   = document;
            ElementIds  = elementIds;
            Lines       = GetLinesFromSelection();
        }

        private Curve[] GetLinesFromSelection()
        {
            List<Element> elements = ElementIds.Select(id => Document_.GetElement(id)).ToList();
            List<Curve> horizontalLines = new List<Curve>();

            Elements = elements;

            foreach (var element in elements)
            {
                if (element == null) continue;

                // Corrimão
                if (element is Railing railing)
                {
                    var path = GetRailingPath(railing);
                    if (path != null && path.Length > 0)
                    {
                        horizontalLines.AddRange(path);
                        LineResults.AddRange(path.Select(line => new LineResult { line = (Line)line, Element = element }));
                    }
                    continue;
                }

                // Parede
                if (element is Wall wall && wall.Location is LocationCurve wallCurve)
                {
                    var curve = wallCurve.Curve;
                    if (curve != null)
                    {
                        var projectedLine = ProjectCurveToZ0(curve);
                        if (projectedLine != null)
                        {
                            horizontalLines.Add(projectedLine);
                            LineResults.Add(new LineResult { line = projectedLine, Element = element });
                        }
                    }
                    continue;
                }

                // Qualquer elemento com LocationCurve (inclusive estrutural)
                if (element.Location is LocationCurve genericCurve)
                {
                    var curve = genericCurve.Curve;
                    if (curve != null)
                    {
                        var projectedLine = ProjectCurveToZ0(curve);
                        if (projectedLine != null)
                        {
                            horizontalLines.Add(projectedLine);
                            LineResults.Add(new LineResult { line = projectedLine, Element = element });
                        }
                    }
                }

                // Geometria genérica
                var options = new Options { View = Document_.ActiveView, IncludeNonVisibleObjects = true };
                var geomElement = element.get_Geometry(options);
                if (geomElement == null) continue;

                foreach (var geoObj in geomElement)
                {
                    if (geoObj is GeometryInstance geoInstance)
                    {
                        foreach (var instanceObj in geoInstance.GetInstanceGeometry())
                        {
                            if (instanceObj is Solid solid && solid.Faces.Size > 0)
                            {
                                var line = GetLineFromGeometry(solid);
                                if (line != null)
                                {
                                    horizontalLines.Add(line);
                                    LineResults.Add(new LineResult { line = line, Element = element });
                                }
                                else if (instanceObj is Face face)
                                {
                                    var faceLine = GetLineFromFace(face);
                                    if (faceLine != null)
                                    {
                                        horizontalLines.Add(faceLine);
                                        LineResults.Add(new LineResult { line = faceLine, Element = element });
                                    }
                                }
                                else if (instanceObj is Line rawLine)
                                {
                                    var projectedLine = ProjectCurveToZ0(rawLine);
                                    if (projectedLine != null)
                                    {
                                        horizontalLines.Add(projectedLine);
                                        LineResults.Add(new LineResult { line = projectedLine, Element = element });
                                    }
                                }
                            } else if (instanceObj is Curve curve)
                            {
                                var line = ProjectCurveToZ0(curve);
                                if (line != null)
                                {
                                    horizontalLines.Add(line);
                                    LineResults.Add(new LineResult { line = line, Element = element });
                                }
                            }
                        }
                    }
                }
            }

            if (horizontalLines.Count == 0)
                return null;

            return horizontalLines.Select(line => ProjectCurveToZ0(line)).ToArray();
        }

        private Line GetLineFromFace(Face face)
        {
            if (face == null) return null;

            var mesh = face.Triangulate();
            if (mesh == null || mesh.Vertices.Count < 2) return null;

            var vertices = mesh.Vertices.Cast<XYZ>().ToList();
            var center = new XYZ(
                vertices.Average(v => v.X),
                vertices.Average(v => v.Y),
                vertices.Average(v => v.Z)
            );

            double minX = vertices.Min(v => v.X);
            double maxX = vertices.Max(v => v.X);

            var start = new XYZ(minX, center.Y, 0);
            var end = new XYZ(maxX, center.Y, 0);

            return Line.CreateBound(start, end);
        }

        private Curve[] GetRailingPath(Railing railing)
        {
            return railing.GetPath()?.Select(line => ProjectCurveToZ0(line)).ToArray() ?? Array.Empty<Curve>();
        }

        private Line ProjectCurveToZ0(Curve curve)
        {
            try
            {
                var p0 = curve.GetEndPoint(0);
                var p1 = curve.GetEndPoint(1);

                return Line.CreateBound(
                    new XYZ(p0.X, p0.Y, 0),
                    new XYZ(p1.X, p1.Y, 0)
                );
            }
            catch
            {
                try
                {
                    Line l = curve as Line;
                    XYZ origin = l.Origin;
                    XYZ direction = l.Direction;
                    double length = curve.ApproximateLength; // ou curve.Length se Curve for Line ou Arc

                    XYZ p0 = new XYZ(origin.X, origin.Y, 0);
                    XYZ p1 = new XYZ(origin.X + direction.X * length, origin.Y + direction.Y * length, 0);

                    return Line.CreateBound(p0, p1);
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        private Line GetLineFromGeometry(Solid solid)
        {
            const double MinRevitLineLength = 0.0025602645572916664;

            if (solid == null) return null;

            List<XYZ> allVertices = new();
            foreach (Face face in solid.Faces)
            {
                Mesh mesh = face.Triangulate();
                if (mesh != null)
                {
                    foreach (XYZ v in mesh.Vertices)
                    {
                        allVertices.Add(v);
                    }
                }
            }

            if (allVertices.Count == 0) return null;

            var center = new XYZ(
                allVertices.Average(v => v.X),
                allVertices.Average(v => v.Y),
                allVertices.Average(v => v.Z)
            );

            double minX = allVertices.Min(v => v.X);
            double maxX = allVertices.Max(v => v.X);

            var start = new XYZ(minX, center.Y, 0);
            var end = new XYZ(maxX, center.Y, 0);

            if (start.DistanceTo(end) < MinRevitLineLength)
                return null;

            return Line.CreateBound(start, end);
        }
    }
}
