using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System.Threading.Tasks;

namespace GvcRevitPlugins.SimpleDebugs.Commands
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
        }
    }
}
