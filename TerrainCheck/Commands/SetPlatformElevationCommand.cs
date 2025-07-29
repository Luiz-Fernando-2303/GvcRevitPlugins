using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.Shared.Commands;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.Commands
{
    public class SetPlatformElevationCommand : AsyncCommandBase, IGvcCommand
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
            PickElevationFace(uiDoc);
        }
        internal static double PickElevationFace(UIDocument uidoc)
        {
            Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Face, "Selecione uma face de referência da elevação do platô");

            if (pickedRef == null) return double.NegativeInfinity; 

            Element element = uidoc.Document.GetElement(pickedRef.ElementId);
            GeometryObject geoObject = element.GetGeometryObjectFromReference(pickedRef);

            Transform transform = null;
            if (element is FamilyInstance familyInstance)
                transform = familyInstance.GetTransform();

            if (geoObject == null || !(geoObject is Face))
            {
                TaskDialog.Show("Error", "The selected object is not a face.");
                return double.NegativeInfinity;
            }

            Face face = geoObject as Face;

            List<XYZ> vertices = new List<XYZ>();

            EdgeArrayArray edgeLoops = face.EdgeLoops;
            foreach (EdgeArray edgeArray in edgeLoops)
                foreach (Edge edge in edgeArray)
                {
                    Curve curve = edge.AsCurve();

                    Curve actualCurve = curve;
                    if (transform != null)
                        actualCurve = actualCurve.CreateTransformed(transform);

                    XYZ startPoint = actualCurve.GetEndPoint(0);
                    XYZ endPoint = actualCurve.GetEndPoint(1);

                    if (!vertices.Contains(startPoint))
                        vertices.Add(startPoint);
                    if (!vertices.Contains(endPoint))
                        vertices.Add(endPoint);
                }

            var result = Math.Round(UnitUtils.ConvertFromInternalUnits(vertices[0].Z, UnitTypeId.Meters), 1);
            TerrainCheckApp._thisApp.Store.PlatformElevation = result;
            return result;
        }
    }
}
