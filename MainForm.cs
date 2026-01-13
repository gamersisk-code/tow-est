using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace SouthernPrideTowEstimator;

public sealed class MainForm : Form
{
    private readonly WebView2 _webView;

    public MainForm()
    {
        Text = "Southern Pride Towing Estimator";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_webView);

        Shown += async (_, _) => await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(AppContext.BaseDirectory, "app-data");
        Directory.CreateDirectory(userDataFolder);
        var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(environment);
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

        var html = LoadEmbeddedHtml();
        _webView.NavigateToString(html);
    }

    private static string LoadEmbeddedHtml()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "tow-estimator.html";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
