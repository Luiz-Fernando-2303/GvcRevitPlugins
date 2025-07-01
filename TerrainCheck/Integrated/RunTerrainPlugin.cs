using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.TerrainCheck.UI;
using Revit.Async;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GvcRevitPlugins.TerrainCheck
{
    public class RunTerrainPlugin : IExternalApplication
    {
        public static UIApplication UIApp { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                var logo16 = (Bitmap)Properties.Resource.ResourceManager.GetObject("Logo16");
                var logo32 = (Bitmap)Properties.Resource.ResourceManager.GetObject("Logo32");

                RibbonPanel panel = CreateRibbonPanel(application, "GVC");
                AddButton(panel, path, "EmccampTerrainCheckExecute V2", "Checagem de Terrenos", typeof(ShowTerrainPluginCommand).FullName, logo16, logo32);
            }
            catch (Exception ex)
            {
                Clipboard.SetText(ex.ToString());
                MessageBox.Show("Erro ao inicializar o plugin. Detalhes copiados para a área de transferência.");
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

        private RibbonPanel CreateRibbonPanel(UIControlledApplication application, string tabName)
        {
            try { application.CreateRibbonTab(tabName); } catch { }
            return application.GetRibbonPanels(tabName).FirstOrDefault(p => p.Name == "ㅤ") ?? application.CreateRibbonPanel(tabName, "ㅤ");
        }

        private void AddButton(RibbonPanel panel, string assemblyPath, string name, string text, string commandNamespace, Bitmap logo16, Bitmap logo32)
        {
            var buttonData = new PushButtonData(name, text, assemblyPath, commandNamespace);
            var pushButton = panel.AddItem(buttonData) as PushButton;
            pushButton.Image = ConvertToBitmapImage(logo16);
            pushButton.LargeImage = ConvertToBitmapImage(logo32);
        }

        private BitmapImage ConvertToBitmapImage(Image img)
        {
            using var memory = new MemoryStream();
            img.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memory;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowTerrainPluginCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                RevitTask.Initialize(commandData.Application);
                RevitTask.RegisterGlobal(new GvcExternalEventHandler());
                RevitTask.RunAsync(() =>
                {
                    var mainWindow = new TerrainPluginInterface(commandData);
                    mainWindow.Show();
                });
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Erro", message);
                return Result.Failed;
            }
        }
    }
}
