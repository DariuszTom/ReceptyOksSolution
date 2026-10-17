using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ReceptyOks.ViewModels;
using UraniumUI.Material.Controls;

namespace ReceptyOks.Views;

public partial class HomeSeekerPage : ContentPage
{
    private readonly HomeSeekerViewModel _viewModel;
    private bool _initialized;
    private bool _componentReady;
    private HomeSeekerMapView? _map;

    public HomeSeekerPage(HomeSeekerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = _viewModel;
        SectionTabs.SelectedTab = TabFor(_viewModel.CurrentSection);
        _componentReady = true;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeSeekerViewModel.CurrentSection))
            return;

        var target = TabFor(_viewModel.CurrentSection);
        if (!ReferenceEquals(SectionTabs.SelectedTab, target))
            MainThread.BeginInvokeOnMainThread(() => SectionTabs.SelectedTab = target);
    }

    private void OnSelectedTabChanged(object? sender, TabItem? tab)
    {
        // TabView raises this while XAML is loading (inside InitializeComponent).
        if (!_componentReady)
            return;

        var section = tab == ProfileTab ? HomeSeekerSection.Profile
            : tab == ScansTab ? HomeSeekerSection.Scans
            : tab == MapTab ? HomeSeekerSection.Map
            : HomeSeekerSection.Listings;

        if (section == HomeSeekerSection.Map)
            EnsureMap();

        if (_viewModel.CurrentSection != section)
            _viewModel.CurrentSection = section;
    }

    private TabItem TabFor(HomeSeekerSection section) => section switch
    {
        HomeSeekerSection.Profile => ProfileTab,
        HomeSeekerSection.Scans => ScansTab,
        HomeSeekerSection.Map => MapTab,
        _ => ListingsTab,
    };

    // The map (tiles, Skia surface) is only created when the tab is opened for the first time.
    private void EnsureMap()
    {
        if (_map is not null)
            return;

        try
        {
            _map = new HomeSeekerMapView(_viewModel, (title, message) => DisplayAlertAsync(title, message, "Ustaw", "Anuluj"));
            MapHost.Children.Insert(0, _map.Control);
        }
        catch (Exception ex)
        {
            _map = null;
            Handler?.MauiContext?.Services.GetService<ILogger<HomeSeekerPage>>()?.LogError(ex, "Creating the map failed");
            _viewModel.ErrorMessage = "Nie udało się utworzyć mapy";
        }
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.NewHandler is null)
        {
            _map?.Detach();
            _map = null;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Coming back from the report page should not reset scroll position / paging.
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
