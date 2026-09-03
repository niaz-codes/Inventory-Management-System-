using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace NHi.Inventory.Views;

/// <summary>
/// Opens a wa.me Click-to-Chat URL inside an embedded WebView2 browser control, so the
/// employee reviews and presses WhatsApp's own Send button without ever leaving the POS or
/// launching an external browser. This window only navigates to the given URL — it has no
/// WhatsApp Cloud/Business API involvement and cannot know whether Send was actually pressed.
/// </summary>
public partial class WhatsAppWebViewWindow : Window
{
    private readonly string _url;
    private bool _coreReady;

    public WhatsAppWebViewWindow(string url)
    {
        InitializeComponent();
        _url = url;
        Loaded += async (_, _) => await InitializeAsync();
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        ShowLoading();
        try
        {
            if (!_coreReady)
            {
                await webView.EnsureCoreWebView2Async(null);
                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                _coreReady = true;
            }
            webView.CoreWebView2.Navigate(_url);
        }
        catch
        {
            // Most commonly: the Microsoft Edge WebView2 Runtime isn't installed on this
            // machine. Never let this bubble up and crash the POS — show an inline retry panel.
            ShowError("WhatsApp couldn't be opened inside the app. Please make sure the Microsoft Edge WebView2 Runtime is installed, then try again.");
        }
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess) HideOverlays();
        else ShowError("Unable to load WhatsApp Web. Please check your internet connection and try again.");
    }

    private void BtnRetry_Click(object sender, RoutedEventArgs e) => _ = InitializeAsync();

    private void ShowLoading()
    {
        loadingPanel.Visibility = Visibility.Visible;
        errorPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        txtError.Text = message;
        errorPanel.Visibility = Visibility.Visible;
        loadingPanel.Visibility = Visibility.Collapsed;
    }

    private void HideOverlays()
    {
        loadingPanel.Visibility = Visibility.Collapsed;
        errorPanel.Visibility = Visibility.Collapsed;
    }
}
