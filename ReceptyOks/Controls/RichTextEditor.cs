using System.Text.Json;

namespace ReceptyOks.Controls;

/// <summary>
/// Kontrolka Rich Text Editor oparta na Quill.js w WebView
/// UWAGA: Binding dzia³a jednostronnie (C# -> JS). Aby pobraæ zawartoœæ u¿ywaj GetContentAsync()
/// </summary>
public class RichTextEditor : ContentView
{
    private WebView _webView;
    private bool _isInitialized;
    private string _pendingContent = string.Empty;

    public static readonly BindableProperty HtmlContentProperty = BindableProperty.Create(
        nameof(HtmlContent),
        typeof(string),
        typeof(RichTextEditor),
        string.Empty,
        BindingMode.TwoWay,
        propertyChanged: OnHtmlContentChanged);

    public string HtmlContent
    {
        get => (string)GetValue(HtmlContentProperty);
        set => SetValue(HtmlContentProperty, value);
    }

    public RichTextEditor()
    {
        _webView = new WebView
        {
            Source = "richeditor.html",
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };

        _webView.Navigated += OnWebViewNavigated;

        Content = _webView;
    }

    private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            _isInitialized = true;
            System.Diagnostics.Debug.WriteLine("[RichTextEditor] WebView navigated successfully");
            
            // Jeœli by³a zawartoœæ oczekuj¹ca, ustaw j¹ teraz
            if (!string.IsNullOrEmpty(_pendingContent))
            {
                await SetContentAsync(_pendingContent);
                _pendingContent = string.Empty;
            }
            else if (!string.IsNullOrEmpty(HtmlContent))
            {
                await SetContentAsync(HtmlContent);
            }
        }
    }

    private static async void OnHtmlContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is RichTextEditor editor && newValue is string html)
        {
            System.Diagnostics.Debug.WriteLine($"[RichTextEditor] HtmlContent changed from binding, length: {html.Length}");
            
            if (editor._isInitialized)
            {
                await editor.SetContentAsync(html);
            }
            else
            {
                editor._pendingContent = html;
            }
        }
    }

    private async Task SetContentAsync(string html)
    {
        if (_webView is null || !_isInitialized) return;

        try
        {
            var escapedHtml = html
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
            
            await _webView.EvaluateJavaScriptAsync($"setContent('{escapedHtml}')");
            System.Diagnostics.Debug.WriteLine($"[RichTextEditor] Content set via JS, length: {html.Length}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RichTextEditor] SetContent error: {ex.Message}");
        }
    }

    /// <summary>
    /// Pobiera aktualn¹ zawartoœæ HTML z edytora
    /// </summary>
    public async Task<string> GetContentAsync()
    {
        if (_webView is null || !_isInitialized) return string.Empty;

        try
        {
            var result = await _webView.EvaluateJavaScriptAsync("getContent()");
            System.Diagnostics.Debug.WriteLine($"[RichTextEditor] GetContent returned: {result?.Length ?? 0} chars");
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RichTextEditor] GetContent error: {ex.Message}");
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Testuje komunikacjê z JavaScript - zwraca wiadomoœæ testow¹
    /// </summary>
    public async Task<string> TestCommunicationAsync()
    {
        if (_webView is null || !_isInitialized) 
            return "ERROR: WebView not initialized";

        try
        {
            var result = await _webView.EvaluateJavaScriptAsync("testCommunication()");
            return result ?? "ERROR: No result returned";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }
}
