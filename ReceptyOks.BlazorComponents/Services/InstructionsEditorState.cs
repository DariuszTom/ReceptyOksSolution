namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Shared state for synchronizing rich-text editor content between MAUI pages and Blazor components.
/// Register as singleton in DI so both sides share the same instance.
/// </summary>
public sealed class InstructionsEditorState
{
    private string _content = string.Empty;
    private string _pendingContent = string.Empty;
    private bool _isBlazorReady;

    /// <summary>
    /// Current HTML content of the editor.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
            if (!_isBlazorReady)
            {
                // Queue content until Blazor editor signals readiness
                _pendingContent = value;
                return;
            }

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

    /// <summary>
    /// Called by the Blazor editor when it is initialized and ready to receive content.
    /// Flushes any pending content that was set before initialization completed.
    /// </summary>
    public void SignalReady()
    {
        _isBlazorReady = true;

        if (!string.IsNullOrEmpty(_pendingContent))
        {
            var pending = _pendingContent;
            _pendingContent = string.Empty;
            Content = pending;
        }
    }

    /// <summary>
    /// Reset internal state when the editor is disposed or navigated away from.
    /// </summary>
    public void Reset()
    {
        _isBlazorReady = false;
        _content = string.Empty;
        _pendingContent = string.Empty;
    }
}
