using Microsoft.Maui.Controls;
using ReceptyOks.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReceptyOks.Views;

public class RichEditorTestPage : ContentPage, INotifyPropertyChanged
{
    private readonly RichTextEditor _richEditor;
    private readonly Label _resultLabel;
    private readonly Label _bindingValueLabel;
    private string _editorContent = "";

    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (_editorContent != value)
            {
                _editorContent = value;
                OnPropertyChanged();
                
                System.Diagnostics.Debug.WriteLine($"[RichEditorTest] EditorContent changed: {value?.Substring(0, Math.Min(100, value?.Length ?? 0))}...");
            }
        }
    }

    public RichEditorTestPage()
    {
        Title = "Test RichTextEditor";
        
        // Utwórz kontrolki
        _richEditor = new RichTextEditor
        {
            HeightRequest = 300,
            BackgroundColor = Colors.LightGray
        };
        _richEditor.SetBinding(RichTextEditor.HtmlContentProperty, nameof(EditorContent));
        
        _resultLabel = new Label
        {
            Text = "Kliknij 'Pobierz zawartoœæ' aby zobaczyæ HTML",
            TextColor = Colors.Gray
        };
        
        _bindingValueLabel = new Label
        {
            TextColor = Colors.Gray
        };
        _bindingValueLabel.SetBinding(Label.TextProperty, nameof(EditorContent));
        
        // Utwórz layout
        var grid = new Grid
        {
            Padding = 10,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        
        // Dodaj edytor
        grid.Add(_richEditor, 0, 0);
        
        // Przyciski testowe
        var buttonLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 10)
        };
        
        var testButton = new Button
        {
            Text = "Test komunikacji JS ? C#",
            BackgroundColor = Colors.Green
        };
        testButton.Clicked += OnTestCommunicationClicked;
        buttonLayout.Add(testButton);
        
        var getContentButton = new Button
        {
            Text = "Pobierz zawartoœæ (GetContentAsync)",
            BackgroundColor = Color.FromArgb("#512BD4")
        };
        getContentButton.Clicked += OnGetContentClicked;
        buttonLayout.Add(getContentButton);
        
        var setContentButton = new Button
        {
            Text = "Ustaw przyk³adow¹ zawartoœæ",
            BackgroundColor = Color.FromArgb("#2B88D4")
        };
        setContentButton.Clicked += OnSetContentClicked;
        buttonLayout.Add(setContentButton);
        
        var clearButton = new Button
        {
            Text = "Wyczyœæ zawartoœæ"
        };
        clearButton.Clicked += OnClearContentClicked;
        buttonLayout.Add(clearButton);
        
        grid.Add(buttonLayout, 0, 1);
        
        // Wynik
        var resultFrame = new Frame
        {
            Padding = 10,
            BorderColor = Color.FromArgb("#512BD4"),
            Margin = new Thickness(0, 10),
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Wynik:", FontAttributes = FontAttributes.Bold },
                    _resultLabel
                }
            }
        };
        grid.Add(resultFrame, 0, 2);
        
        // Binding value
        var bindingFrame = new Frame
        {
            Padding = 10,
            BorderColor = Color.FromArgb("#2B88D4"),
            Margin = new Thickness(0, 10, 0, 0),
            Content = new VerticalStackLayout
            {
                Children =
                {
                    new Label { Text = "Wartoœæ z bindingu (HtmlContent):", FontAttributes = FontAttributes.Bold },
                    _bindingValueLabel
                }
            }
        };
        grid.Add(bindingFrame, 0, 3);
        
        Content = grid;
        BindingContext = this;
    }

    private async void OnTestCommunicationClicked(object? sender, EventArgs e)
    {
        try
        {
            _resultLabel.Text = "Testowanie komunikacji...";
            _resultLabel.TextColor = Colors.Orange;

            var result = await _richEditor.TestCommunicationAsync();
            
            _resultLabel.Text = $"? Komunikacja dzia³a!\n\nOdpowiedŸ: {result}";
            _resultLabel.TextColor = Colors.Green;

            System.Diagnostics.Debug.WriteLine($"[RichEditorTest] Test communication result: {result}");
        }
        catch (Exception ex)
        {
            _resultLabel.Text = $"? B³¹d komunikacji: {ex.Message}";
            _resultLabel.TextColor = Colors.Red;
            
            System.Diagnostics.Debug.WriteLine($"[RichEditorTest] Test communication error: {ex}");
        }
    }

    private async void OnGetContentClicked(object? sender, EventArgs e)
    {
        try
        {
            _resultLabel.Text = "Pobieranie zawartoœci...";
            _resultLabel.TextColor = Colors.Orange;

            var content = await _richEditor.GetContentAsync();
            
            _resultLabel.Text = $"D³ugoœæ HTML: {content?.Length ?? 0} znaków\n\n{content}";
            _resultLabel.TextColor = Colors.Green;

            System.Diagnostics.Debug.WriteLine($"[RichEditorTest] GetContentAsync returned: {content}");
        }
        catch (Exception ex)
        {
            _resultLabel.Text = $"B³¹d: {ex.Message}";
            _resultLabel.TextColor = Colors.Red;
            
            System.Diagnostics.Debug.WriteLine($"[RichEditorTest] Error: {ex}");
        }
    }

    private void OnSetContentClicked(object? sender, EventArgs e)
    {
        EditorContent = @"
            <h1>Przyk³adowy przepis</h1>
            <p>To jest <strong>pogrubiony</strong> tekst i <em>kursywa</em>.</p>
            <h2>Sk³adniki:</h2>
            <ul>
                <li>M¹ka - 500g</li>
                <li>Cukier - 200g</li>
                <li>Mas³o - 100g</li>
            </ul>
            <h2>Przygotowanie:</h2>
            <ol>
                <li>Wymieszaj sk³adniki suche</li>
                <li>Dodaj mas³o</li>
                <li>Wyrabiaj ciasto</li>
            </ol>
            <blockquote>Smacznego!</blockquote>
        ";

        System.Diagnostics.Debug.WriteLine($"[RichEditorTest] Set sample content");
    }

    private void OnClearContentClicked(object? sender, EventArgs e)
    {
        EditorContent = "";
        _resultLabel.Text = "Zawartoœæ wyczyszczona";
        _resultLabel.TextColor = Colors.Gray;
        
        System.Diagnostics.Debug.WriteLine($"[RichEditorTest] Content cleared");
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
