using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.App;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GvcRevitPlugins.SimpleDebugs
{
    public class SimpleDebugApp : IExternalApplication, IApp
    {
        public static SimpleDebugApp _thisApp = null;
        public static UIApplication _UIApp = null;
        public SimpleDebugApp ThisApp => _thisApp;
        public UIApplication UIApp => _UIApp;

        public static Document CurrentDocument { get; private set; }

        public SimpleDebugApp()
        {
            _thisApp = this;
        }
        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location; 
                var tst = Properties.Resource.ResourceManager.GetObject("Logo16");
                var logo16 = (Bitmap)Properties.Resource.ResourceManager.GetObject("Logo16");
                var logo32 = (Bitmap)Properties.Resource.ResourceManager.GetObject("Logo32");
                RibbonPanel panel = CreateRibbonTabAndPanel(application, "GVC");
                CreateStartAppButton(
                    panel,
                    path,
                    "GVCSimpleDebugsExecute",
                    "GVC SimpleDebugs",
                    "GvcRevitPlugins.SimpleDebugs.ExecuteCommand",
                    logo16,
                    logo32);

                _thisApp = this;
            }
            catch (Exception ex)
            {
                string message = $"Exception Message: {ex.Message}\n\nSource: {ex.Source}\n\nStackTrace: {ex.StackTrace}";
                Clipboard.SetText(message);
                MessageBox.Show($"Mensagem copiada para o Clipboard!\n{message}", "Erro ao inicializar");
            }
            return Result.Succeeded;
        }
        private RibbonPanel CreateRibbonTabAndPanel(UIControlledApplication application, string ribbonTabName)
        {
            RibbonPanel ribbonPanel;
            try
            {
                application.CreateRibbonTab(ribbonTabName);
            }
            catch { }

            try
            {
                ribbonPanel = application.CreateRibbonPanel(ribbonTabName, "ㅤ");
            }
            catch { }
            ribbonPanel = application.GetRibbonPanels(ribbonTabName).Single(x => x.Name == "ㅤ");

            return ribbonPanel;
        }
        private void CreateStartAppButton(RibbonPanel panel, string path, string name, string text, string commandPath, object logo16, object logo32)
        {
            PushButtonData button = new PushButtonData(name, text, path, commandPath);

            BitmapImage largeImage = ImageToBitmapImage(logo32 as Bitmap);
            BitmapImage image = ImageToBitmapImage(logo16 as Bitmap); //TODO: Instead Bitmap, pass just path

            PushButton pushButton = panel.AddItem(button) as PushButton;
            pushButton.LargeImage = largeImage;
            pushButton.Image = image;
        }
        public void ShowWindow(UIApplication uiApp)
        {
                _UIApp = uiApp;
                CurrentDocument = uiApp.ActiveUIDocument.Document;

                IDictionary<string, string> args = new Dictionary<string, string>()
                {
                    ["BackEndVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                };

        }

        #region AUXILIARY METHODS
        private BitmapImage ImageToBitmapImage(Image img)
        {
            using (var memory = new MemoryStream())
            {
                img.Save(memory, ImageFormat.Png); // set the input format here
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }
        #endregion
    }
}

