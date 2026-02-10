namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Shared state for synchronizing HTML content between MAUI pages and HtmlViewer Blazor component.
/// Register as singleton in DI so both sides share the same instance.
/// </summary>
public sealed class HtmlViewerState
{
    private string _content = string.Empty;
    private string _pendingContent = string.Empty;
    private bool _isBlazorReady;

    /// <summary>
    /// Indicates whether the Blazor component has signaled readiness.
    /// </summary>
public bool IsBlazorReady => _isBlazorReady;

    /// <summary>
    /// Current HTML content to display.
    /// </summary>
    public string Content
    {
        get => _content;
        set
        {
         // Always store pending content for when Blazor signals ready
            _pendingContent = value;

    if (!_isBlazorReady)
 {
    // Queue content until Blazor is ready - don't update _content yet
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
    /// Called by the Blazor component when it has finished initializing.
    /// Flushes any pending content that was set before initialization completed.
  /// </summary>
    public void SignalReady()
    {
        _isBlazorReady = true;

      // Flush pending content if any was queued (even if it was set after BlazorWebViewInitialized)
        if (!string.IsNullOrEmpty(_pendingContent) && _content != _pendingContent)
        {
            var pending = _pendingContent;
   _pendingContent = string.Empty;
     _content = pending;
       ContentChanged?.Invoke(this, pending);
        }
    }

    /// <summary>
    /// Resets state when navigating away or disposing the component.
    /// </summary>
    public void Reset()
    {
      _isBlazorReady = false;
        _content = string.Empty;
        _pendingContent = string.Empty;
    }

    /// <summary>
    /// Raised when <see cref="Content"/> is updated (from either MAUI or Blazor side).
    /// </summary>
public event EventHandler<string>? ContentChanged;
}
