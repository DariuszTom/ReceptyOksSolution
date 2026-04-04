namespace ReceptyOks.BlazorComponents.Services;

/// <summary>
/// Thread-safe shared state for synchronizing HTML content between MAUI pages and Blazor components.
/// Derive typed subclasses and register each as a separate singleton in DI
/// so each state channel gets its own independent instance.
/// </summary>
public abstract class BlazorContentState
{
    private readonly object _lock = new();
    private string _content = string.Empty;
    private string? _pendingContent;
    private bool _isBlazorReady;

    /// <summary>
    /// Whether the Blazor component has signaled readiness.
    /// </summary>
    public bool IsBlazorReady
    {
        get
        {
            lock (_lock) return _isBlazorReady;
        }
        set => _isBlazorReady = value;
    }

    /// <summary>
    /// Current HTML content. Setting this queues the value until Blazor signals ready.
    /// </summary>
    public string Content
    {
        get
        {
            lock (_lock) return _content;
        }
        set
        {
            lock (_lock)
            {
                _pendingContent = value;

                if (!_isBlazorReady)
                    return;

                if (_content == value)
                    return;

                _content = value;
            }

            ContentChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Raised when <see cref="Content"/> is updated (from either MAUI or Blazor side).
    /// </summary>
    public event EventHandler<string>? ContentChanged;

    /// <summary>
    /// Called by the Blazor component when it is initialized and ready to receive content.
    /// Flushes any pending content that was set before initialization completed.
    /// </summary>
    public void SignalReady()
    {
        string? pendingToFlush = null;

        lock (_lock)
        {
            _isBlazorReady = true;

            if (_pendingContent is not null)
            {
                pendingToFlush = _pendingContent;
                _content = _pendingContent;
                _pendingContent = null;
            }
        }

        if (pendingToFlush is not null)
            ContentChanged?.Invoke(this, pendingToFlush);
    }

    /// <summary>
    /// Pauses delivery when the Blazor component is disposed.
    /// Content is preserved so the next component instance receives it via <see cref="SignalReady"/>.
    /// Prefer this over <see cref="Reset"/> during normal navigation.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _isBlazorReady = false;

            _pendingContent ??= _content;
        }
    }

    /// <summary>
    /// Fully resets internal state (content + readiness flag).
    /// Use only when you need a clean slate (e.g. user logout).
    /// Prefer <see cref="Pause"/> during normal navigation.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _isBlazorReady = false;
            _content = string.Empty;
            _pendingContent = null;
        }
    }
}

/// <summary>
/// Typed state for the rich-text instructions editor.
/// Registered as its own singleton so it doesn't collide with other Blazor content states.
/// </summary>
public sealed class InstructionsEditorState : BlazorContentState;

/// <summary>
/// Typed state for the read-only HTML viewer.
/// Registered as its own singleton so it doesn't collide with other Blazor content states.
/// </summary>
public sealed class HtmlViewerState : BlazorContentState;
