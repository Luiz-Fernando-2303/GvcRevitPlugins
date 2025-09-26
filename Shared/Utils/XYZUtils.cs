using Autodesk.Revit.DB;
using GvcRevitPlugins.TerrainCheck;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace GvcRevitPlugins.Shared.Utils
{
    public static class XYZUtils
    {
        public static bool IsFacingInside(Face face, Element element)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            Mesh mesh = face.Triangulate();
            XYZ faceOrigin = new XYZ(
                mesh.Vertices.Average(v => v.X),
                mesh.Vertices.Average(v => v.Y),
                mesh.Vertices.Average(v => v.Z)
            );

            UV uv = new UV(0.5, 0.5);
            XYZ faceNormal = face.ComputeNormal(uv).Normalize();

            XYZ directionToCenter = (center - faceOrigin).Normalize();

            double dot = faceNormal.DotProduct(directionToCenter);

            return dot < 0;
        }

        public static double UpOrDown(XYZ A, XYZ B)
        {
            try
            {
                return A.Z < B.Z ? 2.5 : 1.5;

            }
            catch
            {
                return 2.5; // Valor padrão em caso de erro
            }
        }

        public static XYZ FaceNormal(Face face, out UV surfaceUV)
        {
            surfaceUV = new UV();

            BoundingBoxUV bbox = face.GetBoundingBox();
            if (bbox == null) return null;

            UV uvSample = new UV(
                (bbox.Min.U + bbox.Max.U) / 2,
                (bbox.Min.V + bbox.Max.V) / 2
            );
            surfaceUV = uvSample;

            XYZ faceNormal = face.ComputeNormal(uvSample);
            return faceNormal;
        }

        public static List<XYZ> DivideCurvesEvenly(IEnumerable<Curve> boundaryPath, double segmentLength)
        {
            if (segmentLength <= 0) return null;
            segmentLength = (segmentLength / 10) / 3;

            var curves = boundaryPath.Where(c => c.IsBound && c.Length > 0).ToList();
            double totalLength = curves.Sum(c => c.Length);
            if (totalLength <= 0) return null;

            int subdivisions = (int)Math.Floor(totalLength / segmentLength);
            if (subdivisions < 1) return new List<XYZ> { curves.First().GetEndPoint(0) };

            List<XYZ> points = new();
            double accumulatedLength = 0;
            int currentCurveIndex = 0;

            Curve currentCurve = curves[currentCurveIndex];
            double currentCurveStart = currentCurve.GetEndParameter(0);
            double currentCurveEnd = currentCurve.GetEndParameter(1);

            points.Add(currentCurve.GetEndPoint(0)); // ponto inicial

            try
            {
                for (int i = 1; i <= subdivisions; i++)
                {
                    double targetLength = segmentLength * i;

                    while (accumulatedLength + currentCurve.Length < targetLength)
                    {
                        accumulatedLength += currentCurve.Length;
                        currentCurveIndex++;

                        if (currentCurveIndex >= curves.Count)
                            return points;

                        currentCurve = curves[currentCurveIndex];
                        currentCurveStart = currentCurve.GetEndParameter(0);
                        currentCurveEnd = currentCurve.GetEndParameter(1);
                    }

                    double remaining = targetLength - accumulatedLength;
                    double fraction = remaining / currentCurve.Length;
                    double param = currentCurveStart + fraction * (currentCurveEnd - currentCurveStart);

                    XYZ pt = currentCurve.Evaluate(param, false);
                    points.Add(pt);
                }
            }
            catch
            {
                // Erros silenciosos (pode-se logar, se desejado)
            }

            return points;
        }

        internal static XYZ[] DivideEvenly(XYZ start, XYZ end, int number)
        {
            XYZ[] points = new XYZ[number + 1];
            XYZ step = (end - start) / number;

            for (int i = 0; i <= number; i++)
                points[i] = start + step * i;

            return points;
        }

        public static XYZ[] RemoveAlmostEquals(List<XYZ> points, double tolerance)
        {
            List<XYZ> result = new List<XYZ>();

            foreach (var point in points)
                if (!result.Any(p => AreAlmostEqual(p, point, tolerance)))
                    result.Add(point);

            return result.ToArray();
        }

        public static bool AreAlmostEqual(XYZ point1, XYZ point2, double tolerance) => point1.IsAlmostEqualTo(point2, tolerance);

        public static XYZ[] ReorderRefFacePoints(XYZ[] refFacePoints, XYZ[] refBoundaryPoints)
        {
            double dist1 = refFacePoints[0].DistanceTo(refBoundaryPoints[0]);
            double dist2 = refFacePoints[0].DistanceTo(refBoundaryPoints[1]);
            if (dist1 > dist2)
                return refFacePoints;
            return new XYZ[] { refFacePoints[1], refFacePoints[0] };
        }

        public static XYZ GetEndPoint(XYZ origin, XYZ direction, double length)
        {
            if (length == 0) return origin;
            XYZ normalizedDirection = direction.Normalize();
            XYZ displacement = normalizedDirection * length;
            XYZ endPoint = origin + displacement;
            return endPoint;
        }

        public static Line GetFaceHorizontalLine(Face face, bool flat = true)
        {
            if (face == null) return null;

            Mesh mesh = face.Triangulate();
            if (mesh == null) return null;

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

        public static Line GetLongestHorizontalEdge(Face face, bool flat = true)
        {
            if (face == null) return null;

            var mesh = face.Triangulate();
            if (mesh == null || mesh.Vertices.Count < 2) return null;

            var vertices = mesh.Vertices.Cast<XYZ>().Distinct(new XYZComparer()).ToList();

            double maxDistance = 0;
            Line longestLine = null;

            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    XYZ p1 = vertices[i];
                    XYZ p2 = vertices[j];

                    if (flat)
                    {
                        p1 = new XYZ(p1.X, p1.Y, 0);
                        p2 = new XYZ(p2.X, p2.Y, 0);
                    }

                    double distance = p1.DistanceTo(p2);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        longestLine = Line.CreateBound(p1, p2);
                    }
                }
            }

            return longestLine;
        }

        public static Face[] FilterTopoFaces(Document doc, ElementId toposolidId, out Toposolid toposolid)
        {
            toposolid = null;
            var element = doc.GetElement(toposolidId);
            if (element is not Toposolid ts) return null;
            toposolid = ts;

            var geometry = ts.get_Geometry(new Options());

            return geometry.OfType<Solid>()
                           .Where(s => s.Faces.Size > 0)
                           .SelectMany(s => s.Faces.Cast<Face>())
                           .Where(f => FilterPlanes(FaceNormal(f, out UV _)))
                           .ToArray();
        }

        public static bool FilterPlanes(XYZ normal)
        {
            return !(Math.Abs(normal.X) == 1 || Math.Abs(normal.Y) == 1 || normal.Z == -1);
        }

        public static XYZ ProjectPointOntoTopography(Face[] faces, XYZ point)
        {
            foreach (var face in faces)
            {
                var normal = FaceNormal(face, out UV _);
                if (!FilterPlanes(normal)) continue;

                var verticalLine = Line.CreateUnbound(new XYZ(point.X, point.Y, 0), XYZ.BasisZ);
                var result = face.Intersect(verticalLine, out IntersectionResultArray intersectionResults);
                if (result == SetComparisonResult.Overlap)
                    return intersectionResults.get_Item(0).XYZPoint;
            }

            return null;
        }

        public static Line ProjectCurveToZ0(Curve curve)
        {
            var p0 = curve.GetEndPoint(0);
            var p1 = curve.GetEndPoint(1);
            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, 0),
                new XYZ(p1.X, p1.Y, 0)
            );
        }
    }

    public static class  ElementUtils
    {
        public static IEnumerable<Material> GetElementMaterials(Document document, Element element)
        {
            var materials = new HashSet<Material>();

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElement = element.get_Geometry(options);
            if (geomElement == null) return materials;

            foreach (GeometryObject geomObj in geomElement)
            {
                if (geomObj is GeometryInstance instance)
                {
                    GeometryElement instGeom = instance.GetInstanceGeometry();
                    if (instGeom == null) continue;

                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid solid) AddSolidMaterials(document, solid, materials);
                    }
                }
                else if (geomObj is Solid solid)
                {
                    AddSolidMaterials(document, solid, materials);
                }
            }

            Parameter matParam = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)
                                 ?? element.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);

            if (matParam != null && matParam.StorageType == StorageType.ElementId)
            {
                Material paramMaterial = document.GetElement(matParam.AsElementId()) as Material;
                if (paramMaterial != null)
                    materials.Add(paramMaterial);
            }

            return materials;
        }

        private static void AddSolidMaterials(Document doc, Solid solid, HashSet<Material> materialSet)
        {
            if (solid == null || solid.Faces.Size == 0) return;

            foreach (Face face in solid.Faces)
            {
                ElementId matId = face.MaterialElementId;
                if (matId != ElementId.InvalidElementId)
                {
                    Material mat = doc.GetElement(matId) as Material;
                    if (mat != null) materialSet.Add(mat);
                }
            }
        }

        public static GeometryObject AddSolidWithColor(Document doc, Solid solid, Color color, int transparency, out Element element, bool addOnScene = false)
        {
            element = null;
            if (solid == null || solid.Faces.Size == 0) return null;

            string materialName = $"SolidColor_{color.Red}_{color.Green}_{color.Blue}_{transparency}";
            Material material = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .FirstOrDefault(m => m.Name == materialName);

            if (material == null)
            {
                material = (Material)doc.GetElement(Material.Create(doc, materialName));
                material.Color = color;
                material.Transparency = transparency;
                material.Shininess = 128;
            }

            TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
            tsb.OpenConnectedFaceSet(true);
            foreach (Face face in solid.Faces)
            {
                Mesh mesh = face.Triangulate();
                for (int i = 0; i < mesh.NumTriangles; i++)
                {
                    MeshTriangle tri = mesh.get_Triangle(i);
                    IList<XYZ> triangle = new List<XYZ>
                    {
                        tri.get_Vertex(0),
                        tri.get_Vertex(1),
                        tri.get_Vertex(2)
                    };
                    TessellatedFace tFace = new TessellatedFace(triangle, material.Id);
                    if (tsb.DoesFaceHaveEnoughLoopsAndVertices(tFace))
                    {
                        tsb.AddFace(tFace);
                    }
                }
            }

            tsb.CloseConnectedFaceSet();
            tsb.Target = TessellatedShapeBuilderTarget.AnyGeometry;
            tsb.Fallback = TessellatedShapeBuilderFallback.Mesh;
            tsb.Build();
            GeometryObject geo = tsb.GetBuildResult().GetGeometricalObjects().First();

            if (addOnScene)
            {
                DirectShape shape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                element = shape;
                shape.AppendShape(new List<GeometryObject> { geo });
                shape.Name = "Colored Solid";
                Draw.directShapes.Add(shape);
                AddParameters(new Dictionary<string, string>
                {
                    {"GvcCreatedBy", "GvcRevitPlugins" },
                    {"GvcPluginName", "TerrainCheck" },
                    {"GvcCreationDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    {"GvcColor", $"{color.Red},{color.Green},{color.Blue}" },
                    {"GvcTransparency", transparency.ToString() },
                    {"GvcMaterialId", material.Id.ToString() },
                    {"GvcMaterialName", material.Name },
                    {"GvcInternalName", "GenericSolid" },
                    {"GvcName", "Demarcador de resultado" }
                }, shape, doc);
            }

            return geo;
        }

        public static void AddParameters(Dictionary<string, string> parameters, Element element, Document document)
        {
            if (element == null || parameters == null || parameters.Count == 0)
                return;

            foreach (var kvp in parameters)
            {
                string paramName = kvp.Key;
                string paramValue = kvp.Value;

                Parameter param = element.LookupParameter(paramName);

                if (param == null)
                    continue;

                if (param.IsReadOnly)
                    continue;

                switch (param.StorageType)
                {
                    case StorageType.String:
                        param.Set(paramValue);
                        break;

                    case StorageType.Double:
                        if (double.TryParse(paramValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
                            param.Set(dVal);
                        break;

                    case StorageType.Integer:
                        if (int.TryParse(paramValue, out int iVal))
                            param.Set(iVal);
                        break;

                    case StorageType.ElementId:
                        break;

                    default:
                        break;
                }
            }
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

        public static void _XYZ(Document doc, XYZ p, double size = 0.5, Color color = null)
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
                color ??= new Color(255, 255, 255); 

                // Criar ou encontrar material
                string materialName = $"PointColor_{color.Red}_{color.Green}_{color.Blue}";
                Material material = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .FirstOrDefault(m => m.Name == materialName);

                if (material == null)
                {
                    material = (Material)doc.GetElement(Material.Create(doc, materialName));
                    material.Color = color;
                    material.Transparency = 0;
                    material.Shininess = 128;
                }

                // Criar geometria sólida
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

                // Converter a geometria sólida em tessellated shape com material
                TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
                tsb.OpenConnectedFaceSet(true);

                foreach (Face face in sphere.Faces)
                {
                    Mesh mesh = face.Triangulate();

                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        MeshTriangle tri = mesh.get_Triangle(i);
                        IList<XYZ> triangle = new List<XYZ>
                        {
                            tri.get_Vertex(0),
                            tri.get_Vertex(1),
                            tri.get_Vertex(2)
                        };

                        TessellatedFace tFace = new TessellatedFace(triangle, material.Id);
                        if (tsb.DoesFaceHaveEnoughLoopsAndVertices(tFace))
                        {
                            tsb.AddFace(tFace);
                        }
                    }
                }

                tsb.CloseConnectedFaceSet();
                tsb.Target = TessellatedShapeBuilderTarget.AnyGeometry;
                tsb.Fallback = TessellatedShapeBuilderFallback.Mesh;
                tsb.Build();

                GeometryObject geo = tsb.GetBuildResult().GetGeometricalObjects().First();

                // Criar o DirectShape com a geometria colorida
                DirectShape shape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                shape.AppendShape(new List<GeometryObject> { geo });
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
                    try
                    {
                        Execute();
                    }
                    catch { }

                    transaction.Commit();
                }

            else
            {
                try
                {
                    Execute();
                }
                catch { }
            }

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

        public static void _Face(Document doc, IEnumerable<Face> faces, Color color = null, int transparency = 0)
        {
            foreach (Face face in faces)
                _Face(doc, face, color, transparency);
        }

        public static void _Face(Document doc, Face face, Color color = null, int transparency = 0)
        {
            if (face == null) return;
            if (!doc.IsModifiable)
            {
                using (Transaction transaction = new Transaction(doc, "Draw Face"))
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
                CreateDummyFaces(doc, face, out Solid solid);
                if (solid == null) return;
                Element element;
                ElementUtils.AddSolidWithColor(doc, solid, color ?? new Color(0, 255, 0), transparency, out element, true);
            }
        }

        public static void _Line(Document doc, Line line, Color color = null, double thickness = 0.01, double height = 0.1)
        {
            if (line == null || line.Length < doc.Application.ShortCurveTolerance)
                return;

            if (!doc.IsModifiable)
            {
                using (Transaction transaction = new Transaction(doc, "Draw Line"))
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
                color ??= new Color(255, 255, 255);

                XYZ start = line.GetEndPoint(0);
                XYZ end = line.GetEndPoint(1);
                XYZ direction = (end - start).Normalize();

                XYZ up = XYZ.BasisZ;
                if (Math.Abs(direction.DotProduct(up)) > 0.999)
                    up = XYZ.BasisY;

                XYZ perp = direction.CrossProduct(up).Normalize();
                XYZ offset = perp * (thickness / 2);

                List<Curve> profile = new List<Curve>
                {
                    Line.CreateBound(start + offset, start - offset),
                    Line.CreateBound(start - offset, end - offset),
                    Line.CreateBound(end - offset, end + offset),
                    Line.CreateBound(end + offset, start + offset)
                };

                CurveLoop loop = CurveLoop.Create(profile);

                XYZ extrusionDir = up;
                Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, extrusionDir, height);

                string materialName = $"LineColor_{color.Red}_{color.Green}_{color.Blue}";
                Material material = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .FirstOrDefault(m => m.Name == materialName);

                if (material == null)
                {
                    material = (Material)doc.GetElement(Material.Create(doc, materialName));
                    material.Color = color;
                    material.Transparency = 0;
                    material.Shininess = 128;
                }

                TessellatedShapeBuilder tsb = new TessellatedShapeBuilder();
                tsb.OpenConnectedFaceSet(true);

                foreach (Face face in solid.Faces)
                {
                    Mesh mesh = face.Triangulate();
                    for (int i = 0; i < mesh.NumTriangles; i++)
                    {
                        MeshTriangle tri = mesh.get_Triangle(i);
                        IList<XYZ> triangle = new List<XYZ>
                        {
                            tri.get_Vertex(0),
                            tri.get_Vertex(1),
                            tri.get_Vertex(2)
                        };
                        TessellatedFace tFace = new TessellatedFace(triangle, material.Id);
                        if (tsb.DoesFaceHaveEnoughLoopsAndVertices(tFace))
                            tsb.AddFace(tFace);
                    }
                }

                tsb.CloseConnectedFaceSet();
                tsb.Target = TessellatedShapeBuilderTarget.AnyGeometry;
                tsb.Fallback = TessellatedShapeBuilderFallback.Mesh;
                tsb.Build();

                GeometryObject geo = tsb.GetBuildResult().GetGeometricalObjects().First();
                DirectShape shape = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                shape.AppendShape(new List<GeometryObject> { geo });
                shape.Name = "LineShape";

                directShapes.Add(shape);
            }
        }

        private static Face[] CreateDummyFaces(Document doc, Face face, out Solid solid)
        {
            solid = null;
            var dummy = new List<Face>();

            if (face == null) return null;

            IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();
            if (loops == null || loops.Count == 0)
                return null;

            XYZ normal = face.ComputeNormal(new UV(0.5, 0.5)).Normalize();

            double thickness = 0.01;

            Solid extrusion = GeometryCreationUtilities.CreateExtrusionGeometry(
                loops.ToList(),
                normal,
                thickness
            );

            solid = extrusion;

            if (extrusion != null)
                dummy.AddRange(extrusion.Faces.OfType<Face>());

            return dummy.ToArray();
        }
    }

    public class XYZComparer : IEqualityComparer<XYZ>
    {
        public bool Equals(XYZ a, XYZ b)
        {
            return a.IsAlmostEqualTo(b);
        }

        public int GetHashCode(XYZ obj)
        {
            return obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
        }
    }
}
