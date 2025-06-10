using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public class SlopeCheckRule : ITerrainCheckRule
    {
        public bool IsActive { get; set; } = true;
        public string Name => "Slope";
        public string Description => "Checks the slope of the terrain";
        public string Color => "#FF0000";
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; } = "Resultado Talude Corte";
        public string ResultFamilyName { get; } = "Linha de Afastamento Mínimo.rfa";

        public Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool, Level> Execute => (uidoc, startPoints, normal, boundaryPoints, baseElevation, draw, Level) =>
        {
            double StrucWallHeight = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);

            getWall(WallTypeName, uidoc.Document);

            WallType wallType = new FilteredElementCollector(uidoc.Document)
                               .OfClass(typeof(WallType))
                               .OfCategory(BuiltInCategory.OST_Walls)
                               .FirstOrDefault(w => w.Name.Equals(WallTypeName)) as WallType; //TODO: Select a wall type

            if (wallType == null)
                TaskDialog.Show("Error", "Cannot find the specified wall type.");

            double minimumDistance = TerrainCheckApp._thisApp.Store.MinimumDistance;
            minimumDistance = minimumDistance > 2 ? minimumDistance : 2;
            minimumDistance = UnitUtils.ConvertToInternalUnits(minimumDistance, UnitTypeId.Meters);
            if (StrucWallHeight > minimumDistance)
                minimumDistance = StrucWallHeight - UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);

            List<Curve> resultCurves = new List<Curve>();
            XYZ[] endPoints = new XYZ[startPoints.Length];
            List<double> heightDiffs = new List<double>();
            for (int i = 0; i < startPoints.Length; i++)
            {
                XYZ boundaryPoint = boundaryPoints[i];
                if (boundaryPoint == null) continue;

                XYZ startPoint = startPoints[i];
                XYZ diff = new XYZ(boundaryPoint.X, boundaryPoint.Y, startPoint.Z) - startPoint;
                heightDiffs.Add(boundaryPoint.Z - baseElevation);
                double offset = (boundaryPoint.Z - baseElevation) / 2;
                offset = offset < minimumDistance ? minimumDistance : offset;
                XYZ endPoint = Shared.Utils.XYZUtils.GetEndPoint(startPoint, normal, offset);
                endPoints[i] = endPoint;

                if (endPoint == null || i == 0 || endPoints[i - 1] == null) continue;

                Line wallLine = Line.CreateBound(endPoints[i - 1], endPoint);
                resultCurves.Add(wallLine);
            }

            GetWorstCase(baseElevation, startPoints, boundaryPoints);

            if (true) //TODO: Fix draw option
            {
                foreach (Curve curve in resultCurves)
                    Wall.Create(uidoc.Document, curve, wallType.Id, Level.Id, minimumDistance, 0.0, false, false);
            }
        };

        private bool DrawResult(Document doc, FamilySymbol structuralFrameType, Curve curve, double minimumOffset, double drawHeight)
        {
            //FamilyInstance newElement = doc.Create.NewFamilyInstance(curve, structuralFrameType, level, Autodesk.Revit.DB.Structure.StructuralType.UnknownFraming);
            FamilyInstance newElement = doc.Create.NewFamilyInstance(curve.GetEndPoint(0), structuralFrameType, StructuralType.Beam);

            if (newElement == null)
                return false;

            (newElement.Location as LocationCurve).Curve = curve;
            doc.Regenerate();

            StructuralFramingUtils.DisallowJoinAtEnd(newElement, 0);
            StructuralFramingUtils.DisallowJoinAtEnd(newElement, 1);

            Parameter zJustificationParam = newElement.GetParameter(ParameterTypeId.ZJustification);
            var zvalue = zJustificationParam.AsInteger();
            zJustificationParam.Set(3);
            newElement.LookupParameter("Altura").Set(drawHeight);

            return true;
        }

        private void GetWorstCase(double baseElevation, XYZ[] facePoints, XYZ[] boundaryPoints)
        {
            int result = -1;
            double resultHeightDiff = 0.0;
            double resultDistance = 0.0;

            for (int i = 0; i < boundaryPoints.Length; i++)
            {
                if (boundaryPoints[i] == null) continue;
                double heightDiff = boundaryPoints[i].Z - baseElevation;
                double distance = Vector2.Distance(new Vector2((float)facePoints[i].X, (float)facePoints[i].Y), new Vector2((float)boundaryPoints[i].X, (float)boundaryPoints[i].Y));

                if (heightDiff > resultHeightDiff || result < 0)
                {
                    result = i;
                    resultHeightDiff = heightDiff;
                    resultDistance = distance;
                }
            }

            TerrainCheckApp._thisApp.Store.TerrainCheckCalcDistance = Math.Round(UnitUtils.ConvertFromInternalUnits(resultDistance, UnitTypeId.Meters), 1);
            TerrainCheckApp._thisApp.Store.TerrainCheckCalcHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(resultHeightDiff, UnitTypeId.Meters), 1);
        }
        private static WallType getWall(string wallTypeName, Document doc)
        {
            WallType wallType = new FilteredElementCollector(doc)
                              .OfClass(typeof(WallType))
                              .OfCategory(BuiltInCategory.OST_Walls)
                              .FirstOrDefault(w => w.Name.Equals(wallTypeName)) as WallType; //TODO: Select a wall type


            if (wallType != null) return wallType;
            return null;


        }
        public class WallTypeData
        {
            public string FamilyName { get; set; }
            public double Width { get; set; }
            public Dictionary<string, object> Parameters { get; set; }
        }

        // Save WallType properties to JSON
        public static string SaveWallTypeToJson(WallType wallType, string filePath)
        {
            WallTypeData wallTypeData = new WallTypeData
            {
                FamilyName = wallType.FamilyName,
                Width = wallType.Width,
                Parameters = GetParameters(wallType)
            };

            string json = JsonConvert.SerializeObject(wallTypeData, Formatting.Indented);
            //File.WriteAllText(filePath, json);
            return json;
        }

        // Extract parameters of the WallType
        private static Dictionary<string, object> GetParameters(Element element)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            foreach (Parameter param in element.Parameters)
                if (param.HasValue)
                    parameters[param.Definition.Name] = param.AsValueString() ?? param.AsString();

            return parameters;
        }

        // Create a new WallType using JSON data
        public static WallType CreateWallTypeFromJson(Document doc, string filePath, string json)
        {
            //string json = File.ReadAllText(filePath);
            WallTypeData wallTypeData = JsonConvert.DeserializeObject<WallTypeData>(json);

            // Duplicate an existing WallType as a template
            WallType newWallType = GetWallTypeByFamilyName(doc, wallTypeData.FamilyName)?.Duplicate("New WallType") as WallType;
            if (newWallType != null)
            {
                //newWallType.Width = wallTypeData.Width;

                // Set parameters from JSON
                foreach (var kvp in wallTypeData.Parameters)
                {
                    Parameter param = newWallType.LookupParameter(kvp.Key);
                    if (param != null && param.StorageType == StorageType.String)
                        param.Set(kvp.Value.ToString());
                    // Add more conditions for other storage types if needed
                }
            }

            return newWallType;
        }

        // Helper function to find a WallType by family name
        private static WallType GetWallTypeByFamilyName(Document doc, string familyName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector.OfClass(typeof(WallType))
                            .Cast<WallType>()
                            .FirstOrDefault(wt => wt.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
