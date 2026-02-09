namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Shared state for synchronizing HTML content between MAUI pages and HtmlViewer Blazor component.
/// Register as singleton in DI so both sides share the same instance.
/// </summary>
public sealed class HtmlViewerState
{
    private string _content = string.Empty;

    /// <summary>
    /// Current HTML content to display.
    /// </summary>
    public string Content
    {
     get => _content;
   set
        {
  if (_content != value)
            {
            _content = value;
       ContentChanged?.Invoke(this, value);
     }
        }
    }

    /// <summary>
    /// Raised when <see cref="Content"/> is updated (from either MAUI or Blazor side).
    /// </summary>
    public event EventHandler<string>? ContentChanged;
}
