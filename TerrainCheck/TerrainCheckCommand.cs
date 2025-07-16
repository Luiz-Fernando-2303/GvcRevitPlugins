using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using utils = GvcRevitPlugins.Shared.Utils;

// Tipo de selecao
// altura previa do plato
// Tipo de objeto de teste
// Remopver ou atualizar parametros da interface

namespace GvcRevitPlugins.TerrainCheck
{
    public static class TerrainCheckCommand
    {
        internal static void Execute(UIApplication uiApp, bool draw = false)
        {
            TerrainCheckCommand_ wallCommand = new();
            wallCommand.Execute(uiApp);
        }
    }

    public class TerrainCheckCommand_
    {
        public virtual void Execute(UIApplication uiApp)
        {
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            SelectionToLines selectionToLines = new SelectionToLines(
                TerrainCheckApp._thisApp.Store.TerrainBoundaryIds,
                doc
            );

            ProjectFaces projectFaces = new ProjectFaces(
                uiDoc,
                TerrainCheckApp._thisApp.Store.IntersectionElementId,
                selectionToLines.Lines, TerrainCheckApp._thisApp.Store.SubdivisionLevel,
                TerrainCheckApp._thisApp.Store.PlatformElevation
            );
        }
    }
}
