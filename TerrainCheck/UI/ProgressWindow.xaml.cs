using System;
using System.Windows;

namespace GvcRevitPlugins.TerrainCheck.UI
{
    public partial class ProgressWindow : Window
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        // Atualiza o progresso de forma thread-safe
        public void UpdateProgress(double percentage, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateProgress(percentage, message));
            }
            else
            {
                ProgressBar.Value = percentage;
                StatusText.Text = message;
            }
        }
    }
}
