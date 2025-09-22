using Autodesk.Revit.UI;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

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

        private void MateriaisListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            List<string> selection = new List<string>();
            foreach (var item in MateriaisListBox.SelectedItems)
            {
                if (item is string materialName)
                {
                    selection.Add(materialName);
                }
            }

            TerrainCheckApp._thisApp.Store.SelectedMaterials = selection;
        }

        private void ToggleTreeButton_Click(object sender, RoutedEventArgs e)
        {
            BoundaryTreePopup.IsOpen = !BoundaryTreePopup.IsOpen;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item)
            {
                TerrainCheckApp._thisApp.Store.BoundarySelectionType = item.Header.ToString();
                BoundaryTreePopup.IsOpen = false;

                if (item.Header.ToString() == "Arrimo")
                    selectionType_.SelectedIndex = 1;
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var selectedItem = e.AddedItems[0] as ComboBoxItem;
                string selectedValue = selectedItem?.Content.ToString();

                if (TerrainCheckApp._thisApp.Store.BoundarySelectionType == "Arrimo" && selectedValue == "Família")
                {
                    TaskDialog.Show("Aviso", "Arrimo não habilita seleção por família. A seleção será alterada para 'Face'.");
                    selectedValue = "Face";
                    selectionType_.SelectedIndex = 1;
                }

                if (selectedValue == "Face")
                {
                    // Hide material selection
                    MateriaisListBox.Visibility = Visibility.Collapsed;

                    // Hide the label
                    MaterialLabel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Show material selection
                    MateriaisListBox.Visibility = Visibility.Visible;
                    // Show the label
                    MaterialLabel.Visibility = Visibility.Visible;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            TerrainCheckApp._thisApp.Store.Reset();
        }

        private void BoundaryTreePopup_Opened(object sender, EventArgs e)
        {
            ArrowIcon.Text = "▲"; // seta para cima quando aberto
        }

        private void BoundaryTreePopup_Closed(object sender, EventArgs e)
        {
            ArrowIcon.Text = "▼"; // seta para baixo quando fechado
        }
    }
}
 