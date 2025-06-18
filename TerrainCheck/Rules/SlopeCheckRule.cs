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
        public Color ColorRGB => new Color(255, 0, 0);
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; } = "Resultado Talude Corte";
        public string ResultFamilyName { get; } = "Linha de Afastamento Mínimo.rfa";

        public Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool, Level> Execute => (uidoc, startPoints, normal, boundaryPoints, baseElevation, draw, Level) =>
        {
            double StrucWallHeight = UnitUtils.ConvertToInternalUnits(TerrainCheckApp._thisApp.Store.TerrainCheckStrucWallHeight, UnitTypeId.Meters);

            WallType wallType = Shared.Utils.RevitUtils.GetOrCreateWallType(uidoc, WallTypeName, BuiltInCategory.OST_Walls, ColorRGB);

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
                {
                    Wall.Create(uidoc.Document, curve, wallType.Id, Level.Id, minimumDistance, 0.0, false, false);
                }
            }
        };

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
    }
}
