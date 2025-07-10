using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public class StructuralWallCheckRule : ITerrainCheckRule
    {
        public bool IsActive { get; set; } = true;
        public string Name => "Structural_Wall";
        public string Description => "Checks the structural wall of the terrain";
        public Color ColorRGB => new Color(255, 255, 0);
        public int WallTypeId { get; set; }
        public string WallTypeName { get; set; } = "Resultado Talude Arrimo";

        public Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool, Level> Execute => (uidoc, startPoints, normal, boundaryPoints, baseElevation, draw, Level) =>
        {
            WallType wallType = Shared.Utils.RevitUtils.GetOrCreateWallType(uidoc, WallTypeName, BuiltInCategory.OST_Walls, ColorRGB);

            //var points = TerrainCheckApp._thisApp.Store.RailingPoins;
            //var highestPoint = points.OrderByDescending(p => p.Z).FirstOrDefault();

            //Line horizontalLine = Line.CreateBound(
            //    new XYZ(highestPoint.X - 1000, highestPoint.Y, highestPoint.Z),
            //    new XYZ(highestPoint.X + 1000, highestPoint.Y, highestPoint.Z)
            //);
            //Draw._Curve(uidoc.Document, horizontalLine);

            if (wallType == null)
                TaskDialog.Show("Error", "Cannot find the specified wall type.");

            if (Level == null)
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

            List<Curve> allCurvesToDraw = new List<Curve>();
            foreach (Curve curve in resultCurves)
            {
                //allCurvesToDraw.Add(curve);

                //IntersectionResult P0 = horizontalLine.Project(curve.GetEndPoint(0));
                //IntersectionResult P1 = horizontalLine.Project(curve.GetEndPoint(1));

                //if (P0 == null || P1 == null)
                //    continue;

                //Line P0line = Line.CreateBound(P0.XYZPoint, curve.GetEndPoint(0));
                //Line P1line = Line.CreateBound(P1.XYZPoint, curve.GetEndPoint(1));

                //allCurvesToDraw.Add(P0line);
                //allCurvesToDraw.Add(P1line);

                //Wall.Create(uidoc.Document, curve, wallType.Id, Level.Id, 30, 0.0, false, false);
            }

            utils.Draw._Curve(uidoc.Document, allCurvesToDraw);
        };
    }
}
