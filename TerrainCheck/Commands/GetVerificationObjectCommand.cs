using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using utils = GvcRevitPlugins.Shared.Utils;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class GetVerificationObjectCommand : AsyncCommandBase, IGvcCommand
    {
        public override async Task ExecuteAsync(object parameter)
        {
            bool dummy = await RevitTask.RunAsync(async app => 
            {
                return await RevitTask.RaiseGlobal<GvcExternalEventHandler, IGvcCommand, bool>(this); 
            });
        }

        public void MakeAction(object uiAppObj)
        {
            var uiApp   = uiAppObj as UIApplication;
            var uidoc   = uiApp.ActiveUIDocument;
            var doc     = uidoc.Document;

            string selectionType = TerrainCheckApp._thisApp.Store.ObjectSelectionType;
            ObjectType objectType = selectionType switch
            {
                "Face"  => ObjectType.Face,
                _       => ObjectType.Element
            };

            Reference reference = uidoc.Selection.PickObject(objectType, $"Select the verification target ({selectionType})");
            if (reference == null) return;

            // Clear previous selection data
            TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = null;
            TerrainCheckApp._thisApp.Store.Elementmaterials = null;

            // Set the element 
            TerrainCheckApp._thisApp.Store.IntersectionElementId = reference.ElementId;
            TerrainCheckApp._thisApp.Store.Element = doc.GetElement(reference.ElementId);

            // Set the material list
            List<Material> elementMaterials = utils.ElementUtils.GetElementMaterials(doc, TerrainCheckApp._thisApp.Store.Element).ToList();
            TerrainCheckApp._thisApp.Store.Elementmaterials = elementMaterials.Select(m => m.Name).Distinct().ToList();

            if (objectType != ObjectType.Element)
            {
                GeometryObject selectedFace = doc.GetElement(reference.ElementId)?.GetGeometryObjectFromReference(reference);
                TerrainCheckApp._thisApp.Store.Element = doc.GetElement(reference.ElementId);

                LocationPoint location = doc.GetElement(reference.ElementId)?.Location as LocationPoint;
                if (location == null)
                {
                    var faceMesh_ = (selectedFace as Face).Triangulate();
                    TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = faceMesh_;
                    return;
                }

                Transform translation = Transform.CreateTranslation(location?.Point ?? XYZ.Zero);
                Transform rotation = Transform.CreateRotation(XYZ.BasisZ, location.Rotation);

                var faceMesh = (selectedFace as Face).Triangulate().get_Transformed(rotation).get_Transformed(translation);
                TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = faceMesh;
            }
        }
    }
} 
  