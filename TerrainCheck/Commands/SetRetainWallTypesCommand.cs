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
    public class SetRetainWallTypesCommand : AsyncCommandBase, IGvcCommand
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
            var selection = uidoc.Selection; 

            List<Reference> pickedRef = selection.PickObjects(ObjectType.Element, "Selecione os arrimos").ToList();
            if (pickedRef.Count == 0) return;

            List<Element> elements = pickedRef
                .Select(r => doc.GetElement(r.ElementId))
                .ToList();

            List<Material> materials = new();

            foreach (Element element in elements)
            {
                List<Material> elementMaterials = GvcRevitPlugins.Shared.Utils.ElementUtils.GetElementMaterials(doc, element).ToList();
                if (elementMaterials.Count == 0) continue;

                materials.AddRange(elementMaterials);
            }

            materials = materials.DistinctBy(material => material.Name).ToList();
            if (materials.Count == 0)
            {
                TaskDialog.Show("Erro", "Nenhum material encontrado nos arrimos selecionados.");
                return;
            }

            TerrainCheckApp._thisApp.Store.selectedRetainWalls = elements;
            TerrainCheckApp._thisApp.Store.retainWallsMaterials = materials;
        }
    }
}
