using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class RecipeDetailPage : ContentPage
{
    private readonly RecipeDetailViewModel _viewModel;

    public RecipeDetailPage(RecipeDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Nas³uchuj zmian w Recipe.Instructions
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.Recipe))
        {
            UpdateInstructionsHtml();
        }
    }

    private void UpdateInstructionsHtml()
    {
        if (_viewModel.Recipe?.Instructions is string html && !string.IsNullOrWhiteSpace(html))
        {
            // Stwórz pe³ny HTML z stylami
            var fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background-color: transparent;
            color: #1f1f1f;
            padding: 16px;
            font-size: 15px;
            line-height: 1.6;
            margin: 0;
        }}
        @media (prefers-color-scheme: dark) {{
            body {{
                color: #ffffff;
            }}
        }}
        h1 {{ font-size: 24px; margin: 0 0 12px 0; font-weight: bold; }}
        h2 {{ font-size: 20px; margin: 0 0 10px 0; font-weight: bold; }}
        h3 {{ font-size: 18px; margin: 0 0 8px 0; font-weight: bold; }}
        p {{ margin: 0 0 12px 0; }}
        ol, ul {{ padding-left: 24px; margin: 0 0 12px 0; }}
        li {{ margin-bottom: 6px; }}
        blockquote {{ 
            border-left: 3px solid #512BD4; 
            padding-left: 16px; 
            margin: 12px 0; 
            font-style: italic; 
        }}
        strong {{ font-weight: bold; }}
        em {{ font-style: italic; }}
    </style>
</head>
<body>
    {html}
</body>
</html>";

            InstructionsWebView.Source = new HtmlWebViewSource { Html = fullHtml };
        }
        else
        {
            InstructionsWebView.Source = new HtmlWebViewSource
            {
                Html = "<html><body style='padding:16px; color:#999;'>Brak instrukcji przygotowania</body></html>"
            };
        }
    }
}
