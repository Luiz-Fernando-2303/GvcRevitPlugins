using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck;
using Revit.Async;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GvcRevitPlugins.TerrainCheck.UI
{

    public class SlopeResultDisplay
    {
        public int ID { get; set; }
        public string Start { get; set; }
        public string Boundary { get; set; }
        public string End { get; set; }
        public double Altura_m { get; set; }
        public double DistanciaXY_m { get; set; }
        public double Offset_m { get; set; }

        public SlopeResultDisplay(SlopeResult result)
        {
            ID = (int)result.WallId.Value;
            Start = FormatXYZ(result.StartPoint);
            Boundary = FormatXYZ(result.BoundaryPoint);
            End = FormatXYZ(result.EndPoint);
            Altura_m = Math.Round(UnitUtils.ConvertFromInternalUnits(result.HeightDifference, UnitTypeId.Meters), 2);
            DistanciaXY_m = Math.Round(UnitUtils.ConvertFromInternalUnits(result.DistanceToBoundary, UnitTypeId.Meters), 2);
            Offset_m = Math.Round(UnitUtils.ConvertFromInternalUnits(result.OffsetUsed, UnitTypeId.Meters), 2);
        }

        private static string FormatXYZ(XYZ pt)
        {
            if (pt == null) return "-";
            return $"({pt.X:F2}, {pt.Y:F2}, {pt.Z:F2})";
        }
    }
    public partial class TerrainPluginInterface : Window
    {
        public TerrainPlugin Plugin { get; set; }

        public TerrainPluginInterface(ExternalCommandData commandData)
        {
            Plugin = new TerrainPlugin();
            Plugin.Initialize(commandData.Application);
            InitializeComponent();
        }

        private void SelectPlatform_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Plugin.SetPlatformElevation())
                    StatusText.Text = "Platô selecionado com sucesso.";
                else
                    StatusText.Text = "Erro ao selecionar o platô.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro: {ex.Message}";
            }
        }

        private void SelectBoundary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Plugin.SetTerrainBoundary())
                    StatusText.Text = "Divisa selecionada com sucesso.";
                else
                    StatusText.Text = "Erro ao selecionar a divisa.";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro: {ex.Message}";
            }
        }

        private void SelectBuildingFace_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Plugin.SetProjectedFace())
                {
                    StatusText.Text = "Face do edifício selecionada com sucesso.";
                    ExecuteCheck_Click(sender, e);
                }
                else
                {
                    StatusText.Text = "Erro ao selecionar a face.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro: {ex.Message}";
            }
        }

        private void ExecuteCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Converte valores da interface
                if (!double.TryParse(WallHeightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double wallHeight) ||
                    !double.TryParse(MinDistanceBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double minDistance) ||
                    !int.TryParse(SubdivisionLevelBox.Text, out int subdivisionLevel))
                {
                    StatusText.Text = "Parâmetros inválidos. Verifique os valores inseridos.";
                    return;
                }

                Plugin.wallHeight_ = wallHeight;
                Plugin.minimumDistance_ = minDistance;
                Plugin.SubdivisionLevel = subdivisionLevel;

                // Executa verificação
                if (!Plugin.SetBoundaryPoints())
                {
                    StatusText.Text = "Erro ao identificar pontos da borda do terreno.";
                    return;
                }

                RevitTask.RunAsync(() =>
                {
                    Plugin.Execute();

                    // Interface gráfica deve ser atualizada no thread principal
                    Dispatcher.Invoke(() =>
                    {
                        var displayResults = Plugin.Results.ConvertAll(r => new SlopeResultDisplay(r));
                        ResultsGrid.ItemsSource = displayResults;
                        StatusText.Text = "Verificação concluída com sucesso.";
                    });
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro durante a execução: {ex.Message}";
            }
        }
    }
}
