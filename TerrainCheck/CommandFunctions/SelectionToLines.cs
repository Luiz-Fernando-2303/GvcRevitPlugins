using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace GvcRevitPlugins.TerrainCheck
{
    public class SelectionToLines
    {
        public Curve[] Lines { get; set; }
        IEnumerable<ElementId> ElementIds { get; set; }
        IEnumerable<Element> Elements { get; set; }
        Document Document_ { get; set; }

        public SelectionToLines(IEnumerable<ElementId> elementIds, Document document)
        {
            Document_ = document;
            ElementIds = elementIds;
            Lines = GetLinesFromSelection();
            
            //Draw._Curve(document, Lines); // Debug drawing lines
        }

        private Curve[] GetLinesFromSelection()
        {
            List<Element> elements = ElementIds.Select(id => Document_.GetElement(id)).ToList();
            List<Curve> horizontalLines = new List<Curve>();

            Elements = elements;

            foreach (var element in elements)
            {
                if (element == null) continue;

                // Caso 1: Corrimão (Railing)
                if (element is Railing railing)
                {
                    var path = GetRailingPath(railing);
                    if (path != null && path.Length > 0)
                    {
                        horizontalLines.AddRange(path);
                    }
                    continue;
                }

                // Obtenha a geometria do elemento
                Options options = new Options();
                options.View = Document_.ActiveView;
                options.IncludeNonVisibleObjects = true;

                GeometryElement geomElement = element.get_Geometry(options);
                if (geomElement == null) continue;

                foreach (GeometryObject geoObj in geomElement)
                {
                    // Caso 2: Paredes (Wall) e outros com geometria sólida
                    if (geoObj is Solid solid && solid.Faces.Size > 0)
                    {
                        var line = GetLineFromGeometry(solid);
                        if (line != null) horizontalLines.Add(line);
                    }
                    // Caso 3: Faces isoladas
                    else if (geoObj is Face face)
                    {
                        var faceLine = GetLineFromFace(face);
                        if (faceLine != null) horizontalLines.Add(faceLine);
                    }
                    // Caso 4: Instâncias aninhadas
                    else if (geoObj is GeometryInstance geoInstance)
                    {
                        foreach (GeometryObject instanceObj in geoInstance.GetInstanceGeometry())
                        {
                            if (instanceObj is Solid instSolid && instSolid.Faces.Size > 0)
                            {
                                var line = GetLineFromGeometry(instSolid);
                                if (line != null) horizontalLines.Add(line);
                            }
                            else if (instanceObj is Face instFace)
                            {
                                var faceLine = GetLineFromFace(instFace);
                                if (faceLine != null) horizontalLines.Add(faceLine);
                            }
                            if (instanceObj is Line instline)
                            {
                                var line = ProjectCurveToZ0(instline);
                                if (line != null) horizontalLines.Add(line);
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
            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, 0),
                new XYZ(p1.X, p1.Y, 0)
            );
        }

        private Line GetLineFromGeometry(Solid solid)
        {
            if (solid == null) return null;

            List<XYZ> allVertices = new();
            foreach (Face f in solid.Faces)
            {
                Mesh mesh = f.Triangulate();
                if (mesh != null && mesh.Vertices != null)
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

            return Line.CreateBound(start, end);

        }
    }
}
