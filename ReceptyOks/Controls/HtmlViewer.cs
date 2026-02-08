namespace ReceptyOks.Controls;

/// <summary>
/// Kontrolka do wyœwietlania sformatowanego HTML (np. instrukcji przepisu)
/// </summary>
public class HtmlViewer : ContentView
{
    private WebView _webView;
    private bool _isInitialized;
    private string _pendingHtml = string.Empty;

    public static readonly BindableProperty HtmlContentProperty = BindableProperty.Create(
        nameof(HtmlContent),
        typeof(string),
        typeof(HtmlViewer),
        string.Empty,
        propertyChanged: OnHtmlContentChanged);

    public string HtmlContent
    {
        get => (string)GetValue(HtmlContentProperty);
        set => SetValue(HtmlContentProperty, value);
    }

    public HtmlViewer()
    {
        _webView = new WebView
        {
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        _webView.Navigated += OnWebViewNavigated;

        Content = _webView;

        // Za³aduj szablon HTML
        LoadTemplate();
    }

    private async void LoadTemplate()
    {
        try
        {
            // Wczytaj szablon z pliku
            using var stream = await FileSystem.OpenAppPackageFileAsync("instructions-viewer.html");
            using var reader = new StreamReader(stream);
            var template = await reader.ReadToEndAsync();

            _webView.Source = new HtmlWebViewSource { Html = template };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HtmlViewer] Error loading template: {ex.Message}");
            // Fallback - u¿yj prostego HTML
            _webView.Source = new HtmlWebViewSource
            {
                Html = "<html><body style='font-family: sans-serif; padding: 16px;'>{{CONTENT}}</body></html>"
            };
        }
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            _isInitialized = true;

            if (!string.IsNullOrEmpty(_pendingHtml))
            {
                UpdateContent(_pendingHtml);
                _pendingHtml = string.Empty;
            }
            else if (!string.IsNullOrEmpty(HtmlContent))
            {
                UpdateContent(HtmlContent);
            }
        }
    }

    private static void OnHtmlContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is HtmlViewer viewer && newValue is string html)
        {
            if (viewer._isInitialized)
            {
                viewer.UpdateContent(html);
            }
            else
            {
                viewer._pendingHtml = html;
            }
        }
    }

    private async void UpdateContent(string html)
    {
        if (_webView is null || string.IsNullOrEmpty(html)) return;

        try
        {
            // Wczytaj szablon ponownie i wstaw treœæ
            using var stream = await FileSystem.OpenAppPackageFileAsync("instructions-viewer.html");
            using var reader = new StreamReader(stream);
            var template = await reader.ReadToEndAsync();

            // Zamieñ {{CONTENT}} na rzeczywist¹ zawartoœæ HTML
            var fullHtml = template.Replace("{{CONTENT}}", html);

            _webView.Source = new HtmlWebViewSource { Html = fullHtml };

            System.Diagnostics.Debug.WriteLine($"[HtmlViewer] Content updated, length: {html.Length}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HtmlViewer] Error updating content: {ex.Message}");
            // Fallback
            _webView.Source = new HtmlWebViewSource
            {
                Html = $"<html><body style='font-family: sans-serif; padding: 16px;'>{html}</body></html>"
            };
        }
    }
}
