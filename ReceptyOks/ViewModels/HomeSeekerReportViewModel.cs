using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Services;

namespace ReceptyOks.ViewModels;

/// <summary>Loads a scan report (server-side HTML template) for display in a sandboxed WebView.</summary>
public partial class HomeSeekerReportViewModel(IHomeSeekerService service) : ObservableObject, IQueryAttributable
{
    public const string ScanIdQueryKey = "scanId";
    public const string TitleQueryKey = "title";

    // The API serves the report with a strict CSP header; a WebView loading inline HTML has no
    // headers, so the same policy is injected as a meta tag (no scripts, no remote resources except images).
    public const string ContentSecurityPolicyMeta =
        "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src https: data:; base-uri 'none'; form-action 'none'\">";

    private Guid _scanId;

    [ObservableProperty]
    private string title = "Raport";

    [ObservableProperty]
    private HtmlWebViewSource? reportSource;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(ScanIdQueryKey, out var id) && id is Guid scanId)
            _scanId = scanId;
        if (query.TryGetValue(TitleQueryKey, out var t) && t is string text)
            Title = text;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading || _scanId == Guid.Empty)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await service.GetReportHtmlAsync(_scanId, cancellationToken);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                ReportSource = new HtmlWebViewSource { Html = WithContentSecurityPolicy(result.Value) };
            }
            else
            {
                ErrorMessage = result.Status == HomeSeekerCallStatus.NotFound
                    ? "Raport dla tego skanu nie jest dostępny."
                    : result.Message;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    public static string WithContentSecurityPolicy(string html)
    {
        var headIndex = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        if (headIndex >= 0)
        {
            var headEnd = html.IndexOf('>', headIndex);
            if (headEnd >= 0)
                return html.Insert(headEnd + 1, ContentSecurityPolicyMeta);
        }

        return $"<!DOCTYPE html><html><head>{ContentSecurityPolicyMeta}</head><body>{html}</body></html>";
    }
}
