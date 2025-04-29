using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class SetBuildingFaceIdCommand : AsyncCommandBase, IGvcCommand
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

            Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Face, "Selecione a face do edifício");
            if (pickedRef == null) return;
            Element element = uiDoc.Document.GetElement(pickedRef.ElementId);

/* Unmerged change from project 'GvcRevitPlugins (net48)'
Before:
            LupaRevitUI.Commands.RevitCommands.BuildingFaceId = (int)element.Id.Value;
        }
After:
            RevitCommands.BuildingFaceId = (int)element.Id.Value;
        }
*/
            Shared.Commands.RevitCommands.BuildingFaceId = (int)element.Id.Value;
        }
    }
}
