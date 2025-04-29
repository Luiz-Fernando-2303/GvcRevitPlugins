using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.Shared.App;
using GvcRevitPlugins.TerrainCheck.UI;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GvcRevitPlugins.TerrainCheck
{
    public class TerrainCheckApp : IExternalApplication, IApp
    {
        public static TerrainCheckApp _thisApp = null;
        public static UIApplication _UIApp = null;
        public TerrainCheckApp ThisApp => _thisApp;
        public UIApplication UIApp => _UIApp;

        public Store Store = new Store();
        private MainWindow mWnd;
        public static Document CurrentDocument { get; private set; }

        public TerrainCheckApp()
        {
            _thisApp = this;
            ShowMainWindowCommand.App = this;
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
                    "EmccampTerrainCheckExecute",
                    "Checagem de Terrenos",
                    "GvcRevitPlugins.TerrainCheck.ShowMainWindowCommand",
                    logo16,
                    logo32);

                mWnd = null;
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
            if (mWnd == null || mWnd.IsLoaded == false)
            {
                _UIApp = uiApp;
                CurrentDocument = uiApp.ActiveUIDocument.Document;

                IDictionary<string, string> args = new Dictionary<string, string>()
                {
                    ["BackEndVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString()
                };


                mWnd = new MainWindow(args, this);
                mWnd.Show();
            }
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
    [Transaction(TransactionMode.Manual)] // setting transactions to manual in order to associate them with our add-in commands, if needed.
    [Regeneration(RegenerationOption.Manual)] //enumeration of the Revit API regeneration options. Not really an option currently. The automatic option was supressed.
    public class ShowMainWindowCommand : IExternalCommand
    {
        internal static IApp App { get; set; }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //RevitTask.Initialize(ExternalApplication.UIApp);
            RevitTask.Initialize(commandData.Application);
            RevitTask.RegisterGlobal(new GvcExternalEventHandler());
            /* Through this method the user will launch the Add-In and open its MainWindow 
             by clicking on the Ribbon panel button created in the ExternalApplication class. */
            try
            {
                /* Calling the application to show the MainWindow with its controls (each containing a different command). */
                App.ShowWindow(commandData.Application);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                /* The message users should see in case of any error when trying to start the Add-In. */
                message = ex.Message;
                TaskDialog.Show("Error!", message);
                return Result.Failed;
            }
        }
    }
}

