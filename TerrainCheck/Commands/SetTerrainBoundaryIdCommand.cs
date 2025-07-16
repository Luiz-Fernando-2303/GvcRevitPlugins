using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class SetTerrainBoundaryIdCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => { return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); });
        }

        public void MakeAction(object uiAppObj)
        {
            var uiApp = uiAppObj as UIApplication;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var selection = uiDoc.Selection;

            List<Reference> pickedRef = selection.PickObjects(ObjectType.Element, "Selecione os objetos de divisa").ToList();
            if (pickedRef.Count == 0) return;

            List<Element> elements = pickedRef
                .Select(r => doc.GetElement(r.ElementId))
                .ToList();

            string selectionType = TerrainCheckApp._thisApp.Store.BoundarySelectionType;

            switch (selectionType)
            {
                case "Linha de Divisa":
                    elements = elements
                        .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_SitePropertyLineSegment)
                        .ToList();
                    break;

                case "Parede":
                    elements = elements
                        .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_Walls)
                        .ToList();
                    break;

                case "Guarda Corpo":
                    elements = elements
                        .Where(e => e.Category?.BuiltInCategory == BuiltInCategory.OST_StairsRailing)
                        .ToList();
                    break;

                default:
                    TaskDialog.Show("Erro", "Tipo de elemento não reconhecido.");
                    return;
            }

            TerrainCheckApp._thisApp.Store.TerrainBoundaryIds = elements.Select(e => e.Id).ToList();
        }

    }
}
