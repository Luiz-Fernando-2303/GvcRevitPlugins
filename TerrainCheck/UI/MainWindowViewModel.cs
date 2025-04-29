using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck.Commands;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GvcRevitPlugins.Shared.Commands;

namespace GvcRevitPlugins.TerrainCheck.UI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public static MainWindowViewModel Instance { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public Action CloseAction { get; set; }
        public Store Store { get; set; }
        private WebViewService _webViewService;

        private int mainWindowHeight = 600;
        public ICommand SetPlatformElevation { get; set; }
        public ICommand SetBuildingFaceId { get; set; }
        public ICommand SetTerrainBoundaryId { get; set; }
        public ICommand GetTerrainInfoCommand { get; set; }
        public ICommand DrawTerrainCheckCommand { get; set; }
        public int MainWindowHeight
        {
            get
            {
                return mainWindowHeight;
            }
            set
            {
                mainWindowHeight = value;
                OnPropertyChanged("MainWindowHeight");
            }
        }

        private int mainWindowWidth = 900;
        public int MainWindowWidth
        {
            get
            {
                return mainWindowWidth;
            }
            set
            {
                mainWindowWidth = value;
                OnPropertyChanged("MainWindowWidth");
            }
        }

        public MainWindowViewModel()
        {

        }
        public MainWindowViewModel(UIApplication uirevitapp, WebView2 webView, TerrainCheckApp app)
        {
            Instance = this;
            _webViewService = new WebViewService(webView, this);
            Store = app.Store;
            SetCommands();
        }
        private void SetCommands()
        {
            SetPlatformElevation = new RelayCommand(() => { new SetPlatformElevationCommand().Execute(this); });
            SetBuildingFaceId = new RelayCommand(() => { new SetBuildingFaceIdCommand().Execute(this); });
            SetTerrainBoundaryId = new RelayCommand(() => { new SetTerrainBoundaryIdCommand().Execute(this); });
            GetTerrainInfoCommand = new RelayCommand(() => { new GetTerrainInfoCommand().Execute(this); });
            DrawTerrainCheckCommand = new RelayCommand(() => { new DrawTerrainCheckCommand().Execute(this); });
        }
        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

    }
}
