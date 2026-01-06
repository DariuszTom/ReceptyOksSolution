using System.Text.Json;

namespace ReceptyOks.Controls;

/// <summary>
/// Kontrolka Rich Text Editor oparta na Quill.js w WebView
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

#if WINDOWS
        _webView.HandlerChanged += (s, e) =>
        {
            if (_webView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webView2)
            {
                webView2.WebMessageReceived += (sender, args) =>
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<EditorMessage>(args.WebMessageAsJson);
                        if (message?.Type == "contentChanged")
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                HtmlContent = message.Html ?? string.Empty;
                            });
                        }
                    }
                    catch { }
                };
            }
        };
#endif

        Content = _webView;
    }

    private async void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            _isInitialized = true;
            
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RichTextEditor SetContent error: {ex.Message}");
        }
    }

    public async Task<string> GetContentAsync()
    {
        if (_webView is null || !_isInitialized) return string.Empty;

        try
        {
            var result = await _webView.EvaluateJavaScriptAsync("getContent()");
            return result ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private class EditorMessage
    {
        public string? Type { get; set; }
        public string? Html { get; set; }
    }
}
