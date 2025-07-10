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
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var selection = uiDoc.Selection;

            Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Element, "Selecione o objeto de verificacao");
            if (pickedRef == null) return;
            Element element = uiDoc.Document.GetElement(pickedRef.ElementId);
            TerrainCheckApp._thisApp.Store.IntersectionElementId = element.Id;

            //TerrainCheckCommand.Execute(uiApp as UIApplication, false);
        }
    }
}
