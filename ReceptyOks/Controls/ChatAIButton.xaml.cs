using System.Windows.Input;

namespace ReceptyOks.Controls;

public partial class ChatAIButton : ContentView
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ChatAIButton), default(ICommand));

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ChatAIButton), null);

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler? Clicked;

    public ChatAIButton()
    {
        InitializeComponent();
    }

    void OnImageButtonClicked(object? sender, EventArgs e)
    {
        if (Command is not null && Command.CanExecute(CommandParameter))
        {
            Command.Execute(CommandParameter);
        }

        Clicked?.Invoke(this, e);
    }
}