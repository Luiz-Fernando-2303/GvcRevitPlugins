using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace GvcRevitPlugins.TerrainCheck.UI
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel viewModel;
        public MainWindow()
        {
            InitializeComponent();
        }
        public MainWindow(IDictionary<string, string> args, TerrainCheckApp app)
        {
            //MainWindowViewModel.Instance.BackEndVersion = args["BackEndVersion"]?.ToString() ?? "";
            //This is an alternate way to initialize MaterialDesignInXAML if you don't use the MaterialDesignResourceDictionary in App.xaml
            //var primaryColor = SwatchHelper.Lookup[MaterialDesignColor.Blue900];
            //var accentColor = SwatchHelper.Lookup[MaterialDesignColor.Grey700];
            
            //var theme = Theme.Create(Theme.Dark, primaryColor, accentColor);
            //Resources.SetTheme(theme);

            var dummy = MaterialDesignThemes.Wpf.BaseTheme.Dark;
            var dummy2 = MaterialDesignColors.MaterialDesignColor.Amber;
            InitializeComponent();
            var coreWebView2CreationProperties = (CoreWebView2CreationProperties)Resources["EvergreenWebView2CreationProperties"];
            coreWebView2CreationProperties.UserDataFolder = Path.GetTempPath();
            viewModel = new MainWindowViewModel(null, app);
            DataContext = viewModel;
        }
    }
}
