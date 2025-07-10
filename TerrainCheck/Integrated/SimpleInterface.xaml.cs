using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GvcRevitPlugins.TerrainCheck;
using GvcRevitPlugins.TerrainCheck.Integrated;
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
        public double X { get; set; }
        public double Y { get; set; }
        public double Height { get; set; }
        public double DistanceFaceToBoundary { get; set; }
        public double DistanceSlopeToBoundary { get; set; }
        public double Result { get; set; } // wall extension

        public SlopeResultDisplay(SlopeResult result)
        {
            ID = (int)result.Wall.Id.Value;
            X = Math.Round(UnitUtils.ConvertFromInternalUnits(result.BoundaryPoint.X, UnitTypeId.Meters), 2);
            Y = Math.Round(UnitUtils.ConvertFromInternalUnits(result.BoundaryPoint.Y, UnitTypeId.Meters), 2);
            Height = Math.Round(UnitUtils.ConvertFromInternalUnits(result.BoundaryPoint.Z, UnitTypeId.Meters), 2);
            DistanceFaceToBoundary = Math.Round(UnitUtils.ConvertFromInternalUnits(result.DistanceFaceToBoundary, UnitTypeId.Meters), 2);
            DistanceSlopeToBoundary = Math.Round(UnitUtils.ConvertFromInternalUnits(result.DistanceSlopeToBoundary, UnitTypeId.Meters), 2);
            Result = Math.Round(UnitUtils.ConvertFromInternalUnits(result.OffsetUsed / 2, UnitTypeId.Meters), 2);
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
            var BouaryLineToRailing = new BoundaryLineToRailing(Plugin.UiDoc);

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
            var BouaryLineToRailing = new BoundaryLineToRailing(Plugin.UiDoc);

            try
            {
                if (BouaryLineToRailing.ViewIs2D)
                {
                    RevitTask.RunAsync(() =>
                    {
                        BouaryLineToRailing.Execute();
                        Dispatcher.Invoke(() =>
                        {
                            if (BouaryLineToRailing.BoundaryLine != null && BouaryLineToRailing.ProjectionTerrain != null)
                            {
                                StatusText.Text = "Divisa e linha selecionadas com sucesso.";
                            }
                            else
                            {
                                StatusText.Text = "Erro ao selecionar divisa ou linha.";
                            }
                        });

                        Plugin.PreMadePath = BouaryLineToRailing.FlatCurves;
                        Plugin.PreMadeTopoSolidId = BouaryLineToRailing.Toposolid?.Id;
                        Plugin.PreMadeTopoFaces = BouaryLineToRailing.ToposolidFaces;
                    });
                }

                else if (Plugin.SetTerrainBoundary())
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

        public void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var DisplayResults = Plugin.Results.ConvertAll(r => new SlopeResultDisplay(r));
                string header = "ID;X;Y;Height;DistanceFaceToBoundary;DistanceSlopeToBoundary;Result";
                List<string> rows = new List<string> { header };

                foreach (var displayResult in DisplayResults)
                {
                    string row = $"{displayResult.ID};{displayResult.X};{displayResult.Y};{displayResult.Height};" +
                        $"{displayResult.DistanceFaceToBoundary};{displayResult.DistanceSlopeToBoundary};{displayResult.Result}";

                    rows.Add(row);
                }

                // Save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    FileName = "SlopeResults.csv"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllLines(saveFileDialog.FileName, rows);
                    StatusText.Text = "Exportação concluída com sucesso.";
                }
                else
                {
                    StatusText.Text = "Exportação cancelada.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Erro ao exportar: {ex.Message}";
            }
        }

        private void ExecuteCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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

                if (!Plugin.SetBoundaryPoints())
                {
                    StatusText.Text = "Erro ao identificar pontos da borda do terreno.";
                    return;
                }

                RevitTask.RunAsync(() =>
                {
                    Plugin.Execute();

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
