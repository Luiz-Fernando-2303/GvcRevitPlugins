using System;
using System.Windows;

namespace GvcRevitPlugins.TerrainCheck.UI
{
    public partial class SimpleInterface : Window
    {
        public Plugin Plugin { get; set; }

        public SimpleInterface()
        {
            Plugin = new Plugin();
            InitializeComponent();
        }

        private void SelectPlatform_Click(object sender, RoutedEventArgs e)
        {
            // Aqui entraria a lógica para seleção do platô no Revit
            StatusText.Text = "Platô selecionado (simulado).";
            Plugin.SetPlatformElevation();
        }

        private void SelectBoundary_Click(object sender, RoutedEventArgs e)
        {
            // Aqui entraria a lógica para seleção da divisa do terreno
            StatusText.Text = "Divisa selecionada (simulado).";
            Plugin.SetBoundaryPoints();
        }

        private void SelectBuildingFace_Click(object sender, RoutedEventArgs e)
        {
            // Aqui entraria a lógica para seleção da face do edifício
            StatusText.Text = "Face do edifício selecionada (simulado).";
            Plugin.SetProjectedFace();
        }

        private void ExecuteCheck_Click(object sender, RoutedEventArgs e)
        {
            // Aqui entraria a lógica principal da verificação topográfica
            StatusText.Text = "Verificação executada (simulado).";
            try
            {
                Plugin.Execute();
                StatusText.Text = "Verificação concluída com sucesso.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro: {ex.Message}";
            }
        }
    }
}
