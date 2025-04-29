using Microsoft.Web.WebView2.Wpf;
using System;
using System.Threading.Tasks;

namespace GvcRevitPlugins.TerrainCheck.UI
{
    internal class WebViewService
    {
        private MainWindowViewModel mainWindowViewModel;
        private WebView2 webView;
        public WebViewService(WebView2 webView, MainWindowViewModel mainWindowViewModel)
        {
            this.webView = webView;
            this.mainWindowViewModel = mainWindowViewModel;
            string uriString = "https://www.geogebra.org/material/iframe/id/zxfkp7n7/border/999999/rc/true/ai/false/sdz/true/smb/false/stb/false/stbh/true/ld/false/sri/false";
            Task ecwTask = webView.EnsureCoreWebView2Async(null);
            webView.Source = new Uri(uriString);
        }
    }
}
