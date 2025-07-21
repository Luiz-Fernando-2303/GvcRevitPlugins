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

            TerrainCheckApp._thisApp.Store.IntersectionElementId = reference.ElementId;
            TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = null;

            if (objectType != ObjectType.Element)
            {
                GeometryObject selectedFace = doc.GetElement(reference.ElementId)?.GetGeometryObjectFromReference(reference);

                LocationPoint location = doc.GetElement(reference.ElementId)?.Location as LocationPoint;
                Transform translation = Transform.CreateTranslation(location?.Point ?? XYZ.Zero);
                Transform rotation = Transform.CreateRotation(XYZ.BasisZ, location.Rotation);

                var faceMesh = (selectedFace as Face).Triangulate().get_Transformed(rotation).get_Transformed(translation);
                TerrainCheckApp._thisApp.Store.IntersectionGeometricObject = faceMesh;
            }
        }
    }
} 
