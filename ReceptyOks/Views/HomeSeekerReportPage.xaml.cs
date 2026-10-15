using ReceptyOks.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class HomeSeekerReportPage : ContentPage
{
    private readonly HomeSeekerReportViewModel _viewModel;

    public HomeSeekerReportPage(HomeSeekerReportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>Keeps the WebView on the inline report; listing links open in the system browser.</summary>
    private async void OnReportNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }

        e.Cancel = true;
        try
        {
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception)
        {
            await SnackBarHelper.ShowErrorSnackbarAsync("Nie można otworzyć linku");
        }
    }
}
