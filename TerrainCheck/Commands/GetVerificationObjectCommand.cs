using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class GetVerificationObjectCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => { return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); });
        }

        public void MakeAction(object uiAppObj)
        {
            var uiApp = uiAppObj as UIApplication;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            string selectionType = TerrainCheckApp._thisApp.Store.ObjectSelectionType;
            ObjectType objectType = selectionType switch
            {
                "Face" => ObjectType.Face,
                "Linha" or "Aresta" => ObjectType.Edge,
                _ => ObjectType.Element
            };

            Reference reference = uidoc.Selection.PickObject(objectType, $"Select the verification target ({selectionType})");
            if (reference == null) return;

            TerrainCheckApp._thisApp.Store.IntersectionElementId = doc.GetElement(reference.ElementId).Id;
        }
    }
}
