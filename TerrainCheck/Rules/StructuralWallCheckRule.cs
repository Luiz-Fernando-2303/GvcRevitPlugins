using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public class StructuralWallCheckRule : ITerrainCheckRule
    {
        public bool IsActive { get; set; } = true;
        public string Name => "Structural_Wall";
        public string Description => "Checks the structural wall of the terrain";
        public string Color => "#FF0000";
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; } = "Resultado Talude Arrimo";

        public Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool> Execute => (uidoc, startPoints, normal, boundaryPoints, baseElevation, draw) =>
        {
            WallType wallType = new FilteredElementCollector(uidoc.Document)
                               .OfClass(typeof(WallType))
                               .OfCategory(BuiltInCategory.OST_Walls)
                               .FirstOrDefault(w => w.Name.Equals(WallTypeName)) as WallType; //TODO: Select a wall type

            if (wallType == null)
                TaskDialog.Show("Error", "Cannot find the specified wall type.");

            Level level = new FilteredElementCollector(uidoc.Document)
                        .OfClass(typeof(Level))
                        .FirstOrDefault(l => l.Name.Equals("Level 1")) as Level; //TODO: Select a level

            if (level == null)
                TaskDialog.Show("Error", "Cannot find the specified level.");

            double minimumDistance = UnitUtils.ConvertToInternalUnits(2, UnitTypeId.Meters);
            List<Curve> resultCurves = new List<Curve>();
            XYZ[] endPoints = new XYZ[startPoints.Length];
            for (int i = 0; i < startPoints.Length; i++)
            {
                XYZ boundaryPoint = boundaryPoints[i];
                if (boundaryPoint == null) continue;

                XYZ startPoint = startPoints[i];
                XYZ diff = new XYZ(boundaryPoint.X, boundaryPoint.Y, startPoint.Z) - startPoint;

                double offset = minimumDistance; //TODO: Implement the offset logic
                offset = offset < minimumDistance ? minimumDistance : offset;

                XYZ endPoint = Shared.Utils.XYZUtils.GetEndPoint(startPoint, normal, offset);
                endPoints[i] = endPoint;

                if (i == 0 || endPoints[i - 1] == null || endPoint == null) continue;

                Line wallLine = Line.CreateBound(endPoints[i - 1], endPoint);
                resultCurves.Add(wallLine);
            }

            foreach (Curve curve in resultCurves)
                Wall.Create(uidoc.Document, curve, wallType.Id, level.Id, 30.0, 0.0, false, false);
        };
    }
}
