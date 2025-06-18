using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public static class CheckRules
    {
        public static ITerrainCheckRule Slope => new SlopeCheckRule();
        public static ITerrainCheckRule StructuralWall => new StructuralWallCheckRule();
        public static ITerrainCheckRule[] Rules => new ITerrainCheckRule[] { Slope, StructuralWall };
        public static void Execute(UIDocument uidoc, XYZ[] startPoints, XYZ normal, XYZ[] boundaryPoints, double baseElevation, bool draw, Level level)
        {
            foreach (ITerrainCheckRule rule in Rules)
                if (rule.IsActive)
                    rule.Execute(uidoc, startPoints, normal, boundaryPoints, baseElevation, draw, level);
        }
    }
}
