using Autodesk.Revit.UI;

namespace GvcRevitPlugins.Shared.App
{
    internal interface IApp
    {
        //IApp ThisApp { get; }
        //UIApplication UIApp { get; }
        Result OnShutdown(UIControlledApplication application);
        Result OnStartup(UIControlledApplication application);
        //Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements);
        void ShowWindow(UIApplication uiApp);
        //BitmapImage GetIcon16x();
        //BitmapImage GetIcon32x();
    }
}