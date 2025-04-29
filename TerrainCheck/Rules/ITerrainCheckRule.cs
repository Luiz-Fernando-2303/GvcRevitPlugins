using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace GvcRevitPlugins.TerrainCheck.Rules
{
    public interface ITerrainCheckRule
    {
        bool IsActive { get; set; }
        string Description { get; }
        string Name { get; }
        string Color { get; }
        Action<UIDocument, XYZ[], XYZ, XYZ[], double, bool> Execute { get; }
    }
}
