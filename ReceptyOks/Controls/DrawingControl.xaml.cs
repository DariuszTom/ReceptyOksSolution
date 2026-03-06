using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ReceptyOks.Controls;

/// <summary>
/// A drawing control that wraps CommunityToolkit.Maui DrawingView with color selection,
/// pencil size, undo, clear, and save capabilities. Bind <see cref="Lines"/> to persist
/// and restore drawings across sessions.
/// </summary>
public partial class DrawingControl : ContentView
{
    #region Bindable Properties

    public static readonly BindableProperty LinesProperty = BindableProperty.Create(
        nameof(Lines),
        typeof(ObservableCollection<IDrawingLine>),
        typeof(DrawingControl),
        defaultValueCreator: _ => new ObservableCollection<IDrawingLine>(),
        defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The collection of drawing lines. Bind this to persist/restore drawings.
    /// </summary>
    public ObservableCollection<IDrawingLine> Lines
    {
        get => (ObservableCollection<IDrawingLine>)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor),
        typeof(Color),
        typeof(DrawingControl),
        Colors.Black,
        defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The currently selected drawing color.
    /// </summary>
    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public static readonly BindableProperty SelectedLineWidthProperty = BindableProperty.Create(
        nameof(SelectedLineWidth),
        typeof(float),
        typeof(DrawingControl),
        5f,
        defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The currently selected pencil width.
    /// </summary>
    public float SelectedLineWidth
    {
        get => (float)GetValue(SelectedLineWidthProperty);
        set => SetValue(SelectedLineWidthProperty, value);
    }

    public static readonly BindableProperty SaveCommandProperty = BindableProperty.Create(
        nameof(SaveCommand),
        typeof(ICommand),
        typeof(DrawingControl));

    /// <summary>
    /// Command invoked when the user taps Save. The command parameter is the <see cref="DrawingView"/>
    /// instance so the consumer can call <c>GetImageStream</c>.
    /// </summary>
    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    #endregion

    public ICommand SelectColorCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand ClearCommand { get; }

    public DrawingControl()
    {
        SelectColorCommand = new Command<string>(OnSelectColor);
        UndoCommand = new Command(OnUndo, () => Lines.Count > 0);
        ClearCommand = new Command(OnClear, () => Lines.Count > 0);

        InitializeComponent();

        Lines.CollectionChanged += (_, _) =>
        {
            ((Command)UndoCommand).ChangeCanExecute();
            ((Command)ClearCommand).ChangeCanExecute();
        };
    }

    private void OnSelectColor(string colorName)
    {
        if (Color.TryParse(colorName, out var color))
        {
            SelectedColor = color;
        }
    }

    private void OnUndo()
    {
        if (Lines.Count > 0)
        {
            Lines.RemoveAt(Lines.Count - 1);
        }
    }

    private void OnClear()
    {
        Lines.Clear();
    }

    /// <summary>
    /// Returns an image stream of the current drawing at the specified dimensions.
    /// </summary>
    public async Task<Stream> GetImageStreamAsync(double desiredWidth, double desiredHeight, CancellationToken cancellationToken = default)
    {
        var drawingView = this.FindByName<DrawingView>("DrawingViewControl");
        return await drawingView.GetImageStream(desiredWidth, desiredHeight, cancellationToken);
    }
}
